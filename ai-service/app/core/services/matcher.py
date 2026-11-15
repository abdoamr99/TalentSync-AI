"""Candidate-to-job matching and skill-gap analysis service with deterministic scoring."""

import json
from datetime import UTC, date, datetime
from pathlib import Path
from typing import Any, Literal

from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage
from pydantic import BaseModel, Field

from app.core.config import get_settings
from app.core.enums.match import MatchVerdict
from app.core.guards.grounding import (
    format_retry_feedback,
    run_with_grounding_retry,
    verify_match_grounding,
)
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import (
    JobMatchAnalysisResult,
    SkillGapItem,
    StrengthAnalysisItem,
)

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "matcher.md"

AssessmentStatus = Literal["demonstrated", "adjacent", "missing"]


class SkillAssessment(BaseModel):
    """Per-requirement assessment produced by the LLM."""

    skill: str = Field(description="Mandatory skill, preferred skill, or ATS keyword evaluated.")
    status: AssessmentStatus = Field(
        description="'demonstrated' if directly on CV, 'adjacent' if close substitute exists, 'missing' if absent.",
    )
    evidence: str | None = Field(
        default=None,
        description="Direct quote or citation from the CV when status is demonstrated or adjacent.",
    )


class MatchJudgement(BaseModel):
    """Internal structured judgement returned by the LLM before deterministic scoring."""

    mandatory_skill_assessments: list[SkillAssessment] = Field(
        default_factory=list,
        description="Per-mandatory-skill assessment with status and CV evidence.",
    )
    preferred_skill_assessments: list[SkillAssessment] = Field(
        default_factory=list,
        description="Per-preferred-skill / ATS-keyword assessment with status and CV evidence.",
    )
    degree_status: AssessmentStatus = Field(
        default="demonstrated",
        description="Assessment of the candidate's education against degree_requirement.",
    )
    strengths_analysis: list[StrengthAnalysisItem] = Field(
        default_factory=list,
        description="Candidate strengths supported by direct CV evidence.",
    )
    skill_gaps: list[SkillGapItem] = Field(
        default_factory=list,
        description="Missing or weak skills with severity, impact, and remediation advice.",
    )
    strategic_recommendations: list[str] = Field(
        default_factory=list,
        description="Actionable recommendations for positioning the candidate's experience.",
    )


def _parse_year_month(value: str | None, fallback: date) -> int | None:
    """Convert a 'YYYY-MM' or 'YYYY' string into a total-months integer (`year * 12 + month`)."""
    if not value or not value.strip():
        return fallback.year * 12 + fallback.month
    cleaned = value.strip()
    parts = cleaned.split("-")
    try:
        year = int(parts[0])
        month = int(parts[1]) if len(parts) > 1 else 1
        month = max(1, min(12, month))
        return year * 12 + month
    except ValueError:
        return None


def compute_years_of_experience(
    profile: CandidateProfileParsed,
    reference_date: date | None = None,
) -> float:
    """Compute non-overlapping years of professional experience from `profile.experience` dates."""
    today = reference_date or datetime.now(UTC).date()
    intervals: list[tuple[int, int]] = []

    for exp in profile.experience:
        start_ym = _parse_year_month(exp.start_date, today)
        end_ym = (
            today.year * 12 + today.month
            if exp.is_current or not exp.end_date
            else _parse_year_month(exp.end_date, today)
        )
        if start_ym is not None and end_ym is not None and end_ym >= start_ym:
            intervals.append((start_ym, end_ym))

    if not intervals:
        return 0.0

    intervals.sort(key=lambda pair: pair[0])
    merged: list[tuple[int, int]] = [intervals[0]]
    for current_start, current_end in intervals[1:]:
        prev_start, prev_end = merged[-1]
        if current_start <= prev_end:
            merged[-1] = (prev_start, max(prev_end, current_end))
        else:
            merged.append((current_start, current_end))

    total_months = sum(max(0, end - start) for start, end in merged)
    return total_months / 12.0


def _status_credit(status: AssessmentStatus) -> float:
    if status == "demonstrated":
        return 1.0
    if status == "adjacent":
        return 0.5
    return 0.0


def _average_assessment_credit(assessments: list[SkillAssessment], expected_count: int) -> float:
    if expected_count <= 0 and not assessments:
        return 1.0
    if not assessments:
        return 0.0
    denom = max(len(assessments), expected_count)
    total = sum(_status_credit(a.status) for a in assessments)
    return min(1.0, total / denom)


