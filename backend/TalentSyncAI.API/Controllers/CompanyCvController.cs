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
    /// Enforces magic bytes validation, max 50 files per batch, and SHA-256 deduplication.
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

        if (files.Count > FileValidationHelper.MaxBulkUploadFiles)
        {
            return BadRequest($"A maximum of {FileValidationHelper.MaxBulkUploadFiles} files can be uploaded per batch. Received {files.Count} files.");
        }

        var response = new CompanyBulkUploadResponseDto
        {
            TotalReceived = files.Count
        };

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            var (isValid, errorMessage) = FileValidationHelper.ValidateFile(stream, file.FileName, file.Length);
            if (!isValid)
            {
                response.SkippedFileNames.Add($"{file.FileName} ({errorMessage})");
                continue;
            }

            // Compute hash for deduplication
            var fileHash = await FileValidationHelper.ComputeSha256HashAsync(stream, cancellationToken);

            var isDuplicate = await _context.CompanyCvDocuments
                .AnyAsync(c => c.CompanyId == companyId && c.FileHash == fileHash, cancellationToken);

            if (isDuplicate)
            {
                response.TotalDuplicatesSkipped++;
                response.SkippedFileNames.Add($"{file.FileName} (Duplicate already exists in company pool)");
                continue;
            }

            // Store in company workspace: companies/{companyId}
            var storedPath = await _fileStorageService.UploadFileAsync(
                stream,
                file.FileName,
                file.ContentType,
                subFolder: $"companies/{companyId}",
                cancellationToken: cancellationToken);

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
            response.TotalUploaded++;

            response.UploadedFiles.Add(new CompanyCvDocumentDto
            {
                Id = cvDocument.Id,
                CompanyId = cvDocument.CompanyId,
                FileName = cvDocument.FileName,
                ContentType = cvDocument.ContentType,
                FileSizeBytes = cvDocument.FileSizeBytes,
                ParsingStatus = cvDocument.ParsingStatus.ToString(),
                FileHash = cvDocument.FileHash,
                CandidateName = null,
                UploadedAtUtc = cvDocument.CreatedAtUtc
            });
        }

        if (response.TotalUploaded > 0)
        {
            // Audit recruiter action
            var auditLog = new RecruiterAction
            {
                CompanyId = companyId,
                UserId = userId.Value,
                ActionType = RecruiterActionType.UploadCv,
                EntityType = "CompanyCvDocument",
                EntityId = companyId,
                Metadata = $"Uploaded {response.TotalUploaded} CV(s) to company pool. {response.TotalDuplicatesSkipped} duplicate(s) skipped."
            };

            _context.RecruiterActions.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(response);
    }

    /// <summary>
    /// Gets a paginated list of all CV documents in the Company's private pool.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyCvDocumentDto>>> GetCompanyCvPool(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] CvParsingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var companyId = _tenantContext.CompanyId!.Value;

        var query = _context.CompanyCvDocuments
            .Include(c => c.CompanyCandidate)
            .Where(c => c.CompanyId == companyId);

        if (status.HasValue)
        {
            query = query.Where(c => c.ParsingStatus == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
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
    /// Gets metadata for a specific CV document in the Company's pool.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyCvDocumentDto>> GetCompanyCvById(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var companyId = _tenantContext.CompanyId!.Value;

        var cv = await _context.CompanyCvDocuments
            .Include(c => c.CompanyCandidate)
            .Where(c => c.Id == id && c.CompanyId == companyId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (cv == null) return NotFound("CV document not found in this company's pool.");

        return Ok(cv);
    }

    /// <summary>
    /// Downloads the physical CV document from the company's pool.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadCompanyCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant)
        {
            return BadRequest("A valid company tenant header 'X-Company-Id' is required.");
        }

        var companyId = _tenantContext.CompanyId!.Value;

        var cv = await _context.CompanyCvDocuments
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, cancellationToken);

        if (cv == null) return NotFound("CV document not found in this company's pool.");

        var exists = await _fileStorageService.FileExistsAsync(cv.StoredFilePath, cancellationToken);
        if (!exists)
        {
            return NotFound("The physical file is missing from company storage.");
        }

        var stream = await _fileStorageService.DownloadFileAsync(cv.StoredFilePath, cancellationToken);
        return File(stream, cv.ContentType, cv.FileName);
    }

    /// <summary>
    /// Deletes a CV document from the company's pool.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCompanyCv(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

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
