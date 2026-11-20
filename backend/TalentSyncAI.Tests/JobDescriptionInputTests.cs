using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentSyncAI.Application.Features.Auth.DTOs;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Tests.Helpers;

namespace TalentSyncAI.Tests;

public class JobDescriptionInputTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public JobDescriptionInputTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateApplication_WithFile_ExtractsRealTextContent()
    {
        var email = $"jd_file_{Guid.NewGuid():N}@test.com";

        // Register & Login
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FirstName = "JD",
            LastName = "Tester",
            UserType = UserType.Candidate
        });

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // Upload JD file via multipart/form-data
        var realJdText = "Real Job Requirements: Must have 5+ years of C#, Azure, and Docker experience.";
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(realJdText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("Stripe"), "TargetCompany");
        formData.Add(new StringContent("Backend Lead"), "TargetRole");
        formData.Add(new StringContent(JdInputType.File.ToString()), "JdInputType");
        formData.Add(fileContent, "File", "job_spec.txt");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/candidate/applications")
        {
            Content = formData
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdDto = await response.Content.ReadFromJsonAsync<TargetJobApplicationDetailDto>();
        Assert.NotNull(createdDto);

        // Assert that the real text was extracted and stored, NOT a mock placeholder
        Assert.Equal(realJdText, createdDto.RawJobDescription);
        Assert.DoesNotContain("[Uploaded JD File", createdDto.RawJobDescription);
    }

    [Fact]
    public async Task CreateApplication_WithUrl_StoresValidUrl()
    {
        var email = $"jd_url_{Guid.NewGuid():N}@test.com";

        // Register & Login
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            FirstName = "URL",
            LastName = "Tester",
            UserType = UserType.Candidate
        });

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var jobUrl = "https://careers.google.com/jobs/results/12345";

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("Google"), "TargetCompany");
        formData.Add(new StringContent("Principal Engineer"), "TargetRole");
        formData.Add(new StringContent(JdInputType.Url.ToString()), "JdInputType");
        formData.Add(new StringContent(jobUrl), "JobPostingUrl");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/candidate/applications")
        {
            Content = formData
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdDto = await response.Content.ReadFromJsonAsync<TargetJobApplicationDetailDto>();
        Assert.NotNull(createdDto);

        // Assert valid URL was stored without dummy prefix
        Assert.Equal(jobUrl, createdDto.JobPostingUrl);
        Assert.DoesNotContain("Job posting parsed from", createdDto.RawJobDescription);
    }
}
