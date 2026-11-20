using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Infrastructure.Services;

public class TargetJobApplicationService : ITargetJobApplicationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICandidateAiService _aiService;
    private readonly IPdfGeneratorService _pdfGeneratorService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public TargetJobApplicationService(
        IApplicationDbContext context,
        ICandidateAiService aiService,
        IPdfGeneratorService pdfGeneratorService)
    {
        _context = context;
        _aiService = aiService;
        _pdfGeneratorService = pdfGeneratorService;
    }

    public async Task<TargetJobApplicationDetailDto> CreateApplicationAsync(
        Guid userId,
        CreateTargetJobApplicationModel model,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(model.TargetCompany))
            throw new ArgumentException("Target company name is required.");

        if (string.IsNullOrWhiteSpace(model.TargetRole))
            throw new ArgumentException("Target role title is required.");

        string rawJd = model.RawJobDescription ?? string.Empty;
        var jobUrl = model.JobPostingUrl;
        if (jobUrl == "string" || string.IsNullOrWhiteSpace(jobUrl))
        {
            jobUrl = null;
        }

        switch (model.JdInputType)
        {
            case JdInputType.File:
                if (model.FileStream == null || !model.FileLength.HasValue || model.FileLength.Value == 0 || string.IsNullOrWhiteSpace(model.FileName))
                    throw new ArgumentException("Job description file is required when input type is File.");

                var (isValid, errorMessage) = FileValidationHelper.ValidateFile(model.FileStream, model.FileName, model.FileLength.Value);
                if (!isValid) throw new ArgumentException(errorMessage);

                var extracted = await FileValidationHelper.TryExtractTextAsync(model.FileStream, model.FileName);
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    throw new ArgumentException("Unable to extract readable text content from the uploaded job description file. Please upload a valid text-based PDF or TXT file, or paste the text directly.");
                }
                rawJd = extracted;
                break;

            case JdInputType.Url:
                if (string.IsNullOrWhiteSpace(jobUrl) || 
                    !Uri.TryCreate(jobUrl, UriKind.Absolute, out var uriResult) || 
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException("A valid HTTP or HTTPS Job Posting URL is required when input type is URL.");
                }

                if (string.IsNullOrWhiteSpace(rawJd) || rawJd == "string")
                {
                    rawJd = string.Empty;
                }
                break;

            case JdInputType.Text:
            default:
                if (string.IsNullOrWhiteSpace(rawJd) || rawJd.Trim() == "string")
                    throw new ArgumentException("Job description text is required and cannot be empty or default placeholder.");
                break;
        }

        Guid? sourceCvId = model.SourceCvId;
        if (sourceCvId.HasValue && sourceCvId.Value != Guid.Empty)
        {
            var cvExists = await _context.CandidateResumes
                .AnyAsync(c => c.Id == sourceCvId.Value && c.CandidateProfileId == profile.Id, cancellationToken);

            if (!cvExists)
            {
                throw new ArgumentException($"The specified SourceCvId '{sourceCvId}' was not found for this candidate profile.");
            }
        }
        else
        {
            var primaryCv = await _context.CandidateResumes
                .Where(c => c.CandidateProfileId == profile.Id)
                .OrderByDescending(c => c.IsPrimary)
                .ThenByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            sourceCvId = primaryCv?.Id;
        }

        var application = new TargetJobApplication
        {
            CandidateProfileId = profile.Id,
            SourceCvId = sourceCvId,
            TargetCompany = model.TargetCompany.Trim(),
            TargetRole = model.TargetRole.Trim(),
            JdInputType = model.JdInputType,
            JobPostingUrl = jobUrl?.Trim(),
            RawJobDescription = rawJd.Trim()
        };

        _context.TargetJobApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetailDto(application);
    }

    public async Task<List<TargetJobApplicationListDto>> ListApplicationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        return await _context.TargetJobApplications
            .AsNoTracking()
            .Where(a => a.CandidateProfileId == profile.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(100)
            .Select(a => new TargetJobApplicationListDto
            {
                Id = a.Id,
                TargetCompany = a.TargetCompany,
                TargetRole = a.TargetRole,
                JdInputType = a.JdInputType,
                JobPostingUrl = a.JobPostingUrl,
                SourceCvId = a.SourceCvId,
                CreatedAtUtc = a.CreatedAtUtc,
                HasMatchAnalysis = a.MatchAnalysis != null,
                MatchScore = a.MatchAnalysis != null ? a.MatchAnalysis.MatchScore : null,
                HasCompanyResearch = a.CompanyResearch != null,
                HasTailoredResume = a.TailoredResume != null,
                HasInterviewPrep = a.InterviewPrep != null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TargetJobApplicationDetailDto?> GetApplicationByIdAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var profile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var application = await _context.TargetJobApplications
            .AsNoTracking()
            .Include(a => a.MatchAnalysis)
            .Include(a => a.CompanyResearch)
            .Include(a => a.TailoredResume)
            .Include(a => a.InterviewPrep)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateProfileId == profile.Id, cancellationToken);

        return application == null ? null : MapToDetailDto(application);
    }

    public async Task<bool> DeleteApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var profile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var application = await _context.TargetJobApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateProfileId == profile.Id, cancellationToken);

        if (application == null) return false;

        application.IsDeleted = true;
        application.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationEntityAsync(userId, applicationId, cancellationToken);
        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var analysisDto = await _aiService.AnalyzeJobMatchAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

        await SaveMatchAnalysisAsync(application.Id, analysisDto, cancellationToken);
        return analysisDto;
    }

    public async Task<CompanyRoleResearchDto> ResearchCompanyAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationEntityAsync(userId, applicationId, cancellationToken);

        var researchDto = await _aiService.ResearchCompanyAsync(
            application.TargetCompany, application.TargetRole, application.JobPostingUrl, application.RawJobDescription, cancellationToken);

        await SaveCompanyResearchAsync(application.Id, researchDto, cancellationToken);
        return researchDto;
    }

    public async Task<TailoredResumeDto> TailorResumeAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationEntityAsync(userId, applicationId, cancellationToken);
        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var tailoredDto = await _aiService.TailorResumeAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

        await SaveTailoredResumeAsync(application.Id, tailoredDto, cancellationToken);
        return tailoredDto;
    }

    public async Task<(byte[] Bytes, string FileName)?> DownloadTailoredResumePdfAsync(
        Guid userId, 
        Guid applicationId, 
        CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationEntityAsync(userId, applicationId, cancellationToken);

        var tailored = await _context.TailoredResumes
            .FirstOrDefaultAsync(t => t.TargetJobApplicationId == application.Id, cancellationToken);

        if (tailored == null || string.IsNullOrWhiteSpace(tailored.TailoredMarkdown))
        {
            var generatedDto = await TailorResumeAsync(userId, applicationId, cancellationToken);
            tailored = await _context.TailoredResumes
                .FirstOrDefaultAsync(t => t.TargetJobApplicationId == application.Id, cancellationToken);

            if (tailored == null) return null;
        }

        var candidateProfile = await _context.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == application.CandidateProfileId, cancellationToken);

        var candidateName = candidateProfile != null ? $"{candidateProfile.FirstName} {candidateProfile.LastName}".Trim() : "Candidate";

        var pdfBytes = await _pdfGeneratorService.GenerateResumePdfAsync(
            candidateName, application.TargetRole, application.TargetCompany, tailored.TailoredMarkdown, cancellationToken);

        var safeCompany = string.Concat(application.TargetCompany.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var fileName = $"{candidateName.Replace(" ", "_")}_Resume_{safeCompany}.pdf";

        return (pdfBytes, fileName);
    }

    public async Task<InterviewPreparationDto> PrepareInterviewAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationEntityAsync(userId, applicationId, cancellationToken);
        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var prepDto = await _aiService.PrepareInterviewAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

        await SaveInterviewPrepAsync(application.Id, prepDto, cancellationToken);
        return prepDto;
    }

    public async Task<TargetJobApplicationDetailDto> GenerateAllArtifactsAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        await AnalyzeJobMatchAsync(userId, applicationId, cancellationToken);
        await ResearchCompanyAsync(userId, applicationId, cancellationToken);
        await TailorResumeAsync(userId, applicationId, cancellationToken);
        await PrepareInterviewAsync(userId, applicationId, cancellationToken);

        var result = await GetApplicationByIdAsync(userId, applicationId, cancellationToken);
        return result ?? throw new KeyNotFoundException("Target job application not found after generation.");
    }

    // --- Artifact Persistence Helpers ---

    private async Task SaveMatchAnalysisAsync(Guid applicationId, JobMatchAnalysisDto analysisDto, CancellationToken cancellationToken)
    {
        var existing = await _context.JobMatchAnalyses
            .FirstOrDefaultAsync(m => m.TargetJobApplicationId == applicationId, cancellationToken);

        if (existing == null)
        {
            existing = new JobMatchAnalysis { TargetJobApplicationId = applicationId };
            _context.JobMatchAnalyses.Add(existing);
        }

        existing.MatchScore = analysisDto.MatchScore;
        existing.MatchingStrengths = JsonSerializer.Serialize(new
        {
            strengths = analysisDto.StrengthsAnalysis,
            recommendations = analysisDto.StrategicRecommendations
        }, JsonOptions);
        existing.SkillGaps = JsonSerializer.Serialize(analysisDto.SkillGaps, JsonOptions);
        existing.OverallAssessment = analysisDto.MatchVerdict;
        existing.AnalyzedAtUtc = analysisDto.AnalyzedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveCompanyResearchAsync(Guid applicationId, CompanyRoleResearchDto researchDto, CancellationToken cancellationToken)
    {
        var existing = await _context.CompanyRoleResearches
            .FirstOrDefaultAsync(r => r.TargetJobApplicationId == applicationId, cancellationToken);

        if (existing == null)
        {
            existing = new CompanyRoleResearch { TargetJobApplicationId = applicationId };
            _context.CompanyRoleResearches.Add(existing);
        }

        existing.CompanyOverview = JsonSerializer.Serialize(researchDto.CompanyOverview, JsonOptions);
        existing.TechStackInsights = JsonSerializer.Serialize(researchDto.TechStackIntelligence, JsonOptions);
        existing.CultureAndValues = JsonSerializer.Serialize(researchDto.CultureAndValues, JsonOptions);
        existing.TypicalInterviewProcess = JsonSerializer.Serialize(new
        {
            rounds = researchDto.InterviewProcessInsights?.StandardRounds,
            killer_questions = researchDto.KillerQuestionsToAskThem
        }, JsonOptions);
        existing.ResearchedAtUtc = researchDto.ResearchedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveTailoredResumeAsync(Guid applicationId, TailoredResumeDto tailoredDto, CancellationToken cancellationToken)
    {
        var existing = await _context.TailoredResumes
            .FirstOrDefaultAsync(t => t.TargetJobApplicationId == applicationId, cancellationToken);

        if (existing == null)
        {
            existing = new TailoredResume { TargetJobApplicationId = applicationId };
            _context.TailoredResumes.Add(existing);
        }

        existing.TailoredMarkdown = tailoredDto.FullMarkdownContent;
        existing.ChangesExplanation = JsonSerializer.Serialize(new
        {
            headline = tailoredDto.TailoredHeadline,
            summary = tailoredDto.TailoredSummary,
            optimized_skills = tailoredDto.OptimizedSkills,
            bullets = tailoredDto.TailoredExperienceBulletPoints
        }, JsonOptions);
        existing.GeneratedPdfPath = tailoredDto.GeneratedPdfUrl;
        existing.GeneratedAtUtc = tailoredDto.GeneratedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveInterviewPrepAsync(Guid applicationId, InterviewPreparationDto prepDto, CancellationToken cancellationToken)
    {
        var existing = await _context.InterviewPreparations
            .FirstOrDefaultAsync(p => p.TargetJobApplicationId == applicationId, cancellationToken);

        if (existing == null)
        {
            existing = new InterviewPreparation { TargetJobApplicationId = applicationId };
            _context.InterviewPreparations.Add(existing);
        }

        existing.ExpectedQuestions = JsonSerializer.Serialize(prepDto.InterviewRoundsPrep, JsonOptions);
        existing.InterviewBattlecard = JsonSerializer.Serialize(prepDto.DayOfBattlecard, JsonOptions);
        existing.StudyPlan = JsonSerializer.Serialize(prepDto.TimeBoxedStudyPlan, JsonOptions);
        existing.StarStories = prepDto.ElevatorPitch60Seconds;
        existing.PreparedAtUtc = prepDto.PreparedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<TargetJobApplication> GetApplicationEntityAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateCandidateProfileAsync(userId, cancellationToken);

        var app = await _context.TargetJobApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateProfileId == profile.Id, cancellationToken);

        if (app == null)
        {
            throw new KeyNotFoundException("Target job application not found.");
        }

        return app;
    }

    private async Task<string> GetCvTextAsync(Guid? cvId, Guid profileId, CancellationToken cancellationToken)
    {
        if (cvId.HasValue)
        {
            var cv = await _context.CandidateResumes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cvId.Value && c.CandidateProfileId == profileId, cancellationToken);
            if (cv != null && !string.IsNullOrWhiteSpace(cv.RawTextContent))
                return cv.RawTextContent;
        }

        var primaryCv = await _context.CandidateResumes
            .AsNoTracking()
            .Where(c => c.CandidateProfileId == profileId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(primaryCv?.RawTextContent))
        {
            return primaryCv.RawTextContent;
        }

        throw new InvalidOperationException("Upload a CV with readable text content first before running AI insights.");
    }

    private async Task<CandidateProfile> GetOrCreateCandidateProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (profile != null) return profile;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Authenticated user does not exist in the database.");
        }

        profile = new CandidateProfile
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber
        };

        _context.CandidateProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private static TargetJobApplicationDetailDto MapToDetailDto(TargetJobApplication app)
    {
        JobMatchAnalysisDto? matchDto = null;
        if (app.MatchAnalysis != null)
        {
            matchDto = new JobMatchAnalysisDto
            {
                MatchScore = app.MatchAnalysis.MatchScore,
                MatchVerdict = app.MatchAnalysis.OverallAssessment ?? string.Empty,
                AnalyzedAtUtc = app.MatchAnalysis.AnalyzedAtUtc
            };

            if (!string.IsNullOrWhiteSpace(app.MatchAnalysis.MatchingStrengths))
            {
                try
                {
                    if (app.MatchAnalysis.MatchingStrengths.TrimStart().StartsWith('{'))
                    {
                        using var doc = JsonDocument.Parse(app.MatchAnalysis.MatchingStrengths);
                        if (doc.RootElement.TryGetProperty("strengths", out var sProp))
                            matchDto.StrengthsAnalysis = JsonSerializer.Deserialize<List<StrengthItemDto>>(sProp.GetRawText(), JsonOptions) ?? new();
                        if (doc.RootElement.TryGetProperty("recommendations", out var rProp))
                            matchDto.StrategicRecommendations = JsonSerializer.Deserialize<List<string>>(rProp.GetRawText(), JsonOptions) ?? new();
                    }
                    else
                    {
                        matchDto.StrengthsAnalysis = JsonSerializer.Deserialize<List<StrengthItemDto>>(app.MatchAnalysis.MatchingStrengths, JsonOptions) ?? new();
                    }
                }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.MatchAnalysis.SkillGaps))
            {
                try { matchDto.SkillGaps = JsonSerializer.Deserialize<List<SkillGapItemDto>>(app.MatchAnalysis.SkillGaps, JsonOptions) ?? new(); } catch { }
            }
        }

        CompanyRoleResearchDto? reconDto = null;
        if (app.CompanyResearch != null)
        {
            reconDto = new CompanyRoleResearchDto
            {
                ResearchedAtUtc = app.CompanyResearch.ResearchedAtUtc
            };

            if (!string.IsNullOrWhiteSpace(app.CompanyResearch.CompanyOverview))
            {
                try { reconDto.CompanyOverview = JsonSerializer.Deserialize<CompanyOverviewDto>(app.CompanyResearch.CompanyOverview, JsonOptions); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.CompanyResearch.TechStackInsights))
            {
                try { reconDto.TechStackIntelligence = JsonSerializer.Deserialize<TechStackIntelligenceDto>(app.CompanyResearch.TechStackInsights, JsonOptions); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.CompanyResearch.CultureAndValues))
            {
                try { reconDto.CultureAndValues = JsonSerializer.Deserialize<CultureAndValuesDto>(app.CompanyResearch.CultureAndValues, JsonOptions); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.CompanyResearch.TypicalInterviewProcess))
            {
                try
                {
                    if (app.CompanyResearch.TypicalInterviewProcess.TrimStart().StartsWith('{'))
                    {
                        using var doc = JsonDocument.Parse(app.CompanyResearch.TypicalInterviewProcess);
                        if (doc.RootElement.TryGetProperty("rounds", out var roundsProp))
                        {
                            var rounds = JsonSerializer.Deserialize<List<InterviewRoundDto>>(roundsProp.GetRawText(), JsonOptions) ?? new();
                            reconDto.InterviewProcessInsights = new InterviewProcessInsightsDto { StandardRounds = rounds };
                        }
                        if (doc.RootElement.TryGetProperty("killer_questions", out var questionsProp))
                        {
                            reconDto.KillerQuestionsToAskThem = JsonSerializer.Deserialize<List<string>>(questionsProp.GetRawText(), JsonOptions) ?? new();
                        }
                    }
                    else
                    {
                        reconDto.InterviewProcessInsights = JsonSerializer.Deserialize<InterviewProcessInsightsDto>(app.CompanyResearch.TypicalInterviewProcess, JsonOptions);
                    }
                }
                catch { }
            }
        }

        TailoredResumeDto? tailoredDto = null;
        if (app.TailoredResume != null)
        {
            tailoredDto = new TailoredResumeDto
            {
                FullMarkdownContent = app.TailoredResume.TailoredMarkdown,
                GeneratedPdfUrl = app.TailoredResume.GeneratedPdfPath,
                GeneratedAtUtc = app.TailoredResume.GeneratedAtUtc
            };

            if (!string.IsNullOrWhiteSpace(app.TailoredResume.ChangesExplanation))
            {
                if (app.TailoredResume.ChangesExplanation.TrimStart().StartsWith('{'))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(app.TailoredResume.ChangesExplanation);
                        if (doc.RootElement.TryGetProperty("headline", out var hProp)) tailoredDto.TailoredHeadline = hProp.GetString() ?? string.Empty;
                        if (doc.RootElement.TryGetProperty("summary", out var sProp)) tailoredDto.TailoredSummary = sProp.GetString() ?? string.Empty;
                        if (doc.RootElement.TryGetProperty("optimized_skills", out var oProp)) tailoredDto.OptimizedSkills = JsonSerializer.Deserialize<OptimizedSkillsDto>(oProp.GetRawText(), JsonOptions);
                        if (doc.RootElement.TryGetProperty("bullets", out var bProp)) tailoredDto.TailoredExperienceBulletPoints = JsonSerializer.Deserialize<List<TailoredExperienceBlockDto>>(bProp.GetRawText(), JsonOptions) ?? new();
                    }
                    catch
                    {
                        tailoredDto.TailoredSummary = app.TailoredResume.ChangesExplanation;
                    }
                }
                else
                {
                    tailoredDto.TailoredSummary = app.TailoredResume.ChangesExplanation;
                }
            }
        }

        InterviewPreparationDto? prepDto = null;
        if (app.InterviewPrep != null)
        {
            prepDto = new InterviewPreparationDto
            {
                ElevatorPitch60Seconds = app.InterviewPrep.StarStories ?? string.Empty,
                PreparedAtUtc = app.InterviewPrep.PreparedAtUtc
            };

            if (!string.IsNullOrWhiteSpace(app.InterviewPrep.ExpectedQuestions))
            {
                try { prepDto.InterviewRoundsPrep = JsonSerializer.Deserialize<List<InterviewRoundPrepDto>>(app.InterviewPrep.ExpectedQuestions, JsonOptions) ?? new(); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.InterviewPrep.InterviewBattlecard))
            {
                try { prepDto.DayOfBattlecard = JsonSerializer.Deserialize<DayOfBattlecardDto>(app.InterviewPrep.InterviewBattlecard, JsonOptions); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(app.InterviewPrep.StudyPlan))
            {
                try { prepDto.TimeBoxedStudyPlan = JsonSerializer.Deserialize<List<StudyPlanDayDto>>(app.InterviewPrep.StudyPlan, JsonOptions) ?? new(); } catch { }
            }
        }

        return new TargetJobApplicationDetailDto
        {
            Id = app.Id,
            TargetCompany = app.TargetCompany,
            TargetRole = app.TargetRole,
            JdInputType = app.JdInputType,
            JobPostingUrl = app.JobPostingUrl,
            RawJobDescription = app.RawJobDescription,
            SourceCvId = app.SourceCvId,
            CreatedAtUtc = app.CreatedAtUtc,
            HasMatchAnalysis = app.MatchAnalysis != null,
            MatchScore = app.MatchAnalysis?.MatchScore,
            HasCompanyResearch = app.CompanyResearch != null,
            HasTailoredResume = app.TailoredResume != null,
            HasInterviewPrep = app.InterviewPrep != null,
            MatchAnalysis = matchDto,
            CompanyResearch = reconDto,
            TailoredResume = tailoredDto,
            InterviewPrep = prepDto
        };
    }
}
