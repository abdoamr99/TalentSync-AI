"""Internal API key authentication and run-id request tracking."""

import hmac
import logging
from contextvars import ContextVar
from typing import Annotated

from fastapi import Header, HTTPException, status

from app.core.config import get_settings
from app.core.enums.messages import AuthErrorMessage

logger = logging.getLogger(__name__)

_run_id_ctx: ContextVar[str | None] = ContextVar("run_id", default=None)


def get_current_run_id() -> str | None:
    """Return the X-Run-Id for the active request, if provided."""
    return _run_id_ctx.get()


async def verify_internal_api_key(
    x_internal_api_key: Annotated[str | None, Header(alias="X-Internal-Api-Key")] = None,
    x_run_id: Annotated[str | None, Header(alias="X-Run-Id")] = None,
) -> str | None:
    """Validate `X-Internal-Api-Key` and record optional `X-Run-Id` for logging.

    Raises:
        HTTPException: 401 if `X-Internal-Api-Key` is missing or invalid.
    """
    expected_key = get_settings().INTERNAL_API_KEY
    if not x_internal_api_key or not expected_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=AuthErrorMessage.INVALID_INTERNAL_API_KEY,
        )
    if not hmac.compare_digest(x_internal_api_key.encode("utf-8"), expected_key.encode("utf-8")):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=AuthErrorMessage.INVALID_INTERNAL_API_KEY,
        )

    _run_id_ctx.set(x_run_id)
    if x_run_id:
        logger.info("Authenticated request with X-Run-Id=%s", x_run_id)
    return x_run_id

