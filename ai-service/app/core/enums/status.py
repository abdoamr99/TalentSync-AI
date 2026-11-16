"""System and health status enumerations."""

from enum import StrEnum


class HealthStatus(StrEnum):
    """Health check status values."""

    HEALTHY = "healthy"
    OK = "ok"
