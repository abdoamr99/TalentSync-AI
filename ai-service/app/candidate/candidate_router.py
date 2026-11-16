"""FastAPI router for stateless candidate AI workflow endpoints."""

import asyncio
from typing import Annotated

from fastapi import APIRouter, Depends, File, HTTPException, Response, UploadFile, status
from fastapi.responses import HTMLResponse, JSONResponse

from app.candidate.enums.messages import (
    CandidateErrorMessage,
    CandidateValidationMessage,
)
from app.candidate.schemas.interview_prep import InterviewPreparationPlan
from app.candidate.schemas.requests import (
    CompanyResearchRequest,
    CVParseRequest,
    DocumentParseResponse,
    InterviewPrepRequest,
    JobMatchRequest,
    JobParseRequest,
    RenderBattlecardRequest,
    RenderResumePdfRequest,
    TailorResumeRequest,
)
from app.candidate.schemas.research import CompanyRoleResearchResult
from app.candidate.schemas.tailored_resume import TailoredResumeResult
from app.candidate.services.company_recon import research_company_and_role
from app.candidate.services.interview_prep import prepare_interview
from app.candidate.services.job_parser import parse_job
from app.candidate.services.pdf_renderer import (
    render_battlecard_html,
    render_battlecard_pdf_bytes,
    render_resume_pdf_bytes,
    resolve_battlecard_document,
)
from app.candidate.services.resume_tailor import tailor_resume
from app.core.enums.error_codes import ErrorCode
from app.core.guards.grounding import GroundingError
from app.core.schemas.candidate_profile import CandidateProfileParsed
from app.core.schemas.job_requirements import TargetJobRequirements
from app.core.schemas.match import JobMatchAnalysisResult
from app.core.security import verify_internal_api_key
from app.core.services.cv_parser import parse_cv
from app.core.services.matcher import analyze_match
from app.core.tools.documents import parse_document_bytes
from app.core.tools.web_search import LoginWallError, UrlExtractionError

candidate_router = APIRouter(
    prefix="/v1/candidate",
    tags=["candidate"],
    dependencies=[Depends(verify_internal_api_key)],
)


# 1. Document Parsing Endpoint
@candidate_router.post("/documents/parse", response_model=DocumentParseResponse,
                    summary="Parse uploaded document into cleaned Markdown text")
async def parse_document_endpoint(file: Annotated[UploadFile, File(description="Document file to parse via LlamaParse.")]) -> DocumentParseResponse | JSONResponse:
    """Convert an uploaded document into cleaned Markdown text."""

    data = await file.read()
    filename = file.filename

    if not data:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=CandidateValidationMessage.EMPTY_DOCUMENT.format(filename=filename),
        )

    if not filename:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=CandidateValidationMessage.MISSING_FILENAME,
        )

    try:
        markdown_text = await asyncio.to_thread(
            parse_document_bytes, data=data, filename=filename
        )

    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.DOCUMENT_PARSE_ERROR,
                "message": CandidateErrorMessage.DOCUMENT_PARSE_FAILED.format(
                    filename=filename, error=exc
                ),
            },
        )

    return DocumentParseResponse(text=markdown_text)


# 2. CV Parsing Endpoint
@candidate_router.post("/cv/parse", response_model=CandidateProfileParsed,
                    summary="Parse CV text into a structured CandidateProfileParsed")
async def parse_cv_endpoint(request: CVParseRequest) -> CandidateProfileParsed | JSONResponse:
    """Extract a structured candidate profile from raw or markdown CV text."""

    if not request.text.strip():
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=CandidateValidationMessage.EMPTY_CV_TEXT,
        )

    try:
        return await parse_cv(text=request.text)
    
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.CV_PARSE_ERROR,
                "message": CandidateErrorMessage.CV_PARSE_FAILED.format(error=exc),
            },
        )


# 3. Job Description Parsing Endpoint
@candidate_router.post("/job/parse", response_model=TargetJobRequirements, 
                    summary="Parse job description text or URL into TargetJobRequirements")
async def parse_job_endpoint(request: JobParseRequest) -> TargetJobRequirements | JSONResponse:
    """Extract structured job requirements from JD text or a public job URL."""

    try:
        return await parse_job(text=request.text, url=request.url)
    
    except (LoginWallError, UrlExtractionError, ValueError) as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.JOB_PARSE_ERROR,
                "message": CandidateErrorMessage.JOB_PARSE_FAILED.format(error=exc),
            },
        )


# 4. Candidate-to-Job Match Analysis Endpoint
@candidate_router.post("/match", response_model=JobMatchAnalysisResult,
                    summary="Analyze candidate-to-job match and skill gaps")
async def match_candidate_endpoint(request: JobMatchRequest) -> JobMatchAnalysisResult | JSONResponse:
    """Compute match score, strengths, skill gaps, and strategic recommendations."""

    try:
        return await analyze_match(profile=request.profile, job=request.job)
    
    except GroundingError as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.GROUNDING_ERROR,
                "message": CandidateErrorMessage.MATCH_ANALYSIS_FAILED.format(error=exc),
            },
        )
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.PROVIDER_ERROR,
                "message": CandidateErrorMessage.MATCH_ANALYSIS_FAILED.format(error=exc),
            },
        )


# 5. Company and Role Research Endpoint
@candidate_router.post("/research", response_model=CompanyRoleResearchResult,
                    summary="Research target company and role via web search agent")
