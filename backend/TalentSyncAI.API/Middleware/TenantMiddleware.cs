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
        // Check for tenant header (X-Company-Id) provided by recruiter/company requests
        if (context.Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
        {
            if (Guid.TryParse(companyIdHeader, out var companyId))
            {
                tenantContext.SetTenant(companyId);
            }
        }

        await _next(context);
    }
}

