"""Core enumerations package."""

from app.core.enums.error_codes import ErrorCode
from app.core.enums.match import MatchVerdict
from app.core.enums.messages import (
    AuthErrorMessage,
    CoreErrorMessage,
    StartupErrorMessage,
)
from app.core.enums.status import HealthStatus

__all__ = [
    "AuthErrorMessage",
    "CoreErrorMessage",
    "ErrorCode",
    "HealthStatus",
    "MatchVerdict",
    "StartupErrorMessage",
]
