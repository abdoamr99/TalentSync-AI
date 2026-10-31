using Microsoft.Extensions.Configuration;
using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;

    public FileStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:UploadDirectory"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            _baseStoragePath = Path.Combine(AppContext.BaseDirectory, "Storage", "Uploads");
        }
        else if (Path.IsPathRooted(configuredPath))
        {
            _baseStoragePath = configuredPath;
        }
        else
        {
            _baseStoragePath = Path.Combine(AppContext.BaseDirectory, configuredPath);
        }

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? subFolder = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedFileName = SanitizeFileName(Path.GetFileName(fileName));
        var uniquePrefix = Guid.CreateVersion7().ToString("N");
        var finalFileName = $"{uniquePrefix}_{sanitizedFileName}";

        string targetDirectory = _baseStoragePath;
        string relativeFilePath = finalFileName;

        if (!string.IsNullOrWhiteSpace(subFolder))
        {
            // Sanitize subFolder to prevent directory traversal
            var sanitizedSubFolder = subFolder.Replace("..", "").Trim('/', '\\');
            targetDirectory = Path.Combine(_baseStoragePath, sanitizedSubFolder);
            relativeFilePath = Path.Combine(sanitizedSubFolder, finalFileName);
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var fullPath = Path.Combine(targetDirectory, finalFileName);

        using (var destStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
            await fileStream.CopyToAsync(destStream, cancellationToken);
        }

        // Return path using forward slashes for cross-platform/URL consistency
        return relativeFilePath.Replace('\\', '/');
    }

    public Task<Stream> DownloadFileAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storedFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Requested file was not found on storage.", storedFilePath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storedFilePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storedFilePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public string GetFullPath(string storedFilePath)
    {
        var normalizedRelative = storedFilePath.Replace("..", "").TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_baseStoragePath, normalizedRelative);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalids = Path.GetInvalidFileNameChars();
        var clean = string.Join("_", fileName.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return string.IsNullOrWhiteSpace(clean) ? "unnamed_document" : clean;
    }
}

