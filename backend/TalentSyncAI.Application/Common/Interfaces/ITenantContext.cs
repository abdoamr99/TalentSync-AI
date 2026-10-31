namespace TalentSyncAI.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? CompanyId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid companyId);
}

