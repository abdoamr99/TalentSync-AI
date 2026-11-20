using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TalentSyncAI.Application.Common.Exceptions;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.Infrastructure.Services;

public class CandidateAiService : ICandidateAiService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CandidateAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public CandidateAiService(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CandidateAiService> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing Job Match for {JobTitle} at {Company}", jobTitle, companyName);

        var payload = new
        {
            cv_text = cvText,
            target_role = jobTitle,
            target_company = companyName,
            raw_job_description = jobDescription,
            job_url = jobPostingUrl,
            job_posting_url = jobPostingUrl
        };

        var result = await PostAsync<object, JobMatchAnalysisDto>("/v1/candidate/match", payload, cancellationToken);
        result.AnalyzedAtUtc = DateTime.UtcNow;
        return result;
    }

    public async Task<CompanyRoleResearchDto> ResearchCompanyAsync(
        string companyName,
        string jobTitle,
        string? jobPostingUrl,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Researching company {Company} for role {JobTitle}", companyName, jobTitle);

        var payload = new
        {
            company_name = companyName,
            job_title = jobTitle,
            job_url = jobPostingUrl,
            extra_context = jobDescription
        };

        var result = await PostAsync<object, CompanyRoleResearchDto>("/v1/candidate/research", payload, cancellationToken);
        result.ResearchedAtUtc = DateTime.UtcNow;
        return result;
    }

    public async Task<TailoredResumeDto> TailorResumeAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating tailored resume for {JobTitle} at {Company}", jobTitle, companyName);

        var payload = new
        {
            cv_text = cvText,
            target_role = jobTitle,
            target_company = companyName,
            raw_job_description = jobDescription,
            job_url = jobPostingUrl,
            job_posting_url = jobPostingUrl
        };

        var result = await PostAsync<object, TailoredResumeDto>("/v1/candidate/tailor", payload, cancellationToken);
        result.GeneratedAtUtc = DateTime.UtcNow;
        return result;
    }

    public async Task<InterviewPreparationDto> PrepareInterviewAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating interview prep for {JobTitle} at {Company}", jobTitle, companyName);

        var payload = new
        {
            cv_text = cvText,
            target_role = jobTitle,
            target_company = companyName,
            raw_job_description = jobDescription,
            job_url = jobPostingUrl,
            job_posting_url = jobPostingUrl
        };

        var result = await PostAsync<object, InterviewPreparationDto>("/v1/candidate/prep", payload, cancellationToken);
        result.PreparedAtUtc = DateTime.UtcNow;
        return result;
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        var incomingRunId = _httpContextAccessor.HttpContext?.Request.Headers["X-Run-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(incomingRunId))
        {
            request.Headers.TryAddWithoutValidation("X-Run-Id", incomingRunId);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI service connection error calling {Path}", path);
            throw new AiServiceUnavailableException("The AI processing service is currently unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "AI service timeout calling {Path}", path);
            throw new AiServiceUnavailableException("The AI processing service timed out.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI service returned {StatusCode} for {Path}: {Body}", response.StatusCode, path, errorBody);

            if ((int)response.StatusCode >= 500)
            {
                throw new AiServiceUnavailableException("The AI processing service encountered an upstream error.");
            }

            throw new AiServiceBadResponseException($"The AI service returned an unprocessable response ({response.StatusCode}).");
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
            if (result == null)
            {
                throw new AiServiceBadResponseException("The AI service returned an empty payload.");
            }
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON response from {Path}", path);
            throw new AiServiceBadResponseException("The AI service returned a malformed response.", ex);
        }
    }
}
