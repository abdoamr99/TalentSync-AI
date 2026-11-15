"""Candidate-stage grounding guards for tailored resumes and interview preparation plans."""

import re

from app.candidate.schemas.interview_prep import InterviewPreparationPlan, InterviewRoundPrep
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.core.guards.grounding import (
    build_profile_corpus_tokens,
    build_profile_numbers,
    check_numbers_grounded,
    extract_content_tokens,
)
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements

_VERSION_SUFFIX_RE = re.compile(r"\s+\d+(?:\.\d+)*$")


def _normalize_skill_term(term: str) -> str:
    """Normalize a skill/keyword string (lowercase, strip leading dot, collapse whitespace)."""
    cleaned = term.strip().lower().lstrip(".")
    return re.sub(r"\s+", " ", cleaned)


def _strip_skill_version(norm_term: str) -> str:
    """Strip trailing version numbers from a normalized skill (e.g. 'net 10' -> 'net')."""
    return _VERSION_SUFFIX_RE.sub("", norm_term).strip()


def _build_profile_skill_matcher(profile: CandidateProfileParsed) -> tuple[set[str], str]:
    """Build a set of normalized skill forms and searchable text from `profile`."""
    explicit_skills = [
        *profile.skills.technical_skills,
        *profile.skills.tools_and_frameworks,
        *profile.skills.soft_skills,
        *(tech for proj in profile.projects for tech in proj.technologies),
    ]
    skill_forms: set[str] = set()
    for item in explicit_skills:
        norm = _normalize_skill_term(item)
        if not norm:
            continue
        skill_forms.add(norm)
        base = _strip_skill_version(norm)
        if base:
            skill_forms.add(base)
        # Also split composite terms like "ASP.NET Core" -> "asp.net core", "net"
        if "asp.net" in norm:
            skill_forms.add("net")
            skill_forms.add("asp.net")

    searchable_parts = [
        *explicit_skills,
        profile.professional_summary,
        *(acc for exp in profile.experience for acc in exp.accomplishments),
        *(proj.description for proj in profile.projects),
    ]
    searchable_text = " ".join(searchable_parts).lower()
    return skill_forms, searchable_text


def _term_in_text(term: str, text_lower: str) -> bool:
    """Return True if `term` appears as a token/phrase in `text_lower`."""
    norm = _normalize_skill_term(term)
    if not norm:
        return False
    pattern = r"(?<![a-z0-9])" + re.escape(norm) + r"(?![a-z0-9])"
    return bool(re.search(pattern, text_lower))


def _is_skill_in_profile(
    skill: str,
    skill_forms: set[str],
    searchable_text: str,
) -> bool:
    """Check whether `skill` exists in the candidate's profile (tolerating '.NET 10' vs '.NET')."""
    norm = _normalize_skill_term(skill)
    if not norm:
        return False
    base = _strip_skill_version(norm)

    if norm in skill_forms or (base and base in skill_forms):
        return True
    if _term_in_text(norm, searchable_text):
        return True
    return bool(base and len(base) >= 2 and _term_in_text(base, searchable_text))


