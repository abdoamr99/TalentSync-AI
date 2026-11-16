"""Stateless PDF and HTML rendering utilities for tailored resumes (RenderCV) and interview battlecards (WeasyPrint)."""

import re
import subprocess
import tempfile
import textwrap
from pathlib import Path
from typing import Any

from jinja2 import Environment, FileSystemLoader

from app.candidate.enums.messages import (
    CandidateErrorMessage,
    CandidateValidationMessage,
)
from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.candidate.services.resume_tailor import (
    _normalize_resume_yaml,
    build_rendercv_yaml,
)
from app.candidate.services.schemas.pdf_renderer import (
    BattlecardDocument,
    BattlecardRoundItem,
    BattlecardStoryItem,
)
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements

_CANDIDATE_DIR: Path = Path(__file__).resolve().parent.parent
BATTLECARD_TEMPLATE_DIR: Path = _CANDIDATE_DIR / "templates" / "battlecard"

_SETTINGS_BLOCK_HEADER_RE = re.compile(r"^settings:", re.MULTILINE)


class RenderStageError(RuntimeError):
    """Raised when a document rendering pipeline stage fails."""


def strip_rendercv_settings_block(yaml_body: str) -> str:
    """Strip any trailing `settings:` block from a RenderCV YAML string."""
    match = _SETTINGS_BLOCK_HEADER_RE.search(yaml_body)
    body = yaml_body[: match.start()] if match else yaml_body
    return body.rstrip() + "\n"


def _hydrate_render_copy(body: str, render_dir: str, stem: str) -> tuple[str, int]:
    """Build the render-copy YAML text: normalized body + canonical settings block."""
    cleaned = strip_rendercv_settings_block(body)
    normalized, fixes = _normalize_resume_yaml(cleaned)
    settings_block = textwrap.dedent(
        f"""\
        settings:
          current_date: today
          render_command:
            output_folder: {render_dir}
            typst_path: {render_dir}/{stem}.typ
            pdf_path: OUTPUT_FOLDER/{stem}.pdf
            dont_generate_markdown: true
            dont_generate_html: true
            dont_generate_png: true
            dont_generate_typst: false
            dont_generate_pdf: false
          bold_keywords: []
        """,
    )
    return normalized + settings_block, fixes


def render_resume_pdf_bytes(
    tailored_resume: TailoredResumeResult | None = None,
    rendercv_payload: dict[str, Any] | None = None,
    yaml_content: str | None = None,
    stem: str = "resume",
) -> bytes:
    """Render a tailored resume to PDF bytes via `rendercv render` in a throwaway temp dir."""
    if yaml_content and yaml_content.strip():
        raw_yaml = yaml_content
    elif rendercv_payload is not None:
        raw_yaml = build_rendercv_yaml(rendercv_payload)
    elif tailored_resume is not None:
        raw_yaml = build_rendercv_yaml(tailored_resume.rendercv_ready_payload)
    else:
        raise ValueError(CandidateValidationMessage.MISSING_RESUME_RENDER_INPUT)

    with tempfile.TemporaryDirectory(prefix="talentsync-rendercv-") as scratch_dir:
        hydrated_yaml, _ = _hydrate_render_copy(raw_yaml, scratch_dir, stem)
        yaml_file = Path(scratch_dir) / f"{stem}.yaml"
        yaml_file.write_text(hydrated_yaml, encoding="utf-8")

        try:
            proc = subprocess.run(
                ["rendercv", "render", str(yaml_file)],
                capture_output=True,
                text=True,
                check=False,
                timeout=60,
            )
        except FileNotFoundError as exc:
            raise RenderStageError(
                CandidateErrorMessage.RENDERCV_NOT_INSTALLED
            ) from exc

        if proc.returncode != 0:
            combined_out = (proc.stdout or "") + "\n" + (proc.stderr or "")
            raise RenderStageError(f"Error (render): rendercv exited {proc.returncode}:\n{combined_out[-6000:]}")

        pdf_path = Path(scratch_dir) / f"{stem}.pdf"
        if not pdf_path.is_file():
            combined_out = (proc.stdout or "") + "\n" + (proc.stderr or "")
            raise RenderStageError(
                f"Error (verify): rendercv reported success but {stem}.pdf was not produced. "
                f"Output tail:\n{combined_out[-2000:]}"
                )

        return pdf_path.read_bytes()


