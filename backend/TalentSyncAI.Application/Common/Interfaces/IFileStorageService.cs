namespace TalentSyncAI.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string storedFilePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storedFilePath, CancellationToken cancellationToken = default);
}

