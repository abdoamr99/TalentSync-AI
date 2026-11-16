"""Core grounding guard primitives and match-evidence verification."""

import re
from collections.abc import Awaitable, Callable

from app.core.enums.messages import CoreErrorMessage
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.match import JobMatchAnalysisResult

_WORD_TOKEN_RE = re.compile(r"[a-zA-Z0-9#+]+")
_NUMBER_METRIC_RE = re.compile(r"(?<![a-zA-Z0-9_])\d+(?:\.\d+)?(?:%|\+|[kKmMbB]\b)?")

_STOPWORDS: frozenset[str] = frozenset(
    {
        "the",
        "and",
        "for",
        "with",
        "from",
        "that",
        "this",
        "into",
        "over",
        "under",
        "our",
        "their",
        "his",
        "her",
        "was",
        "were",
        "are",
        "been",
        "being",
        "have",
        "has",
        "had",
        "not",
        "but",
        "all",
        "any",
        "can",
        "will",
        "just",
        "more",
        "most",
        "other",
        "some",
        "such",
        "than",
        "too",
        "very",
        "via",
        "per",
        "in",
        "at",
        "on",
        "to",
        "of",
        "by",
        "as",
        "is",
        "it",
        "or",
        "an",
        "years",
        "year",
        "experience",
        "building",
        "built",
        "using",
        "used",
        "developing",
        "developed",
        "led",
        "leading",
        "implemented",
        "implementing",
        "designed",
        "designing",
        "architected",
        "architecting",
        "reducing",
        "reduced",
        "improving",
        "improved",
        "cutting",
        "serving",
        "achieved",
        "across",
    }
)


class GroundingError(ValueError):
    """Raised when CV-derived output claims fail grounding verification after retry."""

    def __init__(self, violations: list[str]) -> None:
        self.violations = list(violations)
        joined = "; ".join(self.violations)
        super().__init__(
            CoreErrorMessage.GROUNDING_CLAIMS_UNGROUNDED.format(violations=joined)
        )


def extract_content_tokens(text: str) -> set[str]:
    """Extract lowercase content tokens from text, excluding stopwords and pure numbers."""
    raw = {tok.lower() for tok in _WORD_TOKEN_RE.findall(text)}
    return {
        tok
        for tok in raw
        if len(tok) >= 2 and not _NUMBER_METRIC_RE.fullmatch(tok) and tok not in _STOPWORDS
    }


def _normalize_number_token(raw_num: str) -> str:
    """Normalize a number/metric token (e.g. '5M' -> '5m', '5+' -> '5', '80%' -> '80%')."""
    lowered = raw_num.strip().lower()
    if lowered.endswith("+"):
        return lowered[:-1]
    return lowered


def extract_numbers_and_metrics(text: str) -> set[str]:
    """Extract normalized numbers, percentages, and k/M/B suffixed metrics from text."""
    matches = _NUMBER_METRIC_RE.findall(text)
    return {_normalize_number_token(m) for m in matches if m}


def build_profile_corpus_text(profile: CandidateProfileParsed) -> str:
    """Concatenate all text fields from `CandidateProfileParsed`."""
    chunks: list[str] = [
        profile.personal_info.full_name,
        profile.personal_info.location or "",
        profile.professional_summary,
        *profile.skills.technical_skills,
        *profile.skills.tools_and_frameworks,
        *profile.skills.soft_skills,
        *(lang.language for lang in profile.skills.languages),
    ]
    for exp in profile.experience:
        chunks.extend(
            [
                exp.company,
                exp.job_title,
                exp.location or "",
                exp.start_date,
                exp.end_date or "",
                *exp.accomplishments,
            ]
        )
    for edu in profile.education:
        chunks.extend(
            [
                edu.institution,
                edu.degree,
                edu.field_of_study,
                str(edu.graduation_year),
                edu.gpa or "",
            ]
        )
    for cert in profile.certifications:
        chunks.extend([cert.name, cert.issuer, cert.issue_date or ""])
    for proj in profile.projects:
        chunks.extend([proj.title, proj.description, *proj.technologies])

    return " ".join(chunks)


def build_profile_corpus_tokens(profile: CandidateProfileParsed) -> set[str]:
    """Collect all content tokens present anywhere in `CandidateProfileParsed`."""
    return extract_content_tokens(build_profile_corpus_text(profile))


def build_profile_numbers(profile: CandidateProfileParsed) -> set[str]:
    """Collect all normalized numbers, percentages, and metrics present in `profile`."""
    return extract_numbers_and_metrics(build_profile_corpus_text(profile))


def check_numbers_grounded(
    claim_text: str,
    profile_numbers: set[str],
    context_label: str,
) -> list[str]:
    """Verify every number/percentage/metric in `claim_text` exists in `profile_numbers`."""
    claim_numbers = extract_numbers_and_metrics(claim_text)
    missing = sorted(claim_numbers - profile_numbers)
    if missing:
        return [
            f"{context_label} contains ungrounded number(s)/metric(s) {missing} not present in the candidate profile."
        ]
    return []


def verify_match_grounding(
    result: JobMatchAnalysisResult,
    profile: CandidateProfileParsed,
) -> list[str]:
    """Verify that every strength's `evidence_from_cv` and numbers trace back to `profile`."""
    profile_tokens = build_profile_corpus_tokens(profile)
    profile_numbers = build_profile_numbers(profile)
    violations: list[str] = []

    for idx, item in enumerate(result.strengths_analysis):
        label = f"strengths_analysis[{idx}]"
        if not item.evidence_from_cv or not item.evidence_from_cv.strip():
            violations.append(f"{label} ({item.skill_or_requirement!r}) has empty evidence_from_cv.")
            continue

        combined = f"{item.skill_or_requirement} {item.evidence_from_cv}"
        violations.extend(check_numbers_grounded(combined, profile_numbers, label))

        claim_tokens = extract_content_tokens(combined)
        if not claim_tokens or not (claim_tokens & profile_tokens):
            violations.append(
                f"{label} evidence {item.evidence_from_cv!r} does not trace to the candidate profile."
            )
    return violations


def format_retry_feedback(violations: list[str]) -> str:
    """Format grounding violations into a corrective instruction for the single retry."""
    bullet_list = "\n".join(f"- {v}" for v in violations)
    return (
        "GROUNDING VERIFICATION FAILED on your previous output. "
        "Every claim, number, metric, skill, experience bullet, and STAR story MUST be directly "
        "supported by the candidate's parsed profile without inventing companies, skills, or metrics.\n"
        "Fix the following violations:\n"
        f"{bullet_list}"
    )


async def run_with_grounding_retry[T](
    generate_fn: Callable[[list[str] | None], Awaitable[T]],
    verify_fn: Callable[[T, CandidateProfileParsed], list[str]],
    profile: CandidateProfileParsed,
) -> T:
    """Execute `generate_fn`, verify grounding against `profile`, and retry once on failure."""
    result = await generate_fn(None)
    violations = verify_fn(result, profile)
    if not violations:
        return result

    retry_result = await generate_fn(violations)
    retry_violations = verify_fn(retry_result, profile)
    if retry_violations:
        raise GroundingError(retry_violations)
    return retry_result
