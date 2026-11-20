using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Company.DTOs;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/shortlists")]
[Authorize(Roles = "Recruiter")]
public class CompanyShortlistsController : ControllerBase
{
    private readonly ICompanyShortlistService _shortlistService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public CompanyShortlistsController(
        ICompanyShortlistService shortlistService,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _shortlistService = shortlistService;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Creates a new recruitment shortlist (candidate collection) for the company.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CompanyShortlistDto>> CreateShortlist(
        [FromBody] CreateShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Title))
            return BadRequest("Shortlist title is required.");

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var shortlist = await _shortlistService.CreateShortlistAsync(companyId, userId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetShortlistById), new { id = shortlist.Id }, shortlist);
    }

    /// <summary>
    /// Lists all candidate shortlists for the company (capped at 100).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CompanyShortlistDto>>> ListShortlists(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var list = await _shortlistService.ListShortlistsAsync(companyId, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Gets a shortlist and all its associated candidates with recruiter notes.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyShortlistDto>> GetShortlistById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var shortlist = await _shortlistService.GetShortlistByIdAsync(companyId, id, cancellationToken);
        if (shortlist == null) return NotFound("Shortlist not found.");

        return Ok(shortlist);
    }

    /// <summary>
    /// Adds a candidate from the pool into this shortlist.
    /// </summary>
    [HttpPost("{id:guid}/candidates")]
    public async Task<ActionResult<ShortlistCandidateItemDto>> AddCandidateToShortlist(
        Guid id,
        [FromBody] AddCandidateToShortlistRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var item = await _shortlistService.AddCandidateToShortlistAsync(companyId, userId.Value, id, request, cancellationToken);
        return Ok(item);
    }
}