def build_battlecard_document(
    prep: InterviewPreparationPlan,
    profile: CandidateProfileParsed | None = None,
    job: TargetJobRequirements | None = None,
    research: CompanyRoleResearchResult | None = None,
) -> BattlecardDocument:
    """Convert an `InterviewPreparationPlan` into a `BattlecardDocument` JSON shape."""
    candidate_name = profile.personal_info.full_name if profile is not None else "Candidate"
    document_title = f"Interview Battle Card — {candidate_name}"

    subtitle: str | None = None
    if job is not None:
        subtitle = f"{job.job_metadata.company_name} · {job.job_metadata.job_title}"

    company_facts: list[str] = []
    if research is not None:
        if research.company_overview.recent_news_or_milestones:
            company_facts.append(research.company_overview.recent_news_or_milestones)
        if research.company_overview.mission:
            company_facts.append(f"Mission: {research.company_overview.mission}")
        if research.tech_stack_intelligence.primary_languages:
            langs = ", ".join(research.tech_stack_intelligence.primary_languages)
            company_facts.append(
                f"Stack: {langs} ({research.tech_stack_intelligence.cloud_infrastructure})"
            )
    if not company_facts:
        company_facts = list(prep.day_of_battlecard.key_strengths_to_emphasize[:3])

    watch_outs = [
        f"{item.anticipated_doubt} — {item.counter_response}"
        for item in prep.day_of_battlecard.potential_red_flags_and_counters
    ]
    questions = list(prep.day_of_battlecard.questions_to_ask_interviewers)

    rounds: list[BattlecardRoundItem] = []
    for round_prep in prep.interview_rounds_prep:
        intro_anchors = [
            *prep.day_of_battlecard.key_strengths_to_emphasize[:3],
            *round_prep.key_focus_areas[:3],
        ]
        stories_ready = [
            BattlecardStoryItem(
                title=story.story_title,
                body=f"{story.action} → {story.result}",
            )
            for story in round_prep.star_stories
        ]
        rounds.append(
            BattlecardRoundItem(
                title=round_prep.round_name,
                subtitle=subtitle,
                introduction=intro_anchors[:6],
                stories_ready=stories_ready[:5],
                company_facts=company_facts[:3],
                questions=questions[:3],
                watch_outs=watch_outs[:3],
            )
        )

    return BattlecardDocument(document_title=document_title, rounds=rounds)


def resolve_battlecard_document(
    battlecard: BattlecardDocument | None = None,
    interview_prep: InterviewPreparationPlan | None = None,
    profile: CandidateProfileParsed | None = None,
    job: TargetJobRequirements | None = None,
    research: CompanyRoleResearchResult | None = None,
) -> BattlecardDocument:
    """Return `battlecard` directly or construct one from `interview_prep`."""
    if battlecard is not None:
        return battlecard
    if interview_prep is not None:
        return build_battlecard_document(
            prep=interview_prep,
            profile=profile,
            job=job,
            research=research,
        )
    raise ValueError(CandidateValidationMessage.MISSING_BATTLECARD_OR_PREP)


def render_battlecard_html(
    battlecard: BattlecardDocument,
    template_dir: Path = BATTLECARD_TEMPLATE_DIR,
) -> str:
    """Render a `BattlecardDocument` into HTML using `battlecard.html.j2`."""
    env = Environment(loader=FileSystemLoader(str(template_dir)), autoescape=True)
    template = env.get_template("battlecard.html.j2")
    return template.render(**battlecard.model_dump())


def _weasyprint_pdf_bytes(html_str: str, template_dir: Path = BATTLECARD_TEMPLATE_DIR) -> bytes:
    """Render HTML to PDF bytes via WeasyPrint; raises on empty output."""
    try:
        from weasyprint import HTML
    except ImportError as exc:
        raise RenderStageError(
            CandidateErrorMessage.WEASYPRINT_NOT_INSTALLED
        ) from exc

    pdf_bytes = HTML(string=html_str, base_url=str(template_dir)).write_pdf()
    if not pdf_bytes:
        raise ValueError(CandidateErrorMessage.WEASYPRINT_EMPTY_OUTPUT)
    return pdf_bytes


def render_battlecard_pdf_bytes(
    battlecard: BattlecardDocument,
    template_dir: Path = BATTLECARD_TEMPLATE_DIR,
) -> bytes:
    """Render a `BattlecardDocument` to PDF bytes via Jinja2 + WeasyPrint."""
    html_str = render_battlecard_html(battlecard, template_dir=template_dir)
    return _weasyprint_pdf_bytes(html_str, template_dir=template_dir)

