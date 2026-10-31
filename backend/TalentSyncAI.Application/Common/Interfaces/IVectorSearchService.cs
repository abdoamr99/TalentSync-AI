namespace TalentSyncAI.Application.Common.Interfaces;

/// <summary>
/// Abstraction for future semantic/vector search retrieval without tight coupling to vector DBs.
/// </summary>
public interface IVectorSearchService
{
    Task IndexCandidateAsync(Guid candidateId, string profileText, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> SearchCandidatesAsync(string query, int topK = 10, CancellationToken cancellationToken = default);
}

