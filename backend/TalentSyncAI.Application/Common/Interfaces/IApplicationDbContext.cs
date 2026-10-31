using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Domain.Entities;

namespace TalentSyncAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity & Core
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<CompanyMember> CompanyMembers { get; }

    // Candidate Side (NextRole Style)
    DbSet<CandidateProfile> CandidateProfiles { get; }
    DbSet<CandidateCv> CandidateResumes { get; }
    DbSet<TargetJobApplication> TargetJobApplications { get; }
    DbSet<JobMatchAnalysis> JobMatchAnalyses { get; }
    DbSet<CompanyRoleResearch> CompanyRoleResearches { get; }
    DbSet<TailoredResume> TailoredResumes { get; }
    DbSet<InterviewPreparation> InterviewPreparations { get; }

    // Company / Recruiter Side (CV Pool & Natural Language Search)
    DbSet<CompanyCandidate> CompanyCandidates { get; }
    DbSet<CompanyCvDocument> CompanyCvDocuments { get; }
    DbSet<CompanyCandidateSkill> CompanyCandidateSkills { get; }
    DbSet<CompanyCandidateExperience> CompanyCandidateExperiences { get; }
    DbSet<CompanyCandidateEducation> CompanyCandidateEducations { get; }
    DbSet<CandidateSearch> CandidateSearches { get; }
    DbSet<CandidateSearchResult> CandidateSearchResults { get; }
    DbSet<CompanyShortlist> CompanyShortlists { get; }
    DbSet<CompanyShortlistCandidate> CompanyShortlistCandidates { get; }

    // AI & Audit
    DbSet<AiProcessingRun> AiProcessingRuns { get; }
    DbSet<RecruiterAction> RecruiterActions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
