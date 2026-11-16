# TalentSyncAI — Stateless AI Service

**TalentSyncAI** is a stateless FastAPI microservice powering intelligent candidate workflows: document and CV parsing, job description extraction, deterministic candidate-to-job matching, autonomous company reconnaissance, ATS-optimized resume tailoring (with RenderCV PDF typesetting), and structured interview preparation (with WeasyPrint Battlecard HTML/PDF generation).

---

## ✨ Key Features

- **Document & CV Parsing:** Converts uploaded PDF/DOCX resumes into clean Markdown via **LlamaParse**, then extracts a strictly typed `CandidateProfileParsed` schema.
- **Job Description Parsing:** Extracts structured `TargetJobRequirements` (mandatory/preferred skills, responsibilities, ATS keywords, seniority, and work model) from raw JD text or public job posting URLs (with login-wall detection).
- **Candidate-to-Job Match & Skill-Gap Analysis:** Combines LLM-based qualitative skill/experience evaluation with **deterministic Python scoring** (weighted rubric + exact employment-date duration calculation) to produce a `JobMatchAnalysisResult`.
- **Company & Role Reconnaissance Agent:** Uses a tool-calling LangChain agent backed by **Tavily** (`web_search` and `web_extract`) to gather live intelligence on company mission, tech stack, engineering culture, and interview rounds (`CompanyRoleResearchResult`).
- **Grounded Resume Tailoring & PDF Rendering:** Rewrites and reorders candidate experience bullets to align with target JD keywords while enforcing strict anti-hallucination grounding checks. Outputs Markdown, a RenderCV v2.8 payload, and typeset PDF bytes.
- **Interview Preparation & Day-of Battlecard:** Runs parallel LLM calls to generate round-by-round preparation, grounded STAR stories, strategic questions, red-flag counters, and a printable HTML/PDF Battlecard via **Jinja2 + WeasyPrint**.

---

## 🛡️ Core Engineering & Safety Principles

1. **Zero Hallucination & Deterministic Grounding Guards:**
   - Every LLM-generated claim (match strengths, tailored resume bullets, optimized skills, and interview STAR stories) is verified deterministically against the candidate's parsed profile corpus (`app/core/guards/grounding.py` and `app/candidate/guards/grounding.py`).
   - Fabricated companies, ungrounded skills, or altered numeric metrics trigger an automatic corrective retry (`run_with_grounding_retry`) before returning a `502 GROUNDING_ERROR` if still violated.
2. **PII Privacy Protection:**
   - Candidate personally identifiable information (`personal_info`: name, email, phone, social links) is stripped before sending profile context to downstream LLM calls (`matcher`, `resume_tailor`, `interview_prep`) and re-injected deterministically in code when rendering templates.
3. **Stateless Execution & Internal API Authentication:**
   - No database or persistent local file state is required. All `/v1/candidate/*` endpoints are protected via constant-time `X-Internal-Api-Key` header verification (`hmac.compare_digest`).

---

## 📂 Project Structure

```text
TalentSync-AI/
├── app/
│   ├── main.py                        # FastAPI app factory & startup lifespan validation
│   ├── health_router.py               # Liveness check endpoint (GET /)
│   ├── core/                          # Shared core infrastructure & foundational services
│   │   ├── config.py                  # Pydantic BaseSettings (.env configuration)
│   │   ├── llm.py                     # OpenAI-compatible chat model initialization & caching
│   │   ├── security.py                # X-Internal-Api-Key header verification
│   │   ├── enums/                     # Status, match verdict, error codes & message enums
│   │   ├── guards/grounding.py        # Core token/metric grounding verification & retry helper
│   │   ├── prompts/                   # Shared grounding rules, CV parser & matcher prompts
│   │   ├── schemas/                   # CandidateProfileParsed, TargetJobRequirements, JobMatchAnalysisResult
│   │   ├── services/                  # cv_parser.py & matcher.py
│   │   └── tools/                     # LlamaParse document parser & Tavily web search/extract tools
│   └── candidate/                     # Candidate-specific workflows & rendering pipelines
│       ├── candidate_router.py        # All /v1/candidate/* API endpoints
│       ├── enums/messages.py          # Candidate validation & error messages
│       ├── guards/grounding.py        # Resume tailor & interview prep grounding verifiers
│       ├── prompts/                   # Company recon, resume tailor & interview prep prompts
│       ├── schemas/                   # Request/Response schemas for research, tailor & interview prep
│       ├── services/                  # job_parser, company_recon, resume_tailor, interview_prep, pdf_renderer
│       └── templates/battlecard/      # Jinja2 HTML template, CSS & FiraSans fonts for Battlecard PDFs
├── specs/
│   └── schemas_contract.md            # Canonical JSON schema contracts
├── tests/
│   ├── conftest.py                    # Shared fixtures (sample profile, job, match, research, etc.)
│   ├── test_api.py                    # Endpoint, auth, error mapping & OpenAPI contract tests
│   ├── test_grounding.py              # Anti-hallucination & grounding guard unit tests
│   ├── test_schemas.py                # Pydantic schema round-trip & validation tests
│   └── test_services.py               # Service-layer unit tests with mocked LLM/tools
├── .env.example                       # Template environment variables
└── pyproject.toml                     # Dependencies, Pytest & Ruff configuration
```

