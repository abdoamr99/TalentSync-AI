"""Document parsing utility backed by LlamaCloud LlamaParse."""

import os
import re
import tempfile
from pathlib import Path

from app.core.config import get_settings
from app.core.enums.messages import CoreErrorMessage

# LlamaParse emits `![alt](page_X_image_Y.jpg)` markdown for embedded images
# even when we don't extract them, which renders as broken-image 404s in the
# UI. The v2 ParsingCreateParams API has no flag to suppress these inline
# references — we strip them client-side, keeping the alt text.
_IMAGE_REF_RE = re.compile(r"!(\[[^\]]*\])\([^)]*\)")


def _strip_image_filenames(markdown: str) -> str:
    """Replace `![alt](filename)` with `[alt]` so broken refs don't 404 in the UI."""
    return _IMAGE_REF_RE.sub(r"\1", markdown)


def parse_document_bytes(data: bytes, filename: str) -> str:
    """Parse raw document bytes with LlamaParse and return cleaned markdown.

    Writes `data` to a temporary file preserving `filename`'s suffix so LlamaCloud
    can infer the document type, parses it with agentic tier, strips inline image
    references, removes the temporary file, and returns the markdown text.

    Args:
        data: Raw file bytes (PDF, DOCX, PPTX, etc.).
        filename: Original filename used to preserve the file extension.

    Returns:
        Extracted and cleaned markdown content.

    Raises:
        ValueError: If `data` is empty or LlamaParse returns no markdown.
    """
    if not data:
        raise ValueError(CoreErrorMessage.EMPTY_DOCUMENT.format(filename=filename))

    from llama_cloud import LlamaCloud

    suffix = Path(filename).suffix if filename else ""
    tmp = tempfile.NamedTemporaryFile(
        delete=False,
        prefix="talentsync-parse-",
        suffix=suffix,
    )
    try:
        tmp.write(data)
    finally:
        tmp.close()

    src = Path(tmp.name)
    try:
        llama_key = get_settings().LLAMA_CLOUD_API_KEY

        if not llama_key:
            raise ValueError(CoreErrorMessage.MISSING_LLAMA_CLOUD_KEY)

        os.environ.setdefault("LLAMA_CLOUD_API_KEY", llama_key)

        # Use LlamaCloud to parse the document
        client = LlamaCloud()
        file_obj = client.files.create(file=str(src), purpose="parse")

        result = client.parsing.parse(
            file_id=file_obj.id,
            tier="agentic",
            version="latest",
            disable_cache=False,
            expand=["markdown_full"],
            output_options={
                "markdown": {"annotate_links": True},
                "images_to_save": [],
            },
            processing_options={"cost_optimizer": {"enable": True}},
        )
        markdown = getattr(result, "markdown_full", None) or ""
        if not markdown:
            raise ValueError(
                CoreErrorMessage.EMPTY_LLAMAPARSE_MARKDOWN.format(filename=filename)
            )
        return _strip_image_filenames(markdown)
    finally:
        src.unlink(missing_ok=True)
