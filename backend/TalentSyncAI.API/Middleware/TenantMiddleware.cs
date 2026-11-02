using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Guid DefaultDevCompanyId = Guid.Parse("01A1920B-BBBB-7000-8000-000000000001"); // TechCorp Solutions

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Check for explicit tenant header (X-Company-Id)
        if (context.Request.Headers.TryGetValue("X-Company-Id", out var companyIdHeader))
        {
            if (Guid.TryParse(companyIdHeader, out var companyId))
            {
                tenantContext.SetTenant(companyId);
            }
        }
        else if (context.Request.Path.Value?.StartsWith("/api/company", StringComparison.OrdinalIgnoreCase) == true)
        {
            // DEV TESTING FALLBACK: Auto-set default test company for company endpoints
            tenantContext.SetTenant(DefaultDevCompanyId);
        }

        await _next(context);
    }
}
