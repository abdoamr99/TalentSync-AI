using TalentSyncAI.Application.Common.Interfaces;

namespace TalentSyncAI.Infrastructure.Services;

public class TenantContext : ITenantContext
{
    public Guid? CompanyId { get; private set; }
    public bool HasTenant => CompanyId.HasValue;

    public void SetTenant(Guid companyId)
    {
        CompanyId = companyId;
    }
}

