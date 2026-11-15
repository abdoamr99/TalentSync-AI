"""Web search and URL extraction tools backed by Tavily."""

import os
from typing import Literal
from urllib.parse import urlparse

from langchain_core.tools import tool

from app.core.config import get_settings
from app.core.enums.messages import CoreErrorMessage
from app.core.tools.documents import _strip_image_filenames


def _ensure_tavily_key() -> None:
    """Propagate TAVILY_API_KEY from settings into os.environ when present."""
    tavily_key = get_settings().TAVILY_API_KEY
    if tavily_key:
        os.environ.setdefault("TAVILY_API_KEY", tavily_key)

_MIN_JD_BODY_CHARS = 500

_LOGIN_WALL_SIGNALS: tuple[str, ...] = (
    "sign in",
    "log in",
    "join linkedin",
    "welcome back",
    "email or phone",
    "forgot password",
    "create account",
    "sign in to greenhouse",
)


class UrlExtractionError(ValueError):
    """Raised when a URL cannot be extracted or fails validation."""


class LoginWallError(UrlExtractionError):
    """Raised when an extracted JD URL is behind a login/auth wall or too short."""


@tool
def web_search(
    query: str,
    search_depth: Literal["basic", "advanced", "fast", "ultra-fast"] = "basic",
    topic: Literal["general", "news", "finance"] = "general",
    max_results: int = 5,
) -> dict:
    """Search the web for current information.

    Args:
        query: The search query (be specific and detailed)
        search_depth: The depth of the search. advanced search is tailored to retrieve the most
            relevant sources and content snippets for your query, while basic search provides
            generic content snippets from each source
        topic: "general" for most queries, "news" for current events
        max_results: Number of results to return

    Returns:
        Search results with url, title, content and score

    """
    try:
        from tavily import TavilyClient

        _ensure_tavily_key()
        client = TavilyClient()
        return client.search(
            query=query,
            search_depth=search_depth,
            topic=topic,
            max_results=max_results,
        )
    except Exception as e:
        return {"error": f"Search failed: {e}"}


@tool
def web_extract(
    urls: list[str] | str,
    extract_depth: Literal["basic", "advanced"] = "basic",
    content_format: Literal["markdown", "text"] = "markdown",
) -> dict:
    """Extract markdown or text content from one or more URLs.

    Args:
        urls: The URLs to extract content from.
        extract_depth: The depth of the extraction process. Advanced extraction
            retrieves more data, including tables and embedded content, with higher
            success but may increase latency.
        content_format: The format of the extracted content ("markdown" or "text").

    Returns:
        Extraction results with url, title, raw_content, and images.

    """
    try:
        from tavily import TavilyClient

        _ensure_tavily_key()
        client = TavilyClient()
        return client.extract(
            urls=urls,
            extract_depth=extract_depth,
            format=content_format,
        )
    except Exception as e:
        return {"error": f"Extract failed: {e}"}


def _tavily_extract_one(url: str) -> tuple[str, str]:
    """Extract a single URL via Tavily and return `(title, raw_markdown)`.

    Raises on transport errors or empty results so callers can surface a
    consistent `Error: ...` message.
    """
    from tavily import TavilyClient

    _ensure_tavily_key()
    client = TavilyClient()
    response = client.extract(urls=url, extract_depth="basic", format="markdown")
    results = response.get("results") or []
    if not results:
        raise ValueError(CoreErrorMessage.TAVILY_NO_RESULTS.format(url=url))
    first = results[0]
    return first.get("title") or "", first.get("raw_content") or ""


def _check_login_wall(body: str, url: str) -> None:
    """Validate that the extracted body is not too short or behind a login wall.

    Raises:
        LoginWallError: If the body after the source line is under ~500 chars
            or contains authentication/sign-in flow signals.
    """
    stripped = body.strip()
    if len(stripped) < _MIN_JD_BODY_CHARS:
        raise LoginWallError(
            CoreErrorMessage.SHORT_EXTRACTED_CONTENT.format(
                url=url,
                length=len(stripped),
                min_chars=_MIN_JD_BODY_CHARS,
            )
        )

    lowered = stripped.lower()
    for signal in _LOGIN_WALL_SIGNALS:
        if signal in lowered:
            raise LoginWallError(
                CoreErrorMessage.AUTH_WALL_DETECTED.format(url=url, signal=signal)
            )


def fetch_url_text(url: str) -> str:
    """Fetch and extract markdown text from a URL, checking for login walls.

    Args:
        url: Full HTTP(S) URL to extract.

    Returns:
        Formatted markdown with optional title, `_Source: <url>_` header, and cleaned body.

    Raises:
        UrlExtractionError: If the URL is invalid or extraction fails.
        LoginWallError: If the extracted body is under 500 chars or contains login signals.
    """
    parsed = urlparse(url)
    if parsed.scheme not in ("http", "https") or not parsed.netloc:
        raise UrlExtractionError(CoreErrorMessage.INVALID_URL.format(url=url))

    try:
        title, raw_markdown = _tavily_extract_one(url)
    except Exception as exc:
        raise UrlExtractionError(
            CoreErrorMessage.URL_EXTRACTION_FAILED.format(url=url, error=exc)
        ) from exc

    body = _strip_image_filenames(raw_markdown)
    _check_login_wall(body, url)

    header = f"# {title}\n\n" if title else ""
    return f"{header}_Source: {url}_\n\n{body}"
