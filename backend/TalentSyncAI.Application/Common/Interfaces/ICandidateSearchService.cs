using TalentSyncAI.Application.Features.Company.DTOs;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface ICandidateSearchService
{
    Task<CandidateSearchDto> ExecuteSearchAsync(
        Guid companyId, 
        Guid userId, 
        ExecuteCandidateSearchRequest request, 
        CancellationToken cancellationToken = default);

    Task<List<CandidateSearchDto>> ListSearchesAsync(
        Guid companyId, 
        CancellationToken cancellationToken = default);

    Task<CandidateSearchDto?> GetSearchByIdAsync(
        Guid companyId, 
        Guid id, 
        CancellationToken cancellationToken = default);
}
