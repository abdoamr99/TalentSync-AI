using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Application.Features.TargetJobs.DTOs;

namespace TalentSyncAI.Infrastructure.Services;

public class CandidateAiService : ICandidateAiService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CandidateAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public CandidateAiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CandidateAiService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JobMatchAnalysisDto> AnalyzeJobMatchAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing Job Match for role: {JobTitle} at {Company}", jobTitle, companyName);

        var enableLiveAi = _configuration.GetValue<bool>("AiService:EnableLiveAi");
        var baseUrl = _configuration.GetValue<string>("AiService:BaseUrl") ?? "http://localhost:8000";

        if (enableLiveAi)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("AiService:TimeoutSeconds", 60));

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/api/ai/match", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var liveResult = await response.Content.ReadFromJsonAsync<JobMatchAnalysisDto>(JsonOptions, cancellationToken);
                    if (liveResult != null) return liveResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live AI match request to {BaseUrl} failed. Falling back to local mock engine.", baseUrl);
            }
        }

        // Realistic Fallback/Mock Data
        return new JobMatchAnalysisDto
        {
            MatchScore = 86.5m,
            MatchVerdict = $"Strong Fit for {jobTitle} at {companyName}",
            StrengthsAnalysis = new List<StrengthItemDto>
            {
                new()
                {
                    SkillOrRequirement = $"{jobTitle} Core Competency",
                    EvidenceFromCv = "Demonstrated deep hands-on background and architectural proficiency directly aligned with the target role.",
                    Importance = "High"
                },
                new()
                {
                    SkillOrRequirement = "Clean Architecture & Scalable Services",
                    EvidenceFromCv = "Solid track record of designing distributed, high-performance web systems and reliable data persistence.",
                    Importance = "High"
                },
                new()
                {
                    SkillOrRequirement = "Team Collaboration & Delivery",
                    EvidenceFromCv = "Proven experience delivering production features and collaborating effectively in agile squads.",
                    Importance = "Medium"
                }
            },
            SkillGaps = new List<SkillGapItemDto>
            {
                new()
                {
                    MissingSkill = "Domain-Specific Cloud Tooling / Event Streaming",
                    Severity = "Medium",
                    ImpactDescription = $"The job description for {jobTitle} emphasizes specialized cloud orchestrations and streaming architectures.",
                    RemediationAdvice = "Highlight adjacent asynchronous processing experience and demonstrate familiarity with cloud-native primitives during technical discussions."
                }
            },
            StrategicRecommendations = new List<string>
            {
                $"Tailor your career summary to emphasize high-impact metrics relevant to {companyName}'s current stage.",
                "Highlight quantifiable performance improvements (e.g. latency, throughput, reliability) in your recent experience bullets."
            },
            AnalyzedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<CompanyRoleResearchDto> ResearchCompanyAsync(
        string companyName,
        string jobTitle,
        string? jobPostingUrl,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Conducting Hiring Recon on {Company} for role {JobTitle}", companyName, jobTitle);

        var enableLiveAi = _configuration.GetValue<bool>("AiService:EnableLiveAi");
        var baseUrl = _configuration.GetValue<string>("AiService:BaseUrl") ?? "http://localhost:8000";

        if (enableLiveAi)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("AiService:TimeoutSeconds", 60));

                var payload = new
                {
                    target_company = companyName,
                    target_role = jobTitle,
                    job_posting_url = jobPostingUrl,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/api/ai/recon", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var liveResult = await response.Content.ReadFromJsonAsync<CompanyRoleResearchDto>(JsonOptions, cancellationToken);
                    if (liveResult != null) return liveResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live AI company recon request to {BaseUrl} failed. Falling back to local mock engine.", baseUrl);
            }
        }

        return new CompanyRoleResearchDto
        {
            CompanyOverview = new CompanyOverviewDto
            {
                Industry = "Technology / Intelligent Software Platforms",
                Mission = $"Empowering modern organizations through innovative technology and customer-first solutions at {companyName}.",
                RecentNewsOrMilestones = "Expanding engineering footprint and scaling core distributed platform services."
            },
            TechStackIntelligence = new TechStackIntelligenceDto
            {
                PrimaryLanguages = new List<string> { "C# / .NET", "TypeScript", "Python", "SQL" },
                CloudInfrastructure = "Modern Cloud Platforms (Docker, Kubernetes, CI/CD Pipelines)",
                Databases = new List<string> { "Relational SQL (SQL Server / PostgreSQL)", "Redis Caching", "Vector Storage" }
            },
            CultureAndValues = new CultureAndValuesDto
            {
                CoreValues = new List<string> { "High Ownership", "Engineering Excellence", "Continuous Learning", "Transparency" },
                WorkEnvironment = "Collaborative, agile engineering teams with emphasis on clean code, automated testing, and rapid feedback loops."
            },
            InterviewProcessInsights = new InterviewProcessInsightsDto
            {
                StandardRounds = new List<InterviewRoundDto>
                {
                    new() { RoundIndex = 1, Name = "Initial Screen", Focus = "Background alignment, career goals, and communication skills" },
                    new() { RoundIndex = 2, Name = "Technical Deep-Dive", Focus = $"Core engineering concepts, architecture, and practical scenarios for {jobTitle}" },
                    new() { RoundIndex = 3, Name = "Behavioral & Cultural Fit", Focus = "Teamwork, handling ambiguity, and values alignment" }
                }
            },
            KillerQuestionsToAskThem = new List<string>
            {
                $"What are the most challenging technical initiatives your team at {companyName} is tackling this quarter?",
                "How does the engineering department balance rapid feature shipping with system reliability and technical debt?",
                "What does success look like in the first 90 days for someone stepping into this role?"
            },
            ResearchedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<TailoredResumeDto> TailorResumeAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating Tailored Resume for {JobTitle} at {Company}", jobTitle, companyName);

        var enableLiveAi = _configuration.GetValue<bool>("AiService:EnableLiveAi");
        var baseUrl = _configuration.GetValue<string>("AiService:BaseUrl") ?? "http://localhost:8000";

        if (enableLiveAi)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("AiService:TimeoutSeconds", 60));

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/api/ai/tailor", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var liveResult = await response.Content.ReadFromJsonAsync<TailoredResumeDto>(JsonOptions, cancellationToken);
                    if (liveResult != null) return liveResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live AI resume tailoring request to {BaseUrl} failed. Falling back to local mock engine.", baseUrl);
            }
        }

        return new TailoredResumeDto
        {
            TailoredHeadline = $"{jobTitle} | High-Impact Software Engineering Specialist",
            TailoredSummary = $"Accomplished professional with a proven track record of architecting resilient, scalable systems and driving product excellence. Passionate about bringing targeted expertise in modern engineering practices to drive immediate value for {companyName}.",
            OptimizedSkills = new OptimizedSkillsDto
            {
                TopMatchingSkills = new List<string> { "Modern Backend Engineering", "Clean Architecture", "RESTful APIs", "Relational Databases", "Performance Optimization" },
                SecondarySkills = new List<string> { "Docker & Containerization", "CI/CD Workflows", "Unit & Integration Testing", "Agile Methodologies" }
            },
            TailoredExperienceBulletPoints = new List<TailoredExperienceBlockDto>
            {
                new()
                {
                    Company = "Previous Enterprise Experience",
                    JobTitle = jobTitle,
                    OptimizedBullets = new List<string>
                    {
                        $"Architected and shipped mission-critical service endpoints, improving overall response times by 35%.",
                        $"Enforced Clean Architecture and robust unit testing standards, elevating code test coverage and system stability.",
                        $"Partnered cross-functionally with product managers and engineers to deliver features aligned with business KPIs."
                    }
                }
            },
            FullMarkdownContent = $@"# Candidate Resume
**Target Role:** {jobTitle} at {companyName}

## Professional Summary
Accomplished professional with deep expertise in designing resilient software architectures and scaling distributed systems.

## Core Technical Competencies
- **Primary:** Modern Software Architecture, REST APIs, Database Optimization, Distributed Systems
- **Tools & Practices:** Docker, Git, CI/CD, Agile / Scrum

## Professional Experience
### {jobTitle}
- Spearheaded delivery of high-throughput backend services and data workflows.
- Improved database indexing and query patterns to reduce query execution latency by over 30%.
- Mentored junior engineers and advocated for clean code design patterns.",
            GeneratedPdfUrl = null,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<InterviewPreparationDto> PrepareInterviewAsync(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating Interview Prep for {JobTitle} at {Company}", jobTitle, companyName);

        var enableLiveAi = _configuration.GetValue<bool>("AiService:EnableLiveAi");
        var baseUrl = _configuration.GetValue<string>("AiService:BaseUrl") ?? "http://localhost:8000";

        if (enableLiveAi)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("AiService:TimeoutSeconds", 60));

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/api/ai/prep", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var liveResult = await response.Content.ReadFromJsonAsync<InterviewPreparationDto>(JsonOptions, cancellationToken);
                    if (liveResult != null) return liveResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live AI interview prep request to {BaseUrl} failed. Falling back to local mock engine.", baseUrl);
            }
        }

        return new InterviewPreparationDto
        {
            ElevatorPitch60Seconds = $"I am an experienced engineer focused on designing scalable, maintainable architectures. Over my career, I have specialized in building robust backend services and solving high-throughput performance bottlenecks. I am particularly excited about {companyName} because of your focus on engineering excellence and high-impact technology, and I am eager to contribute directly to the {jobTitle} initiatives.",
            InterviewRoundsPrep = new List<InterviewRoundPrepDto>
            {
                new()
                {
                    RoundName = "Technical System Architecture",
                    KeyFocusAreas = new List<string> { "System Design", "Database Indexing & Caching", "API Design Best Practices" },
                    LikelyQuestions = new List<string>
                    {
                        $"How would you design a scalable service to handle high concurrency requests for {companyName}?",
                        "How do you ensure data consistency and handle transient failures across services?",
                        "Can you explain a challenging performance optimization you diagnosed and resolved?"
                    },
                    StarStories = new List<StarStoryDto>
                    {
                        new()
                        {
                            StoryTitle = "Resolving Production Ingestion Bottlenecks",
                            TargetSkill = "Performance Optimization & Problem Solving",
                            Situation = "Our data ingestion endpoint experienced memory spikes and intermittent timeouts during peak load periods.",
                            Task = "Identify the bottleneck and redesign the ingestion pipeline without breaking client contracts.",
                            Action = "Profiled memory allocations, transitioned from buffering whole payloads in memory to stream-based processing with streaming hashes, and introduced query optimizations.",
                            Result = "Reduced service memory footprint by 55% and eliminated all peak-load timeout incidents."
                        }
                    }
                }
            },
            DayOfBattlecard = new DayOfBattlecardDto
            {
                KeyStrengthsToEmphasize = new List<string>
                {
                    $"Deep mastery of modern engineering principles directly applicable to {jobTitle}.",
                    "Strong background in schema design, caching strategies, and performance tuning.",
                    "Collaborative, pragmatic approach to system design and team delivery."
                },
                PotentialRedFlagsAndCounters = new List<RedFlagCounterDto>
                {
                    new()
                    {
                        AnticipatedDoubt = "Familiarity with niche proprietary tooling",
                        CounterResponse = "Emphasize rapid learning agility, strong conceptual foundations, and demonstrable history of adopting new frameworks quickly."
                    }
                },
                QuestionsToAskInterviewers = new List<string>
                {
                    $"What is the most critical technical milestone the {jobTitle} role will be measured on in the first six months?",
                    "How does your team foster technical innovation while maintaining continuous delivery stability?"
                }
            },
            TimeBoxedStudyPlan = new List<StudyPlanDayDto>
            {
                new() { Day = 1, Task = $"Review core architectural patterns, concurrency mechanisms, and data access principles for {jobTitle}." },
                new() { Day = 2, Task = $"Rehearse the 60-second pitch and practice the STAR stories out loud." },
                new() { Day = 3, Task = $"Review {companyName}'s product offerings and finalize the high-impact questions to ask the interviewers." }
            },
            PreparedAtUtc = DateTime.UtcNow
        };
    }
}
