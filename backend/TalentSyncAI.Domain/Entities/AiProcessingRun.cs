using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

public class AiProcessingRun : BaseEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public AiRunType RunType { get; set; }
    public AiRunStatus Status { get; set; } = AiRunStatus.Pending;

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public Guid TriggeredByUserId { get; set; }
    public User TriggeredByUser { get; set; } = null!;

    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? DurationMs { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

