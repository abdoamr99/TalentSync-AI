"""Request and utility response models for candidate endpoints."""

from typing import Any, Self

from pydantic import BaseModel, Field, model_validator

from app.candidate.enums.messages import CandidateValidationMessage
from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.candidate.services.schemas.pdf_renderer import BattlecardDocument
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult


class DocumentParseResponse(BaseModel):
    """Response returned by the document-to-markdown utility endpoint."""

    text: str = Field(
        description="Markdown text extracted from the uploaded document.",
    )


class CVParseRequest(BaseModel):
    """Request body for `/v1/candidate/cv/parse`."""

    text: str = Field(
        min_length=1,
        description="Raw or markdown CV text to parse into a structured candidate profile.",
    )


class JobParseRequest(BaseModel):
    """Request body for `/v1/candidate/job/parse`."""

    text: str | None = Field(
        default=None,
        description="Raw or markdown text of the job description.",
    )
    url: str | None = Field(
        default=None,
        description="Public HTTP(S) URL of the job posting to extract and parse.",
    )

    @model_validator(mode="after")
    def validate_text_or_url(self) -> Self:
        """Ensure at least one of `text` or `url` is non-empty."""
        has_text = bool(self.text and self.text.strip())
        has_url = bool(self.url and self.url.strip())
        if not has_text and not has_url:
            raise ValueError(CandidateValidationMessage.MISSING_JOB_TEXT_OR_URL)
        return self


class JobMatchRequest(BaseModel):
    """Request body for `/v1/candidate/match`."""

    profile: CandidateProfileParsed = Field(
        description="Structured candidate profile.",
    )
    job: TargetJobRequirements = Field(
        description="Structured target job requirements.",
    )


class CompanyResearchRequest(BaseModel):
    """Request body for `/v1/candidate/research`."""

    company_name: str = Field(
        min_length=1,
        description="Name of the target company to research.",
    )
    job_title: str | None = Field(
        default=None,
        description="Optional target job title for role-specific intelligence.",
    )
    job_url: str | None = Field(
        default=None,
        description="Optional URL to the target job posting.",
    )
    extra_context: str | None = Field(
        default=None,
        description="Optional extra context or intake notes (location, recruiter notes, interview format).",
    )


class TailorResumeRequest(BaseModel):
    """Request body for `/v1/candidate/tailor`."""

    profile: CandidateProfileParsed = Field(
        description="Structured candidate profile (source of truth for all facts).",
    )
    job: TargetJobRequirements = Field(
        description="Structured target job requirements.",
    )
    match: JobMatchAnalysisResult = Field(
        description="Candidate-to-job match and gap analysis result.",
    )
    research: CompanyRoleResearchResult | None = Field(
        default=None,
        description="Optional company and role research result for additional tailoring context.",
    )
    extra_context: str | None = Field(
        default=None,
        description="Optional extra context or intake notes from the candidate.",
    )


class InterviewPrepRequest(BaseModel):
    """Request body for `/v1/candidate/prep`."""

    profile: CandidateProfileParsed = Field(
        description="Structured candidate profile (source of truth for STAR stories).",
    )
    job: TargetJobRequirements = Field(
        description="Structured target job requirements.",
    )
    match: JobMatchAnalysisResult = Field(
        description="Candidate-to-job match and gap analysis result.",
    )
    research: CompanyRoleResearchResult = Field(
        description="Company and role research result.",
    )
    tailored_resume: TailoredResumeResult | None = Field(
        default=None,
        description="Optional tailored resume result.",
    )
    prep_timeline_days: int | None = Field(
        default=None,
        ge=1,
        description="Optional number of days available for the time-boxed study plan.",
    )
    extra_context: str | None = Field(
        default=None,
        description="Optional extra context or intake notes (recruiter notes, interview format).",
    )


class RenderResumePdfRequest(BaseModel):
    """Request body for `/v1/candidate/tailor/pdf`."""

    tailored_resume: TailoredResumeResult | None = Field(
        default=None,
        description="TailoredResumeResult containing `rendercv_ready_payload`.",
    )
    rendercv_payload: dict[str, Any] | None = Field(
        default=None,
        description="Direct `rendercv_ready_payload` dictionary.",
    )
    yaml_content: str | None = Field(
        default=None,
        description="Direct RenderCV YAML string (`cv:`, `design:`, `locale:`).",
    )

    @model_validator(mode="after")
    def validate_source_present(self) -> Self:
        """Ensure at least one resume source is provided."""
        if (
            self.tailored_resume is None
            and self.rendercv_payload is None
            and not (self.yaml_content and self.yaml_content.strip())
        ):
            raise ValueError(CandidateValidationMessage.MISSING_RESUME_RENDER_SOURCE)
        return self


class RenderBattlecardRequest(BaseModel):
    """Request body for `/v1/candidate/prep/battlecard/pdf` and `/v1/candidate/prep/battlecard/html`."""

    battlecard: BattlecardDocument | None = Field(
        default=None,
        description="Direct `BattlecardDocument` JSON (`document_title` + `rounds`).",
    )
    interview_prep: InterviewPreparationPlan | None = Field(
        default=None,
        description="`InterviewPreparationPlan` to convert into a `BattlecardDocument`.",
    )
    profile: CandidateProfileParsed | None = Field(
        default=None,
        description="Optional candidate profile used for the battlecard document title.",
    )
    job: TargetJobRequirements | None = Field(
        default=None,
        description="Optional target job requirements used for round subtitles.",
    )
    research: CompanyRoleResearchResult | None = Field(
        default=None,
        description="Optional company research used for falsifiable company facts.",
    )

    @model_validator(mode="after")
    def validate_battlecard_or_prep(self) -> Self:
        """Ensure either `battlecard` or `interview_prep` is provided."""
        if self.battlecard is None and self.interview_prep is None:
            raise ValueError(CandidateValidationMessage.MISSING_BATTLECARD_OR_PREP)
        return self


