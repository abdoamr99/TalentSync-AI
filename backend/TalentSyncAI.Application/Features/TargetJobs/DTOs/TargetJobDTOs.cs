using System.ComponentModel.DataAnnotations;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Application.Features.TargetJobs.DTOs;

public class CreateTargetJobApplicationRequest
{
    [Required]
    [MaxLength(200)]
    public string TargetCompany { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TargetRole { get; set; } = string.Empty;

    public JdInputType JdInputType { get; set; } = JdInputType.Text;

    [MaxLength(1000)]
    public string? JobPostingUrl { get; set; }

    public string? RawJobDescription { get; set; }

    public Guid? SourceCvId { get; set; }
}

public class TargetJobApplicationListDto
{
    public Guid Id { get; set; }
    public string TargetCompany { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public JdInputType JdInputType { get; set; }
    public string? JobPostingUrl { get; set; }
    public Guid? SourceCvId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Status flags of AI Artifacts
    public bool HasMatchAnalysis { get; set; }
    public decimal? MatchScore { get; set; }
    public bool HasCompanyResearch { get; set; }
    public bool HasTailoredResume { get; set; }
    public bool HasInterviewPrep { get; set; }
}

public class TargetJobApplicationDetailDto : TargetJobApplicationListDto
{
    public string RawJobDescription { get; set; } = string.Empty;
    public JobMatchAnalysisDto? MatchAnalysis { get; set; }
    public CompanyRoleResearchDto? CompanyResearch { get; set; }
    public TailoredResumeDto? TailoredResume { get; set; }
    public InterviewPreparationDto? InterviewPrep { get; set; }
}

public class JobMatchAnalysisDto
{
    public decimal MatchScore { get; set; }
    public string MatchVerdict { get; set; } = string.Empty;
    public List<StrengthItemDto> StrengthsAnalysis { get; set; } = new();
    public List<SkillGapItemDto> SkillGaps { get; set; } = new();
    public List<string> StrategicRecommendations { get; set; } = new();
    public DateTime AnalyzedAtUtc { get; set; }
}

public class StrengthItemDto
{
    public string SkillOrRequirement { get; set; } = string.Empty;
    public string EvidenceFromCv { get; set; } = string.Empty;
    public string Importance { get; set; } = "High";
}

public class SkillGapItemDto
{
    public string MissingSkill { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string ImpactDescription { get; set; } = string.Empty;
    public string RemediationAdvice { get; set; } = string.Empty;
}

public class CompanyRoleResearchDto
{
    public CompanyOverviewDto? CompanyOverview { get; set; }
    public TechStackIntelligenceDto? TechStackIntelligence { get; set; }
    public CultureAndValuesDto? CultureAndValues { get; set; }
    public InterviewProcessInsightsDto? InterviewProcessInsights { get; set; }
    public List<string> KillerQuestionsToAskThem { get; set; } = new();
    public DateTime ResearchedAtUtc { get; set; }
}

public class CompanyOverviewDto
{
    public string Industry { get; set; } = string.Empty;
    public string Mission { get; set; } = string.Empty;
    public string RecentNewsOrMilestones { get; set; } = string.Empty;
}

public class TechStackIntelligenceDto
{
    public List<string> PrimaryLanguages { get; set; } = new();
    public string CloudInfrastructure { get; set; } = string.Empty;
    public List<string> Databases { get; set; } = new();
}

public class CultureAndValuesDto
{
    public List<string> CoreValues { get; set; } = new();
    public string WorkEnvironment { get; set; } = string.Empty;
}

public class InterviewProcessInsightsDto
{
    public List<InterviewRoundDto> StandardRounds { get; set; } = new();
}

public class InterviewRoundDto
{
    public int RoundIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Focus { get; set; } = string.Empty;
}

public class TailoredResumeDto
{
    public string TailoredHeadline { get; set; } = string.Empty;
    public string TailoredSummary { get; set; } = string.Empty;
    public OptimizedSkillsDto? OptimizedSkills { get; set; }
    public List<TailoredExperienceBlockDto> TailoredExperienceBulletPoints { get; set; } = new();
    public string FullMarkdownContent { get; set; } = string.Empty;
    public string? GeneratedPdfUrl { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class OptimizedSkillsDto
{
    public List<string> TopMatchingSkills { get; set; } = new();
    public List<string> SecondarySkills { get; set; } = new();
}

public class TailoredExperienceBlockDto
{
    public string Company { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public List<string> OptimizedBullets { get; set; } = new();
}

public class InterviewPreparationDto
{
    public string ElevatorPitch60Seconds { get; set; } = string.Empty;
    public List<InterviewRoundPrepDto> InterviewRoundsPrep { get; set; } = new();
    public DayOfBattlecardDto? DayOfBattlecard { get; set; }
    public List<StudyPlanDayDto> TimeBoxedStudyPlan { get; set; } = new();
    public DateTime PreparedAtUtc { get; set; }
}

public class InterviewRoundPrepDto
{
    public string RoundName { get; set; } = string.Empty;
    public List<string> KeyFocusAreas { get; set; } = new();
    public List<string> LikelyQuestions { get; set; } = new();
    public List<StarStoryDto> StarStories { get; set; } = new();
}

public class StarStoryDto
{
    public string StoryTitle { get; set; } = string.Empty;
    public string TargetSkill { get; set; } = string.Empty;
    public string Situation { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public class DayOfBattlecardDto
{
    public List<string> KeyStrengthsToEmphasize { get; set; } = new();
    public List<RedFlagCounterDto> PotentialRedFlagsAndCounters { get; set; } = new();
    public List<string> QuestionsToAskInterviewers { get; set; } = new();
}

public class RedFlagCounterDto
{
    public string AnticipatedDoubt { get; set; } = string.Empty;
    public string CounterResponse { get; set; } = string.Empty;
}

public class StudyPlanDayDto
{
    public int Day { get; set; }
    public string Task { get; set; } = string.Empty;
}
