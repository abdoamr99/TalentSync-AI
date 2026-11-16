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

    private HttpClient CreateAiHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = _configuration.GetValue<string>("AiService:BaseUrl") ?? "http://localhost:8000";
        var apiKey = _configuration.GetValue<string>("AiService:InternalApiKey") ?? "dev-internal-api-key";
        var timeoutSeconds = _configuration.GetValue<int>("AiService:TimeoutSeconds", 120);

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
        }
        return client;
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
                var client = CreateAiHttpClient();

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                // Try standard v1 candidate endpoint, fallback to legacy if needed
                var response = await client.PostAsJsonAsync("/v1/candidate/match", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    response = await client.PostAsJsonAsync("/api/ai/match", payload, cancellationToken);
                }

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

        // Dynamic Intelligent Analysis Fallback
        return ComputeDynamicMatch(cvText, jobTitle, companyName, jobDescription);
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
                var client = CreateAiHttpClient();

                var payload = new
                {
                    company_name = companyName,
                    target_company = companyName,
                    job_title = jobTitle,
                    target_role = jobTitle,
                    job_url = jobPostingUrl,
                    job_posting_url = jobPostingUrl,
                    extra_context = jobDescription,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/v1/candidate/research", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    response = await client.PostAsJsonAsync("/api/ai/recon", payload, cancellationToken);
                }

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

        return ComputeDynamicCompanyResearch(companyName, jobTitle, jobPostingUrl, jobDescription);
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
                var client = CreateAiHttpClient();

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/v1/candidate/tailor", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    response = await client.PostAsJsonAsync("/api/ai/tailor", payload, cancellationToken);
                }

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

        return ComputeDynamicTailorResume(cvText, jobTitle, companyName, jobDescription);
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
                var client = CreateAiHttpClient();

                var payload = new
                {
                    cv_text = cvText,
                    target_role = jobTitle,
                    target_company = companyName,
                    raw_job_description = jobDescription
                };

                var response = await client.PostAsJsonAsync("/v1/candidate/prep", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    response = await client.PostAsJsonAsync("/api/ai/prep", payload, cancellationToken);
                }

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

        return ComputeDynamicInterviewPrep(cvText, jobTitle, companyName, jobDescription);
    }

    private static readonly string[] CatalogedSkills =
    {
        "C#", ".NET", "ASP.NET", "Entity Framework", "EF Core", "SQL Server", "SQL", "PostgreSQL", "MySQL", "MongoDB",
        "Docker", "Kubernetes", "Clean Architecture", "Microservices", "REST API", "Azure", "AWS", "GCP", "CI/CD", "Git",
        "Redis", "Kafka", "RabbitMQ", "Unit Testing", "xUnit", "NUnit", "TDD", "SOLID", "Design Patterns",
        "React", "Angular", "Vue", "TypeScript", "JavaScript", "Python", "FastAPI", "Django", "Java", "Spring Boot",
        "GraphQL", "Linux", "DevOps", "HTML", "CSS", "Tailwind", "Agile", "Scrum", "Problem Solving", "OOP",
        "Data Structures", "Algorithms", "System Design", "Elasticsearch", "Next.js", "Node.js"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "with", "from", "that", "this", "have", "will", "your", "their", "about", "which",
        "experience", "years", "candidate", "responsibilities", "requirements", "skills",
        "looking", "strong", "knowledge", "working", "preferred", "qualifications", "must"
    };

    private static JobMatchAnalysisDto ComputeDynamicMatch(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription)
    {
        var textToSearch = $"{jobTitle} {jobDescription}".ToLowerInvariant();
        var cvLower = (cvText ?? string.Empty).ToLowerInvariant();

        // 1. Detect required skills in the job specification
        var requiredSkills = CatalogedSkills
            .Where(skill => textToSearch.Contains(skill.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // If few cataloged skills matched, extract notable words (>= 4 chars)
        if (requiredSkills.Count < 3)
        {
            var fallbackKeywords = textToSearch
                .Split(new[] { ' ', '\r', '\n', ',', '.', ';', ':', '-', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4 && !StopWords.Contains(w))
                .Distinct()
                .Take(6);

            foreach (var kw in fallbackKeywords)
            {
                if (!requiredSkills.Any(s => s.Equals(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    requiredSkills.Add(char.ToUpperInvariant(kw[0]) + kw[1..]);
                }
            }
        }

        // 2. Classify into matched strengths vs missing gaps
        var matched = new List<string>();
        var missing = new List<string>();

        foreach (var skill in requiredSkills)
        {
            if (cvLower.Contains(skill.ToLowerInvariant()))
            {
                matched.Add(skill);
            }
            else
            {
                missing.Add(skill);
            }
        }

        // 3. Compute dynamic score
        double rawRatio;
        if (requiredSkills.Count > 0)
        {
            rawRatio = (double)matched.Count / requiredSkills.Count;
        }
        else
        {
            rawRatio = 0.5;
        }

        // Map ratio to realistic percentage (e.g. 0% matched -> 28.5%, 100% matched -> 94.5%)
        var dynamicScore = Math.Round((decimal)(28.0 + (rawRatio * 65.0) + (cvLower.Contains(jobTitle.ToLowerInvariant()) ? 3.5 : 0.0)), 1);
        dynamicScore = Math.Clamp(dynamicScore, 18.0m, 97.5m);

        // 4. Compute Verdict
        string verdict;
        if (dynamicScore >= 85)
            verdict = $"Exceptional Fit for {jobTitle} at {companyName}";
        else if (dynamicScore >= 70)
            verdict = $"Strong Fit for {jobTitle} with Minor Technical Gaps";
        else if (dynamicScore >= 50)
            verdict = $"Moderate Fit for {jobTitle} - Targeted Upskilling Recommended";
        else
            verdict = $"Low Alignment for {jobTitle} - Substantial Gaps in Core Requirements";

        // 5. Build Strengths list
        var strengths = new List<StrengthItemDto>();
        foreach (var s in matched.Take(4))
        {
            strengths.Add(new StrengthItemDto
            {
                SkillOrRequirement = s,
                EvidenceFromCv = $"Verified hands-on proficiency in '{s}' aligned directly with {jobTitle} specifications.",
                Importance = "High"
            });
        }

        if (strengths.Count == 0)
        {
            strengths.Add(new StrengthItemDto
            {
                SkillOrRequirement = "Foundational Engineering Knowledge",
                EvidenceFromCv = "Demonstrated baseline technical competencies and structured software development background.",
                Importance = "Medium"
            });
        }

        // 6. Build Gaps list
        var gaps = new List<SkillGapItemDto>();
        foreach (var s in missing.Take(4))
        {
            gaps.Add(new SkillGapItemDto
            {
                MissingSkill = s,
                Severity = gaps.Count == 0 ? "High" : "Medium",
                ImpactDescription = $"The job description for {jobTitle} at {companyName} explicitly highlights '{s}', which was not prominently identified in the CV.",
                RemediationAdvice = $"Review core concepts of '{s}' and prepare to discuss adjacent or transferrable project experience in technical interviews."
            });
        }

        // 7. Dynamic Strategic Recommendations
        var recommendations = new List<string>();
        if (matched.Count > 0)
        {
            recommendations.Add($"Lead your application and introductory remarks with strong quantifiable achievements in {string.Join(", ", matched.Take(3))}.");
        }
        if (missing.Count > 0)
        {
            recommendations.Add($"Proactively address missing exposure to {string.Join(", ", missing.Take(2))} by demonstrating rapid learning capability and referencing related architectures.");
        }
        recommendations.Add($"Align your interview stories with {companyName}'s engineering culture and high-impact deliverables.");

        return new JobMatchAnalysisDto
        {
            MatchScore = dynamicScore,
            MatchVerdict = verdict,
            StrengthsAnalysis = strengths,
            SkillGaps = gaps,
            StrategicRecommendations = recommendations,
            AnalyzedAtUtc = DateTime.UtcNow
        };
    }

    private static List<string> DetectSkills(string? text)
    {
        var lower = (text ?? string.Empty).ToLowerInvariant();
        var detected = CatalogedSkills
            .Where(skill => lower.Contains(skill.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (detected.Count == 0)
        {
            detected.AddRange(new[] { "C#", ".NET", "SQL", "Clean Architecture", "REST API" });
        }
        return detected;
    }

    private static CompanyRoleResearchDto ComputeDynamicCompanyResearch(
        string companyName,
        string jobTitle,
        string? jobPostingUrl,
        string jobDescription)
    {
        var jdLower = (jobDescription ?? string.Empty).ToLowerInvariant();
        var detectedSkills = DetectSkills(jobDescription);

        // Determine Industry heuristically
        string industry;
        if (jdLower.Contains("fintech") || jdLower.Contains("bank") || jdLower.Contains("payment") || jdLower.Contains("financial"))
            industry = "FinTech & Financial Services";
        else if (jdLower.Contains("health") || jdLower.Contains("medical") || jdLower.Contains("clinic") || jdLower.Contains("patient"))
            industry = "HealthTech & Digital Healthcare";
        else if (jdLower.Contains("e-commerce") || jdLower.Contains("retail") || jdLower.Contains("marketplace") || jdLower.Contains("shop"))
            industry = "E-Commerce & Digital Marketplace";
        else if (jdLower.Contains("ai") || jdLower.Contains("machine learning") || jdLower.Contains("data platform"))
            industry = "Artificial Intelligence & Data Systems";
        else if (jdLower.Contains("cloud") || jdLower.Contains("saas") || jdLower.Contains("enterprise"))
            industry = "Enterprise Cloud & SaaS";
        else
            industry = "Information Technology & Software Services";

        // Determine Cloud
        string cloud;
        if (jdLower.Contains("aws"))
            cloud = "Amazon Web Services (AWS)";
        else if (jdLower.Contains("gcp") || jdLower.Contains("google cloud"))
            cloud = "Google Cloud Platform (GCP)";
        else if (jdLower.Contains("azure"))
            cloud = "Microsoft Azure";
        else
            cloud = "Multi-Cloud / Hybrid Cloud (Azure & AWS)";

        // Determine Databases
        var databases = new List<string>();
        if (jdLower.Contains("sql server") || jdLower.Contains("mssql")) databases.Add("SQL Server");
        if (jdLower.Contains("postgres")) databases.Add("PostgreSQL");
        if (jdLower.Contains("mongo")) databases.Add("MongoDB");
        if (jdLower.Contains("redis")) databases.Add("Redis");
        if (databases.Count == 0) databases.AddRange(new[] { "SQL Server", "Redis Cache" });

        var topTech = detectedSkills.Take(4).ToList();

        return new CompanyRoleResearchDto
        {
            CompanyOverview = new CompanyOverviewDto
            {
                Industry = industry,
                Mission = $"Delivering cutting-edge solutions and superior business value at {companyName} through modern engineering practices and scalable digital architecture.",
                RecentNewsOrMilestones = $"Expanding core engineering capabilities, accelerating adoption of distributed services, automated CI/CD pipelines, and high-performance cloud backends."
            },
            TechStackIntelligence = new TechStackIntelligenceDto
            {
                PrimaryLanguages = topTech,
                CloudInfrastructure = cloud,
                Databases = databases
            },
            CultureAndValues = new CultureAndValuesDto
            {
                CoreValues = new List<string>
                {
                    "Engineering Craftsmanship & Code Quality",
                    "Customer-Centric Innovation",
                    "Ownership, Accountability & Autonomy",
                    "Continuous Learning & Technical Mentorship"
                },
                WorkEnvironment = "Agile, collaborative, and results-focused software engineering culture fostering psychological safety and rapid iteration."
            },
            InterviewProcessInsights = new InterviewProcessInsightsDto
            {
                StandardRounds = new List<InterviewRoundDto>
                {
                    new() { RoundIndex = 1, Name = "Initial Screening", Focus = "Career trajectory, background overview, communication skills, and general role expectations." },
                    new() { RoundIndex = 2, Name = "Technical Deep-Dive", Focus = $"Deep inspection of {string.Join(", ", topTech.Take(2))}, system fundamentals, design patterns, and debugging proficiency." },
                    new() { RoundIndex = 3, Name = "Architecture & Problem Solving", Focus = "Distributed systems design, scalability, database optimization, and real-time coding exercise." },
                    new() { RoundIndex = 4, Name = "Hiring Manager & Culture Fit", Focus = "Behavioral scenarios, leadership principles, conflict management, and long-term organizational fit." }
                }
            },
            KillerQuestionsToAskThem = new List<string>
            {
                $"What are the top architectural priorities and scalability challenges currently facing {companyName}'s engineering team?",
                $"How does the engineering leadership at {companyName} evaluate tangible success for a {jobTitle} within the first 90 days?",
                $"Can you walk me through the team's typical deployment cycle and how you balance technical debt with new product features?",
                $"What opportunities are there for engineers to lead technical initiatives and influence architectural standards?"
            },
            ResearchedAtUtc = DateTime.UtcNow
        };
    }

    private static TailoredResumeDto ComputeDynamicTailorResume(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription)
    {
        var jdSkills = DetectSkills(jobDescription);
        var cvSkills = DetectSkills(cvText);

        var matchingSkills = jdSkills.Intersect(cvSkills, StringComparer.OrdinalIgnoreCase).ToList();
        if (matchingSkills.Count == 0)
        {
            matchingSkills = jdSkills.Take(3).ToList();
        }

        var secondarySkills = jdSkills.Except(matchingSkills, StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        if (secondarySkills.Count == 0)
        {
            secondarySkills = new List<string> { "Git", "REST APIs", "Docker", "Unit Testing" };
        }

        var headline = $"Results-Oriented {jobTitle} | Specializing in {string.Join(", ", matchingSkills.Take(3))}";
        var summary = $"Dynamic, high-performing {jobTitle} with robust experience designing, building, and maintaining scalable enterprise software systems. Proven technical depth in {string.Join(", ", matchingSkills.Take(3))}. Recognized for solving complex architectural bottlenecks, enforcing Clean Architecture standards, and delivering high-impact features aligned with {companyName}'s mission.";

        var bulletPoints = new List<TailoredExperienceBlockDto>
        {
            new()
            {
                Company = companyName,
                JobTitle = jobTitle,
                OptimizedBullets = new List<string>
                {
                    $"Architected and implemented mission-critical services leveraging {string.Join(" and ", matchingSkills.Take(2))}, improving system throughput by 35% and reducing latency.",
                    $"Engineered scalable RESTful APIs and asynchronous background processing pipelines, ensuring 99.9% uptime across production workloads.",
                    $"Refactored core domain modules in accordance with Clean Architecture and SOLID design patterns, boosting automated unit test coverage to 85%.",
                    $"Collaborated with cross-functional product and infrastructure teams to streamline CI/CD delivery, cutting deployment lead times in half."
                }
            }
        };

        var markdownContent = $@"# Tailored Professional Resume

**Target Role:** {jobTitle}  
**Target Organization:** {companyName}  
**Specializations:** {string.Join(" • ", matchingSkills)}  

---

## Professional Summary
{summary}

## Core Competencies & Technical Skills
- **Core Languages & Frameworks:** {string.Join(", ", matchingSkills)}
- **Supporting Technologies:** {string.Join(", ", secondarySkills)}
- **Architecture & Practices:** Clean Architecture, Domain-Driven Design, RESTful Web APIs, Microservices, CI/CD, TDD, Agile/Scrum

## Key Achievements & Impact
- Designed resilient data access workflows and query optimizations, reducing database roundtrips and operational query latency.
- Authored comprehensive unit and integration test suites using industry best practices, minimizing regression bugs.
- Spearheaded modular software components that enabled independent deployment and rapid feature turnaround.

## Relevant Professional Experience
### {jobTitle} — Targeted for {companyName}
- Built scalable backend services using {string.Join(" and ", matchingSkills.Take(2))}, addressing business-critical workflows.
- Led technical design reviews and implemented defensive programming patterns for robust error resilience.
- Championed continuous improvement, clean code standards, and agile sprint delivery.

---
*Tailored dynamically for {companyName} by TalentSyncAI Resume Intelligence.*";

        return new TailoredResumeDto
        {
            TailoredHeadline = headline,
            TailoredSummary = summary,
            OptimizedSkills = new OptimizedSkillsDto
            {
                TopMatchingSkills = matchingSkills,
                SecondarySkills = secondarySkills
            },
            TailoredExperienceBulletPoints = bulletPoints,
            FullMarkdownContent = markdownContent,
            GeneratedPdfUrl = null,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static InterviewPreparationDto ComputeDynamicInterviewPrep(
        string cvText,
        string jobTitle,
        string companyName,
        string jobDescription)
    {
        var jdSkills = DetectSkills(jobDescription);
        var cvSkills = DetectSkills(cvText);
        var topSkills = jdSkills.Intersect(cvSkills, StringComparer.OrdinalIgnoreCase).ToList();
        if (topSkills.Count == 0) topSkills = jdSkills.Take(3).ToList();

        var skill1 = topSkills.ElementAtOrDefault(0) ?? "C# / .NET";
        var skill2 = topSkills.ElementAtOrDefault(1) ?? "SQL Server";
        var skill3 = topSkills.ElementAtOrDefault(2) ?? "Clean Architecture";

        var pitch = $"I am an experienced software engineer specializing in {skill1}, {skill2}, and {skill3}. Over my career, I have focused on architecting resilient, maintainable software systems and optimizing distributed backend architectures. What drew me specifically to {companyName} is your emphasis on technical excellence and high-impact digital solutions. Given my background in end-to-end feature delivery, clean code practices, and scalable data layers, I am excited about hitting the ground running and driving measurable impact as a {jobTitle} on your team.";

        var rounds = new List<InterviewRoundPrepDto>
        {
            new()
            {
                RoundName = "Technical Deep Dive & Architecture",
                KeyFocusAreas = new List<string> { skill1, skill2, "API Scalability", "Clean Architecture" },
                LikelyQuestions = new List<string>
                {
                    $"How do you handle performance bottlenecks and asynchronous operations in {skill1}?",
                    $"Can you explain your approach to database indexing and query optimization when working with {skill2}?",
                    "How do you organize solution boundaries to keep business rules independent from external frameworks?"
                },
                StarStories = new List<StarStoryDto>
                {
                    new()
                    {
                        StoryTitle = $"Optimizing High-Volume Throughput with {skill1}",
                        TargetSkill = skill1,
                        Situation = $"A legacy system experienced high CPU utilization and latency spikes during peak load periods.",
                        Task = $"Refactor the processing pipeline to handle 3x throughput without provisioning additional hardware.",
                        Action = $"Identified synchronous blocking calls, transitioned to asynchronous I/O with optimized connection pooling, and introduced Redis caching for hot queries.",
                        Result = $"Reduced average p99 response times by 62% and eliminated timeouts during traffic surges."
                    }
                }
            },
            new()
            {
                RoundName = "Behavioral & Cross-Functional Collaboration",
                KeyFocusAreas = new List<string> { "Conflict Resolution", "Ownership", "Team Mentorship", "Stakeholder Communication" },
                LikelyQuestions = new List<string>
                {
                    $"Describe a time you had a technical disagreement with a team member. How was it resolved?",
                    $"How do you prioritize sudden critical production bug fixes against ongoing sprint commitments?",
                    $"Why are you interested in joining {companyName} specifically at this stage of your career?"
                },
                StarStories = new List<StarStoryDto>
                {
                    new()
                    {
                        StoryTitle = "Balancing Tech Debt with Roadmap Delivery",
                        TargetSkill = "Agile Ownership & Communication",
                        Situation = $"Critical feature deadlines conflicted with escalating technical debt in our core domain model.",
                        Task = $"Align product management and engineering on a sustainable refactoring roadmap.",
                        Action = $"Quantified the business cost of recurring bugs and negotiated a dedicated 20% allocation per sprint for debt remediation.",
                        Result = $"Successfully delivered all roadmap commitments while reducing defect escape rate by 45% over the subsequent quarter."
                    }
                }
            }
        };

        var battlecard = new DayOfBattlecardDto
        {
            KeyStrengthsToEmphasize = new List<string>
            {
                $"Strong production proficiency in {skill1} and {skill2}",
                "Pragmatic architectural mindset emphasizing Clean Architecture, maintainability, and testing",
                $"Direct alignment with {companyName}'s core values and delivery tempo"
            },
            PotentialRedFlagsAndCounters = new List<RedFlagCounterDto>
            {
                new()
                {
                    AnticipatedDoubt = "Candidate might have more exposure to monolithic architectures than pure microservices.",
                    CounterResponse = "Emphasize practical experience with modular boundaries, asynchronous messaging (RabbitMQ/Kafka), and containerized deployments with Docker."
                }
            },
            QuestionsToAskInterviewers = new List<string>
            {
                $"What does success look like for a {jobTitle} joining {companyName} during the first 6 months?",
                $"How does the engineering team foster knowledge sharing, architecture reviews, and mentoring?"
            }
        };

        var studyPlan = new List<StudyPlanDayDto>
        {
            new() { Day = 1, Task = $"Review {companyName}'s core products, market position, and tech blog publications." },
            new() { Day = 2, Task = $"Deep dive into {skill1} language internals, memory management, and concurrency best practices." },
            new() { Day = 3, Task = $"Practice distributed system design patterns: caching, database sharding, and idempotency." },
            new() { Day = 4, Task = "Rehearse STAR behavioral responses and calibrate your 60-second elevator pitch." },
            new() { Day = 5, Task = "Conduct a full mock interview, review your cheat sheet, and prepare questions for the interviewers." }
        };

        return new InterviewPreparationDto
        {
            ElevatorPitch60Seconds = pitch,
            InterviewRoundsPrep = rounds,
            DayOfBattlecard = battlecard,
            TimeBoxedStudyPlan = studyPlan,
            PreparedAtUtc = DateTime.UtcNow
        };
    }
}
