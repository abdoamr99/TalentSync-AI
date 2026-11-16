"""Candidate-specific Pydantic schemas."""

from app.candidate.schemas.interview_prep import (
    DayOfBattlecard,
    InterviewPreparationPlan,
    InterviewRoundPrep,
    RedFlagAndCounter,
    StarStory,
    StudyPlanTask,
)
from app.candidate.schemas.requests import (
    CompanyResearchRequest,
    CVParseRequest,
    DocumentParseResponse,
    InterviewPrepRequest,
    JobMatchRequest,
    JobParseRequest,
    TailorResumeRequest,
)
from app.candidate.schemas.research import (
    CompanyOverview,
    CompanyRoleResearchResult,
    CultureAndValues,
    InterviewProcessInsights,
    InterviewRoundInsight,
    TechStackIntelligence,
)
from app.candidate.schemas.tailored_resume import (
    OptimizedSkills,
    TailoredExperienceItem,
    TailoredResumeResult,
)

__all__ = [
    "CVParseRequest",
    "CompanyOverview",
    "CompanyResearchRequest",
    "CompanyRoleResearchResult",
    "CultureAndValues",
    "DayOfBattlecard",
    "DocumentParseResponse",
    "InterviewPrepRequest",
    "InterviewPreparationPlan",
    "InterviewProcessInsights",
    "InterviewRoundInsight",
    "InterviewRoundPrep",
    "JobMatchRequest",
    "JobParseRequest",
    "OptimizedSkills",
    "RedFlagAndCounter",
    "StarStory",
    "StudyPlanTask",
    "TailorResumeRequest",
    "TailoredExperienceItem",
    "TailoredResumeResult",
    "TechStackIntelligence",
]

