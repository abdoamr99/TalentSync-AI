"""FastAPI endpoint, authentication, error mapping, OpenAPI contract, and hygiene tests."""

from pathlib import Path
from typing import Any
from unittest.mock import AsyncMock, patch

import pytest
from fastapi.testclient import TestClient
from pydantic import ValidationError

from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.core.config import Settings, get_settings
from app.core.guards.grounding import GroundingError
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult
from app.core.tools.web_search import LoginWallError
from app.main import app

client = TestClient(app)
API_HEADERS = {
    "X-Internal-Api-Key": get_settings().INTERNAL_API_KEY,
    "X-Run-Id": "run-test-001",
}


def test_health_endpoint_returns_200() -> None:
    response = client.get("/")
    assert response.status_code == 200
    settings = get_settings()
    assert response.json() == {
        "status": "healthy",
        "app_name": settings.APP_NAME,
        "app_version": settings.APP_VERSION,
        "app_description": settings.APP_DESCRIPTION,
    }


def test_settings_fails_when_internal_api_key_unset(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("INTERNAL_API_KEY", raising=False)
    with pytest.raises(ValidationError):
        Settings(_env_file=None)


def test_openapi_lists_all_candidate_routes_and_contract_schemas() -> None:
    response = client.get("/openapi.json")
    assert response.status_code == 200
    spec = response.json()

    expected_paths = {
        "/",
        "/v1/candidate/documents/parse",
        "/v1/candidate/cv/parse",
        "/v1/candidate/job/parse",
        "/v1/candidate/match",
        "/v1/candidate/research",
        "/v1/candidate/tailor",
        "/v1/candidate/prep",
    }
    assert expected_paths.issubset(set(spec["paths"].keys()))

    component_schemas = set(spec["components"]["schemas"].keys())
    expected_contract_schemas = {
        "CandidateProfileParsed",
        "TargetJobRequirements",
        "JobMatchAnalysisResult",
        "CompanyRoleResearchResult",
        "TailoredResumeResult",
        "InterviewPreparationPlan",
    }
    assert expected_contract_schemas.issubset(component_schemas)


def test_missing_or_invalid_api_key_returns_401() -> None:
    resp_missing = client.post("/v1/candidate/cv/parse", json={"text": "Sample CV"})
    assert resp_missing.status_code == 401

    resp_bad = client.post(
        "/v1/candidate/cv/parse",
        headers={"X-Internal-Api-Key": "wrong-key"},
        json={"text": "Sample CV"},
    )
    assert resp_bad.status_code == 401


def test_error_mapping_502_504_and_422(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
) -> None:
    # GroundingError -> 502 {code, message}
    with patch(
        "app.candidate.candidate_router.tailor_resume",
        new=AsyncMock(side_effect=GroundingError(["Ungrounded bullet"])),
    ):
        resp_502_grounding = client.post(
            "/v1/candidate/tailor",
            headers=API_HEADERS,
            json={
                "profile": sample_profile_dict,
                "job": sample_job_dict,
                "match": sample_match_dict,
            },
        )
    assert resp_502_grounding.status_code == 502
    body_502 = resp_502_grounding.json()
    assert body_502["code"] == "GROUNDING_ERROR"
    assert "Ungrounded bullet" in body_502["message"]

    # LLM/provider RuntimeError -> 502 {code, message}
    with patch(
        "app.candidate.candidate_router.parse_cv",
        new=AsyncMock(side_effect=RuntimeError("OpenAI upstream 500")),
    ):
        resp_502_provider = client.post(
            "/v1/candidate/cv/parse",
            headers=API_HEADERS,
            json={"text": "# Resume"},
        )
    assert resp_502_provider.status_code == 502
    assert resp_502_provider.json()["code"] in {"PROVIDER_ERROR", "CV_PARSE_ERROR"}

    # Recon timeout -> 504 {code, message}
    with patch(
        "app.candidate.candidate_router.research_company_and_role",
        new=AsyncMock(side_effect=TimeoutError("Timed out after 180s")),
    ):
        resp_504 = client.post(
            "/v1/candidate/research",
            headers=API_HEADERS,
            json={"company_name": "TechCorp"},
        )
    assert resp_504.status_code == 504
    assert resp_504.json()["code"] == "RECON_TIMEOUT"

    # Empty document upload -> 422
    resp_422_empty_doc = client.post(
        "/v1/candidate/documents/parse",
        headers=API_HEADERS,
        files={"file": ("empty.pdf", b"", "application/pdf")},
    )
    assert resp_422_empty_doc.status_code == 422


def test_all_candidate_endpoints_with_mocked_services(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
    sample_research_dict: dict[str, Any],
    sample_tailored_resume_dict: dict[str, Any],
    sample_interview_prep_dict: dict[str, Any],
) -> None:
    # 1. /v1/candidate/documents/parse
    with patch(
        "app.candidate.candidate_router.parse_document_bytes",
        return_value="# Parsed Markdown CV",
    ):
        resp_doc = client.post(
            "/v1/candidate/documents/parse",
            headers=API_HEADERS,
            files={"file": ("cv.pdf", b"%PDF-1.4", "application/pdf")},
        )
    assert resp_doc.status_code == 200
    assert resp_doc.json() == {"text": "# Parsed Markdown CV"}

    # 2. /v1/candidate/cv/parse
    with patch(
        "app.candidate.candidate_router.parse_cv",
        new=AsyncMock(return_value=CandidateProfileParsed.model_validate(sample_profile_dict)),
    ):
        resp_cv = client.post(
            "/v1/candidate/cv/parse",
            headers=API_HEADERS,
            json={"text": "# Abdelrahman Amr"},
        )
    assert resp_cv.status_code == 200
    assert resp_cv.json() == sample_profile_dict

    # 3. /v1/candidate/job/parse (success + login-wall 422)
    with patch(
        "app.candidate.candidate_router.parse_job",
        new=AsyncMock(return_value=TargetJobRequirements.model_validate(sample_job_dict)),
    ):
        resp_job = client.post(
            "/v1/candidate/job/parse",
            headers=API_HEADERS,
            json={"text": "Senior Backend Engineer"},
        )
    assert resp_job.status_code == 200
    assert resp_job.json() == sample_job_dict

    with patch(
        "app.candidate.candidate_router.parse_job",
        new=AsyncMock(side_effect=LoginWallError("Auth wall detected")),
    ):
        resp_job_422 = client.post(
            "/v1/candidate/job/parse",
            headers=API_HEADERS,
            json={"url": "https://linkedin.com/jobs/view/1"},
        )
    assert resp_job_422.status_code == 422

    # 4. /v1/candidate/match
    with patch(
        "app.candidate.candidate_router.analyze_match",
        new=AsyncMock(return_value=JobMatchAnalysisResult.model_validate(sample_match_dict)),
    ):
        resp_match = client.post(
            "/v1/candidate/match",
            headers=API_HEADERS,
            json={"profile": sample_profile_dict, "job": sample_job_dict},
        )
    assert resp_match.status_code == 200
    assert resp_match.json() == sample_match_dict

    # 5. /v1/candidate/research
    with patch(
        "app.candidate.candidate_router.research_company_and_role",
        new=AsyncMock(return_value=CompanyRoleResearchResult.model_validate(sample_research_dict)),
    ):
        resp_research = client.post(
            "/v1/candidate/research",
            headers=API_HEADERS,
            json={"company_name": "TechCorp Global", "job_title": "Senior Backend Engineer"},
        )
    assert resp_research.status_code == 200
    assert resp_research.json() == sample_research_dict

    # 6. /v1/candidate/tailor
    with patch(
        "app.candidate.candidate_router.tailor_resume",
        new=AsyncMock(
            return_value=TailoredResumeResult.model_validate(sample_tailored_resume_dict)
        ),
    ):
        resp_tailor = client.post(
            "/v1/candidate/tailor",
            headers=API_HEADERS,
            json={
                "profile": sample_profile_dict,
                "job": sample_job_dict,
                "match": sample_match_dict,
                "research": sample_research_dict,
            },
        )
    assert resp_tailor.status_code == 200
    assert resp_tailor.json() == sample_tailored_resume_dict

    # 7. /v1/candidate/prep
    with patch(
        "app.candidate.candidate_router.prepare_interview",
        new=AsyncMock(
            return_value=InterviewPreparationPlan.model_validate(sample_interview_prep_dict)
        ),
    ):
        resp_prep = client.post(
            "/v1/candidate/prep",
            headers=API_HEADERS,
            json={
                "profile": sample_profile_dict,
                "job": sample_job_dict,
                "match": sample_match_dict,
                "research": sample_research_dict,
                "tailored_resume": sample_tailored_resume_dict,
                "prep_timeline_days": 2,
            },
        )
    assert resp_prep.status_code == 200
    assert resp_prep.json() == sample_interview_prep_dict

    # 8. /v1/candidate/tailor/pdf
    with patch(
        "app.candidate.candidate_router.render_resume_pdf_bytes",
        return_value=b"%PDF-1.4-tailored-resume",
    ):
        resp_tailor_pdf = client.post(
            "/v1/candidate/tailor/pdf",
            headers=API_HEADERS,
            json={"tailored_resume": sample_tailored_resume_dict},
        )
    assert resp_tailor_pdf.status_code == 200
    assert resp_tailor_pdf.headers["content-type"] == "application/pdf"
    assert resp_tailor_pdf.content == b"%PDF-1.4-tailored-resume"

    # 9. /v1/candidate/prep/battlecard/html & /v1/candidate/prep/battlecard/pdf
    resp_bc_html = client.post(
        "/v1/candidate/prep/battlecard/html",
        headers=API_HEADERS,
        json={
            "interview_prep": sample_interview_prep_dict,
            "profile": sample_profile_dict,
            "job": sample_job_dict,
            "research": sample_research_dict,
        },
    )
    assert resp_bc_html.status_code == 200
    assert "text/html" in resp_bc_html.headers["content-type"]
    assert "Interview Battle Card — Abdelrahman Amr" in resp_bc_html.text

    with patch(
        "app.candidate.candidate_router.render_battlecard_pdf_bytes",
        return_value=b"%PDF-1.4-battlecard",
    ):
        resp_bc_pdf = client.post(
            "/v1/candidate/prep/battlecard/pdf",
            headers=API_HEADERS,
            json={
                "interview_prep": sample_interview_prep_dict,
                "profile": sample_profile_dict,
                "job": sample_job_dict,
                "research": sample_research_dict,
            },
        )
    assert resp_bc_pdf.status_code == 200
    assert resp_bc_pdf.headers["content-type"] == "application/pdf"
    assert resp_bc_pdf.content == b"%PDF-1.4-battlecard"


def test_codebase_hygiene() -> None:
    """Verify no forbidden imports/virtual paths exist in app/ code and prompts."""
    root = Path(__file__).resolve().parent.parent
    app_dir = root / "app"
    forbidden_substrings = (
        "deepagents",
        "langgraph",
        "/processed/",
        "/research/",
        "/upload/",
        "/tailored_resume/",
        "/interview_coach/",
        "/interview_battlecard/",
    )

    for file_path in app_dir.rglob("*"):
        if not file_path.is_file() or file_path.suffix not in {".py", ".md"}:
            continue
        content = file_path.read_text(encoding="utf-8")
        assert "\r\n" not in content, f"CRLF found in {file_path}"
        for forbidden in forbidden_substrings:
            assert forbidden not in content.lower(), (
                f"Forbidden string {forbidden!r} in {file_path}"
            )
