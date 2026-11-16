"""Core shared Pydantic schemas."""

from app.core.schemas.candidate_profile import (
    CandidateProfileParsed,
    CandidateSkills,
    CertificationEntry,
    EducationEntry,
    ExperienceEntry,
    LanguageProficiency,
    LanguageSkill,
    PersonalInfo,
    ProjectEntry,
)
from app.core.schemas.job_requirements import (
    EmploymentType,
    JobMetadata,
    RequiredQualifications,
    TargetJobRequirements,
    WorkplaceType,
)
from app.core.schemas.match import (
    ImportanceLevel,
    JobMatchAnalysisResult,
    SeverityLevel,
    SkillGapItem,
    StrengthAnalysisItem,
)

__all__ = [
    "CandidateProfileParsed",
    "CandidateSkills",
    "CertificationEntry",
    "EducationEntry",
    "EmploymentType",
    "ExperienceEntry",
    "ImportanceLevel",
    "JobMatchAnalysisResult",
    "JobMetadata",
    "LanguageProficiency",
    "LanguageSkill",
    "PersonalInfo",
    "ProjectEntry",
    "RequiredQualifications",
    "SeverityLevel",
    "SkillGapItem",
    "StrengthAnalysisItem",
    "TargetJobRequirements",
    "WorkplaceType",
]

