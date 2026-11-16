"""Pydantic schemas for tailored resume generation (Contract 3.5)."""

from typing import Any

from pydantic import BaseModel, Field


class OptimizedSkills(BaseModel):
    """Candidate skills prioritized and grouped for the target role."""

    top_matching_skills: list[str] = Field(
        default_factory=list,
        description="Candidate's true skills that directly match the target job's core requirements.",
    )
    secondary_skills: list[str] = Field(
        default_factory=list,
        description="Additional true skills from the candidate's CV preserved for completeness.",
    )


class TailoredExperienceItem(BaseModel):
    """Reordered and rewritten bullet points for a specific work experience entry."""

    company: str = Field(
        description="Company name matching an entry in the candidate's parsed profile.",
    )
    job_title: str = Field(
        description="Exact job title held by the candidate at this company.",
    )
    optimized_bullets: list[str] = Field(
        default_factory=list,
        description="Tailored accomplishment bullets incorporating JD keywords without inventing facts.",
    )


class TailoredResumeResult(BaseModel):
    """Structured tailored resume output with deterministic Markdown rendering (Contract 3.5)."""

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
    full_markdown_content: str = Field(
        description="Complete print-ready Markdown resume deterministically rendered from structured fields.",
    )
    # TODO: Replace free-form dict[str, Any] with a strict RenderCV payload schema once the PDF rendering contract is finalized.
    rendercv_ready_payload: dict[str, Any] = Field(
        default_factory=dict,
        description="Free-form payload dictionary prepared for downstream RenderCV typesetting.",
    )
