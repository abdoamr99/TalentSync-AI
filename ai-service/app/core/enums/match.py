"""Enumerations for candidate-to-job matching verdicts and assessment statuses."""

from enum import StrEnum


class MatchVerdict(StrEnum):
    """Human-readable verdict bands for deterministic match scores."""

    STRONG_MATCH = "Strong Match"
    STRONG_WITH_MINOR_GAPS = "Strong Candidate with Minor Gaps"
    MODERATE_MATCH = "Moderate Match with Addressable Gaps"
    WEAK_MATCH = "Weak Match — Significant Skill Gaps"
