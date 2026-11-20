using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
[Authorize(Roles = "Candidate")]
public class TargetJobApplicationsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICandidateAiService _aiService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IPdfGeneratorService _pdfGeneratorService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public TargetJobApplicationsController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICandidateAiService aiService,
        IFileStorageService fileStorageService,
        IPdfGeneratorService pdfGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiService = aiService;
        _fileStorageService = fileStorageService;
        _pdfGeneratorService = pdfGeneratorService;
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

        // Sanitize optional Swagger defaults
        var jobUrl = request.JobPostingUrl;
        if (jobUrl == "string" || string.IsNullOrWhiteSpace(jobUrl))
        {
            jobUrl = null;
        }

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

                    var extracted = await FileValidationHelper.TryExtractTextAsync(stream, request.File.FileName);
                    if (string.IsNullOrWhiteSpace(extracted))
                    {
                        return BadRequest("Unable to extract readable text content from the uploaded job description file. Please upload a valid text-based PDF or TXT file, or paste the text directly.");
                    }
                    rawJd = extracted;
                }
                break;

            case JdInputType.Url:
                if (string.IsNullOrWhiteSpace(jobUrl) || 
                    !Uri.TryCreate(jobUrl, UriKind.Absolute, out var uriResult) || 
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest("A valid HTTP or HTTPS Job Posting URL is required when input type is URL.");
                }

                if (string.IsNullOrWhiteSpace(rawJd) || rawJd == "string")
                {
                    rawJd = string.Empty;
                }
                break;

            case JdInputType.Text:
            default:
                if (string.IsNullOrWhiteSpace(rawJd) || rawJd.Trim() == "string")
                    return BadRequest("Job description text is required and cannot be empty or default placeholder.");
                break;
        }

        // Resolve source CV (explicit, or fallback to candidate's primary CV)
        Guid? sourceCvId = request.SourceCvId;

        if (sourceCvId.HasValue && sourceCvId.Value != Guid.Empty)
        {
            var cvExists = await _context.CandidateResumes
                .AnyAsync(c => c.Id == sourceCvId.Value && c.CandidateProfileId == profile.Id, cancellationToken);

            if (!cvExists)
            {
                return BadRequest($"The specified SourceCvId '{sourceCvId}' was not found for this candidate profile.");
            }
        }
        else
        {
            // Default to candidate's primary CV, or latest uploaded CV
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
            JobPostingUrl = jobUrl?.Trim(),
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
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

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
        existingAnalysis.MatchingStrengths = JsonSerializer.Serialize(new
        {
            strengths = analysisDto.StrengthsAnalysis,
            recommendations = analysisDto.StrategicRecommendations
        }, JsonOptions);
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
        existingResearch.TypicalInterviewProcess = JsonSerializer.Serialize(new
        {
            rounds = researchDto.InterviewProcessInsights?.StandardRounds,
            killer_questions = researchDto.KillerQuestionsToAskThem
        }, JsonOptions);
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
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

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
        existingTailored.ChangesExplanation = JsonSerializer.Serialize(new
        {
            headline = tailoredDto.TailoredHeadline,
            summary = tailoredDto.TailoredSummary,
            optimized_skills = tailoredDto.OptimizedSkills,
            bullets = tailoredDto.TailoredExperienceBulletPoints
        }, JsonOptions);
        existingTailored.GeneratedPdfPath = tailoredDto.GeneratedPdfUrl;
        existingTailored.GeneratedAtUtc = tailoredDto.GeneratedAtUtc;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(tailoredDto);
    }

    /// <summary>
    /// Downloads the tailored resume compiled as an ATS-friendly, professional PDF document.
    /// </summary>
    [HttpGet("{id:guid}/tailor-resume/download")]
    public async Task<IActionResult> DownloadTailoredResumePdf(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await GetApplicationWithProfileAsync(id, cancellationToken);
        if (application == null) return NotFound("Target job application not found.");

        var tailored = await _context.TailoredResumes
            .FirstOrDefaultAsync(t => t.TargetJobApplicationId == application.Id, cancellationToken);

        if (tailored == null || string.IsNullOrWhiteSpace(tailored.TailoredMarkdown))
        {
            // If not tailored yet, generate it automatically
            var tailorResult = await TailorResume(id, cancellationToken);
            tailored = await _context.TailoredResumes
                .FirstOrDefaultAsync(t => t.TargetJobApplicationId == application.Id, cancellationToken);

            if (tailored == null)
            {
                return BadRequest("Tailored resume has not been generated for this application yet.");
            }
        }

        var candidateProfile = await _context.CandidateProfiles
            .FirstOrDefaultAsync(p => p.Id == application.CandidateProfileId, cancellationToken);

        var candidateName = candidateProfile != null ? $"{candidateProfile.FirstName} {candidateProfile.LastName}".Trim() : "Candidate";

        var pdfBytes = await _pdfGeneratorService.GenerateResumePdfAsync(
            candidateName, application.TargetRole, application.TargetCompany, tailored.TailoredMarkdown, cancellationToken);

        var safeCompany = string.Concat(application.TargetCompany.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var fileName = $"{candidateName.Replace(" ", "_")}_Resume_{safeCompany}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
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
            cvText, application.TargetRole, application.TargetCompany, application.RawJobDescription, application.JobPostingUrl, cancellationToken);

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

public class CreateTargetJobApplicationApiRequest : CreateTargetJobApplicationRequest
{
    public IFormFile? File { get; set; }
}
