from fastapi import APIRouter, status
from fastapi.responses import JSONResponse

from app.core.config import get_settings
from app.core.enums.status import HealthStatus

health_router = APIRouter(
    tags=["system"],
)


@health_router.get("/", summary="Health check endpoint")
async def health_check() -> JSONResponse:
    """Return service liveness status."""
    
    settings = get_settings()

    return JSONResponse(
        status_code=status.HTTP_200_OK,
        content={
            "status": HealthStatus.HEALTHY,
            "app_name": settings.APP_NAME,
            "app_version": settings.APP_VERSION,
            "app_description": settings.APP_DESCRIPTION,
        },
    )