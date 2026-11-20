using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Domain.Enums;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Tests.Helpers;

namespace TalentSyncAI.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/candidate/cv");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CandidateEndpoint_WithRecruiterToken_Returns403Forbidden()
    {
        // 1. Register a Recruiter
        var recruiterEmail = $"recruiter_{Guid.NewGuid():N}@test.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = recruiterEmail,
            Password = "Password123!",
            FirstName = "Recruiter",
            LastName = "User",
            UserType = UserType.Recruiter
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // 2. Login to get token
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = recruiterEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // 3. Attempt candidate endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/candidate/cv");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanyEndpoint_WithCandidateToken_Returns403Forbidden()
    {
        // 1. Register a Candidate
        var candidateEmail = $"candidate_{Guid.NewGuid():N}@test.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = candidateEmail,
            Password = "Password123!",
            FirstName = "Candidate",
            LastName = "User",
            UserType = UserType.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // 2. Login to get token
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = candidateEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // 3. Attempt company endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/company/shortlists");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthHappyPath_RegisterLoginAndAuthorizedRequest_Succeeds()
    {
        var email = $"happypath_{Guid.NewGuid():N}@test.com";

        // Register
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "SecurePassword123!",
            FirstName = "Happy",
            LastName = "Path",
            UserType = UserType.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "SecurePassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        // Use token to access candidate CV endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/candidate/cv");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var cvResponse = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, cvResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RotationAndHashing_WorksCorrectly()
    {
        var email = $"refresh_{Guid.NewGuid():N}@test.com";

        // 1. Register & Login
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FirstName = "Refresh",
            LastName = "Tester",
            UserType = UserType.Candidate
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var rawRefreshToken = auth.RefreshToken;

        // 2. Assert raw refresh token is NOT stored plain in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == email);
            Assert.NotNull(user.RefreshToken);
            Assert.NotEqual(rawRefreshToken, user.RefreshToken); // Hashed, not plain
        }

        // 3. Rotate refresh token successfully
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = rawRefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedAuth);
        Assert.NotEqual(rawRefreshToken, refreshedAuth.RefreshToken); // Rotated to new token

        // 4. Re-using the old refresh token MUST fail
        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = rawRefreshToken // Old token!
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }
}
