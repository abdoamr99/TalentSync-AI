using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.Company.DTOs;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/company/audit")]
[Authorize(Roles = "Recruiter")]
public class RecruiterAuditController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public RecruiterAuditController(IApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Gets the recent audit log history of recruiter actions for this company.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RecruiterActionDto>>> GetAuditLogs(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant || !_tenantContext.CompanyId.HasValue) return Forbid();
        var companyId = _tenantContext.CompanyId.Value;

        var logs = await _context.RecruiterActions
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId)
            .OrderByDescending(a => a.PerformedAtUtc)
            .Take(Math.Min(limit, 100))
            .Select(a => new RecruiterActionDto
            {
                Id = a.Id,
                UserId = a.UserId,
                ActionType = a.ActionType.ToString(),
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Metadata = a.Metadata,
                PerformedAtUtc = a.PerformedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}
