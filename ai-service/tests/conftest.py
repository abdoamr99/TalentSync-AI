"""Shared sample payloads from `specs/schemas_contract.md` and test fixtures."""

from typing import Any

import pytest

SAMPLE_CANDIDATE_PROFILE_JSON: dict[str, Any] = {
    "personal_info": {
        "full_name": "Abdelrahman Amr",
        "email": "abdelrahman@example.com",
        "phone": "+201000000000",
        "location": "Cairo, Egypt",
        "linkedin_url": "https://linkedin.com/in/abdelrahman",
        "github_url": "https://github.com/abdelrahman",
        "portfolio_url": "https://abdelrahman.dev",
    },
    "professional_summary": (
        "Senior .NET engineer with 5+ years of experience building distributed microservices, "
        "streaming file ingestion pipelines with SHA-256, and high-throughput Web APIs."
    ),
    "skills": {
        "technical_skills": ["C#", ".NET 10", "SQL Server", "Python", "System Design"],
        "tools_and_frameworks": [
            "EF Core",
            "Docker",
            "Redis",
            "RabbitMQ",
            "Clean Architecture",
            "CI/CD",
            "Unit Testing",
        ],
        "soft_skills": ["Technical Leadership", "Cross-functional Collaboration"],
        "languages": [
            {"language": "Arabic", "proficiency": "Native"},
            {"language": "English", "proficiency": "Fluent"},
        ],
    },
    "experience": [
        {
            "company": "TechCorp",
            "job_title": "Backend Developer",
            "location": "Cairo, Egypt",
            "start_date": "2021-03",
            "end_date": None,
            "is_current": True,
            "accomplishments": [
                "Led migration of microservices improving latency by 35% using .NET 10",
                "Architected scalable .NET Web APIs reducing response time by 42% for 200k active users",
                "Implemented secure JWT authentication and multi-tenant row-level security using EF Core 10",
                "Implemented streaming hashes with SHA-256 and decoupled background PDF processing reducing memory footprint by 60%",
            ],
        }
    ],
    "education": [
        {
            "institution": "Cairo University",
            "degree": "Bachelor's",
            "field_of_study": "Computer Science",
            "graduation_year": 2024,
            "gpa": "3.8",
        }
    ],
    "certifications": [
        {
            "name": "Azure Solutions Architect Expert",
            "issuer": "Microsoft",
            "issue_date": "2023-06",
            "credential_url": "https://learn.microsoft.com/credentials/123",
        }
    ],
    "projects": [
        {
            "title": "High-Throughput Ingestion Service",
            "description": "Multi-tenant file upload and async processing pipeline.",
            "technologies": [".NET 10", "C#", "Redis", "RabbitMQ"],
            "project_url": "https://github.com/abdelrahman/ingestion-service",
        }
    ],
}

SAMPLE_TARGET_JOB_JSON: dict[str, Any] = {
    "job_metadata": {
        "job_title": "Senior Backend Engineer",
        "company_name": "TechCorp Global",
        "location": "Cairo, Egypt / Remote",
        "workplace_type": "Remote",
        "employment_type": "Full-Time",
    },
    "role_summary": "Lead the architecture and delivery of distributed microservices and AI inference endpoints.",
    "required_qualifications": {
        "mandatory_skills": ["C#", ".NET 10", "SQL Server", "Docker"],
        "preferred_skills": ["Kubernetes", "Redis", "Kafka", "Elasticsearch"],
        "minimum_years_of_experience": 5,
        "degree_requirement": "Bachelor's degree in CS or equivalent",
    },
    "core_responsibilities": [
        "Design and build resilient, distributed RESTful microservices",
        "Collaborate with AI engineers to deploy high-throughput inference endpoints",
    ],
    "ats_keywords": [
        "Clean Architecture",
        "EF Core",
        "Distributed Tracing",
        "OpenTelemetry",
    ],
}

SAMPLE_JOB_MATCH_JSON: dict[str, Any] = {
    "match_score": 84,
    "match_verdict": "Strong Candidate with Minor Gaps",
    "strengths_analysis": [
        {
            "skill_or_requirement": ".NET Backend Architecture",
            "evidence_from_cv": "5+ years developing robust C# Web APIs using Clean Architecture",
            "importance": "High",
        }
    ],
    "skill_gaps": [
        {
            "missing_skill": "Kafka Event Streaming",
            "severity": "Medium",
            "impact_description": "The JD prioritizes Kafka for event-driven systems; candidate has RabbitMQ experience.",
            "remediation_advice": "Highlight RabbitMQ mastery and review Kafka partitioned topic architectures.",
        }
    ],
    "strategic_recommendations": [
        "Reorder experience bullets to emphasize high-concurrency systems early",
        "Quantify database tuning metrics in past positions",
    ],
}

