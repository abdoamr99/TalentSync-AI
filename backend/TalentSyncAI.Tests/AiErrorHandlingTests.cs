using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Tests.Helpers;

namespace TalentSyncAI.Tests;

public class AiErrorHandlingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AiErrorHandlingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AiFailure_Returns503_AndPersistsNoRecordInDatabase()
    {
        var email = $"ai_error_{Guid.NewGuid():N}@test.com";
        Guid applicationId = Guid.Empty;

        // 1. Register candidate
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FirstName = "AI",
            LastName = "Candidate",
            UserType = UserType.Candidate
        });

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // 2. Setup CV and application for existing profile
        await _factory.SeedAsync(async (db, sp) =>
        {
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var profile = await db.CandidateProfiles.FirstAsync(p => p.UserId == user.Id);

            var cv = new CandidateCv
            {
                CandidateProfileId = profile.Id,
                FileName = "cv.txt",
                StoredFilePath = "cv.txt",
                ContentType = "text/plain",
                FileSizeBytes = 100,
                IsPrimary = true,
                FileHash = "hash123",
                RawTextContent = "Senior .NET Software Engineer with 8 years building distributed systems."
            };
            db.CandidateResumes.Add(cv);

            var app = new TargetJobApplication
            {
                CandidateProfileId = profile.Id,
                SourceCvId = cv.Id,
                TargetCompany = "Google",
                TargetRole = "Staff Engineer",
                JdInputType = JdInputType.Text,
                RawJobDescription = "Looking for a Staff Engineer with cloud and C# expertise."
            };
            db.TargetJobApplications.Add(app);

            await db.SaveChangesAsync();
            applicationId = app.Id;
        });

        // 3. Configure Fake AI Service to throw AiServiceUnavailableException
        _factory.FakeAiService.ShouldThrowAiUnavailable = true;

        try
        {
            // 4. Request Job Match Analysis
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/candidate/applications/{applicationId}/match");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            var response = await _client.SendAsync(request);

            // 5. Assert 503 Service Unavailable
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            // 6. Verify strictly in DB that NO JobMatchAnalysis record was created
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var matchRecord = await db.JobMatchAnalyses
                .FirstOrDefaultAsync(m => m.TargetJobApplicationId == applicationId);

            Assert.Null(matchRecord); // Nothing fake or unverified was persisted!
        }
        finally
        {
            _factory.FakeAiService.ShouldThrowAiUnavailable = false;
        }
    }
}