def compute_match_verdict(match_score: int, skill_gaps: list[SkillGapItem]) -> str:
    """Map deterministic `match_score` (0-100) and gaps to a human-readable verdict band."""
    if match_score >= 85:
        return (
            MatchVerdict.STRONG_WITH_MINOR_GAPS
            if skill_gaps
            else MatchVerdict.STRONG_MATCH
        )
    if match_score >= 70:
        return MatchVerdict.STRONG_WITH_MINOR_GAPS
    if match_score >= 50:
        return MatchVerdict.MODERATE_MATCH
    return MatchVerdict.WEAK_MATCH


def build_match_result_from_judgement(
    judgement: MatchJudgement,
    profile: CandidateProfileParsed,
    job: TargetJobRequirements,
    reference_date: date | None = None,
) -> JobMatchAnalysisResult:
    """Compute `match_score` (50/20/20/10 rubric) and `match_verdict` deterministically."""
    mandatory_expected = len(job.required_qualifications.mandatory_skills)
    mandatory_points = 50.0 * _average_assessment_credit(
        judgement.mandatory_skill_assessments, mandatory_expected
    )

    preferred_expected = len(job.required_qualifications.preferred_skills)
    preferred_points = 20.0 * _average_assessment_credit(
        judgement.preferred_skill_assessments, preferred_expected
    )

    min_years = job.required_qualifications.minimum_years_of_experience
    if min_years <= 0:
        experience_points = 20.0
    else:
        candidate_years = compute_years_of_experience(profile, reference_date=reference_date)
        experience_points = 20.0 * min(1.0, candidate_years / float(min_years))

    degree_req = job.required_qualifications.degree_requirement.strip().lower()
    if not degree_req or degree_req in {"none", "not specified", "n/a"}:
        degree_points = 10.0
    else:
        degree_points = 10.0 * _status_credit(judgement.degree_status)

    raw_score = round(mandatory_points + preferred_points + experience_points + degree_points)
    match_score = max(0, min(100, int(raw_score)))
    match_verdict = compute_match_verdict(match_score, judgement.skill_gaps)

    return JobMatchAnalysisResult(
        match_score=match_score,
        match_verdict=match_verdict,
        strengths_analysis=judgement.strengths_analysis,
        skill_gaps=judgement.skill_gaps,
        strategic_recommendations=judgement.strategic_recommendations,
    )


def _coerce_to_match_result(
    raw: Any,
    profile: CandidateProfileParsed,
    job: TargetJobRequirements,
) -> JobMatchAnalysisResult:
    """Convert `MatchJudgement` (or a pre-built `JobMatchAnalysisResult`) into `JobMatchAnalysisResult`."""
    if isinstance(raw, JobMatchAnalysisResult):
        return raw
    if isinstance(raw, MatchJudgement):
        return build_match_result_from_judgement(raw, profile, job)
    if isinstance(raw, dict):
        if "match_score" in raw and "match_verdict" in raw:
            return JobMatchAnalysisResult.model_validate(raw)
        judgement = MatchJudgement.model_validate(raw)
        return build_match_result_from_judgement(judgement, profile, job)
    msg = f"Unexpected matcher output type: {type(raw)!r}"
    raise TypeError(msg)


async def analyze_match(
    profile: CandidateProfileParsed,
    job: TargetJobRequirements,
    model: BaseChatModel | None = None,
) -> JobMatchAnalysisResult:
    """Evaluate a candidate profile against target job requirements.

    Uses `model.with_structured_output(MatchJudgement)`, computes `match_score` and
    `match_verdict` deterministically in code, and verifies grounding of all
    `strengths_analysis` items against `profile` (retrying once on failure).
    """
    llm = model or get_chat_model(get_settings().MATCHER_MODEL, temperature=0.0)
    structured_llm = llm.with_structured_output(MatchJudgement)
    system_prompt = load_prompt(_PROMPT_PATH)

    sanitized_profile = profile.model_dump(exclude={"personal_info"})
    base_user_content = (
        "Evaluate the candidate profile against the target job requirements.\n\n"
        f"## Candidate Profile (JSON)\n```json\n{json.dumps(sanitized_profile, indent=2)}\n```\n\n"
        f"## Target Job Requirements (JSON)\n```json\n{json.dumps(job.model_dump(), indent=2)}\n```"
    )

    async def _generate(violations: list[str] | None) -> JobMatchAnalysisResult:
        user_content = base_user_content
        if violations:
            user_content = f"{base_user_content}\n\n{format_retry_feedback(violations)}"
        messages = [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_content),
        ]
        raw = await structured_llm.ainvoke(messages)
        return _coerce_to_match_result(raw, profile, job)

    return await run_with_grounding_retry(_generate, verify_match_grounding, profile)
