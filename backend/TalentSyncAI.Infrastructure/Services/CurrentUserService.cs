using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId)) return userId;

            // DEV TESTING FALLBACK (JWT Disabled / Bypass mode):
            var path = _httpContextAccessor.HttpContext?.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.Contains("/api/company"))
            {
                // Sara Recruiter member of TechCorp Solutions
                return Guid.Parse("01A1920B-BB54-78EE-AA25-895CE7C1248C");
            }

            // Test Candidate
            return Guid.Parse("01a1925a-1825-7eaa-acb4-416264bf7202");
        }
    }

    public string? Email
    {
        get
        {
            var emailClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
            return emailClaim ?? "test.user@talentsync.ai";
        }
    }

    public bool IsAuthenticated => true;
}
