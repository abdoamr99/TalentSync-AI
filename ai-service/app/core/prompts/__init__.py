"""Prompt loading utilities that inject shared truthfulness rules into every system prompt."""

from functools import lru_cache
from pathlib import Path

_CORE_PROMPTS_DIR = Path(__file__).parent
_SHARED_RULES_PATH = _CORE_PROMPTS_DIR / "_shared_rules.md"


@lru_cache(maxsize=1)
def get_shared_rules() -> str:
    """Read and cache the shared truthfulness rules markdown."""
    return _SHARED_RULES_PATH.read_text(encoding="utf-8").strip()


def load_prompt(prompt_path: Path | str) -> str:
    """Load a prompt markdown file and append `_shared_rules.md` to build the system prompt.

    Args:
        prompt_path: Path to the stage prompt `.md` file (or filename in `core/prompts`).

    Returns:
        Combined prompt text with shared truthfulness rules injected.
    """
    path = Path(prompt_path)
    if not path.is_absolute() and not path.exists():
        path = _CORE_PROMPTS_DIR / path
    stage_prompt = path.read_text(encoding="utf-8").strip()
    shared_rules = get_shared_rules()
    return f"{stage_prompt}\n\n---\n\n{shared_rules}"

