"""Internal schemas for the interview preparation service."""

from pydantic import BaseModel, Field

from app.candidate.schemas.interview_prep import (
    DayOfBattlecard,
    InterviewRoundPrep,
    StudyPlanTask,
)


class RoundsAndStoriesPart(BaseModel):
    """Sub-schema for parallel call 1: round-by-round prep and STAR stories."""

    interview_rounds_prep: list[InterviewRoundPrep] = Field(
        default_factory=list,
        description="Round-by-round preparation guides with likely questions and CV-grounded STAR stories.",
    )


class PitchBattlecardStudyPart(BaseModel):
    """Sub-schema for parallel call 2: elevator pitch, day-of battlecard, and study plan."""

    elevator_pitch_60_seconds: str = Field(
        description="60-second first-person elevator pitch tailored to the company and role.",
    )
    day_of_battlecard: DayOfBattlecard = Field(
        description="Day-of-interview cheat sheet with strengths, red-flag counters, and questions to ask.",
    )
    time_boxed_study_plan: list[StudyPlanTask] = Field(
        default_factory=list,
        description="Day-by-day preparation tasks tailored to the candidate's gaps and timeline.",
    )
