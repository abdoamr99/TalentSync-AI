"""Mocked-LLM unit tests for every candidate and core service plus deterministic helpers."""

from datetime import date
from pathlib import Path
from typing import Any
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.research import CompanyRoleResearchResult, InterviewProcessInsights
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.candidate.services.company_recon import (
    RECON_RECURSION_LIMIT,
    research_company_and_role,
)
from app.candidate.services.interview_prep import (
    PitchBattlecardStudyPart,
    RoundsAndStoriesPart,
    _resolve_rounds,
    prepare_interview,
)
from app.candidate.services.job_parser import parse_job
from app.candidate.services.pdf_renderer import (
    _hydrate_render_copy,
    build_battlecard_document,
    render_battlecard_html,
    render_battlecard_pdf_bytes,
    render_resume_pdf_bytes,
)
from app.candidate.services.resume_tailor import (
    TailoredResumeLLMOutput,
    _coerce_string_entry,
    _normalize_resume_yaml,
    build_rendercv_payload,
    build_rendercv_yaml,
    select_rendercv_theme,
    tailor_resume,
)
from app.core.llm import clear_model_cache, get_chat_model
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult
from app.core.services.cv_parser import parse_cv
from app.core.services.matcher import (
    MatchJudgement,
    SkillAssessment,
    analyze_match,
    build_match_result_from_judgement,
    compute_years_of_experience,
)
from app.core.tools.documents import _strip_image_filenames, parse_document_bytes
from app.core.tools.web_search import LoginWallError, fetch_url_text


def _make_mock_llm(structured_return: Any) -> tuple[MagicMock, MagicMock]:
    """Create a mocked BaseChatModel and its inner structured runnable."""
    runnable = MagicMock()
    runnable.ainvoke = AsyncMock(return_value=structured_return)
    llm = MagicMock()
    llm.with_structured_output = MagicMock(return_value=runnable)
    return llm, runnable


async def test_cv_parser_service_returns_candidate_profile(
    sample_profile_dict: dict[str, Any],
) -> None:
    expected = CandidateProfileParsed.model_validate(sample_profile_dict)
    mock_llm, _ = _make_mock_llm(expected)

    result = await parse_cv("# Abdelrahman Amr\nSenior .NET Engineer", model=mock_llm)
    assert isinstance(result, CandidateProfileParsed)
    assert result == expected
    mock_llm.with_structured_output.assert_called_once_with(CandidateProfileParsed)


async def test_job_parser_service_with_text_returns_target_job_requirements(
    sample_job_dict: dict[str, Any],
) -> None:
    expected = TargetJobRequirements.model_validate(sample_job_dict)
    mock_llm, _ = _make_mock_llm(expected)

    result = await parse_job(text="Senior Backend Engineer at TechCorp Global...", model=mock_llm)
    assert isinstance(result, TargetJobRequirements)
    assert result == expected
    mock_llm.with_structured_output.assert_called_once_with(TargetJobRequirements)


async def test_job_parser_service_with_url_fetches_text_in_thread(
    sample_job_dict: dict[str, Any],
) -> None:
    expected = TargetJobRequirements.model_validate(sample_job_dict)
    mock_llm, _ = _make_mock_llm(expected)
    long_jd_body = (
        "We are looking for a Senior Backend Engineer with 5+ years of C# and .NET 10 experience. "
        * 10
    )

    with patch(
        "app.core.tools.web_search._tavily_extract_one",
        return_value=("Senior Backend Engineer", long_jd_body),
    ):
        result = await parse_job(url="https://example.com/jobs/123", model=mock_llm)

    assert isinstance(result, TargetJobRequirements)
    assert result == expected