def verify_tailored_resume_grounding(
    result: TailoredResumeResult,
    profile: CandidateProfileParsed,
    job: TargetJobRequirements | None = None,
    min_bullet_token_fraction: float = 0.6,
) -> list[str]:
    """Verify that a `TailoredResumeResult` is strictly grounded in `profile`.

    Checks:
    (a) Every number/percentage/metric in bullets and summary appears in `profile`.
    (b) Each bullet is checked against the accomplishments of ITS OWN company entry,
        requiring at least `min_bullet_token_fraction` (default 0.6) of its content
        tokens to appear there.
    (c) Every skill in `optimized_skills.top_matching_skills` and `secondary_skills`
        exists in profile skills, tools, project technologies, or accomplishment text
        (case-insensitive, tolerating version differences like '.NET 10' vs '.NET').
    (d) Any JD mandatory/preferred/ATS term that appears in a bullet must also exist
        in the profile corpus.
    """
    profile_numbers = build_profile_numbers(profile)
    skill_forms, searchable_profile_text = _build_profile_skill_matcher(profile)
    violations: list[str] = []

    # Check numbers in tailored_summary
    violations.extend(
        check_numbers_grounded(result.tailored_summary, profile_numbers, "tailored_summary")
    )

    # (c) Check optimized_skills
    all_tailored_skills = [
        ("top_matching_skills", s) for s in result.optimized_skills.top_matching_skills
    ] + [("secondary_skills", s) for s in result.optimized_skills.secondary_skills]
    for group_name, skill in all_tailored_skills:
        if not _is_skill_in_profile(skill, skill_forms, searchable_profile_text):
            violations.append(
                f"optimized_skills.{group_name} contains ungrounded skill {skill!r} not present in the candidate profile."
            )

    # Map company name -> content tokens from that company's accomplishments (plus title/company)
    company_accomplishment_tokens: dict[str, set[str]] = {}
    for exp in profile.experience:
        key = exp.company.strip().lower()
        if not key:
            continue
        acc_text = " ".join([exp.company, exp.job_title, *exp.accomplishments])
        existing = company_accomplishment_tokens.get(key, set())
        company_accomplishment_tokens[key] = existing | extract_content_tokens(acc_text)

    jd_terms: list[str] = []
    if job is not None:
        jd_terms = [
            *job.required_qualifications.mandatory_skills,
            *job.required_qualifications.preferred_skills,
            *job.ats_keywords,
        ]

    # (a), (b), (d) Check each company entry and its bullets
    for exp_idx, exp_item in enumerate(result.tailored_experience_bullet_points):
        company_key = exp_item.company.strip().lower()
        own_tokens = company_accomplishment_tokens.get(company_key)
        if own_tokens is None:
            violations.append(
                f"tailored_experience_bullet_points[{exp_idx}] references unknown company {exp_item.company!r}."
            )

        for b_idx, bullet in enumerate(exp_item.optimized_bullets):
            bullet_label = (
                f"tailored_experience_bullet_points[{exp_idx}].optimized_bullets[{b_idx}]"
            )
            if not bullet or not bullet.strip():
                violations.append(f"{bullet_label} is empty.")
                continue

            # (a) Number check
            violations.extend(check_numbers_grounded(bullet, profile_numbers, bullet_label))

            # (b) Per-company token fraction check
            bullet_tokens = extract_content_tokens(bullet)
            if not bullet_tokens:
                violations.append(f"{bullet_label} ({bullet!r}) contains no content tokens.")
            elif own_tokens is not None:
                matched = bullet_tokens & own_tokens
                fraction = len(matched) / len(bullet_tokens)
                if fraction < min_bullet_token_fraction:
                    violations.append(
                        f"{bullet_label} ({bullet!r}) has insufficient token overlap "
                        f"({fraction:.2f} < {min_bullet_token_fraction:.2f}) with accomplishments at {exp_item.company!r}."
                    )

            # (d) JD terms appearing in the bullet must also exist in the profile
            bullet_lower = bullet.lower()
            for jd_term in jd_terms:
                if _term_in_text(jd_term, bullet_lower) and not _is_skill_in_profile(
                    jd_term, skill_forms, searchable_profile_text
                ):
                    violations.append(
                        f"{bullet_label} incorporates JD term {jd_term!r} which is absent from the candidate profile."
                    )

    return violations


def verify_rounds_grounding(
    rounds: list[InterviewRoundPrep],
    profile: CandidateProfileParsed,
) -> list[str]:
    """Verify that every STAR story and its numbers/metrics in `rounds` trace back to `profile`."""
    profile_tokens = build_profile_corpus_tokens(profile)
    profile_numbers = build_profile_numbers(profile)
    violations: list[str] = []

    for r_idx, round_prep in enumerate(rounds):
        for s_idx, story in enumerate(round_prep.star_stories):
            label = f"interview_rounds_prep[{r_idx}].star_stories[{s_idx}]"
            story_text = " ".join(
                [
                    story.story_title,
                    story.target_skill,
                    story.situation,
                    story.task,
                    story.action,
                    story.result,
                ]
            )
            violations.extend(check_numbers_grounded(story_text, profile_numbers, label))

            story_tokens = extract_content_tokens(story_text)
            if not story_tokens or not (story_tokens & profile_tokens):
                violations.append(
                    f"{label} ({story.story_title!r}) does not trace back to the candidate profile."
                )

    return violations


def verify_interview_prep_grounding(
    result: InterviewPreparationPlan,
    profile: CandidateProfileParsed,
) -> list[str]:
    """Verify that every STAR story in `InterviewPreparationPlan` traces back to `profile`."""
    return verify_rounds_grounding(result.interview_rounds_prep, profile)

