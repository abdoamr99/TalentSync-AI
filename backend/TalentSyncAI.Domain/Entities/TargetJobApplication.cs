using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Common;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Domain.Entities;

/// <summary>
/// A target job application that a Candidate creates for career preparation.
/// Candidate inputs a Job Description via Text, File, or URL (NextRole style).
/// </summary>
public class TargetJobApplication : SoftDeletableEntity
{
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile CandidateProfile { get; set; } = null!;

    public Guid? SourceCvId { get; set; }
    public CandidateCv? SourceCv { get; set; }

    [Required]
    [MaxLength(200)]
    public string TargetCompany { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TargetRole { get; set; } = string.Empty;

    public JdInputType JdInputType { get; set; } = JdInputType.Text;

    [MaxLength(1000)]
    public string? JobPostingUrl { get; set; }

    public string RawJobDescription { get; set; } = string.Empty; // Full JD content

    // AI Generated Insights & Artifacts
    public JobMatchAnalysis? MatchAnalysis { get; set; }
    public CompanyRoleResearch? CompanyResearch { get; set; }
    public TailoredResume? TailoredResume { get; set; }
    public InterviewPreparation? InterviewPrep { get; set; }
}

