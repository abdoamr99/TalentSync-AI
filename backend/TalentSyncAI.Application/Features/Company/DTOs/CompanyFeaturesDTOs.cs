using System.ComponentModel.DataAnnotations;

namespace TalentSyncAI.Application.Features.Company.DTOs;

public class ExecuteCandidateSearchRequest
{
    [Required]
    [MaxLength(1000)]
    public string NaturalLanguageQuery { get; set; } = string.Empty;

    public bool IsSavedSearch { get; set; } = false;

    [MaxLength(200)]
    public string? SavedSearchTitle { get; set; }
}

public class CandidateSearchDto
{
    public Guid Id { get; set; }
    public string NaturalLanguageQuery { get; set; } = string.Empty;
    public bool IsSavedSearch { get; set; }
    public string? SavedSearchTitle { get; set; }
    public int TotalResultsCount { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public Guid PerformedByUserId { get; set; }
    public List<CandidateSearchResultItemDto> Results { get; set; } = new();
}

public class CandidateSearchResultItemDto
{
    public Guid CompanyCandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? CurrentTitle { get; set; }
    public string? Location { get; set; }
    public int? YearsOfExperience { get; set; }
    public int Rank { get; set; }
    public decimal MatchScore { get; set; }
    public string? MatchingStrengths { get; set; }
    public string? MatchingGaps { get; set; }
    public string? MatchExplanation { get; set; }
}

public class CreateShortlistRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class CompanyShortlistDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CandidatesCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<ShortlistCandidateItemDto> Candidates { get; set; } = new();
}

public class AddCandidateToShortlistRequest
{
    [Required]
    public Guid CompanyCandidateId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ShortlistCandidateItemDto
{
    public Guid CandidateId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CurrentTitle { get; set; }
    public string? Location { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAtUtc { get; set; }
}

public class RecruiterActionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTime PerformedAtUtc { get; set; }
}

