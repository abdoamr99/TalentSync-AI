using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

/// <summary>
/// Append-only immutable audit log for actions performed by company recruiters.
/// </summary>
public class RecruiterAction : BaseEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public RecruiterActionType ActionType { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string? Metadata { get; set; } // JSON metadata

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}

