"""Core startup, authentication, and shared service error message enumerations."""

from enum import StrEnum


class StartupErrorMessage(StrEnum):
    """Error messages raised during application startup validation."""

    MISSING_INTERNAL_API_KEY = "INTERNAL_API_KEY must be set before starting the service."


class AuthErrorMessage(StrEnum):
    """Error messages raised during request authentication."""

    INVALID_INTERNAL_API_KEY = "Missing or invalid X-Internal-Api-Key header."


class CoreErrorMessage(StrEnum):
    """Error message templates used by core services, guards, and tools."""

    EMPTY_CV_TEXT = "CV text must not be empty."
    EMPTY_DOCUMENT = "Cannot parse empty document: {filename!r}"
    MISSING_LLAMA_CLOUD_KEY = (
        "LLAMA_CLOUD_API_KEY is not set. Please set it in your environment or .env file."
    )
    EMPTY_LLAMAPARSE_MARKDOWN = "LlamaParse returned no markdown for {filename}"
    GROUNDING_CLAIMS_UNGROUNDED = (
        "Output claims are not grounded in the candidate profile: {violations}"
    )
    INVALID_URL = "Invalid URL {url!r} (must be http(s)://...)"
    URL_EXTRACTION_FAILED = "Failed to extract content from {url}: {error}"
    TAVILY_NO_RESULTS = "Tavily returned no results for {url}"
    SHORT_EXTRACTED_CONTENT = (
        "Extracted content from {url} is implausibly short "
        "({length} chars < {min_chars}); page may be login-walled or empty."
    )
    AUTH_WALL_DETECTED = (
        "URL {url} appears to be behind an authentication wall "
        "(detected sign-in signal: {signal!r})."
    )
