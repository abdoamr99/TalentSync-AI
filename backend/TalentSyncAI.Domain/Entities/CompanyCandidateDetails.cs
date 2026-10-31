using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

public class CompanyCandidateSkill : BaseEntity
{
    public Guid CompanyCandidateId { get; set; }
    public CompanyCandidate CompanyCandidate { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? YearsExperience { get; set; }
}

public class CompanyCandidateExperience : BaseEntity
{
    public Guid CompanyCandidateId { get; set; }
    public CompanyCandidate CompanyCandidate { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string JobTitle { get; set; } = string.Empty;

    public string? DurationText { get; set; }
    public string? Description { get; set; }
}

public class CompanyCandidateEducation : BaseEntity
{
    public Guid CompanyCandidateId { get; set; }
    public CompanyCandidate CompanyCandidate { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Degree { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FieldOfStudy { get; set; }
}

