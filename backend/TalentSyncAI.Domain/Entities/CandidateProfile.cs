using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

/// <summary>
/// Independent Candidate profile root (NextRole style).
/// Completely separate from any Company.
/// </summary>
public class CandidateProfile : SoftDeletableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? TargetJobTitle { get; set; }

    [MaxLength(500)]
    public string? LinkedInUrl { get; set; }

    [MaxLength(500)]
    public string? GitHubUrl { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }

    // Navigation
    public ICollection<CandidateCv> Resumes { get; set; } = new List<CandidateCv>();
    public ICollection<TargetJobApplication> TargetApplications { get; set; } = new List<TargetJobApplication>();
}