SAMPLE_COMPANY_RESEARCH_JSON: dict[str, Any] = {
    "company_overview": {
        "industry": "FinTech / SaaS",
        "mission": "Empowering enterprise recruitment through transparent AI",
        "recent_news_or_milestones": "Closed Series B funding; expanded into MENA market",
    },
    "tech_stack_intelligence": {
        "primary_languages": [".NET / C#", "TypeScript", "Python"],
        "cloud_infrastructure": "Azure, Kubernetes, Terraform",
        "databases": ["SQL Server", "Redis", "Qdrant Vector DB"],
    },
    "culture_and_values": {
        "core_values": ["Extreme Ownership", "Customer Obsession", "Velocity"],
        "work_environment": "Fast-paced startup culture with focus on autonomous squads",
    },
    "interview_process_insights": {
        "standard_rounds": [
            {"round_index": 1, "name": "Recruiter Screen", "focus": "Salary, culture, communication"},
            {
                "round_index": 2,
                "name": "Technical Deep Dive",
                "focus": "System architecture, .NET internals",
            },
            {
                "round_index": 3,
                "name": "Executive / Culture Fit",
                "focus": "Problem-solving and leadership",
            },
        ]
    },
    "killer_questions_to_ask_them": [
        "How does the engineering team balance new AI feature delivery with technical debt repayment?"
    ],
}

SAMPLE_TAILORED_RESUME_JSON: dict[str, Any] = {
    "tailored_headline": "Senior .NET & Distributed Systems Engineer",
    "tailored_summary": (
        "Results-driven Backend Engineer with 5+ years of experience architecting high-throughput "
        "microservices using .NET 10, EF Core, and SQL Server..."
    ),
    "optimized_skills": {
        "top_matching_skills": [".NET 10", "C#", "SQL Server", "Clean Architecture", "Docker"],
        "secondary_skills": ["Redis", "RabbitMQ", "CI/CD", "Unit Testing"],
    },
    "tailored_experience_bullet_points": [
        {
            "company": "TechCorp",
            "job_title": "Backend Developer",
            "optimized_bullets": [
                "Architected scalable .NET Web APIs reducing response time by 42% for 200k active users.",
                "Implemented secure JWT authentication and multi-tenant row-level security using EF Core 10.",
            ],
        }
    ],
    "full_markdown_content": "# Abdelrahman Amr\n # Senior .NET Engineer\n...",
    "rendercv_ready_payload": {
        "theme": "classic",
        "sections": {"experience": [], "skills": []},
    },
}

SAMPLE_INTERVIEW_PREP_JSON: dict[str, Any] = {
    "elevator_pitch_60_seconds": "I am a backend specialist passionate about scalable .NET architectures...",
    "interview_rounds_prep": [
        {
            "round_name": "Technical System Design",
            "key_focus_areas": ["Concurrency", "Database Indexing", "Caching Strategies"],
            "likely_questions": [
                "How would you design a multi-tenant file upload service handling 10,000 requests/minute?"
            ],
            "star_stories": [
                {
                    "story_title": "Scaling File Ingestion Pipeline",
                    "target_skill": "Scalability & Resilience",
                    "situation": "Our upload service crashed under bulk PDF processing spikes.",
                    "task": "Redesign ingestion to stream files directly to storage and process asynchronously.",
                    "action": "Implemented streaming hashes with SHA-256 and decoupled background processing.",
                    "result": "Zero downtime achieved and memory footprint reduced by 60%.",
                }
            ],
        }
    ],
    "day_of_battlecard": {
        "key_strengths_to_emphasize": [
            "Mastery of modern .NET 10 and Clean Architecture",
            "Strong background in relational schema optimization",
        ],
        "potential_red_flags_and_counters": [
            {
                "anticipated_doubt": "Limited direct Kafka production experience",
                "counter_response": "Deep familiarity with event-driven principles via RabbitMQ, readily applicable to Kafka.",
            }
        ],
        "questions_to_ask_interviewers": [
            "What is the single biggest bottleneck facing your platform today?"
        ],
    },
    "time_boxed_study_plan": [
        {"day": 1, "task": "Review C# 13 memory allocation patterns and async ValueTask"},
        {"day": 2, "task": "Practice system design diagrams for large-scale document pipelines"},
    ],
}


@pytest.fixture
def sample_profile_dict() -> dict[str, Any]:
    return SAMPLE_CANDIDATE_PROFILE_JSON


@pytest.fixture
def sample_job_dict() -> dict[str, Any]:
    return SAMPLE_TARGET_JOB_JSON


@pytest.fixture
def sample_match_dict() -> dict[str, Any]:
    return SAMPLE_JOB_MATCH_JSON


@pytest.fixture
def sample_research_dict() -> dict[str, Any]:
    return SAMPLE_COMPANY_RESEARCH_JSON


@pytest.fixture
def sample_tailored_resume_dict() -> dict[str, Any]:
    return SAMPLE_TAILORED_RESUME_JSON


@pytest.fixture
def sample_interview_prep_dict() -> dict[str, Any]:
    return SAMPLE_INTERVIEW_PREP_JSON
