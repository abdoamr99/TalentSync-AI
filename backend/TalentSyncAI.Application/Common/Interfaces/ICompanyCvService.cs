using TalentSyncAI.Application.Common.Models;
using TalentSyncAI.Application.Features.Cv.DTOs;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Application.Common.Interfaces;

public record CompanyCvUploadItem(Stream Stream, string FileName, string ContentType, long Length);

public interface ICompanyCvService
{
    Task<CompanyBulkUploadResponseDto> UploadCompanyCvsAsync(
        Guid companyId, 
        Guid userId, 
        IReadOnlyList<CompanyCvUploadItem> files, 
        CancellationToken cancellationToken = default);

    Task<PagedResult<CompanyCvDocumentDto>> GetCompanyCvPoolAsync(
        Guid companyId, 
        int pageIndex, 
        int pageSize, 
        CvParsingStatus? status, 
        CancellationToken cancellationToken = default);

    Task<CompanyCvDocumentDto?> GetCompanyCvByIdAsync(
        Guid companyId, 
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)?> DownloadCompanyCvAsync(
        Guid companyId, 
        Guid id, 
        CancellationToken cancellationToken = default);
}
