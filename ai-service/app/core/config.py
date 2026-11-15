from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

_PROJECT_ROOT = Path(__file__).resolve().parents[2]
_ROOT_ENV_FILE = _PROJECT_ROOT / ".env"


class Settings(BaseSettings):
    """Application settings loaded from environment variables or .env file."""

    APP_NAME : str
    APP_VERSION : str
    APP_DESCRIPTION : str

    OPENAI_API_KEY: str = Field(
        description="OpenAI API key for LLM calls.",
    )
    OPENAI_BASE_URL: str = Field(
        description="OpenAI-compatible base URL for LLM calls.",
    )
    TAVILY_API_KEY: str = Field(
        description="Tavily API key for web search and URL extraction.",
    )
    LLAMA_CLOUD_API_KEY: str = Field(
        description="LlamaCloud API key for document parsing via LlamaParse.",
    )
    INTERNAL_API_KEY: str = Field(
        min_length=1,
        description="Shared secret required in the X-Internal-Api-Key header.",
    )

    # Configurable model names per pipeline stage
    CV_PARSER_MODEL: str = Field(
        description="Model identifier used by the CV parsing stage.",
    )
    JOB_PARSER_MODEL: str = Field(
        description="Model identifier used by the job requirements parsing stage.",
    )
    MATCHER_MODEL: str = Field(
        description="Model identifier used by the candidate-job matching stage.",
    )
    COMPANY_RECON_MODEL: str = Field(
        description="Model identifier used by the company reconnaissance agent stage.",
    )
    RESUME_TAILOR_MODEL: str = Field(
        description="Model identifier used by the resume tailoring stage.",
    )
    INTERVIEW_PREP_MODEL: str = Field(
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
