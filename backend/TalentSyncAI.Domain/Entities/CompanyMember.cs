using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

public class CompanyMember : AuditableEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public CompanyMemberRole Role { get; set; } = CompanyMemberRole.Recruiter;
    public bool IsActive { get; set; } = true;
}
