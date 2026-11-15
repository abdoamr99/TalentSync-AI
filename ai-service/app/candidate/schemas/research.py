"""Pydantic schemas for company and role reconnaissance (Contract 3.4)."""

from pydantic import BaseModel, Field


class CompanyOverview(BaseModel):
    """High-level company background, mission, and recent milestones."""

    industry: str = Field(
        description="Industry sector(s) the company operates in (e.g., 'FinTech / SaaS').",
    )
    mission: str = Field(
        description="Company mission or core value proposition (or 'Unknown — no public signal').",
    )
    recent_news_or_milestones: str = Field(
        description="Recent funding rounds, product launches, expansions, or milestones.",
    )


class TechStackIntelligence(BaseModel):
    """Technical stack signals gathered from the JD, engineering blogs, and public repos."""

    primary_languages: list[str] = Field(
        default_factory=list,
        description="Primary programming languages used by the engineering organization.",
    )
    cloud_infrastructure: str = Field(
        description="Cloud providers, orchestration, and infrastructure tools (e.g., 'Azure, Kubernetes, Terraform').",
    )
    databases: list[str] = Field(
        default_factory=list,
        description="Relational, NoSQL, cache, and vector databases in use.",
    )


class CultureAndValues(BaseModel):
    """Engineering and organizational culture and values."""

    core_values: list[str] = Field(
        default_factory=list,
        description="Stated or observed core company values.",
    )
    work_environment: str = Field(
        description="Characterization of the work environment, pace, and team structure.",
    )


class InterviewRoundInsight(BaseModel):
    """A single expected round in the company's interview loop."""

    round_index: int = Field(
        ge=1,
        description="1-based sequential index of the interview round.",
    )
    name: str = Field(
        description="Name of the interview round (e.g., 'Recruiter Screen', 'Technical Deep Dive').",
    )
    focus: str = Field(
        description="Primary evaluation areas and topics covered in this round.",
    )


class InterviewProcessInsights(BaseModel):
    """Insights into the company's typical interview process and rounds."""

    standard_rounds: list[InterviewRoundInsight] = Field(
        default_factory=list,
        description="Ordered list of standard interview rounds expected for this role.",
    )


class CompanyRoleResearchResult(BaseModel):
    """Structured reconnaissance report on the target company and role (Contract 3.4)."""

    company_overview: CompanyOverview = Field(
        description="Overview of the company's industry, mission, and recent news.",
    )
    tech_stack_intelligence: TechStackIntelligence = Field(
        description="Intelligence on the company's programming languages, cloud infra, and databases.",
    )
    culture_and_values: CultureAndValues = Field(
        description="Company core values and work environment summary.",
    )
    interview_process_insights: InterviewProcessInsights = Field(
        description="Typical interview rounds and focus areas for the role.",
    )
    killer_questions_to_ask_them: list[str] = Field(
        default_factory=list,
        description="High-signal, research-grounded questions for the candidate to ask interviewers.",
    )

