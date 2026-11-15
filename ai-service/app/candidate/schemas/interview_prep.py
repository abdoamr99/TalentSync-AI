"""Pydantic schemas for interview preparation plans and battlecards (Contract 3.6)."""

from pydantic import BaseModel, Field


class StarStory(BaseModel):
    """A single behavioral/technical STAR story grounded in the candidate's CV."""

    story_title: str = Field(
        description="Concise, memorable title for the STAR story.",
    )
    target_skill: str = Field(
        description="Core competency or requirement this story demonstrates.",
    )
    situation: str = Field(
        description="Situation: context and challenge faced in the candidate's real experience.",
    )
    task: str = Field(
        description="Task: specific responsibility or objective the candidate owned.",
    )
    action: str = Field(
        description="Action: concrete technical or leadership steps taken by the candidate.",
    )
    result: str = Field(
        description="Result: measurable or concrete outcome achieved (strictly from the CV).",
    )


class InterviewRoundPrep(BaseModel):
    """Preparation guide for a single interview round."""

    round_name: str = Field(
        description="Name of the interview round (e.g., 'Technical System Design').",
    )
    key_focus_areas: list[str] = Field(
        default_factory=list,
        description="Key competencies and topics evaluated in this round.",
    )
    likely_questions: list[str] = Field(
        default_factory=list,
        description="Likely interview questions tailored to the role and round focus.",
    )
    star_stories: list[StarStory] = Field(
        default_factory=list,
        description="CV-grounded STAR stories mapped to this round.",
    )


class RedFlagAndCounter(BaseModel):
    """An anticipated interviewer doubt/gap and how the candidate should counter it."""

    anticipated_doubt: str = Field(
        description="Potential concern or skill gap an interviewer might probe.",
    )
    counter_response: str = Field(
        description="Honest, adjacent-experience-grounded counter-response strategy.",
    )


class DayOfBattlecard(BaseModel):
    """Quick-reference day-of-interview battlecard."""

    key_strengths_to_emphasize: list[str] = Field(
        default_factory=list,
        description="Top candidate strengths and proof points to emphasize across rounds.",
    )
    potential_red_flags_and_counters: list[RedFlagAndCounter] = Field(
        default_factory=list,
        description="Anticipated doubts from skill gaps paired with counter-responses.",
    )
    questions_to_ask_interviewers: list[str] = Field(
        default_factory=list,
        description="Specific, research-grounded questions to ask interviewers.",
    )


class StudyPlanTask(BaseModel):
    """A single day's preparation task in the time-boxed study plan."""

    day: int = Field(
        ge=1,
        description="1-based day number in the preparation schedule.",
    )
    task: str = Field(
        description="Concrete study, review, or practice task for this day.",
    )


class InterviewPreparationPlan(BaseModel):
    """Complete interview preparation plan including pitch, rounds, battlecard, and study plan (Contract 3.6)."""

    elevator_pitch_60_seconds: str = Field(
        description="60-second first-person elevator pitch connecting candidate achievements to the target role and company.",
    )
    interview_rounds_prep: list[InterviewRoundPrep] = Field(
        default_factory=list,
        description="Round-by-round preparation guides with likely questions and STAR stories.",
    )
    day_of_battlecard: DayOfBattlecard = Field(
        description="Day-of-interview cheat sheet with strengths, red-flag counters, and questions to ask.",
    )
    time_boxed_study_plan: list[StudyPlanTask] = Field(
        default_factory=list,
        description="Day-by-day preparation tasks tailored to the candidate's gaps and timeline.",
    )

