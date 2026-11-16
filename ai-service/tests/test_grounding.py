"""Unit tests for grounding verification (core and candidate guards) and single-retry logic."""

from pathlib import Path

import pytest

from app.candidate.guards.grounding import (
    verify_interview_prep_grounding,
    verify_tailored_resume_grounding,
)
from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.tailored_resume import (
    OptimizedSkills,
    TailoredExperienceItem,
    TailoredResumeResult,
)
from app.core.guards.grounding import (
    GroundingError,
    run_with_grounding_retry,
    verify_match_grounding,
)
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult, StrengthAnalysisItem


def test_core_does_not_import_from_candidate() -> None:
    """Ensure app/core never imports from app/candidate."""
    core_dir = Path(__file__).resolve().parent.parent / "app" / "core"
    for py_file in core_dir.rglob("*.py"):
        content = py_file.read_text(encoding="utf-8")
        assert "app.candidate" not in content, f"{py_file} imports from app.candidate"


def test_regression_techcorp_kubernetes_kafka_bullet_and_skills_produce_violations() -> None:
    """Regression test: C#/SQL Server/ASP.NET Core profile vs fabricated Kubernetes/Kafka/5M/80% bullet."""
    profile = CandidateProfileParsed.model_validate(
        {
            "personal_info": {
                "full_name": "Abdelrahman Amr",
                "email": "abdelrahman@example.com",
            },
            "professional_summary": "Backend engineer experienced in C#, SQL Server, and ASP.NET Core.",
            "skills": {
                "technical_skills": ["C#", "SQL Server"],
                "tools_and_frameworks": ["ASP.NET Core"],
                "soft_skills": [],
                "languages": [{"language": "English", "proficiency": "Fluent"}],
            },
            "experience": [
                {
                    "company": "TechCorp",
                    "job_title": "Backend Developer",
                    "start_date": "2022-01",
                    "end_date": None,
                    "is_current": True,
                    "accomplishments": [
                        "Built RESTful APIs using C#, ASP.NET Core, and SQL Server for enterprise clients."
                    ],
                }
            ],
            "education": [
                {
                    "institution": "Cairo University",
                    "degree": "B.Sc.",
                    "field_of_study": "Computer Science",
                    "graduation_year": 2022,
                }
            ],
            "certifications": [],
            "projects": [],
        }
    )

    job = TargetJobRequirements.model_validate(
        {
            "job_metadata": {
                "job_title": "Senior Backend Engineer",
                "company_name": "Acme",
                "location": "Remote",
                "workplace_type": "Remote",
                "employment_type": "Full-Time",
            },
            "role_summary": "Distributed systems role.",
            "required_qualifications": {
                "mandatory_skills": ["C#", "Kubernetes", "Kafka"],
                "preferred_skills": ["SQL Server"],
                "minimum_years_of_experience": 3,
                "degree_requirement": "B.Sc.",
            },
            "core_responsibilities": ["Build microservices"],
            "ats_keywords": ["Kubernetes", "Kafka"],
        }
    )

    fabricated_tailored = TailoredResumeResult(
        tailored_headline="Senior Cloud Engineer",
        tailored_summary="Backend engineer experienced in C#, SQL Server, and ASP.NET Core.",
        optimized_skills=OptimizedSkills(
            top_matching_skills=["C#", "Kubernetes", "Kafka"],
            secondary_skills=["SQL Server"],
        ),
        tailored_experience_bullet_points=[
            TailoredExperienceItem(
                company="TechCorp",
                job_title="Backend Developer",
                optimized_bullets=[
                    "Led Kubernetes and Kafka migration serving 5M users, cutting latency by 80% at TechCorp"
                ],
            )
        ],
        full_markdown_content="# Abdelrahman Amr",
        rendercv_ready_payload={},
    )

    violations = verify_tailored_resume_grounding(fabricated_tailored, profile, job=job)
    joined = "\n".join(violations)
    assert violations, "Expected grounding violations for fabricated bullet and skills"
    # Verify all 4 checks triggered:
    assert "5m" in joined and "80%" in joined  # (a) ungrounded numbers/metrics
    assert "insufficient token overlap" in joined  # (b) < 0.6 overlap with TechCorp accomplishments
    assert "'Kubernetes'" in joined and "'Kafka'" in joined  # (c) & (d) ungrounded skills and JD terms


