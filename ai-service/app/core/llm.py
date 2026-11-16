"""Chat model initialization and caching."""

import logging
import os
from typing import Any

from langchain.chat_models import init_chat_model
from langchain_core.language_models import BaseChatModel

from app.core.config import get_settings

logger = logging.getLogger(__name__)

# Module-level cache: `init_chat_model` builds a client each call (allocates
# network plumbing, reads env vars). Reuse instances by `(spec, temperature)` key.
_MODEL_CACHE: dict[str, BaseChatModel] = {}


def _normalize_spec(spec: str) -> str:
    """Default bare model names to the `openai:` provider for OpenAI-compatible endpoints."""
    cleaned = spec.strip()
    if ":" not in cleaned:
        return f"openai:{cleaned}"
    return cleaned


def _cache_key(spec: str, temperature: float | None) -> str:
    norm = _normalize_spec(spec)
    return norm if temperature is None else f"{norm}|temperature={temperature}"


def _resolve_model(name: str, temperature: float | None = None) -> BaseChatModel | None:
    """Build (and memoize) a chat model from a `provider:model` string.

    Returns `None` on any failure — bad provider, unknown model, missing
    credentials — so callers that want soft fallback can handle it.
    """
    key = _cache_key(name, temperature)
    if cached := _MODEL_CACHE.get(key):
        return cached
    try:
        model = get_chat_model(name, temperature=temperature)
    except Exception:
        logger.warning("Cannot initialize chat model '%s'", name, exc_info=True)
        return None
    return model


def _build_provider_kwargs(normalized_spec: str) -> dict[str, Any]:
    """Build `base_url` and `api_key` kwargs for `openai:` models from `Settings`."""
    kwargs: dict[str, Any] = {}
    if not normalized_spec.startswith("openai:"):
        return kwargs

    try:
        cfg = get_settings()
    except Exception:
        return kwargs

    if cfg.OPENAI_BASE_URL and cfg.OPENAI_BASE_URL.strip():
        base_url = cfg.OPENAI_BASE_URL.strip()
        kwargs["base_url"] = base_url
        os.environ.setdefault("OPENAI_BASE_URL", base_url)
    if cfg.OPENAI_API_KEY and cfg.OPENAI_API_KEY.strip():
        api_key = cfg.OPENAI_API_KEY.strip()
        kwargs["api_key"] = api_key
        os.environ.setdefault("OPENAI_API_KEY", api_key)
    return kwargs


def get_chat_model(spec: str, temperature: float | None = None) -> BaseChatModel:
    """Return a cached chat model initialized via `init_chat_model`.

    Automatically configures `base_url` and `api_key` from `Settings` for
    OpenAI-compatible providers (`https://backend.sovereigneg.com/v1`).
    If `temperature` is provided and the underlying model rejects `temperature`,
    falls back to initializing without `temperature`.
    """
    normalized_spec = _normalize_spec(spec)
    key = _cache_key(normalized_spec, temperature)
    if cached := _MODEL_CACHE.get(key):
        return cached

    provider_kwargs = _build_provider_kwargs(normalized_spec)

    if temperature is not None:
        try:
            model = init_chat_model(
                normalized_spec,
                temperature=temperature,
                **provider_kwargs,
            )
        except Exception:
            logger.warning(
                "Model '%s' rejected temperature=%s; falling back to default temperature.",
                normalized_spec,
                temperature,
            )
            model = init_chat_model(normalized_spec, **provider_kwargs)
    else:
        model = init_chat_model(normalized_spec, **provider_kwargs)

    _MODEL_CACHE[key] = model
    return model


def clear_model_cache() -> None:
    """Clear the module-level model cache (useful for tests)."""
    _MODEL_CACHE.clear()
