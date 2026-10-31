using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService) : base(options)
    {
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    // Identity & Core
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    // Candidate Side (NextRole)
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<CandidateCv> CandidateResumes => Set<CandidateCv>();
    public DbSet<TargetJobApplication> TargetJobApplications => Set<TargetJobApplication>();
    public DbSet<JobMatchAnalysis> JobMatchAnalyses => Set<JobMatchAnalysis>();
    public DbSet<CompanyRoleResearch> CompanyRoleResearches => Set<CompanyRoleResearch>();
    public DbSet<TailoredResume> TailoredResumes => Set<TailoredResume>();
    public DbSet<InterviewPreparation> InterviewPreparations => Set<InterviewPreparation>();

    // Company / Recruiter CV Pool & Natural Language Search
    public DbSet<CompanyCandidate> CompanyCandidates => Set<CompanyCandidate>();
    public DbSet<CompanyCvDocument> CompanyCvDocuments => Set<CompanyCvDocument>();
    public DbSet<CompanyCandidateSkill> CompanyCandidateSkills => Set<CompanyCandidateSkill>();
    public DbSet<CompanyCandidateExperience> CompanyCandidateExperiences => Set<CompanyCandidateExperience>();
    public DbSet<CompanyCandidateEducation> CompanyCandidateEducations => Set<CompanyCandidateEducation>();
    public DbSet<CandidateSearch> CandidateSearches => Set<CandidateSearch>();
    public DbSet<CandidateSearchResult> CandidateSearchResults => Set<CandidateSearchResult>();
    public DbSet<CompanyShortlist> CompanyShortlists => Set<CompanyShortlist>();
    public DbSet<CompanyShortlistCandidate> CompanyShortlistCandidates => Set<CompanyShortlistCandidate>();

    // AI & Audit
    public DbSet<AiProcessingRun> AiProcessingRuns => Set<AiProcessingRun>();
    public DbSet<RecruiterAction> RecruiterActions => Set<RecruiterAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // 1. Soft-Delete Global Filters
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Company>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CandidateProfile>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CandidateCv>().HasQueryFilter(e => !e.IsDeleted && !e.CandidateProfile.IsDeleted);
        modelBuilder.Entity<TargetJobApplication>().HasQueryFilter(e => !e.IsDeleted && !e.CandidateProfile.IsDeleted);

        // 2. Strict Tenant-Scoped Filters for Company Pool
        modelBuilder.Entity<CompanyCandidate>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));
        modelBuilder.Entity<CompanyCvDocument>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));
        modelBuilder.Entity<CompanyShortlist>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));

        modelBuilder.Entity<CompanyMember>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<CandidateSearch>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<AiProcessingRun>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<RecruiterAction>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedByUserId ??= currentUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedByUserId = currentUserId;
            }
        }

        if (_tenantContext.HasTenant)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            {
                if (entry.State == EntityState.Added && entry.Entity.CompanyId == Guid.Empty)
                {
                    var property = entry.Property("CompanyId");
                    property.CurrentValue = _tenantContext.CompanyId!.Value;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
