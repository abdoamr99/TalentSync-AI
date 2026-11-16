"""Company and role reconnaissance agent service."""

import asyncio
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from langchain.agents import create_agent
from langchain_core.language_models import BaseChatModel

from app.candidate.enums.messages import CandidateValidationMessage
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.core.config import get_settings
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.tools.web_search import web_extract, web_search

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "company_recon.md"

RECON_TIMEOUT_SECONDS: float = 180.0
RECON_RECURSION_LIMIT: int = 15


def _build_recon_system_prompt() -> str:
    """Build the company recon system prompt with shared rules and today's UTC date."""
    base_prompt = load_prompt(_PROMPT_PATH)
    today = datetime.now(UTC).date().isoformat()
    return f"{base_prompt}\n\nCurrent UTC date: {today}"


def _extract_structured_result(raw_result: Any) -> CompanyRoleResearchResult:
    """Extract `CompanyRoleResearchResult` from an agent invocation response."""
    if isinstance(raw_result, CompanyRoleResearchResult):
        return raw_result
    if isinstance(raw_result, dict):
        if "structured_response" in raw_result:
            sr = raw_result["structured_response"]
            if isinstance(sr, CompanyRoleResearchResult):
                return sr
            if isinstance(sr, dict):
                return CompanyRoleResearchResult.model_validate(sr)
        return CompanyRoleResearchResult.model_validate(raw_result)
    msg = f"Unexpected agent response type: {type(raw_result)!r}"
    raise TypeError(msg)


async def research_company_and_role(
    company_name: str,
    job_title: str | None = None,
    job_url: str | None = None,
    extra_context: str | None = None,
    model: BaseChatModel | None = None,
    timeout_seconds: float = RECON_TIMEOUT_SECONDS,
) -> CompanyRoleResearchResult:
    """Run an agent loop with `web_search` and `web_extract` to research a company and role.

    Uses `langchain.agents.create_agent` with `response_format=CompanyRoleResearchResult`,
    injects today's UTC date into the system prompt, sets a recursion limit, and enforces
    a 180-second timeout via `asyncio.wait_for`.
    """
    if not company_name or not company_name.strip():
        raise ValueError(CandidateValidationMessage.EMPTY_COMPANY_NAME)

    llm = model or get_chat_model(get_settings().COMPANY_RECON_MODEL)
    system_prompt = _build_recon_system_prompt()

    agent = create_agent(
        llm,
        tools=[web_search, web_extract],
        system_prompt=system_prompt,
        response_format=CompanyRoleResearchResult,
    )

    parts = [f"Research company: {company_name.strip()}"]
    if job_title and job_title.strip():
        parts.append(f"Target role: {job_title.strip()}")
    if job_url and job_url.strip():
        parts.append(f"Job posting URL: {job_url.strip()}")
    if extra_context and extra_context.strip():
        parts.append(f"Additional context / intake notes: {extra_context.strip()}")
    user_message = "\n".join(parts)

    payload = {"messages": [{"role": "user", "content": user_message}]}
    
    raw_result = await asyncio.wait_for(
        agent.ainvoke(payload, config={"recursion_limit": RECON_RECURSION_LIMIT}),
        timeout=timeout_seconds,
    )
    return _extract_structured_result(raw_result)