async def research_company_endpoint(request: CompanyResearchRequest) -> CompanyRoleResearchResult | JSONResponse:
    """Perform pre-interview reconnaissance on the company and role."""

    if not request.company_name.strip():
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=CandidateValidationMessage.EMPTY_COMPANY_NAME,
        )

    try:
        return await research_company_and_role(
            company_name=request.company_name,
            job_title=request.job_title,
            job_url=request.job_url,
            extra_context=request.extra_context,
        )
    
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except TimeoutError as exc:
        return JSONResponse(
            status_code=status.HTTP_504_GATEWAY_TIMEOUT,
            content={
                "code": ErrorCode.RECON_TIMEOUT,
                "message": CandidateErrorMessage.RECON_TIMEOUT.format(error=exc),
            },
        )
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.RECON_ERROR,
                "message": CandidateErrorMessage.RECON_FAILED.format(error=exc),
            },
        )


# 6. Tailored Resume Generation Endpoint
@candidate_router.post("/tailor", response_model=TailoredResumeResult,
                    summary="Generate a tailored resume grounded in the candidate profile")
async def tailor_resume_endpoint(request: TailorResumeRequest) -> TailoredResumeResult | JSONResponse:
    """Rewrite and reorder resume bullets for the target job and render Markdown."""

    try:
        return await tailor_resume(
            profile=request.profile,
            job=request.job,
            match=request.match,
            research=request.research,
            extra_context=request.extra_context,
        )
    
    except GroundingError as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.GROUNDING_ERROR,
                "message": CandidateErrorMessage.TAILOR_RESUME_FAILED.format(error=exc),
            },
        )
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.PROVIDER_ERROR,
                "message": CandidateErrorMessage.TAILOR_RESUME_FAILED.format(error=exc),
            },
        )


# 7. Interview Preparation Plan Generation Endpoint
@candidate_router.post("/prep", response_model=InterviewPreparationPlan,
                    summary="Generate an interview preparation plan, STAR stories, and battlecard")
async def prepare_interview_endpoint(request: InterviewPrepRequest) -> InterviewPreparationPlan | JSONResponse:
    """Generate elevator pitch, per-round STAR stories, day-of battlecard, and study plan."""

    try:
        return await prepare_interview(
            profile=request.profile,
            job=request.job,
            match=request.match,
            research=request.research,
            tailored_resume=request.tailored_resume,
            prep_timeline_days=request.prep_timeline_days,
            extra_context=request.extra_context,
        )
    
    except GroundingError as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.GROUNDING_ERROR,
                "message": CandidateErrorMessage.PREPARE_INTERVIEW_FAILED.format(
                    error=exc
                ),
            },
        )
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.PROVIDER_ERROR,
                "message": CandidateErrorMessage.PREPARE_INTERVIEW_FAILED.format(
                    error=exc
                ),
            },
        )


# 8. Tailored Resume PDF Rendering Endpoint
@candidate_router.post("/tailor/pdf", 
                    summary="Render a tailored resume payload or YAML into a typeset PDF via RenderCV",
                    responses={200: {"content": {"application/pdf": {}}}})
async def render_tailored_resume_pdf_endpoint(request: RenderResumePdfRequest) -> Response:
    """Render `TailoredResumeResult`, `rendercv_payload`, or `yaml_content` to PDF bytes."""

    try:
        pdf_bytes = await asyncio.to_thread(
            render_resume_pdf_bytes,
            tailored_resume=request.tailored_resume,
            rendercv_payload=request.rendercv_payload,
            yaml_content=request.yaml_content,
        )

    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.RENDER_ERROR,
                "message": CandidateErrorMessage.RENDER_RESUME_PDF_FAILED.format(
                    error=exc
                ),
            },
        )

    return Response(content=pdf_bytes, media_type="application/pdf")


# 9. Interview Battlecard Rendering Endpoints
@candidate_router.post("/prep/battlecard/html",
                    summary="Render the day-of interview battlecard into HTML using the battlecard Jinja2 template",
                    response_class=HTMLResponse)
async def render_battlecard_html_endpoint(request: RenderBattlecardRequest) -> Response:
    """Render `BattlecardDocument` or `InterviewPreparationPlan` into battlecard HTML."""

    try:
        doc = resolve_battlecard_document(
            battlecard=request.battlecard,
            interview_prep=request.interview_prep,
            profile=request.profile,
            job=request.job,
            research=request.research,
        )
        html_str = await asyncio.to_thread(render_battlecard_html, doc)

    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.RENDER_ERROR,
                "message": CandidateErrorMessage.RENDER_BATTLECARD_HTML_FAILED.format(
                    error=exc
                ),
            },
        )

    return HTMLResponse(content=html_str)


# 10. Interview Battlecard PDF Rendering Endpoint
@candidate_router.post("/prep/battlecard/pdf",
                    summary="Render the day-of interview battlecard into a PDF via Jinja2 + WeasyPrint",
                    responses={200: {"content": {"application/pdf": {}}}})
async def render_battlecard_pdf_endpoint(request: RenderBattlecardRequest) -> Response:
    """Render `BattlecardDocument` or `InterviewPreparationPlan` to PDF bytes."""

    try:
        doc = resolve_battlecard_document(
            battlecard=request.battlecard,
            interview_prep=request.interview_prep,
            profile=request.profile,
            job=request.job,
            research=request.research,
        )
        pdf_bytes = await asyncio.to_thread(render_battlecard_pdf_bytes, doc)

    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_CONTENT,
            detail=str(exc),
        ) from exc
    
    except Exception as exc:
        return JSONResponse(
            status_code=status.HTTP_502_BAD_GATEWAY,
            content={
                "code": ErrorCode.RENDER_ERROR,
                "message": CandidateErrorMessage.RENDER_BATTLECARD_PDF_FAILED.format(
                    error=exc
                ),
            },
        )

    return Response(content=pdf_bytes, media_type="application/pdf")