---

## 🔌 API Endpoints

### System
| Method | Endpoint | Auth Required | Description |
| :--- | :--- | :---: | :--- |
| `GET` | `/` | No | Service health check and application metadata (`status`, `app_name`, `app_version`, `app_description`). |

### Candidate Workflows (`/v1/candidate`)
> All endpoints below require the `X-Internal-Api-Key: <INTERNAL_API_KEY>` header.

| Method | Endpoint | Summary | Response Model / Type |
| :--- | :--- | :--- | :--- |
| `POST` | `/v1/candidate/documents/parse` | Parse an uploaded document (`multipart/form-data`) into cleaned Markdown text via LlamaParse | `DocumentParseResponse` (`application/json`) |
| `POST` | `/v1/candidate/cv/parse` | Extract a structured candidate profile from raw or Markdown CV text | `CandidateProfileParsed` (`application/json`) |
| `POST` | `/v1/candidate/job/parse` | Extract structured job requirements from JD text or a public job URL | `TargetJobRequirements` (`application/json`) |
| `POST` | `/v1/candidate/match` | Compute deterministic match score, strengths, skill gaps, and recommendations | `JobMatchAnalysisResult` (`application/json`) |
| `POST` | `/v1/candidate/research` | Run the web reconnaissance agent on a target company and role | `CompanyRoleResearchResult` (`application/json`) |
| `POST` | `/v1/candidate/tailor` | Generate a grounded, tailored resume (Markdown + RenderCV v2.8 payload) | `TailoredResumeResult` (`application/json`) |
| `POST` | `/v1/candidate/tailor/pdf` | Render a `TailoredResumeResult`, RenderCV payload, or YAML string into a PDF | `application/pdf` |
| `POST` | `/v1/candidate/prep` | Generate an interview preparation plan, per-round STAR stories, and battlecard | `InterviewPreparationPlan` (`application/json`) |
| `POST` | `/v1/candidate/prep/battlecard/html` | Render the day-of interview battlecard into styled HTML | `text/html` |
| `POST` | `/v1/candidate/prep/battlecard/pdf` | Render the day-of interview battlecard into a PDF via WeasyPrint | `application/pdf` |

---

## ⚙️ Configuration (`.env`)

Copy `.env.example` to `.env` and configure the following variables:

```ini
APP_NAME="TalentSyncAI AI Service"
APP_VERSION="1.0.0"
APP_DESCRIPTION="AI service for candidate CV parsing, job matching, company recon, resume tailoring, and interview preparation."

# LLM Provider Configuration (OpenAI-compatible)
OPENAI_API_KEY=your-openai-api-key
OPENAI_BASE_URL=https://api.openai.com/v1

# External Tool API Keys
TAVILY_API_KEY=your-tavily-api-key
LLAMA_CLOUD_API_KEY=your-llama-cloud-api-key

# Internal Service-to-Service Auth
INTERNAL_API_KEY=dev-internal-api-key

# Per-Stage Model Identifiers
CV_PARSER_MODEL=openai:gpt-4o-mini
JOB_PARSER_MODEL=openai:gpt-4o-mini
MATCHER_MODEL=openai:gpt-4o
COMPANY_RECON_MODEL=openai:gpt-4o
RESUME_TAILOR_MODEL=openai:gpt-4o
INTERVIEW_PREP_MODEL=openai:gpt-4o
```

---

## 🚀 Getting Started

### 1. Prerequisites
- **Python:** `>= 3.13`
- **Optional PDF Rendering Engines** (for `/tailor/pdf` and `/prep/battlecard/pdf` in production):
  - `rendercv` (for resume PDF generation)
  - `weasyprint` (for battlecard PDF generation)

### 2. Install Dependencies
Using `pip` or `uv`:
```bash
pip install -e .
pip install pytest pytest-asyncio ruff
```

### 3. Run the Development Server
```bash
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```
- **Interactive Swagger UI:** `http://localhost:8000/docs`
- **OpenAPI JSON Spec:** `http://localhost:8000/openapi.json`

---

## 🧪 Testing & Code Quality

Run the full test suite (35 unit and integration tests):
```bash
pytest -v
```

Run Ruff linter:
```bash
ruff check .
```

---

## 📄 License

Licensed under the [Apache License 2.0](LICENSE).
