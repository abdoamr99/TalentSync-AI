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
    public const long DefaultMinFileSize = 16;                // 16 Bytes minimum
    public const int MaxBulkUploadFiles = 50;                 // Max 50 files per bulk upload batch

    // Magic Bytes signatures
    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] ZipDocxSignature = [0x50, 0x4B, 0x03, 0x04]; // PK..
    private static readonly byte[] DocSignature = [0xD0, 0xCF, 0x11, 0xE0]; // OLE CFB

    public static bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
    }

    /// <summary>
    /// Validates file extension, length bounds, and magic bytes signature to prevent extension spoofing.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateFile(Stream stream, string fileName, long length)
    {
        if (length < DefaultMinFileSize)
        {
            return (false, "File is empty or corrupted (file size is too small to be a valid document).");
        }

        if (length > DefaultMaxFileSize)
        {
            return (false, $"File size exceeds the allowed limit of {DefaultMaxFileSize / (1024 * 1024)} MB.");
        }

        if (!IsAllowedExtension(fileName))
        {
            return (false, "Unsupported file format. Allowed formats: .pdf, .docx, .doc, .txt.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        Span<byte> header = stackalloc byte[8];
        var bytesRead = stream.Read(header);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (bytesRead < 4 && ext != ".txt")
        {
            return (false, "Unable to read document header for validation.");
        }

        switch (ext)
        {
            case ".pdf":
                if (!header[..4].SequenceEqual(PdfSignature))
                {
                    return (false, "File content does not match a valid PDF document signature.");
                }
                break;

            case ".docx":
                if (!header[..4].SequenceEqual(ZipDocxSignature))
                {
                    return (false, "File content does not match a valid DOCX document signature.");
                }
                break;

            case ".doc":
                if (!header[..4].SequenceEqual(DocSignature))
                {
                    return (false, "File content does not match a valid DOC document signature.");
                }
                break;

            case ".txt":
                // Verify text does not contain excessive binary null bytes
                for (var i = 0; i < bytesRead; i++)
                {
                    if (header[i] == 0)
                    {
                        return (false, "Text file contains binary data and is not a valid plain text document.");
                    }
                }
                break;
        }

        return (true, null);
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

    /// <summary>
    /// Attempts text extraction from plain text and PDF streams.
    /// </summary>
    public static async Task<string?> TryExtractTextAsync(Stream stream, string fileName)
    {
        try
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (stream.CanSeek) stream.Position = 0;

            if (ext == ".txt")
            {
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                if (stream.CanSeek) stream.Position = 0;
                return content?.Trim();
            }

            if (ext == ".pdf")
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                if (stream.CanSeek) stream.Position = 0;
                var rawBytes = ms.ToArray();
                var text = ExtractSimplePdfText(rawBytes);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                {
                    return text.Trim();
                }
            }
        }
        catch
        {
            // Silently fall back if format cannot be parsed
        }
        return null;
    }

    private static string ExtractSimplePdfText(byte[] rawBytes)
    {
        var text = System.Text.Encoding.Latin1.GetString(rawBytes);
        var sb = new System.Text.StringBuilder();

        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\(([^()]+)\)\s*T[jJ]");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (m.Groups.Count > 1)
            {
                sb.Append(m.Groups[1].Value).Append(' ');
            }
        }

        var result = sb.ToString().Trim();
        if (result.Length > 20) return result;

        var words = System.Text.RegularExpressions.Regex.Matches(text, @"[A-Za-z0-9#\+\.]{3,}")
            .Select(m => m.Value)
            .Where(w => !w.StartsWith("obj") && !w.StartsWith("endobj") && !w.StartsWith("xref") && !w.StartsWith("stream"))
            .Distinct();

        return string.Join(" ", words.Take(200));
    }
}
