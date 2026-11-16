"""Interview preparation and battlecard service running two parallel LLM calls."""

import asyncio
import json
from pathlib import Path
from typing import Any

from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage

from app.candidate.guards.grounding import verify_rounds_grounding
from app.candidate.schemas.interview_prep import (
    InterviewPreparationPlan,
    InterviewRoundPrep,
)
from app.candidate.schemas.research import CompanyRoleResearchResult, InterviewRoundInsight
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.candidate.services.schemas.interview_prep import (
    PitchBattlecardStudyPart,
    RoundsAndStoriesPart,
)
from app.core.config import get_settings
from app.core.guards.grounding import (
    format_retry_feedback,
    run_with_grounding_retry,
)
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "interview_prep.md"

_DEFAULT_ROUNDS: tuple[InterviewRoundInsight, ...] = (
    InterviewRoundInsight(
        round_index=1,
        name="Recruiter Screen",
        focus="Logistics, role alignment, communication, salary expectations (generic, no public signal)",
    ),
    InterviewRoundInsight(
        round_index=2,
        name="Hiring-Manager Behavioral",
        focus="Leadership, ownership, problem-solving, motivation (generic, no public signal)",
    ),
    InterviewRoundInsight(
        round_index=3,
        name="Technical / Role-Specific",
        focus="Deep technical skills, architecture, and system design (generic, no public signal)",
    ),
)


def _resolve_rounds(research: CompanyRoleResearchResult) -> list[InterviewRoundInsight]:
    """Return rounds from `research.interview_process_insights.standard_rounds` or the 3-round default."""
    rounds = research.interview_process_insights.standard_rounds
    return list(rounds) if rounds else list(_DEFAULT_ROUNDS)


def _coerce_rounds_part(raw: Any) -> list[InterviewRoundPrep]:
    """Extract `list[InterviewRoundPrep]` from either `RoundsAndStoriesPart`, `InterviewPreparationPlan`, or dict."""
    if isinstance(raw, RoundsAndStoriesPart):
        return raw.interview_rounds_prep
    if isinstance(raw, InterviewPreparationPlan):
        return raw.interview_rounds_prep
    if isinstance(raw, dict) and "interview_rounds_prep" in raw:
        return RoundsAndStoriesPart.model_validate(
            {"interview_rounds_prep": raw["interview_rounds_prep"]}
        ).interview_rounds_prep
    msg = f"Unexpected response type for rounds and STAR stories: {type(raw)!r}"
    raise TypeError(msg)


def _coerce_pitch_battlecard_part(raw: Any) -> PitchBattlecardStudyPart:
    """Extract `PitchBattlecardStudyPart` from `PitchBattlecardStudyPart`, `InterviewPreparationPlan`, or dict."""
    if isinstance(raw, PitchBattlecardStudyPart):
        return raw
    if isinstance(raw, InterviewPreparationPlan):
        return PitchBattlecardStudyPart(
            elevator_pitch_60_seconds=raw.elevator_pitch_60_seconds,
            day_of_battlecard=raw.day_of_battlecard,
            time_boxed_study_plan=raw.time_boxed_study_plan,
        )
    if isinstance(raw, dict):
        return PitchBattlecardStudyPart.model_validate(raw)
    msg = f"Unexpected response type for pitch/battlecard/study plan: {type(raw)!r}"
    raise TypeError(msg)


async def prepare_interview(
    profile: CandidateProfileParsed,
    job: TargetJobRequirements,
    match: JobMatchAnalysisResult,
    research: CompanyRoleResearchResult,
    tailored_resume: TailoredResumeResult | None = None,
    prep_timeline_days: int | None = None,
    extra_context: str | None = None,
    model: BaseChatModel | None = None,
) -> InterviewPreparationPlan:
    """Generate a comprehensive `InterviewPreparationPlan` using two parallel LLM calls.

    Runs (1) rounds + STAR stories and (2) elevator pitch + day-of battlecard +
    time-boxed study plan concurrently via `asyncio.gather`, verifies grounding of
    STAR stories against `profile` (retrying once on violations), and merges both
    into a single `InterviewPreparationPlan`.
    """
    llm = model or get_chat_model(get_settings().INTERVIEW_PREP_MODEL)
    system_prompt = load_prompt(_PROMPT_PATH)
    target_rounds = _resolve_rounds(research)
    timeline_days = prep_timeline_days or 5

    sanitized_profile = profile.model_dump(exclude={"personal_info"})
    shared_context_parts = [
        f"## Candidate Profile (JSON)\n```json\n{json.dumps(sanitized_profile, indent=2)}\n```",
        f"## Target Job Requirements (JSON)\n```json\n{json.dumps(job.model_dump(), indent=2)}\n```",
        f"## Match Analysis (JSON)\n```json\n{json.dumps(match.model_dump(), indent=2)}\n```",
        f"## Company Research (JSON)\n```json\n{json.dumps(research.model_dump(), indent=2)}\n```",
        f"## Target Interview Rounds (JSON)\n```json\n{json.dumps([r.model_dump() for r in target_rounds], indent=2)}\n```",
        f"## Prep Timeline Days: {timeline_days}",
    ]
    if tailored_resume is not None:
        sanitized_tailored = tailored_resume.model_dump(exclude={"full_markdown_content", "rendercv_ready_payload"})
        shared_context_parts.append(
            f"## Tailored Resume (JSON)\n```json\n{json.dumps(sanitized_tailored, indent=2)}\n```"
        )
    if extra_context and extra_context.strip():
        shared_context_parts.append(
            f"## Additional Candidate / Intake Context\n{extra_context.strip()}"
        )
    shared_context = "\n\n".join(shared_context_parts)

    rounds_llm = llm.with_structured_output(RoundsAndStoriesPart)
    pitch_llm = llm.with_structured_output(PitchBattlecardStudyPart)

    async def _generate_rounds(violations: list[str] | None) -> list[InterviewRoundPrep]:
        prompt_body = (
            "Generate the round-by-round interview preparation (`interview_rounds_prep`) "
            "including `key_focus_areas`, `likely_questions`, and CV-grounded `star_stories` "
            "for each target round.\n\n"
            f"{shared_context}"
        )
        if violations:
            prompt_body = f"{prompt_body}\n\n{format_retry_feedback(violations)}"
        messages = [
            SystemMessage(content=system_prompt),
            HumanMessage(content=prompt_body),
        ]
        raw = await rounds_llm.ainvoke(messages)
        return _coerce_rounds_part(raw)

    async def _generate_pitch_battlecard_study() -> PitchBattlecardStudyPart:
        prompt_body = (
            "Generate the `elevator_pitch_60_seconds`, `day_of_battlecard`, and "
            f"`time_boxed_study_plan` (for {timeline_days} days).\n\n"
            f"{shared_context}"
        )
        messages = [
            SystemMessage(content=system_prompt),
            HumanMessage(content=prompt_body),
        ]
        raw = await pitch_llm.ainvoke(messages)
        return _coerce_pitch_battlecard_part(raw)

    rounds_task = run_with_grounding_retry(_generate_rounds, verify_rounds_grounding, profile)
    pitch_task = _generate_pitch_battlecard_study()

    rounds_prep, pitch_part = await asyncio.gather(rounds_task, pitch_task)

    return InterviewPreparationPlan(
        elevator_pitch_60_seconds=pitch_part.elevator_pitch_60_seconds,
        interview_rounds_prep=rounds_prep,
        day_of_battlecard=pitch_part.day_of_battlecard,
        time_boxed_study_plan=pitch_part.time_boxed_study_plan,
    )
