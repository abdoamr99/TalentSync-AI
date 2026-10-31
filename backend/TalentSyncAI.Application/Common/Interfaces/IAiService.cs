namespace TalentSyncAI.Application.Common.Interfaces;

/// <summary>
/// Abstraction for communicating with the separate external AI service.
/// Kept behind an interface so Phase 1 doesn't bind to external AI implementations.
/// </summary>
public interface IAiService
{
    Task<string> ParseCvAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task<string> ExtractCandidateProfileAsync(string parsedText, CancellationToken cancellationToken = default);
    Task<decimal> CalculateJobMatchScoreAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default);
}

