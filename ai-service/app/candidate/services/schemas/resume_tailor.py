"""Internal LLM output schema for the resume tailoring service."""

from pydantic import BaseModel, Field

from app.candidate.schemas.tailored_resume import (
    OptimizedSkills,
    TailoredExperienceItem,
)


class TailoredResumeLLMOutput(BaseModel):
    """Internal LLM output schema excluding deterministically rendered Markdown and RenderCV payload."""

    tailored_headline: str = Field(
        description="Role-aligned professional headline tailored to the target job.",
    )
    tailored_summary: str = Field(
        description="Tailored professional summary highlighting truthful relevant experience and metrics.",
    )
    optimized_skills: OptimizedSkills = Field(
        description="Skills reordered into top matching and secondary categories.",
    )
    tailored_experience_bullet_points: list[TailoredExperienceItem] = Field(
        default_factory=list,
        description="Tailored experience entries ordered by relevance with optimized bullets.",
    )
