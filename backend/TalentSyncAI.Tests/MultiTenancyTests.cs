using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Application.Features.Company.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Tests.Helpers;

namespace TalentSyncAI.Tests;

public class MultiTenancyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MultiTenancyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RecruiterA_CannotAccessCompanyB_Shortlists()
    {
        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        var recruiterAEmail = $"recruiterA_{Guid.NewGuid():N}@compA.com";
        var recruiterBEmail = $"recruiterB_{Guid.NewGuid():N}@compB.com";

        Guid shortlistBId = Guid.Empty;

        // 1. Seed two companies, two recruiters, and memberships
        await _factory.SeedAsync(async (db, sp) =>
        {
            var companyA = new Company { Id = companyAId, Name = "Company Alpha" };
            var companyB = new Company { Id = companyBId, Name = "Company Beta" };
            db.Companies.AddRange(companyA, companyB);

            var hasher = sp.GetRequiredService<TalentSyncAI.Application.Common.Interfaces.IPasswordHasher>();
            var hash = hasher.HashPassword("Password123!");

            var userA = new User
            {
                Id = Guid.NewGuid(),
                Email = recruiterAEmail,
                NormalizedEmail = recruiterAEmail.ToUpperInvariant(),
                PasswordHash = hash,
                FirstName = "Recruiter",
                LastName = "Alpha",
                UserType = UserType.Recruiter,
                IsActive = true
            };

            var userB = new User
            {
                Id = Guid.NewGuid(),
                Email = recruiterBEmail,
                NormalizedEmail = recruiterBEmail.ToUpperInvariant(),
                PasswordHash = hash,
                FirstName = "Recruiter",
                LastName = "Beta",
                UserType = UserType.Recruiter,
                IsActive = true
            };

            db.Users.AddRange(userA, userB);

            var memberA = new CompanyMember
            {
                CompanyId = companyAId,
                UserId = userA.Id,
                Role = CompanyMemberRole.Admin,
                IsActive = true
            };

            var memberB = new CompanyMember
            {
                CompanyId = companyBId,
                UserId = userB.Id,
                Role = CompanyMemberRole.Admin,
                IsActive = true
            };

            db.CompanyMembers.AddRange(memberA, memberB);

            // Create a shortlist owned strictly by Company B
            var shortlistB = new CompanyShortlist
            {
                CompanyId = companyBId,
                Title = "Beta Secret Shortlist",
                Description = "Private to Company Beta"
            };
            db.CompanyShortlists.Add(shortlistB);

            await db.SaveChangesAsync();
            shortlistBId = shortlistB.Id;
        });

        // 2. Login as Recruiter A (has company_id = companyAId claim)
        var loginResponseA = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = recruiterAEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponseA.StatusCode);
        var authA = await loginResponseA.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authA);

        // 3. Recruiter A attempts to GET shortlists (should only see Company A shortlists, NOT Company B)
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/company/shortlists");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var listResponse = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var shortlists = await listResponse.Content.ReadFromJsonAsync<List<CompanyShortlistDto>>();
        Assert.NotNull(shortlists);
        Assert.DoesNotContain(shortlists, s => s.Id == shortlistBId);

        // 4. Recruiter A attempts to GET Company B's specific shortlist by ID (fail-closed query filter returns 404)
        var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/company/shortlists/{shortlistBId}");
        detailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);

        var detailResponse = await _client.SendAsync(detailReq);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task RecruiterWithoutCompany_Returns403Forbidden_OnCompanyEndpoints()
    {
        var recruiterEmail = $"unaffiliated_{Guid.NewGuid():N}@test.com";

        // Register recruiter without company membership
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = recruiterEmail,
            Password = "Password123!",
            FirstName = "Solo",
            LastName = "Recruiter",
            UserType = UserType.Recruiter
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = recruiterEmail,
            Password = "Password123!"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // Accessing company endpoints without company claim returns 403
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/company/shortlists");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
