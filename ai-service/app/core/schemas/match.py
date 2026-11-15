"""Pydantic schemas for candidate-to-job match and gap analysis (Contract 3.3)."""

from typing import Literal

from pydantic import BaseModel, Field

ImportanceLevel = Literal["High", "Medium", "Low"]
SeverityLevel = Literal["High", "Medium", "Low"]


class StrengthAnalysisItem(BaseModel):
    """A matched strength backed by direct evidence from the candidate's CV."""

    skill_or_requirement: str = Field(
        description="Job skill or requirement where the candidate demonstrates alignment.",
    )
    evidence_from_cv: str = Field(
        description="Concrete evidence directly traceable to the candidate's CV.",
    )
    importance: ImportanceLevel = Field(
        description="Importance of this requirement for the target role: High, Medium, or Low.",
    )


class SkillGapItem(BaseModel):
    """A missing or partial skill gap identified against the target job requirements."""

    missing_skill: str = Field(
        description="Skill or qualification required/preferred by the job that is missing or weak.",
    )
    severity: SeverityLevel = Field(
        description="Severity of the gap for hiring decisions: High, Medium, or Low.",
    )
    impact_description: str = Field(
        description="Why this skill matters for the role and what adjacent experience the candidate has.",
    )
    remediation_advice: str = Field(
        description="Actionable advice on how to mitigate or bridge this gap in the resume or interview.",
    )


class JobMatchAnalysisResult(BaseModel):
    """Structured result of evaluating a candidate profile against job requirements (Contract 3.3)."""

    match_score: int = Field(
        ge=0,
        le=100,
        description="Overall match score from 0 to 100 computed via the weighted rubric.",
    )
    match_verdict: str = Field(
        description="Concise human-readable verdict summarizing candidate fit (e.g., 'Strong Candidate with Minor Gaps').",
    )
    strengths_analysis: list[StrengthAnalysisItem] = Field(
        default_factory=list,
        description="List of candidate strengths supported by direct CV evidence.",
    )
    skill_gaps: list[SkillGapItem] = Field(
        default_factory=list,
        description="List of missing skills, their severity, impact, and remediation advice.",
    )
    strategic_recommendations: list[str] = Field(
        default_factory=list,
        description="Strategic recommendations for positioning and tailoring the candidate's application.",
    )

