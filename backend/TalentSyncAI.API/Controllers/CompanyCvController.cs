using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Common.Models;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/cv")]
[Authorize(Roles = "Recruiter")]
public class CompanyCvController : ControllerBase
{
    private readonly ICompanyCvService _companyCvService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public CompanyCvController(
        ICompanyCvService companyCvService,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _companyCvService = companyCvService;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
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
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var files = request?.Files;
        if (files == null || files.Count == 0)
        {
            return BadRequest("Please provide at least one CV file to upload.");
        }

        var items = files.Select(f => new CompanyCvUploadItem(f.OpenReadStream(), f.FileName, f.ContentType, f.Length)).ToList();

        try
        {
            var response = await _companyCvService.UploadCompanyCvsAsync(companyId, userId.Value, items, cancellationToken);
            return Ok(response);
        }
        finally
        {
            foreach (var item in items)
            {
                item.Stream.Dispose();
            }
        }
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

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var result = await _companyCvService.GetCompanyCvPoolAsync(companyId, pageIndex, pageSize, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets metadata for a specific CV document in the Company's pool.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyCvDocumentDto>> GetCompanyCvById(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var cv = await _companyCvService.GetCompanyCvByIdAsync(companyId, id, cancellationToken);
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

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var fileData = await _companyCvService.DownloadCompanyCvAsync(companyId, id, cancellationToken);
        if (fileData == null) return NotFound("CV document or physical file not found.");

        return File(fileData.Value.Stream, fileData.Value.ContentType, fileData.Value.FileName);
    }
}

public class UploadCompanyCvsRequest
{
    public List<IFormFile> Files { get; set; } = new();
}
