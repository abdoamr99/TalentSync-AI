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

/// <summary>
/// Phase 1 File Storage implementation for CV uploads (saved to local disk / uploads directory).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _uploadDirectory;

    public LocalFileStorageService()
    {
        _uploadDirectory = Path.Combine(AppContext.BaseDirectory, "uploads");
        if (!Directory.Exists(_uploadDirectory))
        {
            Directory.CreateDirectory(_uploadDirectory);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var uniqueFileName = $"{Guid.CreateVersion7()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(_uploadDirectory, uniqueFileName);

        using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(file, cancellationToken);

        return uniqueFileName;
    }

    public Task<Stream> DownloadFileAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uploadDirectory, storedFilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File not found on storage.", storedFilePath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storedFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uploadDirectory, storedFilePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }
}

