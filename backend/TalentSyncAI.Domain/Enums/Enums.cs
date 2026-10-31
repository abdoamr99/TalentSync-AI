namespace TalentSyncAI.Domain.Enums;

public enum UserType
{
    Candidate = 0,   // Independent career seeker (NextRole style)
    Recruiter = 1    // Company recruiter / HR staff
}

public enum CompanyMemberRole
{
    Owner = 0,
    Admin = 1,
    Recruiter = 2,
    Viewer = 3
}

public enum CvParsingStatus
{
    Uploaded = 0,
    Parsing = 1,
    Parsed = 2,
    Failed = 3
}

public enum JdInputType
{
    Text = 0,
    File = 1,
    Url = 2
}

public enum AiRunType
{
    CvParsing = 0,
    CvToJdMatch = 1,
    SkillGapAnalysis = 2,
    CompanyRoleResearch = 3,
    ResumeTailoring = 4,
    InterviewPrep = 5,
    NaturalLanguageSearch = 6,
    CandidateReRanking = 7
}

public enum AiRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum CandidateMatchDecision
{
    UnderReview = 0,
    Shortlisted = 1,
    Saved = 2,
    Rejected = 3
}

public enum RecruiterActionType
{
    UploadCv = 0,
    NaturalLanguageSearch = 1,
    ViewCandidate = 2,
    ShortlistCandidate = 3,
    CompareCandidates = 4,
    ExportReport = 5
}
