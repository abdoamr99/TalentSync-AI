"""Pydantic schemas for target job requirements (Contract 3.2)."""

from typing import Literal

from pydantic import BaseModel, Field

WorkplaceType = Literal["Remote", "Hybrid", "Onsite"]
EmploymentType = Literal["Full-Time", "Contract", "Part-Time"]


class JobMetadata(BaseModel):
    """Core metadata about the job posting."""

    job_title: str = Field(description="Title of the target role (e.g., Senior Software Engineer).")
    company_name: str = Field(description="Hiring company or organization name.")
    location: str = Field(description="Geographic location of the job (e.g., Cairo, Egypt / Remote).")
    workplace_type: WorkplaceType = Field(
        description="Workplace arrangement: Remote, Hybrid, or Onsite.",
    )
    employment_type: EmploymentType = Field(
        description="Employment contract type: Full-Time, Contract, or Part-Time.",
    )


class RequiredQualifications(BaseModel):
    """Mandatory and preferred qualifications required for the role."""

    mandatory_skills: list[str] = Field(
        default_factory=list,
        description="Must-have technical and domain skills explicitly required by the job ad.",
    )
    preferred_skills: list[str] = Field(
        default_factory=list,
        description="Nice-to-have or bonus skills mentioned in the job ad.",
    )
    minimum_years_of_experience: int = Field(
        ge=0,
        description="Minimum years of professional experience required (0 if not specified).",
    )
    degree_requirement: str = Field(
        description="Educational degree requirement stated in the job ad.",
    )


class TargetJobRequirements(BaseModel):
    """Structured requirements extracted from a target job description (Contract 3.2)."""

    job_metadata: JobMetadata = Field(
        description="Metadata describing the role, company, location, and work arrangement.",
    )
    role_summary: str = Field(
        description="Concise summary of the role's purpose and team mission.",
    )
    required_qualifications: RequiredQualifications = Field(
        description="Mandatory skills, preferred skills, experience, and degree requirements.",
    )
    core_responsibilities: list[str] = Field(
        default_factory=list,
        description="Key day-to-day responsibilities and deliverables expected in the role.",
    )
    ats_keywords: list[str] = Field(
        default_factory=list,
        description="High-signal domain, architectural, and technical ATS keywords from the JD.",
    )
