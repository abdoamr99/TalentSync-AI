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
    /// Validates magic bytes, file extensions (.pdf, .docx, .doc, .txt), and size bounds (16B - 10MB).
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

        // Validate file content, length, extension, and magic bytes
        using var stream = file.OpenReadStream();
        var (isValid, errorMessage) = FileValidationHelper.ValidateFile(stream, file.FileName, file.Length);
        if (!isValid)
        {
            return BadRequest(errorMessage);
        }

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        // Compute SHA-256 hash for deduplication check
        var fileHash = await FileValidationHelper.ComputeSha256HashAsync(stream, cancellationToken);

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
        var storedRelativePath = await _fileStorageService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            subFolder: $"candidates/{candidateProfile.Id}",
            cancellationToken: cancellationToken);

        // If setting as primary, demote any previous primary CV
        if (setAsPrimary)
        {
            var previousPrimaries = await _context.CandidateResumes
                .Where(cv => cv.CandidateProfileId == candidateProfile.Id && cv.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var prev in previousPrimaries)
            {
                prev.IsPrimary = false;
            }
        }
        else
        {
            var hasAnyCv = await _context.CandidateResumes
                .AnyAsync(cv => cv.CandidateProfileId == candidateProfile.Id, cancellationToken);

            if (!hasAnyCv)
            {
                setAsPrimary = true;
            }
        }

        var candidateCv = new CandidateCv
        {
            CandidateProfileId = candidateProfile.Id,
            FileName = Path.GetFileName(file.FileName),
            StoredFilePath = storedRelativePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Length,
            IsPrimary = setAsPrimary,
            FileHash = fileHash
        };

        _context.CandidateResumes.Add(candidateCv);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new CandidateCvDto
        {
            Id = candidateCv.Id,
            FileName = candidateCv.FileName,
            ContentType = candidateCv.ContentType,
            FileSizeBytes = candidateCv.FileSizeBytes,
            IsPrimary = candidateCv.IsPrimary,
            FileHash = candidateCv.FileHash,
            CreatedAtUtc = candidateCv.CreatedAtUtc,
            RawTextPreview = candidateCv.RawTextContent
        };

        return CreatedAtAction(nameof(GetCvById), new { id = candidateCv.Id }, dto);
    }

    /// <summary>
    /// Gets all CVs uploaded by the authenticated candidate.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CandidateCvDto>>> GetAllMyCvs(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

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
                RawTextPreview = cv.RawTextContent
            })
            .ToListAsync(cancellationToken);

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

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var cv = await _context.CandidateResumes
            .Where(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id)
            .Select(c => new CandidateCvDto
            {
                Id = c.Id,
                FileName = c.FileName,
                ContentType = c.ContentType,
                FileSizeBytes = c.FileSizeBytes,
                IsPrimary = c.IsPrimary,
                FileHash = c.FileHash,
                CreatedAtUtc = c.CreatedAtUtc,
                RawTextPreview = c.RawTextContent
            })
            .FirstOrDefaultAsync(cancellationToken);

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

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var cv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return NotFound("CV document not found.");

        var exists = await _fileStorageService.FileExistsAsync(cv.StoredFilePath, cancellationToken);
        if (!exists)
        {
            return NotFound("The physical file is missing from storage.");
        }

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return File(stream, cv.ContentType, cv.FileName);
    }

    /// <summary>
    /// Sets a specific CV as the primary resume for the candidate.
    /// </summary>
    [HttpPut("{id:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var targetCv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (targetCv == null) return NotFound("CV document not found.");

        var allProfileCvs = await _context.CandidateResumes
            .Where(c => c.CandidateProfileId == candidateProfile.Id)
            .ToListAsync(cancellationToken);

        foreach (var cv in allProfileCvs)
        {
            cv.IsPrimary = (cv.Id == id);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"CV '{targetCv.FileName}' set as primary resume." });
    }

    /// <summary>
    /// Soft-deletes a candidate CV and cleans up storage.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var candidateProfile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var cv = await _context.CandidateResumes
            .FirstOrDefaultAsync(c => c.Id == id && c.CandidateProfileId == candidateProfile.Id, cancellationToken);

        if (cv == null) return NotFound("CV document not found.");

        cv.IsDeleted = true;
        cv.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteFileAsync(cv.StoredFilePath, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Resolves candidate profile or creates one on the fly (Lazy creation) from user account data.
    /// </summary>
    private async Task<CandidateProfile> GetOrCreateCandidateProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (profile != null) return profile;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        profile = new CandidateProfile
        {
            UserId = userId,
            FirstName = user?.FirstName ?? "Candidate",
            LastName = user?.LastName ?? "User",
            Email = user?.Email ?? "candidate@talentsync.ai",
            Phone = user?.PhoneNumber
        };

        _context.CandidateProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        return profile;
    }
}

public class UploadCandidateCvRequest
{
    public IFormFile File { get; set; } = null!;
}
