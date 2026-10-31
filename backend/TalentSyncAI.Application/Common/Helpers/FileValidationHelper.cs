using System.Security.Cryptography;

namespace TalentSyncAI.Application.Common.Helpers;

public static class FileValidationHelper
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".doc",
        ".txt"
    };

    public const long DefaultMaxFileSize = 10 * 1024 * 1024; // 10 MB

    public static bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
    }

    public static async Task<string> ComputeSha256HashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

