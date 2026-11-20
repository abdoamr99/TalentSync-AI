using TalentSyncAI.Application.Features.Company.DTOs;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface ICompanyShortlistService
{
    Task<CompanyShortlistDto> CreateShortlistAsync(
        Guid companyId, 
        Guid userId, 
        CreateShortlistRequest request, 
        CancellationToken cancellationToken = default);

    Task<List<CompanyShortlistDto>> ListShortlistsAsync(
        Guid companyId, 
        CancellationToken cancellationToken = default);

    Task<CompanyShortlistDto?> GetShortlistByIdAsync(
        Guid companyId, 
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<ShortlistCandidateItemDto> AddCandidateToShortlistAsync(
        Guid companyId, 
        Guid userId, 
        Guid shortlistId, 
        AddCandidateToShortlistRequest request, 
        CancellationToken cancellationToken = default);
}
