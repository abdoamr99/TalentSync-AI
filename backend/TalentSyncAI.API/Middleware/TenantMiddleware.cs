using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Tenant is resolved strictly from the authenticated user's company_id claim
        var companyIdClaim = context.User.FindFirst("company_id")?.Value;
        if (!string.IsNullOrEmpty(companyIdClaim) && Guid.TryParse(companyIdClaim, out var companyId))
        {
            tenantContext.SetTenant(companyId);
        }

        await _next(context);
    }
}
