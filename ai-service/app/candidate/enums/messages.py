"""Validation and error message enumerations for candidate endpoints and services."""

from enum import StrEnum


class CandidateValidationMessage(StrEnum):
    """Validation error messages (422 / ValueError) for candidate workflows."""

    EMPTY_DOCUMENT = "Cannot parse empty document: {filename!r}"
    MISSING_FILENAME = "Uploaded file must have a filename."
    EMPTY_CV_TEXT = "CV text must not be empty."
    EMPTY_COMPANY_NAME = "company_name must not be empty."
    MISSING_JOB_TEXT_OR_URL = "Either 'text' or 'url' must be provided."
    MISSING_JOB_SOURCE = "Either job 'text' or 'url' must be provided."
    MISSING_RESUME_RENDER_SOURCE = (
        "Provide at least one of 'tailored_resume', 'rendercv_payload', or 'yaml_content'."
    )
    MISSING_RESUME_RENDER_INPUT = (
        "One of tailored_resume, rendercv_payload, or yaml_content must be provided."
    )
    MISSING_BATTLECARD_OR_PREP = "Either 'battlecard' or 'interview_prep' must be provided."


class CandidateErrorMessage(StrEnum):
    """Upstream, timeout, and rendering error messages for candidate workflows."""

    DOCUMENT_PARSE_FAILED = "Failed to parse document {filename!r}: {error}"
    CV_PARSE_FAILED = "Failed to parse CV: {error}"
    JOB_PARSE_FAILED = "Failed to parse job description: {error}"
    MATCH_ANALYSIS_FAILED = "Failed to analyze match: {error}"
    RECON_TIMEOUT = "Company reconnaissance timed out: {error}"
    RECON_FAILED = "Failed to research company and role: {error}"
    TAILOR_RESUME_FAILED = "Failed to tailor resume: {error}"
    PREPARE_INTERVIEW_FAILED = "Failed to prepare interview: {error}"
    RENDER_RESUME_PDF_FAILED = "Failed to render resume PDF: {error}"
    RENDER_BATTLECARD_HTML_FAILED = "Failed to render battlecard HTML: {error}"
    RENDER_BATTLECARD_PDF_FAILED = "Failed to render battlecard PDF: {error}"
    RENDERCV_NOT_INSTALLED = (
        "Error (render): 'rendercv' CLI is not installed in the runtime environment."
    )
    WEASYPRINT_NOT_INSTALLED = (
        "Error (render): 'weasyprint' is not installed in the runtime environment."
    )
    WEASYPRINT_EMPTY_OUTPUT = "weasyprint produced no output"
