namespace TalentSyncAI.Domain.Common;

/// <summary>
/// Defines tenant scoping for B2B resources owned by a specific company.
/// Used by EF Core Global Query Filters to enforce strict row-level tenant isolation.
/// </summary>
public interface ITenantScoped
{
    Guid CompanyId { get; }
}

