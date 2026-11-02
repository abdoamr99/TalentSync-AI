using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/candidate/cv")]
// [Authorize] // Temporarily disabled for testing
public class CandidateCvController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public CandidateCvController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// Uploads a new CV for the authenticated candidate (NextRole style).
    /// Supports PDF, DOCX, DOC, and TXT (max 10MB).
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CandidateCvDto>> UploadCv(
        [FromForm] UploadCandidateCvRequest request,
        [FromQuery] bool setAsPrimary = true,
        CancellationToken cancellationToken = default)
    {
        var file = request?.File;
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest("A valid non-empty file is required.");
        }

        if (file.Length > FileValidationHelper.DefaultMaxFileSize)
        {
            return BadRequest($"File size exceeds the allowed limit of {FileValidationHelper.DefaultMaxFileSize / (1024 * 1024)} MB.");
        }

        if (!FileValidationHelper.IsAllowedExtension(file.FileName))
        {
            return BadRequest("Unsupported file format. Allowed extensions: .pdf, .docx, .doc, .txt.");
        }

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null)
        {
            return BadRequest("Candidate profile does not exist for the authenticated user.");
        }

        // Compute SHA-256 hash for deduplication check
        string fileHash;
        using (var stream = file.OpenReadStream())
        {
            fileHash = await FileValidationHelper.ComputeSha256HashAsync(stream, cancellationToken);
        }

        var existingDuplicate = await _context.CandidateResumes
            .FirstOrDefaultAsync(cv => cv.CandidateProfileId == candidateProfile.Id && cv.FileHash == fileHash, cancellationToken);

        if (existingDuplicate != null)
        {
            return Conflict(new
            {
                message = "This exact CV file has already been uploaded.",
                existingCvId = existingDuplicate.Id
            });
        }

        // Store file organized in subfolder: candidates/{profileId}
        string storedRelativePath;
        using (var stream = file.OpenReadStream())
        {
            storedRelativePath = await _fileStorageService.UploadFileAsync(
                stream,
                file.FileName,
                file.ContentType,
                subFolder: $"candidates/{candidateProfile.Id}",
                cancellationToken: cancellationToken);
        }

        // If setting as primary, demote any previous primary CV
        if (setAsPrimary)
        {
            var previousPrimaryCvs = await _context.CandidateResumes
                .Where(cv => cv.CandidateProfileId == candidateProfile.Id && cv.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var prev in previousPrimaryCvs)
            {
                prev.IsPrimary = false;
            }
        }

        // Extract raw text preview if it's plain text (or ready for AI extractor)
        string? rawTextPreview = null;
        if (Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var fullText = await reader.ReadToEndAsync(cancellationToken);
            rawTextPreview = fullText.Length > 500 ? fullText[..500] : fullText;
        }

        var candidateCv = new CandidateCv
        {
            CandidateProfileId = candidateProfile.Id,
            FileName = Path.GetFileName(file.FileName),
            StoredFilePath = storedRelativePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Length,
            IsPrimary = setAsPrimary,
            FileHash = fileHash,
            RawTextContent = rawTextPreview
        };

        _context.CandidateResumes.Add(candidateCv);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCvById), new { id = candidateCv.Id }, new CandidateCvDto
        {
            Id = candidateCv.Id,
            FileName = candidateCv.FileName,
            ContentType = candidateCv.ContentType,
            FileSizeBytes = candidateCv.FileSizeBytes,
            IsPrimary = candidateCv.IsPrimary,
            FileHash = candidateCv.FileHash,
            CreatedAtUtc = candidateCv.CreatedAtUtc,
            RawTextPreview = rawTextPreview
        });
    }

    /// <summary>
    /// Gets all CVs uploaded by the authenticated candidate.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CandidateCvDto>>> GetAllMyCvs(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null)
        {
            return NotFound("Candidate profile not found.");
        }

        var cvs = await _context.CandidateResumes
            .Where(cv => cv.CandidateProfileId == candidateProfile.Id)
            .OrderByDescending(cv => cv.IsPrimary)
            .ThenByDescending(cv => cv.CreatedAtUtc)
            .Select(cv => new CandidateCvDto
            {
                Id = cv.Id,
                FileName = cv.FileName,
                ContentType = cv.ContentType,
                FileSizeBytes = cv.FileSizeBytes,
                IsPrimary = cv.IsPrimary,
                FileHash = cv.FileHash,
                CreatedAtUtc = cv.CreatedAtUtc,
                RawTextPreview = cv.RawTextContent != null && cv.RawTextContent.Length > 200 
                    ? cv.RawTextContent.Substring(0, 200) + "..." 
                    : cv.RawTextContent
            })
            .ToListAsync(cancellationToken);

        return Ok(cvs);
    }

    /// <summary>
    /// Gets metadata for a specific CV belonging to the candidate.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateCvDto>> GetCvById(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null) return NotFound("Candidate profile not found.");

        var cv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return NotFound("CV document not found.");

        return Ok(new CandidateCvDto
        {
            Id = cv.Id,
            FileName = cv.FileName,
            ContentType = cv.ContentType,
            FileSizeBytes = cv.FileSizeBytes,
            IsPrimary = cv.IsPrimary,
            FileHash = cv.FileHash,
            CreatedAtUtc = cv.CreatedAtUtc,
            RawTextPreview = cv.RawTextContent
        });
    }

    /// <summary>
    /// Downloads the original CV file.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null) return NotFound("Candidate profile not found.");

        var cv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return NotFound("CV document not found.");

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return File(stream, cv.ContentType, cv.FileName);
    }

    /// <summary>
    /// Sets a specific CV as the primary resume for this candidate.
    /// </summary>
    [HttpPut("{id:guid}/set-primary")]
    public async Task<IActionResult> SetAsPrimary(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null) return NotFound("Candidate profile not found.");

        var targetCv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (targetCv == null) return NotFound("CV document not found.");

        var allCvs = await _context.CandidateResumes
            .Where(c => c.CandidateProfileId == candidateProfile.Id)
            .ToListAsync(cancellationToken);

        foreach (var cv in allCvs)
        {
            cv.IsPrimary = (cv.Id == id);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"CV '{targetCv.FileName}' set as primary successfully." });
    }

    /// <summary>
    /// Soft-deletes a CV document and marks it as deleted.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId.Value, cancellationToken);

        if (candidateProfile == null) return NotFound("Candidate profile not found.");

        var cv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return NotFound("CV document not found.");

        cv.IsDeleted = true;
        cv.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Optionally delete the underlying physical file
        await _fileStorageService.DeleteFileAsync(cv.StoredFilePath, cancellationToken);

        return NoContent();
    }
}

public class UploadCandidateCvRequest
{
    public IFormFile File { get; set; } = null!;
}

