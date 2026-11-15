from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager
from pathlib import Path
import sys

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fastapi import FastAPI

from app.candidate.candidate_router import candidate_router
from app.core.config import get_settings
from app.core.enums.messages import StartupErrorMessage
from app.health_router import health_router


@asynccontextmanager
async def lifespan(_app: FastAPI) -> AsyncGenerator[None]:
    """Validate mandatory configuration at application startup."""
    settings = get_settings()
    if not settings.INTERNAL_API_KEY or not settings.INTERNAL_API_KEY.strip():
        raise RuntimeError(StartupErrorMessage.MISSING_INTERNAL_API_KEY)
    yield


def create_app() -> FastAPI:
    """Create, configure, and return the FastAPI application instance."""
    application = FastAPI(
        title=get_settings().APP_NAME,
        version=get_settings().APP_VERSION,
        description=get_settings().APP_DESCRIPTION,
        lifespan=lifespan
    )

    # Register API routers
    application.include_router(health_router)
    application.include_router(candidate_router)

    return application


app = create_app()
