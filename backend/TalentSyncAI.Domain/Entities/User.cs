using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

public class User : SoftDeletableEntity
{
    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Phone]
    public string? PhoneNumber { get; set; }

    public UserType UserType { get; set; } = UserType.Candidate;

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }

    [MaxLength(500)]
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryUtc { get; set; }

    // Navigation properties
    public CandidateProfile? CandidateProfile { get; set; } // Candidate Side (1:1)
    public ICollection<CompanyMember> CompanyMemberships { get; set; } = new List<CompanyMember>(); // Recruiter Side (1:N)
}
