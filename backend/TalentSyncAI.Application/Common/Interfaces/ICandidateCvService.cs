using TalentSyncAI.Application.Features.Cv.DTOs;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface ICandidateCvService
{
    Task<CandidateCvDto> UploadCvAsync(
        Guid userId, 
        Stream stream, 
        string fileName, 
        string contentType, 
        long fileLength, 
        bool setAsPrimary = true, 
        CancellationToken cancellationToken = default);

    Task<List<CandidateCvDto>> GetAllMyCvsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<CandidateCvDto?> GetCvByIdAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string FileName)?> DownloadCvAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default);

    Task<bool> SetPrimaryCvAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default);

    Task<bool> DeleteCvAsync(Guid userId, Guid cvId, CancellationToken cancellationToken = default);
}
