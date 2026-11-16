from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

_PROJECT_ROOT = Path(__file__).resolve().parents[2]
_ROOT_ENV_FILE = _PROJECT_ROOT / ".env"


class Settings(BaseSettings):
    """Application settings loaded from environment variables or .env file."""

    APP_NAME: str = "TalentSync-AI Service"
    APP_VERSION: str = "1.0.0"
    APP_DESCRIPTION: str = "AI service for candidate CV parsing, job matching, company recon, resume tailoring, and interview preparation."

    OPENAI_API_KEY: str = Field(
        default="",
        description="OpenAI API key for LLM calls.",
    )
    OPENAI_BASE_URL: str = Field(
        default="https://api.openai.com/v1",
        description="OpenAI-compatible base URL for LLM calls.",
    )
    TAVILY_API_KEY: str = Field(
        default="",
        description="Tavily API key for web search and URL extraction.",
    )
    LLAMA_CLOUD_API_KEY: str = Field(
        default="",
        description="LlamaCloud API key for document parsing via LlamaParse.",
    )
    INTERNAL_API_KEY: str = Field(
        default="dev-internal-api-key",
        min_length=1,
        description="Shared secret required in the X-Internal-Api-Key header.",
    )

    # Configurable model names per pipeline stage
    CV_PARSER_MODEL: str = Field(
        default="openai:gpt-4o-mini",
        description="Model identifier used by the CV parsing stage.",
    )
    JOB_PARSER_MODEL: str = Field(
        default="openai:gpt-4o-mini",
        description="Model identifier used by the job requirements parsing stage.",
    )
    MATCHER_MODEL: str = Field(
        default="openai:gpt-4o",
        description="Model identifier used by the candidate-job matching stage.",
    )
    COMPANY_RECON_MODEL: str = Field(
        default="openai:gpt-4o",
        description="Model identifier used by the company reconnaissance agent stage.",
    )
    RESUME_TAILOR_MODEL: str = Field(
        default="openai:gpt-4o",
        description="Model identifier used by the resume tailoring stage.",
    )
    INTERVIEW_PREP_MODEL: str = Field(
        default="openai:gpt-4o",
        description="Model identifier used by the interview preparation stage.",
    )




    model_config = SettingsConfigDict(
        env_file=(_ROOT_ENV_FILE, ".env"),
        env_file_encoding="utf-8",
        extra="ignore",
    )


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Return cached application settings."""
    return Settings()
