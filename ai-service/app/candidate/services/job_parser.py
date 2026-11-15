"""Job description parsing service converting JD text or URL into `TargetJobRequirements`."""

import asyncio
from pathlib import Path
from typing import cast

from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage

from app.candidate.enums.messages import CandidateValidationMessage
from app.core.config import get_settings
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.tools.web_search import fetch_url_text

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "job_parser.md"


async def parse_job(
    text: str | None = None,
    url: str | None = None,
    model: BaseChatModel | None = None,
) -> TargetJobRequirements:
    """Parse a job description from raw text or a public URL into `TargetJobRequirements`.

    If `text` is omitted or empty and `url` is supplied, extracts the page via
    `asyncio.to_thread(fetch_url_text, url)` (which raises `LoginWallError` on
    auth-walled or short pages).
    """
    resolved_text = (text or "").strip()
    if not resolved_text:
        if not url or not url.strip():
            raise ValueError(CandidateValidationMessage.MISSING_JOB_SOURCE)
        resolved_text = await asyncio.to_thread(fetch_url_text, url.strip())

    llm = model or get_chat_model(get_settings().JOB_PARSER_MODEL, temperature=0.0)
    structured_llm = llm.with_structured_output(TargetJobRequirements)
    system_prompt = load_prompt(_PROMPT_PATH)

    messages = [
        SystemMessage(content=system_prompt),
        HumanMessage(
            content=f"Parse the following job description into the required schema:\n\n{resolved_text}"
        ),
    ]
    result = await structured_llm.ainvoke(messages)
    if isinstance(result, dict):
        return TargetJobRequirements.model_validate(result)
    return cast(TargetJobRequirements, result)
