using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.Infrastructure.Services;

/// <summary>
/// Phase 1 Stub for AI Service. The actual integration will be via internal HTTP API in a future phase.
/// </summary>
public class StubAiService : IAiService
{
    public Task<string> ParseCvAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"[AI Stub] Parsed CV content for {fileName}");
    }

    public Task<string> ExtractCandidateProfileAsync(string parsedText, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("{\"status\": \"extracted\", \"message\": \"Phase 1 AI Stub\"}");
    }

    public Task<decimal> CalculateJobMatchScoreAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0.85m);
    }
}

/// <summary>
/// Phase 1 Stub for Vector Search Service. Decoupled from any vector database.
/// </summary>
public class StubVectorSearchService : IVectorSearchService
{
    public Task IndexCandidateAsync(Guid candidateId, string profileText, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> SearchCandidatesAsync(string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> emptyResults = Array.Empty<Guid>();
        return Task.FromResult(emptyResults);
    }
}
