using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/candidate/applications")]
[Authorize(Roles = "Candidate")]
public class TargetJobApplicationsController : ControllerBase
{
    private readonly ITargetJobApplicationService _applicationService;
    private readonly ICurrentUserService _currentUserService;

    public TargetJobApplicationsController(
        ITargetJobApplicationService applicationService,
        ICurrentUserService currentUserService)
    {
        _applicationService = applicationService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Creates a new Target Job Application for the authenticated candidate (NextRole style).
    /// Accepts JD via Text, File (PDF/DOCX/TXT), or Job Posting URL.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> CreateApplication(
        [FromForm] CreateTargetJobApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        Stream? fileStream = null;
        if (request.File != null && request.File.Length > 0)
        {
            fileStream = request.File.OpenReadStream();
        }

        using (fileStream)
        {
            var model = new CreateTargetJobApplicationModel(
                request.TargetCompany,
                request.TargetRole,
                request.JdInputType,
                request.JobPostingUrl,
                request.RawJobDescription,
                request.SourceCvId,
                fileStream,
                request.File?.FileName,
                request.File?.Length);

            var dto = await _applicationService.CreateApplicationAsync(userId.Value, model, cancellationToken);
            return CreatedAtAction(nameof(GetApplicationById), new { id = dto.Id }, dto);
        }
    }

    /// <summary>
    /// Lists all target job applications for the candidate (capped at 100).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TargetJobApplicationListDto>>> ListApplications(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var list = await _applicationService.ListApplicationsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Gets full detail of a target job application including all generated AI artifacts.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> GetApplicationById(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var application = await _applicationService.GetApplicationByIdAsync(userId.Value, id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        return Ok(application);
    }

    /// <summary>
    /// Soft deletes a target job application.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var success = await _applicationService.DeleteApplicationAsync(userId.Value, id, cancellationToken);
        if (!success) return NotFound("Target job application not found.");

        return NoContent();
    }

    /// <summary>
    /// Stage 1 AI: Runs Job Match & Skill Gap Analysis for this target application.
    /// </summary>
    [HttpPost("{id:guid}/match")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<JobMatchAnalysisDto>> AnalyzeJobMatch(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var analysisDto = await _applicationService.AnalyzeJobMatchAsync(userId.Value, id, cancellationToken);
        return Ok(analysisDto);
    }

    /// <summary>
    /// Stage 2 AI: Runs Company & Role Recon for this target application.
    /// </summary>
    [HttpPost("{id:guid}/research")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<CompanyRoleResearchDto>> ResearchCompany(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var researchDto = await _applicationService.ResearchCompanyAsync(userId.Value, id, cancellationToken);
        return Ok(researchDto);
    }

    /// <summary>
    /// Stage 3 AI: Generates Tailored Resume for this target application.
    /// </summary>
    [HttpPost("{id:guid}/tailor-resume")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<TailoredResumeDto>> TailorResume(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var tailoredDto = await _applicationService.TailorResumeAsync(userId.Value, id, cancellationToken);
        return Ok(tailoredDto);
    }

    /// <summary>
    /// Downloads the tailored resume compiled as an ATS-friendly, professional PDF document.
    /// </summary>
    [HttpGet("{id:guid}/tailor-resume/download")]
    public async Task<IActionResult> DownloadTailoredResumePdf(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var pdfData = await _applicationService.DownloadTailoredResumePdfAsync(userId.Value, id, cancellationToken);
        if (pdfData == null) return BadRequest("Tailored resume has not been generated for this application yet.");

        return File(pdfData.Value.Bytes, "application/pdf", pdfData.Value.FileName);
    }

    /// <summary>
    /// Stage 4 AI: Generates comprehensive Interview Preparation and Battlecard.
    /// </summary>
    [HttpPost("{id:guid}/interview-prep")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<InterviewPreparationDto>> PrepareInterview(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var prepDto = await _applicationService.PrepareInterviewAsync(userId.Value, id, cancellationToken);
        return Ok(prepDto);
    }

    /// <summary>
    /// Executes all candidate AI stages (Match, Recon, Tailor, Prep) sequentially in one request.
    /// </summary>
    [HttpPost("{id:guid}/generate-all")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> GenerateAllArtifacts(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var detail = await _applicationService.GenerateAllArtifactsAsync(userId.Value, id, cancellationToken);
        return Ok(detail);
    }
}

public class CreateTargetJobApplicationApiRequest : CreateTargetJobApplicationRequest
{
    public IFormFile? File { get; set; }
}
