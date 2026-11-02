using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface ICandidateAiService
{
    Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(
        string cvText, 
        string jobTitle, 
        string companyName, 
        string jobDescription, 
        CancellationToken cancellationToken = default);

    Task<CompanyRoleResearchDto> ResearchCompanyAsync(
        string companyName, 
        string jobTitle, 
        string? jobPostingUrl, 
        string jobDescription, 
        CancellationToken cancellationToken = default);

    Task<TailoredResumeDto> TailorResumeAsync(
        string cvText, 
        string jobTitle, 
        string companyName, 
        string jobDescription, 
        CancellationToken cancellationToken = default);

    Task<InterviewPreparationDto> PrepareInterviewAsync(
        string cvText, 
        string jobTitle, 
        string companyName, 
        string jobDescription, 
        CancellationToken cancellationToken = default);
}

