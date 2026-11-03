namespace TalentSyncAI.Application.Common.Interfaces;

public interface IPdfGeneratorService
{
    Task<byte[]> GenerateResumePdfAsync(
        string candidateName, 
        string targetRole, 
        string targetCompany, 
        string markdownContent, 
        CancellationToken cancellationToken = default);
}

