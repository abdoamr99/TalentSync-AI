"""Round-trip validation tests for all six authoritative candidate schemas."""

from typing import Any

import pytest
from pydantic import ValidationError

from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.requests import JobParseRequest
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult


def test_candidate_profile_parsed_round_trip(sample_profile_dict: dict[str, Any]) -> None:
    """Contract 3.1: CandidateProfileParsed round-trips sample JSON identically."""
    model = CandidateProfileParsed.model_validate(sample_profile_dict)
    dumped = model.model_dump()
    assert dumped == sample_profile_dict
    reloaded = CandidateProfileParsed.model_validate_json(model.model_dump_json())
    assert reloaded == model


def test_candidate_profile_optional_fields_default_none() -> None:
    """Contract 3.1: Nullable fields ('string?', 'YYYY-MM?') default to None when omitted."""
    minimal = {
        "personal_info": {
            "full_name": "Jane Doe",
            "email": "jane@example.com",
        },
        "professional_summary": "Experienced software engineer.",
        "skills": {
            "technical_skills": ["Python"],
            "tools_and_frameworks": ["FastAPI"],
            "soft_skills": ["Communication"],
            "languages": [{"language": "English", "proficiency": "Native"}],
        },
        "experience": [
            {
                "company": "Acme",
                "job_title": "Engineer",
                "start_date": "2022-01",
                "is_current": True,
                "accomplishments": ["Shipped APIs"],
            }
        ],
        "education": [
            {
                "institution": "MIT",
                "degree": "B.Sc.",
                "field_of_study": "CS",
                "graduation_year": 2021,
            }
        ],
        "certifications": [{"name": "AWS SAA", "issuer": "AWS"}],
        "projects": [{"title": "Demo", "description": "App", "technologies": ["Python"]}],
    }
    parsed = CandidateProfileParsed.model_validate(minimal)
    assert parsed.personal_info.phone is None
    assert parsed.personal_info.location is None
    assert parsed.personal_info.linkedin_url is None
    assert parsed.experience[0].end_date is None
    assert parsed.education[0].gpa is None
    assert parsed.certifications[0].issue_date is None
    assert parsed.projects[0].project_url is None


def test_target_job_requirements_round_trip(sample_job_dict: dict[str, Any]) -> None:
    """Contract 3.2: TargetJobRequirements round-trips sample JSON identically."""
    model = TargetJobRequirements.model_validate(sample_job_dict)
    assert model.model_dump() == sample_job_dict
    reloaded = TargetJobRequirements.model_validate_json(model.model_dump_json())
    assert reloaded == model


def test_job_match_analysis_result_round_trip(sample_match_dict: dict[str, Any]) -> None:
    """Contract 3.3: JobMatchAnalysisResult round-trips sample JSON identically."""
    model = JobMatchAnalysisResult.model_validate(sample_match_dict)
    assert model.model_dump() == sample_match_dict
    reloaded = JobMatchAnalysisResult.model_validate_json(model.model_dump_json())
    assert reloaded == model

    with pytest.raises(ValidationError):
        JobMatchAnalysisResult.model_validate({**sample_match_dict, "match_score": 105})


def test_company_role_research_result_round_trip(sample_research_dict: dict[str, Any]) -> None:
    """Contract 3.4: CompanyRoleResearchResult round-trips sample JSON identically."""
    model = CompanyRoleResearchResult.model_validate(sample_research_dict)
    assert model.model_dump() == sample_research_dict
    reloaded = CompanyRoleResearchResult.model_validate_json(model.model_dump_json())
    assert reloaded == model


def test_tailored_resume_result_round_trip(sample_tailored_resume_dict: dict[str, Any]) -> None:
    """Contract 3.5: TailoredResumeResult round-trips sample JSON identically."""
    model = TailoredResumeResult.model_validate(sample_tailored_resume_dict)
    assert model.model_dump() == sample_tailored_resume_dict
    reloaded = TailoredResumeResult.model_validate_json(model.model_dump_json())
    assert reloaded == model


def test_interview_preparation_plan_round_trip(sample_interview_prep_dict: dict[str, Any]) -> None:
    """Contract 3.6: InterviewPreparationPlan round-trips sample JSON identically."""
    model = InterviewPreparationPlan.model_validate(sample_interview_prep_dict)
    assert model.model_dump() == sample_interview_prep_dict
    reloaded = InterviewPreparationPlan.model_validate_json(model.model_dump_json())
    assert reloaded == model


def test_job_parse_request_requires_text_or_url() -> None:
    """JobParseRequest rejects empty payloads where both text and url are missing."""
    with pytest.raises(ValidationError):
        JobParseRequest(text=None, url=None)
    with pytest.raises(ValidationError):
        JobParseRequest(text="   ", url="")
    valid = JobParseRequest(url="https://example.com/jobs/1")
    assert valid.url == "https://example.com/jobs/1"

