"""Standardized API error code enumerations."""

from enum import StrEnum


class ErrorCode(StrEnum):
    """Machine-readable error codes returned in 502/504 JSON responses."""

    DOCUMENT_PARSE_ERROR = "DOCUMENT_PARSE_ERROR"
    CV_PARSE_ERROR = "CV_PARSE_ERROR"
    JOB_PARSE_ERROR = "JOB_PARSE_ERROR"
    GROUNDING_ERROR = "GROUNDING_ERROR"
    PROVIDER_ERROR = "PROVIDER_ERROR"
    RECON_TIMEOUT = "RECON_TIMEOUT"
    RECON_ERROR = "RECON_ERROR"
    RENDER_ERROR = "RENDER_ERROR"
