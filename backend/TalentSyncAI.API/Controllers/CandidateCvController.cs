using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Cv.DTOs;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/candidate/cv")]
[Authorize(Roles = "Candidate")]
public class CandidateCvController : ControllerBase
{
    private readonly ICandidateCvService _cvService;
    private readonly ICurrentUserService _currentUserService;

    public CandidateCvController(ICandidateCvService cvService, ICurrentUserService currentUserService)
    {
        _cvService = cvService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Uploads a new CV for the authenticated candidate (NextRole style).
    /// Validates magic bytes, file extensions (.pdf, .docx, .doc, .txt), and size bounds (16B - 10MB).
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CandidateCvDto>> UploadCv(
        [FromForm] UploadCandidateCvRequest request,
        [FromQuery] bool setAsPrimary = true,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var file = request?.File;
        if (file == null || file.Length == 0)
        {
            return BadRequest("A valid non-empty file is required.");
        }

        using var stream = file.OpenReadStream();
        var result = await _cvService.UploadCvAsync(
            userId.Value, stream, file.FileName, file.ContentType, file.Length, setAsPrimary, cancellationToken);

        return CreatedAtAction(nameof(GetCvById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Gets all CVs uploaded by the authenticated candidate (capped at 100).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CandidateCvDto>>> GetAllMyCvs(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var cvs = await _cvService.GetAllMyCvsAsync(userId.Value, cancellationToken);
        return Ok(cvs);
    }

    /// <summary>
    /// Gets metadata for a specific candidate CV by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateCvDto>> GetCvById(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var cv = await _cvService.GetCvByIdAsync(userId.Value, id, cancellationToken);
        if (cv == null) return NotFound("CV document not found.");

        return Ok(cv);
    }

    /// <summary>
    /// Downloads the physical file of a candidate CV.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var fileData = await _cvService.DownloadCvAsync(userId.Value, id, cancellationToken);
        if (fileData == null) return NotFound("CV document or physical file not found.");

        return File(fileData.Value.Stream, fileData.Value.ContentType, fileData.Value.FileName);
    }

    /// <summary>
    /// Sets a specific CV as the primary resume for the candidate.
    /// </summary>
    [HttpPut("{id:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var success = await _cvService.SetPrimaryCvAsync(userId.Value, id, cancellationToken);
        if (!success) return NotFound("CV document not found.");

        return Ok(new { message = "CV set as primary successfully." });
    }

    /// <summary>
    /// Deletes a candidate CV and its stored file.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var success = await _cvService.DeleteCvAsync(userId.Value, id, cancellationToken);
        if (!success) return NotFound("CV document not found.");

        return NoContent();
    }
}

public class UploadCandidateCvRequest
{
    public IFormFile? File { get; set; }
}
