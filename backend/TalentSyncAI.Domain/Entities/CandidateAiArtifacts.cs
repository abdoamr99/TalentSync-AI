using TalentSyncAI.Domain.Common;

namespace TalentSyncAI.Domain.Entities;

public class JobMatchAnalysis : BaseEntity
{
    public Guid TargetJobApplicationId { get; set; }
    public TargetJobApplication TargetJobApplication { get; set; } = null!;

    public decimal MatchScore { get; set; } // 0.00 to 100.00
    public string? MatchingStrengths { get; set; } // JSON array of strengths
    public string? SkillGaps { get; set; } // JSON array of missing skills / gaps
    public string? OverallAssessment { get; set; } // AI explanation
    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CompanyRoleResearch : BaseEntity
{
    public Guid TargetJobApplicationId { get; set; }
    public TargetJobApplication TargetJobApplication { get; set; } = null!;

    public string? CompanyOverview { get; set; }
    public string? CultureAndValues { get; set; }
    public string? TechStackInsights { get; set; }
    public string? TypicalInterviewProcess { get; set; }
    public DateTime ResearchedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TailoredResume : BaseEntity
{
    public Guid TargetJobApplicationId { get; set; }
    public TargetJobApplication TargetJobApplication { get; set; } = null!;

    public string TailoredMarkdown { get; set; } = string.Empty;
    public string? GeneratedPdfPath { get; set; } // Downloadable tailored PDF
    public string? ChangesExplanation { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public class InterviewPreparation : BaseEntity
{
    public Guid TargetJobApplicationId { get; set; }
    public TargetJobApplication TargetJobApplication { get; set; } = null!;

    public string? ExpectedQuestions { get; set; } // JSON array of rounds & questions
    public string? StarStories { get; set; } // JSON of tailored STAR answers
    public string? StudyPlan { get; set; } // Personalized preparation plan
    public string? InterviewBattlecard { get; set; } // Quick-reference battlecard
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
}

