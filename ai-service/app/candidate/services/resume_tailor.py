"""Resume tailoring service with compact LLM schema, Jinja2 Markdown, and RenderCV v2.8 payload/YAML generation."""

import datetime
import json
import re
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

import yaml
from jinja2 import Environment
from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage

from app.candidate.guards.grounding import verify_tailored_resume_grounding
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import (
    OptimizedSkills,
    TailoredExperienceItem,
    TailoredResumeResult,
)
from app.candidate.services.schemas.resume_tailor import TailoredResumeLLMOutput
from app.core.config import get_settings
from app.core.guards.grounding import format_retry_feedback, run_with_grounding_retry
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "resume_tailor.md"


_RESUME_MARKDOWN_TEMPLATE_STR = """\
# {{ profile.personal_info.full_name }}
## {{ tailored_headline }}

{% set contact_items = [] -%}
{% if profile.personal_info.email %}{% set _ = contact_items.append(profile.personal_info.email) %}{% endif -%}
{% if profile.personal_info.phone %}{% set _ = contact_items.append(profile.personal_info.phone) %}{% endif -%}
{% if profile.personal_info.location %}{% set _ = contact_items.append(profile.personal_info.location) %}{% endif -%}
{% if profile.personal_info.linkedin_url %}{% set _ = contact_items.append("[LinkedIn](" ~ profile.personal_info.linkedin_url ~ ")") %}{% endif -%}
{% if profile.personal_info.github_url %}{% set _ = contact_items.append("[GitHub](" ~ profile.personal_info.github_url ~ ")") %}{% endif -%}
{% if profile.personal_info.portfolio_url %}{% set _ = contact_items.append("[Portfolio](" ~ profile.personal_info.portfolio_url ~ ")") %}{% endif -%}
{{ contact_items | join(" | ") }}

## Professional Summary
{{ tailored_summary }}

## Skills
{% if optimized_skills.top_matching_skills -%}
- **Core / Matching Skills:** {{ optimized_skills.top_matching_skills | join(", ") }}
{% endif -%}
{% if optimized_skills.secondary_skills -%}
- **Additional Skills:** {{ optimized_skills.secondary_skills | join(", ") }}
{% endif -%}
{% if profile.skills.soft_skills -%}
- **Soft Skills:** {{ profile.skills.soft_skills | join(", ") }}
{% endif -%}
{% if profile.skills.languages -%}
- **Languages:** {% for lang in profile.skills.languages %}{{ lang.language }} ({{ lang.proficiency }}){% if not loop.last %}, {% endif %}{% endfor %}
{% endif %}
{% if tailored_experience_bullet_points -%}
## Experience
{% for exp in tailored_experience_bullet_points -%}
### {{ exp.job_title }} — {{ exp.company }}
{% for bullet in exp.optimized_bullets -%}
- {{ bullet }}
{% endfor %}
{% endfor -%}
{% endif -%}
{% if profile.projects -%}
## Projects
{% for proj in profile.projects -%}
### {{ proj.title }}{% if proj.project_url %} ([Link]({{ proj.project_url }})){% endif %}
- {{ proj.description }}
{% if proj.technologies %}- **Technologies:** {{ proj.technologies | join(", ") }}{% endif %}
{% endfor -%}
{% endif -%}
{% if profile.education -%}
## Education
{% for edu in profile.education -%}
- **{{ edu.degree }} in {{ edu.field_of_study }}**, {{ edu.institution }} ({{ edu.graduation_year }}){% if edu.gpa %} — GPA: {{ edu.gpa }}{% endif %}
{% endfor -%}
{% endif -%}
{% if profile.certifications -%}
## Certifications
{% for cert in profile.certifications -%}
- **{{ cert.name }}** — {{ cert.issuer }}{% if cert.issue_date %} ({{ cert.issue_date }}){% endif %}{% if cert.credential_url %} [Credential]({{ cert.credential_url }}){% endif %}
{% endfor -%}
{% endif -%}
"""

_jinja_env = Environment(autoescape=False, trim_blocks=True, lstrip_blocks=True)
_RESUME_TEMPLATE = _jinja_env.from_string(_RESUME_MARKDOWN_TEMPLATE_STR)


def render_resume_markdown(
    profile: CandidateProfileParsed,
    tailored_headline: str,
    tailored_summary: str,
    optimized_skills: OptimizedSkills,
    tailored_experience_bullet_points: list[TailoredExperienceItem],
) -> str:
    """Deterministically render `full_markdown_content` from structured resume fields."""
    rendered = _RESUME_TEMPLATE.render(
        profile=profile,
        tailored_headline=tailored_headline,
        tailored_summary=tailored_summary,
        optimized_skills=optimized_skills,
        tailored_experience_bullet_points=tailored_experience_bullet_points,
    )
    return rendered.strip()


