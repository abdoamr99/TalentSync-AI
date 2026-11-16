"""Internal schemas for the PDF and battlecard rendering service."""

from pydantic import BaseModel, Field


class BattlecardStoryItem(BaseModel):
    """Single 15-second story anchor on a battlecard round page."""

    title: str = Field(description="Short story title.")
    body: str = Field(description="15-second one-liner distilled from the STAR story.")


class BattlecardRoundItem(BaseModel):
    """Per-round page entry for the battlecard Jinja2 template."""

    title: str = Field(description="Round name and format heading.")
    subtitle: str | None = Field(
        default=None,
        description="Company and target role subtitle (e.g., 'TechCorp Global · Senior Backend Engineer').",
    )
    introduction: list[str] = Field(
        default_factory=list,
        description="4-6 short keyword anchors for the candidate's self-introduction in this round.",
    )
    stories_ready: list[BattlecardStoryItem] = Field(
        default_factory=list,
        description="3-5 condensed STAR story one-liners mapped to this round.",
    )
    company_facts: list[str] = Field(
        default_factory=list,
        description="Concrete, falsifiable company facts to reference during the round.",
    )
    questions: list[str] = Field(
        default_factory=list,
        description="Grounded questions to ask interviewers in this round.",
    )
    watch_outs: list[str] = Field(
        default_factory=list,
        description="Anticipated doubts or risks paired with a 1-line counter/mitigation.",
    )


class BattlecardDocument(BaseModel):
    """Structured battlecard document rendered by `battlecard.html.j2`."""

    document_title: str = Field(
        default="Interview Battle Card",
        description="Document title rendered in the HTML `<title>` element.",
    )
    rounds: list[BattlecardRoundItem] = Field(
        default_factory=list,
        description="Ordered interview rounds rendered one per landscape A4 page.",
    )
