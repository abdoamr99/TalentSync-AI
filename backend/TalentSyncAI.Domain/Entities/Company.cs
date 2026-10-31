using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

public class Company : SoftDeletableEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Website { get; set; }

    [MaxLength(200)]
    public string? Industry { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties for Company CV Pool & Recruiter Workspace
    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
    public ICollection<CompanyCandidate> CandidatePool { get; set; } = new List<CompanyCandidate>();
    public ICollection<CompanyCvDocument> UploadedCvs { get; set; } = new List<CompanyCvDocument>();
    public ICollection<CandidateSearch> Searches { get; set; } = new List<CandidateSearch>();
    public ICollection<CompanyShortlist> Shortlists { get; set; } = new List<CompanyShortlist>();
    public ICollection<AiProcessingRun> AiRuns { get; set; } = new List<AiProcessingRun>();
    public ICollection<RecruiterAction> RecruiterActions { get; set; } = new List<RecruiterAction>();
}
