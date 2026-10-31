namespace TalentSyncAI.Domain.Common;

public abstract class SoftDeletableEntity : AuditableEntity
{
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

