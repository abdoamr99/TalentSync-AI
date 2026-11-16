"""CV parsing service converting markdown/plain text into `CandidateProfileParsed`."""

from pathlib import Path
from typing import cast

from langchain_core.language_models import BaseChatModel
from langchain_core.messages import HumanMessage, SystemMessage

from app.core.config import get_settings
from app.core.enums.messages import CoreErrorMessage
from app.core.llm import get_chat_model
from app.core.prompts import load_prompt
from app.core.schemas.candidate_profile import CandidateProfileParsed

_PROMPT_PATH = Path(__file__).resolve().parent.parent / "prompts" / "cv_parser.md"


async def parse_cv(
    text: str,
    model: BaseChatModel | None = None,
) -> CandidateProfileParsed:
    """Parse raw or markdown CV text into a structured `CandidateProfileParsed`.

    Args:
        text: Markdown or plain text extracted from the candidate's CV.
        model: Optional pre-initialized chat model (used for testing).

    Returns:
        Validated `CandidateProfileParsed` instance.

    Raises:
        ValueError: If `text` is empty or whitespace-only.
    """
    if not text or not text.strip():
        raise ValueError(CoreErrorMessage.EMPTY_CV_TEXT)

    llm = model or get_chat_model(get_settings().CV_PARSER_MODEL, temperature=0.0)
    structured_llm = llm.with_structured_output(CandidateProfileParsed)
    system_prompt = load_prompt(_PROMPT_PATH)

    messages = [
        SystemMessage(content=system_prompt),
        HumanMessage(content=f"Parse the following CV into the required schema:\n\n{text}"),
    ]
    result = await structured_llm.ainvoke(messages)
    if isinstance(result, dict):
        return CandidateProfileParsed.model_validate(result)
    return cast(CandidateProfileParsed, result)

