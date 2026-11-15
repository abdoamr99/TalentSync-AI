"""Pydantic schemas for parsed candidate profiles (Contract 3.1)."""

from typing import Literal

from pydantic import BaseModel, Field

LanguageProficiency = Literal["Native", "Fluent", "Professional", "Basic"]


class PersonalInfo(BaseModel):
    """Candidate contact and profile links."""

    full_name: str = Field(description="Candidate's full name as written on the CV.")
    email: str = Field(description="Candidate's primary email address.")
    phone: str | None = Field(
        default=None,
        description="Candidate's phone number if present on the CV.",
    )
    location: str | None = Field(
        default=None,
        description="Candidate's city/country or geographical location if present.",
    )
    linkedin_url: str | None = Field(
        default=None,
        description="URL to the candidate's LinkedIn profile if present.",
    )
    github_url: str | None = Field(
        default=None,
        description="URL to the candidate's GitHub profile if present.",
    )
    portfolio_url: str | None = Field(
        default=None,
        description="URL to the candidate's personal website or portfolio if present.",
    )


class LanguageSkill(BaseModel):
    """Spoken or written language and proficiency level."""

    language: str = Field(description="Name of the language (e.g., English, Arabic).")
    proficiency: LanguageProficiency = Field(
        description="Proficiency level: Native, Fluent, Professional, or Basic.",
    )


class CandidateSkills(BaseModel):
    """Categorized skills extracted from the candidate's CV."""

    technical_skills: list[str] = Field(
        default_factory=list,
        description="Programming languages, core technical domains, and engineering concepts.",
    )
    tools_and_frameworks: list[str] = Field(
        default_factory=list,
        description="Frameworks, libraries, databases, cloud platforms, and developer tools.",
    )
    soft_skills: list[str] = Field(
        default_factory=list,
        description="Interpersonal, leadership, communication, and collaboration skills.",
    )
    languages: list[LanguageSkill] = Field(
        default_factory=list,
        description="Human languages spoken or written by the candidate with proficiency levels.",
    )


class ExperienceEntry(BaseModel):
    """A single work experience or employment entry."""

    company: str = Field(description="Employer or organization name.")
    job_title: str = Field(description="Role or job title held by the candidate.")
    location: str | None = Field(
        default=None,
        description="Location of the role (city/country or Remote) if stated.",
    )
    start_date: str = Field(
        description="Start date normalized to YYYY-MM format.",
    )
    end_date: str | None = Field(
        default=None,
        description="End date normalized to YYYY-MM format, or null if currently employed here.",
    )
    is_current: bool = Field(
        description="True if this is the candidate's current role, otherwise False.",
    )
    accomplishments: list[str] = Field(
        default_factory=list,
        description="Bullet points describing responsibilities, achievements, and measurable impact.",
    )


class EducationEntry(BaseModel):
    """A single academic degree or education entry."""

    institution: str = Field(description="University, college, or educational institution name.")
    degree: str = Field(description="Degree type obtained or pursued (e.g., Bachelor's, M.Sc.).")
    field_of_study: str = Field(description="Major, discipline, or field of study.")
    graduation_year: int = Field(description="Four-digit graduation or expected graduation year.")
    gpa: str | None = Field(
        default=None,
        description="Grade Point Average or academic honors if stated on the CV.",
    )


class CertificationEntry(BaseModel):
    """Professional certification earned by the candidate."""

    name: str = Field(description="Official name of the certification.")
    issuer: str = Field(description="Issuing organization or authority.")
    issue_date: str | None = Field(
        default=None,
        description="Issue date in YYYY-MM format if provided.",
    )
    credential_url: str | None = Field(
        default=None,
        description="Verification or credential URL if present on the CV.",
    )


class ProjectEntry(BaseModel):
    """Notable academic, open-source, or personal project."""

    title: str = Field(description="Project name or title.")
    description: str = Field(description="Concise summary of what the project does and its impact.")
    technologies: list[str] = Field(
        default_factory=list,
        description="Languages, frameworks, and tools used in the project.",
    )
    project_url: str | None = Field(
        default=None,
        description="URL to the project repository or live demo if present.",
    )


class CandidateProfileParsed(BaseModel):
    """Structured representation of a candidate's CV (Contract 3.1)."""

    personal_info: PersonalInfo = Field(
        description="Personal contact details and profile URLs.",
    )
    professional_summary: str = Field(
        description="Candidate's professional summary or objective synthesized from the CV.",
    )
    skills: CandidateSkills = Field(
        description="Categorized technical, tool, soft, and language skills.",
    )
    experience: list[ExperienceEntry] = Field(
        default_factory=list,
        description="Chronological work experience entries.",
    )
    education: list[EducationEntry] = Field(
        default_factory=list,
        description="Academic education entries.",
    )
    certifications: list[CertificationEntry] = Field(
        default_factory=list,
        description="Professional certifications listed on the CV.",
    )
    projects: list[ProjectEntry] = Field(
        default_factory=list,
        description="Projects listed on the CV.",
    )

