using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

public class CandidateSearch : BaseEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    public string NaturalLanguageQuery { get; set; } = string.Empty; // e.g., 'Find .NET developers with 3+ years'

    public string? StructuredFiltersJson { get; set; } // skills, years of exp, etc.
    public bool IsSavedSearch { get; set; } = false;

    [MaxLength(200)]
    public string? SavedSearchTitle { get; set; }

    public int TotalResultsCount { get; set; }
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;

    // Ranked Results
    public ICollection<CandidateSearchResult> Results { get; set; } = new List<CandidateSearchResult>();
}

public class CandidateSearchResult : BaseEntity
{
    public Guid CandidateSearchId { get; set; }
    public CandidateSearch CandidateSearch { get; set; } = null!;

    public Guid CompanyCandidateId { get; set; }
    public CompanyCandidate CompanyCandidate { get; set; } = null!;

    public int Rank { get; set; }
    public decimal MatchScore { get; set; } // 0.00 to 100.00

    public string? MatchingStrengths { get; set; } // Why candidate matched
    public string? MatchingGaps { get; set; } // Missing requirements
    public string? MatchExplanation { get; set; } // Grounded explanation in CV
}

public class CompanyShortlist : SoftDeletableEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ICollection<CompanyShortlistCandidate> Candidates { get; set; } = new List<CompanyShortlistCandidate>();
}

public class CompanyShortlistCandidate : BaseEntity
{
    public Guid CompanyShortlistId { get; set; }
    public CompanyShortlist CompanyShortlist { get; set; } = null!;

    public Guid CompanyCandidateId { get; set; }
    public CompanyCandidate CompanyCandidate { get; set; } = null!;

    public Guid AddedByUserId { get; set; }
    public User AddedByUser { get; set; } = null!;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

