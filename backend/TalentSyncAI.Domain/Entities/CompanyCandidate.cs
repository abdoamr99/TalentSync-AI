using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

/// <summary>
/// A candidate profile extracted from CVs in the Company's private CV Pool.
/// Strictly tenant-scoped: Owned by Company.
/// </summary>
public class CompanyCandidate : SoftDeletableEntity, ITenantScoped
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? CurrentTitle { get; set; }

    public int? YearsOfExperience { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public string? Summary { get; set; }

    // Navigation
    public ICollection<CompanyCvDocument> CvFiles { get; set; } = new List<CompanyCvDocument>();
    public ICollection<CompanyCandidateSkill> Skills { get; set; } = new List<CompanyCandidateSkill>();
    public ICollection<CompanyCandidateExperience> Experiences { get; set; } = new List<CompanyCandidateExperience>();
    public ICollection<CompanyCandidateEducation> Educations { get; set; } = new List<CompanyCandidateEducation>();
}