RENDERCV_THEME_ALLOWLIST: tuple[str, ...] = (
    "classic",
    "classic_legacy",
    "sb2nov",
    "moderncv",
    "engineeringclassic",
    "engineeringresumes",
)

RENDERCV_LOCALE_ALLOWLIST: tuple[str, ...] = (
    "english",
    "french",
    "german",
    "italian",
    "spanish",
    "portuguese",
    "dutch",
    "turkish",
)

_ENGINEERING_SIGNALS: tuple[str, ...] = (
    "engineer",
    "developer",
    "python",
    "c#",
    ".net",
    "sre",
    "mlops",
    "data",
    "backend",
    "frontend",
    "devops",
    "cloud",
    "architect",
    "software",
    "fullstack",
    "full-stack",
)

_E164_CLEAN_RE = re.compile(r"[^\d+]")


def select_rendercv_theme(
    profile: CandidateProfileParsed,
    tailored_headline: str | None = None,
    requested_theme: str | None = None,
) -> str:
    """Select the RenderCV v2.8 theme based on profile signals.

    Honours `requested_theme` if it is in `RENDERCV_THEME_ALLOWLIST`; otherwise
    scans the resume for engineering signals and returns `'engineeringclassic'`
    for engineering profiles or `'classic'` otherwise.
    """
    if requested_theme and requested_theme in RENDERCV_THEME_ALLOWLIST:
        return requested_theme

    corpus_parts = [
        tailored_headline or "",
        profile.professional_summary,
        *profile.skills.technical_skills,
        *profile.skills.tools_and_frameworks,
        *(exp.job_title for exp in profile.experience),
    ]
    lowered = " ".join(corpus_parts).lower()
    if any(signal in lowered for signal in _ENGINEERING_SIGNALS):
        return "engineeringclassic"
    return "classic"


def _format_e164_phone(phone: str | None) -> str | None:
    """Return a clean E.164 phone string (`+...`) or `None` if unparseable."""
    if not phone or not phone.strip():
        return None
    cleaned = _E164_CLEAN_RE.sub("", phone.strip())
    if cleaned.startswith("00"):
        cleaned = "+" + cleaned[2:]
    if cleaned.startswith("+") and cleaned[1:].isdigit() and 7 <= len(cleaned[1:]) <= 15:
        return cleaned
    return None


def _extract_username_from_url(url: str) -> str:
    """Extract the profile handle/slug from a social profile URL."""
    parsed = urlparse(url.strip())
    path = parsed.path.strip("/")
    if not path:
        return url.strip()
    parts = [p for p in path.split("/") if p]
    if parts and parts[0].lower() in {"in", "pub", "u", "users"} and len(parts) > 1:
        return parts[1]
    return parts[-1]


def _coerce_string_entry(item: object) -> str | None:
    """Coerce a should-be-string YAML list entry back to a string, if possible.

    Repairs unquoted `: ` inside a bullet, trailing colons (`{text: None}`), and bare numbers.
    """
    if isinstance(item, bool):
        return None
    if isinstance(item, (int, float)):
        return str(item)
    if not isinstance(item, dict) or len(item) != 1:
        return None
    key, value = next(iter(item.items()))
    if not isinstance(key, str) or isinstance(value, bool):
        return None
    if value is None or isinstance(value, (str, int, float, datetime.date)):
        return f"{key}:" if value is None else f"{key}: {value}"
    return None


def _repair_string_entries(items: object) -> int:
    """Repair should-be-string entries of a list in place; returns fix count."""
    if not isinstance(items, list):
        return 0
    entries: list[Any] = items
    fixes = 0
    for i, item in enumerate(entries):
        coerced = _coerce_string_entry(item)
        if coerced is not None:
            entries[i] = coerced
            fixes += 1
    return fixes


def _normalize_resume_yaml(body: str) -> tuple[str, int]:
    """Repair mechanically-fixable string entries in a rendercv YAML body.

    Walks `cv.sections`: items of text-style sections (lists of strings) and every
    entry's `highlights` list. Returns `(body, fix_count)`.
    """
    try:
        tree = yaml.safe_load(body)
    except yaml.YAMLError:
        return body, 0
    if not isinstance(tree, dict):
        return body, 0
    cv = tree.get("cv")
    sections = cv.get("sections") if isinstance(cv, dict) else None
    if not isinstance(sections, dict):
        return body, 0

    fixes = 0
    for section in sections.values():
        fixes += _repair_string_entries(section)
        if isinstance(section, list):
            for entry in section:
                if isinstance(entry, dict):
                    fixes += _repair_string_entries(entry.get("highlights"))

    if fixes == 0:
        return body, 0
    return yaml.safe_dump(tree, sort_keys=False, allow_unicode=True, width=4096), fixes


