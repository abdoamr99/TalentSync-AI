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
        ICurrentUserService currentUserService)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    // Core Identity & Tenancy
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    // Candidate Aggregate (B2C NextRole style)
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<CandidateCv> CandidateResumes => Set<CandidateCv>();
    public DbSet<TargetJobApplication> TargetJobApplications => Set<TargetJobApplication>();
    public DbSet<JobMatchAnalysis> JobMatchAnalyses => Set<JobMatchAnalysis>();
    public DbSet<CompanyRoleResearch> CompanyRoleResearches => Set<CompanyRoleResearch>();
    public DbSet<TailoredResume> TailoredResumes => Set<TailoredResume>();
    public DbSet<InterviewPreparation> InterviewPreparations => Set<InterviewPreparation>();

    // Company CV Pool & Search (B2B Workspace)
    public DbSet<CompanyCvDocument> CompanyCvDocuments => Set<CompanyCvDocument>();
    public DbSet<CompanyCandidate> CompanyCandidates => Set<CompanyCandidate>();
    public DbSet<CompanyCandidateSkill> CompanyCandidateSkills => Set<CompanyCandidateSkill>();
    public DbSet<CompanyCandidateExperience> CompanyCandidateExperiences => Set<CompanyCandidateExperience>();
    public DbSet<CompanyCandidateEducation> CompanyCandidateEducations => Set<CompanyCandidateEducation>();
    public DbSet<CandidateSearch> CandidateSearches => Set<CandidateSearch>();
    public DbSet<CandidateSearchResult> CandidateSearchResults => Set<CandidateSearchResult>();
    public DbSet<CompanyShortlist> CompanyShortlists => Set<CompanyShortlist>();
    public DbSet<CompanyShortlistCandidate> CompanyShortlistCandidates => Set<CompanyShortlistCandidate>();

    // Audit & Observability
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

        // Matching Child Filters for TargetJobApplication
        modelBuilder.Entity<JobMatchAnalysis>().HasQueryFilter(e => !e.TargetJobApplication.IsDeleted);
        modelBuilder.Entity<TailoredResume>().HasQueryFilter(e => !e.TargetJobApplication.IsDeleted);
        modelBuilder.Entity<InterviewPreparation>().HasQueryFilter(e => !e.TargetJobApplication.IsDeleted);
        modelBuilder.Entity<CompanyRoleResearch>().HasQueryFilter(e => !e.TargetJobApplication.IsDeleted);

        // 2. Strict Tenant-Scoped Filters for Company Pool
        modelBuilder.Entity<CompanyCandidate>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));
        modelBuilder.Entity<CompanyCvDocument>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));
        modelBuilder.Entity<CompanyShortlist>().HasQueryFilter(e => !e.IsDeleted && (!_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId));

        // Matching Child Filters for CompanyCandidate
        modelBuilder.Entity<CompanyCandidateSkill>().HasQueryFilter(e => !e.CompanyCandidate.IsDeleted);
        modelBuilder.Entity<CompanyCandidateExperience>().HasQueryFilter(e => !e.CompanyCandidate.IsDeleted);
        modelBuilder.Entity<CompanyCandidateEducation>().HasQueryFilter(e => !e.CompanyCandidate.IsDeleted);

        // Tenant Scoped Operations
        modelBuilder.Entity<CompanyMember>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<CandidateSearch>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<CandidateSearchResult>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CandidateSearch.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<CompanyShortlistCandidate>().HasQueryFilter(e => !e.AddedByUser.IsDeleted && !e.CompanyCandidate.IsDeleted);
        modelBuilder.Entity<AiProcessingRun>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
        modelBuilder.Entity<RecruiterAction>().HasQueryFilter(e => !_tenantContext.HasTenant || e.CompanyId == _tenantContext.CompanyId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedByUserId = currentUserId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedByUserId = currentUserId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
