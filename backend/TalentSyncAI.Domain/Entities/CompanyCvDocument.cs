using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

public class CompanyCvDocument : SoftDeletableEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid? CompanyCandidateId { get; set; }
    public CompanyCandidate? CompanyCandidate { get; set; }

    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string StoredFilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
    public CvParsingStatus ParsingStatus { get; set; } = CvParsingStatus.Uploaded;

    [MaxLength(64)]
    public string? FileHash { get; set; }

    public string? ExtractedText { get; set; }
    public string? ExtractedStructuredJson { get; set; }

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;
}

