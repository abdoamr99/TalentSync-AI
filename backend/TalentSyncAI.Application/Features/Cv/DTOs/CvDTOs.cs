namespace TalentSyncAI.Application.Features.Cv.DTOs;

public class CandidateCvDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsPrimary { get; set; }
    public string? FileHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? RawTextPreview { get; set; }
}

public class CompanyCvDocumentDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ParsingStatus { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public string? CandidateName { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public class CompanyBulkUploadResponseDto
{
    public int TotalReceived { get; set; }
    public int TotalUploaded { get; set; }
    public int TotalDuplicatesSkipped { get; set; }
    public List<CompanyCvDocumentDto> UploadedFiles { get; set; } = new();
    public List<string> SkippedFileNames { get; set; } = new();
}

