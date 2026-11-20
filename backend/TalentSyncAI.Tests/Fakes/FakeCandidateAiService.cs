using TalentSyncAI.Application.Common.Exceptions;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.Tests.Fakes;

public class FakeCandidateAiService : ICandidateAiService
{
    public bool ShouldThrowAiUnavailable { get; set; } = false;

    public Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowAiUnavailable)
        {
            throw new AiServiceUnavailableException("The AI processing service is currently unreachable.");
        }

        return Task.FromResult(new JobMatchAnalysisDto
        {
            MatchScore = 85.5m,
            MatchVerdict = "Strong Candidate Fit",
            AnalyzedAtUtc = DateTime.UtcNow,
            StrengthsAnalysis = new List<StrengthItemDto>
            {
                new() { SkillOrRequirement = "Core Competency", EvidenceFromCv = "Strong alignment with role", Importance = "High" }
            },
            SkillGaps = new List<SkillGapItemDto>(),
            StrategicRecommendations = new List<string> { "Highlight distributed systems experience" }
        });
    }

    public Task<CompanyRoleResearchDto> ResearchCompanyAsync(
        string companyName,
        string jobTitle,
        string? jobPostingUrl,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowAiUnavailable)
        {
            throw new AiServiceUnavailableException("The AI processing service is currently unreachable.");
        }

        return Task.FromResult(new CompanyRoleResearchDto
        {
            ResearchedAtUtc = DateTime.UtcNow,
            CompanyOverview = new CompanyOverviewDto { Mission = "Empowering AI", Industry = "Technology" },
            TechStackIntelligence = new TechStackIntelligenceDto { PrimaryLanguages = new List<string> { "C#", "Python" } },
            CultureAndValues = new CultureAndValuesDto { CoreValues = new List<string> { "Innovation", "Integrity" } },
            KillerQuestionsToAskThem = new List<string> { "What does success look like in 6 months?" }
        });
    }

    public Task<TailoredResumeDto> TailorResumeAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowAiUnavailable)
        {
            throw new AiServiceUnavailableException("The AI processing service is currently unreachable.");
        }

        return Task.FromResult(new TailoredResumeDto
        {
            TailoredHeadline = $"{jobTitle} at {companyName}",
            TailoredSummary = "Experienced engineer tailored for the position.",
            FullMarkdownContent = $"# Resume\n\n## Summary\n\nExperienced engineer.\n\n## Experience\n\n- Accomplished X with Y.",
            GeneratedAtUtc = DateTime.UtcNow
        });
    }

    public Task<InterviewPreparationDto> PrepareInterviewAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        string? jobPostingUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowAiUnavailable)
        {
            throw new AiServiceUnavailableException("The AI processing service is currently unreachable.");
        }

        return Task.FromResult(new InterviewPreparationDto
        {
            ElevatorPitch60Seconds = "I am a high-impact developer.",
            PreparedAtUtc = DateTime.UtcNow
        });
    }
}