def build_rendercv_payload(
    profile: CandidateProfileParsed,
    optimized_skills: OptimizedSkills,
    tailored_experience_bullet_points: list[TailoredExperienceItem],
    tailored_headline: str | None = None,
    tailored_summary: str | None = None,
    requested_theme: str | None = None,
) -> dict[str, Any]:
    """Construct a deterministic RenderCV v2.8 `rendercv_ready_payload` dictionary.

    Includes both the contract's top-level `theme` and `sections` keys and the
    complete RenderCV v2.8 `cv`, `design`, and `locale` structure.
    """
    theme = select_rendercv_theme(
        profile=profile,
        tailored_headline=tailored_headline,
        requested_theme=requested_theme,
    )

    exp_lookup = {
        (e.company.strip().lower(), e.job_title.strip().lower()): e for e in profile.experience
    }
    company_lookup = {e.company.strip().lower(): e for e in profile.experience}

    experience_entries: list[dict[str, Any]] = []
    for exp in tailored_experience_bullet_points:
        source_exp = exp_lookup.get(
            (exp.company.strip().lower(), exp.job_title.strip().lower())
        ) or company_lookup.get(exp.company.strip().lower())
        entry: dict[str, Any] = {
            "company": exp.company,
            "position": exp.job_title,
        }
        if source_exp is not None:
            if source_exp.start_date:
                entry["start_date"] = source_exp.start_date
            if source_exp.is_current:
                entry["end_date"] = "present"
            elif source_exp.end_date:
                entry["end_date"] = source_exp.end_date
            if source_exp.location:
                entry["location"] = source_exp.location
        entry["highlights"] = list(exp.optimized_bullets)
        experience_entries.append(entry)

    skills_entries: list[dict[str, str]] = []
    if optimized_skills.top_matching_skills:
        skills_entries.append(
            {
                "label": "Core Skills",
                "details": ", ".join(optimized_skills.top_matching_skills),
            }
        )
    if optimized_skills.secondary_skills:
        skills_entries.append(
            {
                "label": "Additional Skills",
                "details": ", ".join(optimized_skills.secondary_skills),
            }
        )
    if profile.skills.soft_skills:
        skills_entries.append(
            {
                "label": "Soft Skills",
                "details": ", ".join(profile.skills.soft_skills),
            }
        )
    if profile.skills.languages:
        skills_entries.append(
            {
                "label": "Languages",
                "details": ", ".join(
                    f"{lang.language} ({lang.proficiency})" for lang in profile.skills.languages
                ),
            }
        )

    education_entries: list[dict[str, Any]] = [
        {
            "institution": edu.institution,
            "area": edu.field_of_study,
            "degree": edu.degree,
            "date": str(edu.graduation_year),
            **({"highlights": [f"GPA: {edu.gpa}"]} if edu.gpa else {}),
        }
        for edu in profile.education
    ]

    sections: dict[str, Any] = {}
    summary_text = tailored_summary or profile.professional_summary
    if summary_text:
        sections["summary"] = [summary_text]
    sections["experience"] = experience_entries
    sections["skills"] = skills_entries
    sections["education"] = education_entries

    if profile.projects:
        sections["projects"] = [
            {
                "name": (
                    f"[{proj.title}]({proj.project_url})" if proj.project_url else proj.title
                ),
                "summary": proj.description,
                **(
                    {"highlights": [f"Technologies: {', '.join(proj.technologies)}"]}
                    if proj.technologies
                    else {}
                ),
            }
            for proj in profile.projects
        ]

    if profile.certifications:
        sections["certifications"] = [
            {
                "label": cert.issue_date or cert.issuer,
                "details": (
                    f"[{cert.name} — {cert.issuer}]({cert.credential_url})"
                    if cert.credential_url
                    else f"{cert.name} — {cert.issuer}"
                ),
            }
            for cert in profile.certifications
        ]

    social_networks: list[dict[str, str]] = []
    if profile.personal_info.linkedin_url:
        social_networks.append(
            {
                "network": "LinkedIn",
                "username": _extract_username_from_url(profile.personal_info.linkedin_url),
            }
        )
    if profile.personal_info.github_url:
        social_networks.append(
            {
                "network": "GitHub",
                "username": _extract_username_from_url(profile.personal_info.github_url),
            }
        )

    cv_block: dict[str, Any] = {
        "name": profile.personal_info.full_name,
        "headline": tailored_headline or "",
        "email": profile.personal_info.email,
    }
    e164_phone = _format_e164_phone(profile.personal_info.phone)
    if e164_phone:
        cv_block["phone"] = e164_phone
    if profile.personal_info.location:
        cv_block["location"] = profile.personal_info.location
    if profile.personal_info.portfolio_url and profile.personal_info.portfolio_url.startswith(
        ("http://", "https://")
    ):
        cv_block["website"] = profile.personal_info.portfolio_url
    if social_networks:
        cv_block["social_networks"] = social_networks
    cv_block["sections"] = sections

    return {
        "theme": theme,
        "sections": sections,
        "cv": cv_block,
        "design": {"theme": theme},
        "locale": {"language": "english"},
    }


