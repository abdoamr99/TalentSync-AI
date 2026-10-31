using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

public class CandidateCv : SoftDeletableEntity
{
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile CandidateProfile { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string StoredFilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
    public bool IsPrimary { get; set; } = true;

    public string? RawTextContent { get; set; } // Parsed text content
    public string? StructuredJson { get; set; } // Extracted profile schema
}

