using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Company.DTOs;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/search")]
[Authorize(Roles = "Recruiter")]
public class CompanySearchController : ControllerBase
{
    private readonly ICandidateSearchService _searchService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public CompanySearchController(
        ICandidateSearchService searchService,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService)
    {
        _searchService = searchService;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Executes a keyword-based candidate search and relevance ranking over the company's private CV pool
    /// (interim solution pending vector search integration).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CandidateSearchDto>> ExecuteSearch(
        [FromBody] ExecuteCandidateSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.NaturalLanguageQuery))
            return BadRequest("Natural language query cannot be empty.");

        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var result = await _searchService.ExecuteSearchAsync(companyId, userId.Value, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists all past searches performed by recruiters for this company (capped at 50).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CandidateSearchDto>>> ListSearches(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var searches = await _searchService.ListSearchesAsync(companyId, cancellationToken);
        return Ok(searches);
    }

    /// <summary>
    /// Gets full details and ranked candidate results for a specific past search.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateSearchDto>> GetSearchById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var search = await _searchService.GetSearchByIdAsync(companyId, id, cancellationToken);
        if (search == null) return NotFound("Search record not found.");

        return Ok(search);
    }
}
