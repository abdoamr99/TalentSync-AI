using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Common.Models;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/cv")]
// [Authorize] // Temporarily disabled for testing
public class CompanyCvController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public CompanyCvController(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// Uploads single or bulk CVs into the Company's private CV Pool (Strict Multi-Tenancy).
    /// Computes SHA-256 for each file to prevent duplicate uploads inside the company's pool.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CompanyBulkUploadResponseDto>> UploadCompanyCvs(
        [FromForm] UploadCompanyCvsRequest request,
        CancellationToken cancellationToken = default)
    {
        var files = request?.Files;
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var companyId = _tenantContext.CompanyId!.Value;

        // Verify that the user has an active membership in this company
        var hasMembership = await _context.CompanyMembers
            .AnyAsync(m => m.CompanyId == companyId && m.UserId == userId.Value && m.IsActive, cancellationToken);

        if (!hasMembership)
        {
            return Forbid("User is not an authorized active member of this company.");
        }

        if (files == null || files.Count == 0)
        {
            return BadRequest("Please provide at least one CV file to upload.");
        }

        var response = new CompanyBulkUploadResponseDto
        {
            TotalReceived = files.Count
        };

        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > FileValidationHelper.DefaultMaxFileSize || !FileValidationHelper.IsAllowedExtension(file.FileName))
            {
                response.SkippedFileNames.Add($"{file.FileName} (Invalid file format or size limit exceeded)");
                continue;
            }

            // Compute hash for deduplication
            string fileHash;
            using (var stream = file.OpenReadStream())
            {
                fileHash = await FileValidationHelper.ComputeSha256HashAsync(stream, cancellationToken);
            }

            var isDuplicate = await _context.CompanyCvDocuments
                .AnyAsync(c => c.CompanyId == companyId && c.FileHash == fileHash, cancellationToken);

            if (isDuplicate)
            {
                response.TotalDuplicatesSkipped++;
                response.SkippedFileNames.Add($"{file.FileName} (Duplicate already exists in pool)");
                continue;
            }

            // Store in company workspace: companies/{companyId}
            string storedPath;
            using (var stream = file.OpenReadStream())
            {
                storedPath = await _fileStorageService.UploadFileAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    subFolder: $"companies/{companyId}",
                    cancellationToken: cancellationToken);
            }

            var cvDocument = new CompanyCvDocument
            {
                CompanyId = companyId,
                FileName = Path.GetFileName(file.FileName),
                StoredFilePath = storedPath,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSizeBytes = file.Length,
                ParsingStatus = CvParsingStatus.Uploaded,
                FileHash = fileHash,
                UploadedByUserId = userId.Value
            };

            _context.CompanyCvDocuments.Add(cvDocument);
            response.UploadedFiles.Add(new CompanyCvDocumentDto
            {
                Id = cvDocument.Id,
                CompanyId = companyId,
                FileName = cvDocument.FileName,
                ContentType = cvDocument.ContentType,
                FileSizeBytes = cvDocument.FileSizeBytes,
                ParsingStatus = cvDocument.ParsingStatus.ToString(),
                FileHash = cvDocument.FileHash,
                UploadedAtUtc = DateTime.UtcNow
            });
        }

        if (response.UploadedFiles.Count > 0)
        {
            // Log recruiter audit action
            _context.RecruiterActions.Add(new RecruiterAction
            {
                CompanyId = companyId,
                UserId = userId.Value,
                ActionType = RecruiterActionType.UploadCv,
                EntityType = nameof(CompanyCvDocument),
                EntityId = response.UploadedFiles.First().Id,
                Metadata = $"{{\"uploadedCount\": {response.UploadedFiles.Count}, \"skippedCount\": {response.SkippedFileNames.Count}}}"
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        response.TotalUploaded = response.UploadedFiles.Count;
        return Ok(response);
    }

    /// <summary>
    /// Lists all CV documents in the Company's private CV pool (paginated).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyCvDocumentDto>>> GetCompanyCvs(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] CvParsingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var query = _context.CompanyCvDocuments
            .Include(c => c.CompanyCandidate)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(c => c.ParsingStatus == status.Value);
        }

        query = query.OrderByDescending(c => c.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyCvDocumentDto
            {
                Id = c.Id,
                CompanyId = c.CompanyId,
                FileName = c.FileName,
                ContentType = c.ContentType,
                FileSizeBytes = c.FileSizeBytes,
                ParsingStatus = c.ParsingStatus.ToString(),
                FileHash = c.FileHash,
                CandidateName = c.CompanyCandidate != null ? c.CompanyCandidate.FullName : null,
                UploadedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<CompanyCvDocumentDto>(items, totalCount, pageIndex, pageSize));
    }

    /// <summary>
    /// Gets metadata for a specific CV in the company pool.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyCvDocumentDto>> GetCvById(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var cv = await _context.CompanyCvDocuments
            .Include(c => c.CompanyCandidate)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cv == null) return NotFound("CV document not found in this company's pool.");

        return Ok(new CompanyCvDocumentDto
        {
            Id = cv.Id,
            CompanyId = cv.CompanyId,
            FileName = cv.FileName,
            ContentType = cv.ContentType,
            FileSizeBytes = cv.FileSizeBytes,
            ParsingStatus = cv.ParsingStatus.ToString(),
            FileHash = cv.FileHash,
            CandidateName = cv.CompanyCandidate?.FullName,
            UploadedAtUtc = cv.CreatedAtUtc
        });
    }

    /// <summary>
    /// Downloads the original CV file from the company's pool.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadCv(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var cv = await _context.CompanyCvDocuments
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cv == null) return NotFound("CV document not found in this company's pool.");

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return File(stream, cv.ContentType, cv.FileName);
    }

    /// <summary>
    /// Soft-deletes a CV document from the company's pool.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCv(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var cv = await _context.CompanyCvDocuments
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cv == null) return NotFound("CV document not found in this company's pool.");

        cv.IsDeleted = true;
        cv.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteFileAsync(cv.StoredFilePath, cancellationToken);

        return NoContent();
    }
}

public class UploadCompanyCvsRequest
{
    public List<IFormFile> Files { get; set; } = new();
}

