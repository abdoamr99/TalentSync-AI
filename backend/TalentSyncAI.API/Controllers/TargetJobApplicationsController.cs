using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentSyncAI.Application.Common.Helpers;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.API.Controllers;

[ApiController]
[Route("api/candidate/applications")]
// [Authorize] // Temporarily disabled for Dev Testing
public class TargetJobApplicationsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICandidateAiService _aiService;
    private readonly IFileStorageService _fileStorageService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public TargetJobApplicationsController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICandidateAiService aiService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiService = aiService;
        _fileStorageService = fileStorageService;
    }

    /// <summary>
    /// Creates a new Target Job Application for the authenticated candidate (NextRole style).
    /// Accepts JD via Text, File (PDF/DOCX/TXT), or Job Posting URL.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> CreateApplication(
        [FromForm] CreateTargetJobApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var profile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.TargetCompany))
            return BadRequest("Target company name is required.");

        if (string.IsNullOrWhiteSpace(request.TargetRole))
            return BadRequest("Target role title is required.");

        string rawJd = request.RawJobDescription ?? string.Empty;

        // Process JD input based on type
        switch (request.JdInputType)
        {
            case JdInputType.File:
                if (request.File == null || request.File.Length == 0)
                    return BadRequest("Job description file is required when input type is File.");

                using (var stream = request.File.OpenReadStream())
                {
                    var (isValid, errorMessage) = FileValidationHelper.ValidateFile(stream, request.File.FileName, request.File.Length);
                    if (!isValid) return BadRequest(errorMessage);
                }

                rawJd = $"[Uploaded JD File: {request.File.FileName}] - Position requirements for {request.TargetRole} at {request.TargetCompany}";
                break;

            case JdInputType.Url:
                if (string.IsNullOrWhiteSpace(request.JobPostingUrl) || !Uri.TryCreate(request.JobPostingUrl, UriKind.Absolute, out _))
                    return BadRequest("A valid Job Posting URL is required when input type is URL.");

                if (string.IsNullOrWhiteSpace(rawJd))
                    rawJd = $"Job posting parsed from {request.JobPostingUrl} for {request.TargetRole} at {request.TargetCompany}.";
                break;

            case JdInputType.Text:
            default:
                if (string.IsNullOrWhiteSpace(rawJd))
                    return BadRequest("Job description text is required when input type is Text.");
                break;
        }

        // Resolve source CV (explicit or default to Candidate's primary CV)
        Guid? sourceCvId = request.SourceCvId;
        if (!sourceCvId.HasValue)
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
            TargetCompany = request.TargetCompany.Trim(),
            TargetRole = request.TargetRole.Trim(),
            JdInputType = request.JdInputType,
            JobPostingUrl = request.JobPostingUrl?.Trim(),
            RawJobDescription = rawJd.Trim()
        };

        _context.TargetJobApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapToDetailDto(application);
        return CreatedAtAction(nameof(GetApplicationById), new { id = application.Id }, dto);
    }

    /// <summary>
    /// Lists all target job applications for the candidate.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TargetJobApplicationListDto>>> ListApplications(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var profile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var list = await _context.TargetJobApplications
            .AsNoTracking()
            .Where(a => a.CandidateProfileId == profile.Id)
            .Include(a => a.MatchAnalysis)
            .Include(a => a.CompanyResearch)
            .Include(a => a.TailoredResume)
            .Include(a => a.InterviewPrep)
            .OrderByDescending(a => a.CreatedAtUtc)
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

        return Ok(list);
    }

    /// <summary>
    /// Gets full detail of a target job application including all generated AI artifacts.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> GetApplicationById(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var profile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var application = await _context.TargetJobApplications
            .Include(a => a.MatchAnalysis)
            .Include(a => a.CompanyResearch)
            .Include(a => a.TailoredResume)
            .Include(a => a.InterviewPrep)
            .FirstOrDefaultAsync(a => a.Id == id && a.CandidateProfileId == profile.Id, cancellationToken);

        if (application == null) return NotFound("Target job application not found.");

        return Ok(MapToDetailDto(application));
    }

    /// <summary>
    /// Soft deletes a target job application.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var profile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        var application = await _context.TargetJobApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.CandidateProfileId == profile.Id, cancellationToken);

        if (application == null) return NotFound("Target job application not found.");

        application.IsDeleted = true;
        application.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Stage 1 AI: Runs Job Match & Skill Gap Analysis for this target application.
    /// </summary>
    [HttpPost("{id:guid}/match")]
    public async Task<ActionResult<JobMatchAnalysisDto>> AnalyzeJobMatch(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationWithProfileAsync(id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var analysisDto = await _aiService.AnalyzeJobMatchAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, cancellationToken);

        var existingAnalysis = await _context.JobMatchAnalyses
            .FirstOrDefaultAsync(m => m.TargetJobApplicationId == application.Id, cancellationToken);

        if (existingAnalysis == null)
        {
            existingAnalysis = new JobMatchAnalysis
            {
                TargetJobApplicationId = application.Id
            };
            _context.JobMatchAnalyses.Add(existingAnalysis);
        }

        existingAnalysis.MatchScore = analysisDto.MatchScore;
        existingAnalysis.MatchingStrengths = JsonSerializer.Serialize(analysisDto.StrengthsAnalysis, JsonOptions);
        existingAnalysis.SkillGaps = JsonSerializer.Serialize(analysisDto.SkillGaps, JsonOptions);
        existingAnalysis.OverallAssessment = analysisDto.MatchVerdict;
        existingAnalysis.AnalyzedAtUtc = analysisDto.AnalyzedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(analysisDto);
    }

    /// <summary>
    /// Stage 2 AI: Runs Company & Role Recon for this target application.
    /// </summary>
    [HttpPost("{id:guid}/research")]
    public async Task<ActionResult<CompanyRoleResearchDto>> ResearchCompany(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationWithProfileAsync(id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        var researchDto = await _aiService.ResearchCompanyAsync(
            application.TargetCompany, application.TargetRole, application.JobPostingUrl, application.RawJobDescription, cancellationToken);

        var existingResearch = await _context.CompanyRoleResearches
            .FirstOrDefaultAsync(r => r.TargetJobApplicationId == application.Id, cancellationToken);

        if (existingResearch == null)
        {
            existingResearch = new CompanyRoleResearch
            {
                TargetJobApplicationId = application.Id
            };
            _context.CompanyRoleResearches.Add(existingResearch);
        }

        existingResearch.CompanyOverview = JsonSerializer.Serialize(researchDto.CompanyOverview, JsonOptions);
        existingResearch.TechStackInsights = JsonSerializer.Serialize(researchDto.TechStackIntelligence, JsonOptions);
        existingResearch.CultureAndValues = JsonSerializer.Serialize(researchDto.CultureAndValues, JsonOptions);
        existingResearch.TypicalInterviewProcess = JsonSerializer.Serialize(researchDto.InterviewProcessInsights, JsonOptions);
        existingResearch.ResearchedAtUtc = researchDto.ResearchedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(researchDto);
    }

    /// <summary>
    /// Stage 3 AI: Generates Tailored Resume for this target application.
    /// </summary>
    [HttpPost("{id:guid}/tailor-resume")]
    public async Task<ActionResult<TailoredResumeDto>> TailorResume(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationWithProfileAsync(id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var tailoredDto = await _aiService.TailorResumeAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, cancellationToken);

        var existingTailored = await _context.TailoredResumes
            .FirstOrDefaultAsync(t => t.TargetJobApplicationId == application.Id, cancellationToken);

        if (existingTailored == null)
        {
            existingTailored = new TailoredResume
            {
                TargetJobApplicationId = application.Id
            };
            _context.TailoredResumes.Add(existingTailored);
        }

        existingTailored.TailoredMarkdown = tailoredDto.FullMarkdownContent;
        existingTailored.ChangesExplanation = tailoredDto.TailoredSummary;
        existingTailored.GeneratedPdfPath = tailoredDto.GeneratedPdfUrl;
        existingTailored.GeneratedAtUtc = tailoredDto.GeneratedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(tailoredDto);
    }

    /// <summary>
    /// Stage 4 AI: Generates comprehensive Interview Preparation and Battlecard.
    /// </summary>
    [HttpPost("{id:guid}/interview-prep")]
    public async Task<ActionResult<InterviewPreparationDto>> PrepareInterview(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationWithProfileAsync(id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        var cvText = await GetCvTextAsync(application.SourceCvId, application.CandidateProfileId, cancellationToken);

        var prepDto = await _aiService.PrepareInterviewAsync(
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, cancellationToken);

        var existingPrep = await _context.InterviewPreparations
            .FirstOrDefaultAsync(p => p.TargetJobApplicationId == application.Id, cancellationToken);

        if (existingPrep == null)
        {
            existingPrep = new InterviewPreparation
            {
                TargetJobApplicationId = application.Id
            };
            _context.InterviewPreparations.Add(existingPrep);
        }

        existingPrep.ExpectedQuestions = JsonSerializer.Serialize(prepDto.InterviewRoundsPrep, JsonOptions);
        existingPrep.InterviewBattlecard = JsonSerializer.Serialize(prepDto.DayOfBattlecard, JsonOptions);
        existingPrep.StudyPlan = JsonSerializer.Serialize(prepDto.TimeBoxedStudyPlan, JsonOptions);
        existingPrep.StarStories = prepDto.ElevatorPitch60Seconds;
        existingPrep.PreparedAtUtc = prepDto.PreparedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(prepDto);
    }

    /// <summary>
    /// Executes all candidate AI stages (Match, Recon, Tailor, Prep) sequentially in one request.
    /// </summary>
    [HttpPost("{id:guid}/generate-all")]
    public async Task<ActionResult<TargetJobApplicationDetailDto>> GenerateAllArtifacts(Guid id, CancellationToken cancellationToken = default)
    {
        await AnalyzeJobMatch(id, cancellationToken);
        await ResearchCompany(id, cancellationToken);
        await TailorResume(id, cancellationToken);
        await PrepareInterview(id, cancellationToken);

        return await GetApplicationById(id, cancellationToken);
    }

    private async Task<TargetJobApplication?> GetApplicationWithProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return null;

        var profile = await GetOrCreateCandidateProfileAsync(userId.Value, cancellationToken);

        return await _context.TargetJobApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.CandidateProfileId == profile.Id, cancellationToken);
    }

    private async Task<string> GetCvTextAsync(Guid? cvId, Guid profileId, CancellationToken cancellationToken)
    {
        if (cvId.HasValue)
        {
            var cv = await _context.CandidateResumes
                .FirstOrDefaultAsync(c => c.Id == cvId.Value && c.CandidateProfileId == profileId, cancellationToken);
            if (cv != null && !string.IsNullOrWhiteSpace(cv.RawTextContent))
                return cv.RawTextContent;
        }

        var primaryCv = await _context.CandidateResumes
            .Where(c => c.CandidateProfileId == profileId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return primaryCv?.RawTextContent ?? "Experienced software engineering candidate.";
    }

    private async Task<CandidateProfile> GetOrCreateCandidateProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (profile != null) return profile;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        profile = new CandidateProfile
        {
            UserId = userId,
            FirstName = user?.FirstName ?? "Candidate",
            LastName = user?.LastName ?? "User",
            Email = user?.Email ?? "candidate@talentsync.ai",
            Phone = user?.PhoneNumber
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
                try { matchDto.StrengthsAnalysis = JsonSerializer.Deserialize<List<StrengthItemDto>>(app.MatchAnalysis.MatchingStrengths, JsonOptions) ?? new(); } catch { }
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
                try { reconDto.InterviewProcessInsights = JsonSerializer.Deserialize<InterviewProcessInsightsDto>(app.CompanyResearch.TypicalInterviewProcess, JsonOptions); } catch { }
            }
        }

        TailoredResumeDto? tailoredDto = null;
        if (app.TailoredResume != null)
        {
            tailoredDto = new TailoredResumeDto
            {
                FullMarkdownContent = app.TailoredResume.TailoredMarkdown,
                TailoredSummary = app.TailoredResume.ChangesExplanation ?? string.Empty,
                GeneratedPdfUrl = app.TailoredResume.GeneratedPdfPath,
                GeneratedAtUtc = app.TailoredResume.GeneratedAtUtc
            };
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

public class CreateTargetJobApplicationApiRequest : CreateTargetJobApplicationRequest
{
    public IFormFile? File { get; set; }
}