def test_deterministic_experience_years_and_match_score_rubric(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    job = TargetJobRequirements.model_validate(sample_job_dict)

    years = compute_years_of_experience(profile, reference_date=date(2026, 3, 1))
    assert years == pytest.approx(5.0)

    judgement = MatchJudgement(
        mandatory_skill_assessments=[
            SkillAssessment(skill="C#", status="demonstrated", evidence="C# Web APIs"),
            SkillAssessment(skill=".NET 10", status="demonstrated", evidence=".NET 10"),
            SkillAssessment(skill="SQL Server", status="demonstrated", evidence="SQL Server"),
            SkillAssessment(skill="Docker", status="demonstrated", evidence="Docker"),
        ],
        preferred_skill_assessments=[
            SkillAssessment(skill="Kubernetes", status="missing", evidence=None),
            SkillAssessment(skill="Redis", status="demonstrated", evidence="Redis"),
            SkillAssessment(skill="Kafka", status="adjacent", evidence="RabbitMQ"),
            SkillAssessment(skill="Elasticsearch", status="missing", evidence=None),
        ],
        degree_status="demonstrated",
        strengths_analysis=JobMatchAnalysisResult.model_validate(
            sample_match_dict
        ).strengths_analysis,
        skill_gaps=JobMatchAnalysisResult.model_validate(sample_match_dict).skill_gaps,
        strategic_recommendations=sample_match_dict["strategic_recommendations"],
    )

    built = build_match_result_from_judgement(
        judgement, profile, job, reference_date=date(2026, 3, 1)
    )
    # Mandatory: 4/4 = 50 pts; Preferred: (0 + 1 + 0.5 + 0)/4 * 20 = 7.5 pts; Exp: 5/5 * 20 = 20 pts; Degree: 10 pts -> total 87.5 -> 88
    assert built.match_score == 88
    assert built.match_verdict == "Strong Candidate with Minor Gaps"


async def test_matcher_service_uses_match_judgement_and_excludes_pii(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    job = TargetJobRequirements.model_validate(sample_job_dict)
    expected_match = JobMatchAnalysisResult.model_validate(sample_match_dict)

    judgement = MatchJudgement(
        mandatory_skill_assessments=[
            SkillAssessment(skill="C#", status="demonstrated", evidence="C#"),
            SkillAssessment(skill=".NET 10", status="demonstrated", evidence=".NET 10"),
            SkillAssessment(skill="SQL Server", status="demonstrated", evidence="SQL Server"),
            SkillAssessment(skill="Docker", status="demonstrated", evidence="Docker"),
        ],
        preferred_skill_assessments=[
            SkillAssessment(skill="Redis", status="demonstrated", evidence="Redis"),
            SkillAssessment(skill="Kafka", status="adjacent", evidence="RabbitMQ"),
        ],
        degree_status="demonstrated",
        strengths_analysis=expected_match.strengths_analysis,
        skill_gaps=expected_match.skill_gaps,
        strategic_recommendations=expected_match.strategic_recommendations,
    )
    mock_llm, runnable = _make_mock_llm(judgement)

    result = await analyze_match(profile=profile, job=job, model=mock_llm)
    assert isinstance(result, JobMatchAnalysisResult)
    mock_llm.with_structured_output.assert_called_once_with(MatchJudgement)

    # Verify PII (full_name, email, phone) was excluded from the prompt sent to the LLM
    sent_messages = runnable.ainvoke.call_args.args[0]
    human_text = sent_messages[1].content
    assert "abdelrahman@example.com" not in human_text
    assert "+201000000000" not in human_text


async def test_company_recon_service_uses_create_agent_timeout_and_recursion_limit(
    sample_research_dict: dict[str, Any],
) -> None:
    expected = CompanyRoleResearchResult.model_validate(sample_research_dict)
    mock_agent = MagicMock()
    mock_agent.ainvoke = AsyncMock(return_value={"structured_response": expected})
    mock_llm = MagicMock()

    with patch(
        "app.candidate.services.company_recon.create_agent",
        return_value=mock_agent,
    ) as create_agent_mock:
        result = await research_company_and_role(
            company_name="TechCorp Global",
            job_title="Senior Backend Engineer",
            job_url="https://example.com/jobs/123",
            model=mock_llm,
        )

    assert isinstance(result, CompanyRoleResearchResult)
    assert result == expected
    create_agent_mock.assert_called_once()
    call_kwargs = create_agent_mock.call_args.kwargs
    assert call_kwargs["response_format"] is CompanyRoleResearchResult
    assert "Current UTC date:" in call_kwargs["system_prompt"]
    assert mock_agent.ainvoke.call_args.kwargs["config"] == {
        "recursion_limit": RECON_RECURSION_LIMIT
    }


async def test_resume_tailor_uses_compact_llm_schema_excludes_pii_and_renders_jinja(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
    sample_research_dict: dict[str, Any],
    sample_tailored_resume_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    job = TargetJobRequirements.model_validate(sample_job_dict)
    match = JobMatchAnalysisResult.model_validate(sample_match_dict)
    research = CompanyRoleResearchResult.model_validate(sample_research_dict)

    llm_output = TailoredResumeLLMOutput(
        tailored_headline=sample_tailored_resume_dict["tailored_headline"],
        tailored_summary=sample_tailored_resume_dict["tailored_summary"],
        optimized_skills=sample_tailored_resume_dict["optimized_skills"],
        tailored_experience_bullet_points=sample_tailored_resume_dict[
            "tailored_experience_bullet_points"
        ],
    )
    mock_llm, runnable = _make_mock_llm(llm_output)

    result = await tailor_resume(
        profile=profile,
        job=job,
        match=match,
        research=research,
        model=mock_llm,
    )
    assert isinstance(result, TailoredResumeResult)
    mock_llm.with_structured_output.assert_called_once_with(TailoredResumeLLMOutput)

    # Verify PII was excluded from the LLM prompt, but used in the rendered Markdown
    sent_messages = runnable.ainvoke.call_args.args[0]
    human_text = sent_messages[1].content
    assert "abdelrahman@example.com" not in human_text
    assert "# Abdelrahman Amr" in result.full_markdown_content
    assert "abdelrahman@example.com" in result.full_markdown_content
    assert "## Senior .NET & Distributed Systems Engineer" in result.full_markdown_content


async def test_interview_prep_service_runs_two_parallel_calls_excludes_pii_and_merges(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_match_dict: dict[str, Any],
    sample_research_dict: dict[str, Any],
    sample_interview_prep_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    job = TargetJobRequirements.model_validate(sample_job_dict)
    match = JobMatchAnalysisResult.model_validate(sample_match_dict)
    research = CompanyRoleResearchResult.model_validate(sample_research_dict)
    expected = InterviewPreparationPlan.model_validate(sample_interview_prep_dict)

    rounds_part = RoundsAndStoriesPart(interview_rounds_prep=expected.interview_rounds_prep)
    pitch_part = PitchBattlecardStudyPart(
        elevator_pitch_60_seconds=expected.elevator_pitch_60_seconds,
        day_of_battlecard=expected.day_of_battlecard,
        time_boxed_study_plan=expected.time_boxed_study_plan,
    )

    rounds_runnable = MagicMock()
    rounds_runnable.ainvoke = AsyncMock(return_value=rounds_part)
    pitch_runnable = MagicMock()
    pitch_runnable.ainvoke = AsyncMock(return_value=pitch_part)

    def _with_structured_output(schema: type) -> MagicMock:
        if schema is RoundsAndStoriesPart:
            return rounds_runnable
        if schema is PitchBattlecardStudyPart:
            return pitch_runnable
        raise AssertionError(f"Unexpected schema: {schema}")

    mock_llm = MagicMock()
    mock_llm.with_structured_output = MagicMock(side_effect=_with_structured_output)

    result = await prepare_interview(
        profile=profile,
        job=job,
        match=match,
        research=research,
        prep_timeline_days=2,
        model=mock_llm,
    )
    assert isinstance(result, InterviewPreparationPlan)
    assert result == expected
    assert rounds_runnable.ainvoke.await_count == 1
    assert pitch_runnable.ainvoke.await_count == 1

    # Verify PII was excluded from the LLM prompt
    sent_messages = rounds_runnable.ainvoke.call_args.args[0]
    assert "abdelrahman@example.com" not in sent_messages[1].content

    # Verify fallback rounds mark generic focus when research has no rounds
    empty_rounds_research = research.model_copy(
        update={"interview_process_insights": InterviewProcessInsights(standard_rounds=[])}
    )
    fallback_rounds = _resolve_rounds(empty_rounds_research)
    assert len(fallback_rounds) == 3
    assert all("(generic, no public signal)" in r.focus for r in fallback_rounds)


def test_get_chat_model_temperature_fallback() -> None:
    clear_model_cache()
    fake_model = MagicMock()

    calls: list[dict[str, Any]] = []

    def _fake_init(spec: str, **kwargs: Any) -> MagicMock:
        calls.append({"spec": spec, **kwargs})
        if "temperature" in kwargs:
            msg = "Reasoning model does not support temperature"
            raise ValueError(msg)
        return fake_model

    with patch("app.core.llm.init_chat_model", side_effect=_fake_init):
        model = get_chat_model("openai:o3-mini", temperature=0.0)

    assert model is fake_model
    assert len(calls) == 2
    assert calls[0]["spec"] == "openai:o3-mini"
    assert calls[0]["temperature"] == 0.0
    assert calls[0]["base_url"] == "https://backend.sovereigneg.com/v1"
    assert calls[1]["spec"] == "openai:o3-mini"
    assert "temperature" not in calls[1]
    assert calls[1]["base_url"] == "https://backend.sovereigneg.com/v1"
    clear_model_cache()


def test_fetch_url_text_rejects_login_walls_and_short_bodies() -> None:
    with (
        patch(
            "app.core.tools.web_search._tavily_extract_one",
            return_value=("LinkedIn", "Sign in to view this job posting. " * 30),
        ),
        pytest.raises(LoginWallError),
    ):
        fetch_url_text("https://www.linkedin.com/jobs/view/123")

    with (
        patch(
            "app.core.tools.web_search._tavily_extract_one",
            return_value=("Short JD", "Too short."),
        ),
        pytest.raises(LoginWallError),
    ):
        fetch_url_text("https://example.com/jobs/short")


def test_parse_document_bytes_strips_images_and_cleans_temp_file() -> None:
    fake_client = MagicMock()
    fake_client.files.create.return_value = MagicMock(id="file-123")
    fake_client.parsing.parse.return_value = MagicMock(
        markdown_full="# Resume\n\n![logo](page_1_image_1.jpg)\n\nBuilt .NET microservices."
    )

    with patch("llama_cloud.LlamaCloud", return_value=fake_client):
        md = parse_document_bytes(b"%PDF-1.4 dummy bytes", "resume.pdf")

    assert "![logo]" not in md
    assert "[logo]" in md
    assert "Built .NET microservices." in md
    assert _strip_image_filenames("Hello ![alt](img.png)") == "Hello [alt]"


def test_rendercv_payload_and_yaml_normalization(
    sample_profile_dict: dict[str, Any],
    sample_tailored_resume_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    tailored = TailoredResumeResult.model_validate(sample_tailored_resume_dict)

    assert select_rendercv_theme(profile, tailored.tailored_headline) == "engineeringclassic"

    payload = build_rendercv_payload(
        profile=profile,
        optimized_skills=tailored.optimized_skills,
        tailored_experience_bullet_points=tailored.tailored_experience_bullet_points,
        tailored_headline=tailored.tailored_headline,
        tailored_summary=tailored.tailored_summary,
    )
    assert payload["theme"] == "engineeringclassic"
    assert payload["design"] == {"theme": "engineeringclassic"}
    assert payload["locale"] == {"language": "english"}
    assert payload["cv"]["name"] == "Abdelrahman Amr"
    assert payload["cv"]["phone"] == "+201000000000"
    assert payload["cv"]["website"] == "https://abdelrahman.dev"
    assert {"network": "LinkedIn", "username": "abdelrahman"} in payload["cv"]["social_networks"]
    assert {"network": "GitHub", "username": "abdelrahman"} in payload["cv"]["social_networks"]
    assert payload["cv"]["sections"]["experience"][0]["start_date"] == "2021-03"
    assert payload["cv"]["sections"]["experience"][0]["end_date"] == "present"
    assert payload["cv"]["sections"]["experience"][0]["location"] == "Cairo, Egypt"

    yaml_str = build_rendercv_yaml(payload)
    assert "cv:" in yaml_str
    assert "design:" in yaml_str
    assert "locale:" in yaml_str

    # Verify YAML repair for unquoted colons, trailing colons, and bare numbers
    assert _coerce_string_entry({"Strongest where AI meets execution": "reusable patterns"}) == (
        "Strongest where AI meets execution: reusable patterns"
    )
    assert _coerce_string_entry({"Utilize different approaches": None}) == (
        "Utilize different approaches:"
    )
    assert _coerce_string_entry(2024) == "2024"

    broken_yaml = (
        "cv:\n"
        "  sections:\n"
        "    experience:\n"
        "      - company: TechCorp\n"
        "        highlights:\n"
        "          - Built API: reduced latency by 42%\n"
        "          - Trailing colon:\n"
        "          - 2024\n"
    )
    repaired_yaml, fixes = _normalize_resume_yaml(broken_yaml)
    assert fixes == 3
    assert "Built API: reduced latency by 42%" in repaired_yaml

    hydrated, _ = _hydrate_render_copy(repaired_yaml, "/tmp/scratch", "resume")
    assert "settings:" in hydrated
    assert "output_folder: /tmp/scratch" in hydrated


def test_render_resume_pdf_and_battlecard_html_and_pdf(
    sample_profile_dict: dict[str, Any],
    sample_job_dict: dict[str, Any],
    sample_research_dict: dict[str, Any],
    sample_tailored_resume_dict: dict[str, Any],
    sample_interview_prep_dict: dict[str, Any],
) -> None:
    profile = CandidateProfileParsed.model_validate(sample_profile_dict)
    job = TargetJobRequirements.model_validate(sample_job_dict)
    research = CompanyRoleResearchResult.model_validate(sample_research_dict)
    tailored = TailoredResumeResult.model_validate(sample_tailored_resume_dict)
    prep = InterviewPreparationPlan.model_validate(sample_interview_prep_dict)

    # Battlecard document & HTML rendering using real Jinja2 template
    battlecard_doc = build_battlecard_document(
        prep=prep,
        profile=profile,
        job=job,
        research=research,
    )
    assert battlecard_doc.document_title == "Interview Battle Card — Abdelrahman Amr"
    assert len(battlecard_doc.rounds) == 1
    assert battlecard_doc.rounds[0].subtitle == "TechCorp Global · Senior Backend Engineer"

    html_out = render_battlecard_html(battlecard_doc)
    assert "<!DOCTYPE html>" in html_out
    assert "Interview Battle Card — Abdelrahman Amr" in html_out
    assert "Technical System Design" in html_out
    assert "Scaling File Ingestion Pipeline" in html_out

    with patch(
        "app.candidate.services.pdf_renderer._weasyprint_pdf_bytes",
        return_value=b"%PDF-1.4-battlecard",
    ):
        pdf_bytes = render_battlecard_pdf_bytes(battlecard_doc)
    assert pdf_bytes == b"%PDF-1.4-battlecard"

    # Resume PDF rendering via subprocess rendercv mock
    def _fake_subprocess_run(cmd: list[str], **kwargs: Any) -> MagicMock:
        yaml_file = Path(cmd[2])
        pdf_file = yaml_file.with_suffix(".pdf")
        pdf_file.write_bytes(b"%PDF-1.4-rendercv")
        return MagicMock(returncode=0, stdout="Rendered OK", stderr="")

    with patch("app.candidate.services.pdf_renderer.subprocess.run", side_effect=_fake_subprocess_run):
        resume_pdf = render_resume_pdf_bytes(tailored_resume=tailored)
    assert resume_pdf == b"%PDF-1.4-rendercv"

