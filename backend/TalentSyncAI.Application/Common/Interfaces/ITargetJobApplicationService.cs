using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface ITargetJobApplicationService
{
    Task<TargetJobApplicationDetailDto> CreateApplicationAsync(
        Guid userId, 
        CreateTargetJobApplicationModel model, 
        CancellationToken cancellationToken = default);

    Task<List<TargetJobApplicationListDto>> ListApplicationsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    Task<TargetJobApplicationDetailDto?> GetApplicationByIdAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<bool> DeleteApplicationAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<CompanyRoleResearchDto> ResearchCompanyAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<TailoredResumeDto> TailorResumeAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string FileName)?> DownloadTailoredResumePdfAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<InterviewPreparationDto> PrepareInterviewAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);

    Task<TargetJobApplicationDetailDto> GenerateAllArtifactsAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default);
}