def test_dotnet_version_tolerance_in_skill_grounding() -> None:
    """Verify '.NET 10' vs '.NET' is tolerated when checking optimized_skills."""
    profile = CandidateProfileParsed.model_validate(
        {
            "personal_info": {"full_name": "Jane Doe", "email": "jane@example.com"},
            "professional_summary": "Engineer working with .NET and C#.",
            "skills": {
                "technical_skills": ["C#", ".NET"],
                "tools_and_frameworks": [],
                "soft_skills": [],
                "languages": [],
            },
            "experience": [
                {
                    "company": "TechCorp",
                    "job_title": "Engineer",
                    "start_date": "2022-01",
                    "is_current": True,
                    "accomplishments": ["Developed web services in C# and .NET."],
                }
            ],
            "education": [],
            "certifications": [],
            "projects": [],
        }
    )
    tailored = TailoredResumeResult(
        tailored_headline=".NET Engineer",
        tailored_summary="Engineer working with .NET and C#.",
        optimized_skills=OptimizedSkills(
            top_matching_skills=[".NET 10", "C#"],
            secondary_skills=[],
        ),
        tailored_experience_bullet_points=[
            TailoredExperienceItem(
                company="TechCorp",
                job_title="Engineer",
                optimized_bullets=["Developed web services in C# and .NET."],
            )
        ],
        full_markdown_content="# Jane Doe",
        rendercv_ready_payload={},
    )
    violations = verify_tailored_resume_grounding(tailored, profile)
    assert violations == []


def test_verify_match_grounding_detects_ungrounded_strength_and_numbers(
    sample_profile_dict: dict,
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    ungrounded = JobMatchAnalysisResult(
        match_score=80,
        match_verdict="Good Match",
        strengths_analysis=[
            StrengthAnalysisItem(
                skill_or_requirement="Quantum Rust Kernel",
                evidence_from_cv="99% uptime building quantum kernels in Rust at SpaceX",
                importance="High",
            )
        ],
        skill_gaps=[],
        strategic_recommendations=[],
    )
    violations = verify_match_grounding(ungrounded, profile)
    assert len(violations) >= 2
    assert any("99%" in v for v in violations)


def test_verify_interview_prep_grounding_detects_fabricated_star_story_and_numbers(
    sample_profile_dict: dict,
    sample_interview_prep_dict: dict,
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    bad_prep = InterviewPreparationPlan.model_validate(
        {
            **sample_interview_prep_dict,
            "interview_rounds_prep": [
                {
                    "round_name": "Round 1",
                    "key_focus_areas": ["Kernel Hacking"],
                    "likely_questions": ["Tell me about eBPF."],
                    "star_stories": [
                        {
                            "story_title": "Wrote Solaris Driver",
                            "target_skill": "Assembly",
                            "situation": "Mainframe tape drive failed in Cobol.",
                            "task": "Rewrite firmware in SPARC assembly.",
                            "action": "Patched punchcard reader interrupts.",
                            "result": "Saved 999M terawatts and 77% power.",
                        }
                    ],
                }
            ],
        }
    )
    violations = verify_interview_prep_grounding(bad_prep, profile)
    assert len(violations) >= 2
    assert any("77%" in v or "999m" in v for v in violations)


async def test_run_with_grounding_retry_succeeds_on_second_attempt(
    sample_profile_dict: dict,
    sample_match_dict: dict,
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    valid_match = JobMatchAnalysisResult.model_validate(sample_match_dict)
    invalid_match = JobMatchAnalysisResult(
        match_score=80,
        match_verdict="Good",
        strengths_analysis=[
            StrengthAnalysisItem(
                skill_or_requirement="Cobol Mainframe",
                evidence_from_cv="Programmed punchcards in Fortran at Enron",
                importance="High",
            )
        ],
        skill_gaps=[],
        strategic_recommendations=[],
    )

    calls: list[list[str] | None] = []

    async def _gen(violations: list[str] | None) -> JobMatchAnalysisResult:
        calls.append(violations)
        if violations is None:
            return invalid_match
        return valid_match

    result = await run_with_grounding_retry(_gen, verify_match_grounding, profile)
    assert result == valid_match
    assert len(calls) == 2
    assert calls[0] is None
    assert calls[1] is not None and len(calls[1]) >= 1


async def test_run_with_grounding_retry_raises_after_single_retry(
    sample_profile_dict: dict,
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    invalid_match = JobMatchAnalysisResult(
        match_score=80,
        match_verdict="Good",
        strengths_analysis=[
            StrengthAnalysisItem(
                skill_or_requirement="Cobol Mainframe",
                evidence_from_cv="Programmed punchcards in Fortran at Enron",
                importance="High",
            )
        ],
        skill_gaps=[],
        strategic_recommendations=[],
    )

    call_count = 0

    async def _gen(violations: list[str] | None) -> JobMatchAnalysisResult:
        nonlocal call_count
        call_count += 1
        return invalid_match

    with pytest.raises(GroundingError) as exc_info:
        await run_with_grounding_retry(_gen, verify_match_grounding, profile)

    assert call_count == 2
    assert len(exc_info.value.violations) >= 1