def build_rendercv_yaml(payload: dict[str, Any]) -> str:
    """Serialize a `rendercv_ready_payload` dictionary into a clean RenderCV v2.8 YAML string."""
    tree = {
        "cv": payload.get("cv") or {"sections": payload.get("sections", {})},
        "design": payload.get("design") or {"theme": payload.get("theme", "classic")},
        "locale": payload.get("locale") or {"language": "english"},
    }
    raw_yaml = yaml.safe_dump(tree, sort_keys=False, allow_unicode=True, width=4096)
    normalized, _ = _normalize_resume_yaml(raw_yaml)
    return normalized


def _coerce_llm_output(raw: Any) -> TailoredResumeLLMOutput:
    if isinstance(raw, TailoredResumeLLMOutput):
        return raw
    if isinstance(raw, TailoredResumeResult):
        return TailoredResumeLLMOutput(
            tailored_headline=raw.tailored_headline,
            tailored_summary=raw.tailored_summary,
            optimized_skills=raw.optimized_skills,
            tailored_experience_bullet_points=raw.tailored_experience_bullet_points,
        )
    if isinstance(raw, dict):
        return TailoredResumeLLMOutput.model_validate(raw)
    msg = f"Unexpected resume tailor output type: {type(raw)!r}"
    raise TypeError(msg)


async def tailor_resume(
    profile: CandidateProfileParsed,
    job: TargetJobRequirements,
    match: JobMatchAnalysisResult,
    research: CompanyRoleResearchResult | None = None,
    extra_context: str | None = None,
    model: BaseChatModel | None = None,
) -> TailoredResumeResult:
    """Tailor a candidate's resume to target job requirements and match analysis.

    Uses `model.with_structured_output(TailoredResumeLLMOutput)` (excluding PII from the
    prompt), builds `TailoredResumeResult` with deterministic Jinja2 Markdown and
    `rendercv_ready_payload`, and verifies grounding against `profile` and `job`.
    """
    llm = model or get_chat_model(get_settings().RESUME_TAILOR_MODEL)
    structured_llm = llm.with_structured_output(TailoredResumeLLMOutput)
    system_prompt = load_prompt(_PROMPT_PATH)

    sanitized_profile = profile.model_dump(exclude={"personal_info"})
    sections = [
        "Tailor the candidate's resume for the target job using the inputs below.",
        f"## Candidate Profile (JSON)\n```json\n{json.dumps(sanitized_profile, indent=2)}\n```",
        f"## Target Job Requirements (JSON)\n```json\n{json.dumps(job.model_dump(), indent=2)}\n```",
        f"## Match Analysis (JSON)\n```json\n{json.dumps(match.model_dump(), indent=2)}\n```",
    ]
    if research is not None:
        sections.append(
            f"## Company Research (JSON)\n```json\n{json.dumps(research.model_dump(), indent=2)}\n```"
        )
    if extra_context and extra_context.strip():
        sections.append(f"## Additional Candidate / Intake Context\n{extra_context.strip()}")
    base_user_content = "\n\n".join(sections)

    async def _generate(violations: list[str] | None) -> TailoredResumeResult:
        user_content = base_user_content
        if violations:
            user_content = f"{base_user_content}\n\n{format_retry_feedback(violations)}"
        messages = [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_content),
        ]
        raw = await structured_llm.ainvoke(messages)
        llm_out = _coerce_llm_output(raw)

        markdown = render_resume_markdown(
            profile=profile,
            tailored_headline=llm_out.tailored_headline,
            tailored_summary=llm_out.tailored_summary,
            optimized_skills=llm_out.optimized_skills,
            tailored_experience_bullet_points=llm_out.tailored_experience_bullet_points,
        )
        payload = build_rendercv_payload(
            profile=profile,
            optimized_skills=llm_out.optimized_skills,
            tailored_experience_bullet_points=llm_out.tailored_experience_bullet_points,
            tailored_headline=llm_out.tailored_headline,
            tailored_summary=llm_out.tailored_summary,
        )
        return TailoredResumeResult(
            tailored_headline=llm_out.tailored_headline,
            tailored_summary=llm_out.tailored_summary,
            optimized_skills=llm_out.optimized_skills,
            tailored_experience_bullet_points=llm_out.tailored_experience_bullet_points,
            full_markdown_content=markdown,
            rendercv_ready_payload=payload,
        )

    def _verify(res: TailoredResumeResult, prof: CandidateProfileParsed) -> list[str]:
        return verify_tailored_resume_grounding(res, prof, job=job)

    return await run_with_grounding_retry(_generate, _verify, profile)
