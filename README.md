# TalentSyncAI — Enterprise AI Recruitment Platform

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-0.115+-009688?logo=fastapi&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.13+-3776AB?logo=python&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)
![SQL Server](https://img.shields.io/badge/Database-SQL_Server-CC292B?logo=microsoftsqlserver&logoColor=white)
![Architecture](https://img.shields.io/badge/Architecture-Clean_Architecture-blue)
![License](https://img.shields.io/badge/License-MIT-green)

An enterprise-grade, AI-driven recruitment and career preparation platform. Built with **ASP.NET Core (.NET 10)** following **Clean Architecture**, integrated with a stateless **Python / FastAPI AI Microservice**, and fully containerized with **Docker & Docker Compose**.

Provides dual-track workflows for:
1. **Candidates (B2C):** AI-powered job matching, target company research, resume tailoring, and interview prep kits with instant ATS PDF export.
2. **Recruiters (B2B):** Multi-tenant company workspaces, candidate search & ranking, shortlists with hiring pipeline tracking, private CV bank, and immutable audit logs.

---

## 🏗️ Architecture Overview

The system is organized as a unified monorepo:

```text
TalentSync-AI/
├── backend/                         # ASP.NET Core Web API (.NET 10)
│   ├── TalentSyncAI.API/            # HTTP Controllers, Middlewares, Rate Limiting, Swagger
│   ├── TalentSyncAI.Application/    # DTOs, Interfaces, Exceptions, Business Contracts
│   ├── TalentSyncAI.Domain/         # Domain Entities, Rules, Enums
│   ├── TalentSyncAI.Infrastructure/ # EF Core DbContext, AI Client, Services, File Storage
│   ├── TalentSyncAI.Tests/          # Automated Integration & Unit Tests
│   └── Dockerfile                   # Multi-stage .NET 10 container build
├── ai-service/                      # Python / FastAPI AI Microservice (Python >= 3.13)
│   ├── app/
│   │   ├── candidate/               # AI match, research, tailor, prep endpoints
│   │   ├── core/                    # LLM config, security, prompt templates
│   │   └── main.py                  # FastAPI app entry point
│   ├── pyproject.toml               # Python dependencies
│   └── Dockerfile                   # Python 3.13 container build
├── .env.example                     # Environment template for secrets and config
├── docker-compose.yml               # Full stack orchestration (DB + AI + Backend)
└── README.md                        # Documentation
```

```mermaid
flowchart LR
    Client["Client / Postman / Swagger\n(Port 5000)"]
    
    subgraph Docker ["Docker Network (talentsyncai-network)"]
        Backend["Backend Web API\nASP.NET Core (.NET 10)\nContainer: talentsyncai-backend"]
        AIService["AI Microservice\nFastAPI (Python 3.13)\nContainer: talentsyncai-ai-service:8000"]
        Database[("SQL Server 2022\nDatabase: TalentSyncAIDb\nContainer: talentsyncai-db:1433")]
    end

    Client -->|HTTP / REST (JWT)| Backend
    Backend -->|Internal API Key| AIService
    Backend -->|EF Core Migrations & Tenancy| Database
```

---

## ⚡ Quick Start with Docker (Recommended)

### 1. Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
- [Git](https://git-scm.com/) installed.

### 2. Configure Environment Secrets
Copy the environment template before starting:
```bash
cp .env.example .env
```
Ensure `MSSQL_SA_PASSWORD` and `JWT_SECRET_KEY` (minimum 32 characters) are configured.

### 3. Run Everything
```bash
docker compose up --build
```

Docker will:
1. Start **SQL Server 2022** and wait until healthy (bound to `127.0.0.1:1433`).
2. Start the **FastAPI AI Microservice** on the internal network.
3. Start the **ASP.NET Core Backend** on port `5000`.
4. Automatically apply all database migrations on startup.

---

## 🌐 Service Access & URLs

| Service | URL | Description |
| :--- | :--- | :--- |
| **Backend Swagger UI** | **`http://localhost:5000/swagger`** | Interactive API documentation & testing |
| **Health Liveness** | **`http://localhost:5000/health/live`** | Liveness probe (200 OK) |
| **Health Readiness** | **`http://localhost:5000/health/ready`** | Readiness probe (validates SQL Server) |
| **SQL Server** | **`127.0.0.1:1433`** | User: `sa` / Password from `.env` |

---

## 🔐 Authentication & Authorization Guide

All API endpoints (except `/api/auth/*` and `/health/*`) require JWT Bearer authentication and role-based policies.

### Workflow:
1. **Register:**
   - Call `POST /api/auth/register` with `userType: "Candidate"` or `userType: "Recruiter"`.
2. **Login:**
   - Call `POST /api/auth/login` to obtain an `accessToken` and `refreshToken`.
   - Refresh tokens are securely hashed using SHA-256 and rotated on use (`POST /api/auth/refresh-token`).
3. **Authorize in Swagger:**
   - In Swagger UI (`/swagger`), click **Authorize** at the top right.
   - Enter your `accessToken` (without `"Bearer "` prefix).
4. **Access Control Policies:**
   - Candidate endpoints (`/api/candidate/*`) require `Roles = "Candidate"`.
   - Company endpoints (`/api/company/*`) require `Roles = "Recruiter"` and an active `company_id` tenant claim.

---

## 🛡️ Rate Limiting & Resilience

* **Rate Limiting:**
  - **Auth endpoints (`/api/auth/*`):** 10 requests / minute / IP.
  - **AI endpoints (`/api/candidate/applications/*/match`, `research`, `tailor-resume`, `interview-prep`, `generate-all`):** 10 requests / minute / authenticated user.
  - Exceeding the rate limit returns `429 Too Many Requests`.
* **AI Resilience & Failure Contract:**
  - AI calls use typed HTTP clients configured with resilience handlers (retry with exponential backoff and circuit breaking).
  - If the upstream AI service is unreachable or encounters an error, the backend returns a clean `503 Service Unavailable` ProblemDetails. **No fake or mock data is ever persisted to the database.**

---

## 🔌 Core API Endpoints Summary

| Category | Method | Endpoint | Description |
| :--- | :---: | :--- | :--- |
| **Auth** | `POST` | `/api/auth/register` | Register Candidate or Recruiter |
| **Auth** | `POST` | `/api/auth/login` | Authenticate and obtain JWT Bearer Token |
| **Auth** | `POST` | `/api/auth/refresh-token` | Rotate refresh token and get fresh JWT |
| **Candidate CV** | `POST` | `/api/candidate/cv/upload` | Upload CV with signature validation and deduplication |
| **Candidate CV** | `GET` | `/api/candidate/cv` | List uploaded CVs (capped at 100) |
| **Candidate CV** | `GET` | `/api/candidate/cv/{id}` | Get CV metadata |
| **Candidate CV** | `GET` | `/api/candidate/cv/{id}/download` | Download stored original CV |
| **Candidate CV** | `PUT` | `/api/candidate/cv/{id}/set-primary` | Set primary CV |
| **Candidate CV** | `DELETE`| `/api/candidate/cv/{id}` | Delete CV and stored file |
| **Target Jobs** | `POST` | `/api/candidate/applications` | Create target application via Text, File, or URL |
| **Target Jobs** | `GET` | `/api/candidate/applications` | List applications with AI processing status |
| **Target Jobs** | `GET` | `/api/candidate/applications/{id}` | Get application detail with all AI insights |
| **Target Jobs** | `DELETE`| `/api/candidate/applications/{id}` | Soft delete target job application |
| **AI Insights** | `POST` | `/api/candidate/applications/{id}/match` | Run Job Match & Skill Gap Analysis |
| **AI Insights** | `POST` | `/api/candidate/applications/{id}/research` | Run Company & Hiring Recon |
| **AI Insights** | `POST` | `/api/candidate/applications/{id}/tailor-resume` | Generate STAR-formatted tailored resume |
| **AI Insights** | `POST` | `/api/candidate/applications/{id}/interview-prep` | Generate Interview Battlecard & Prep Plan |
| **AI Insights** | `POST` | `/api/candidate/applications/{id}/generate-all` | Execute all 4 AI stages sequentially |
| **Resume Export** | `GET` | `/api/candidate/applications/{id}/tailor-resume/download` | Export tailored resume as a formatted ATS PDF |
| **Company CV** | `POST` | `/api/company/cv/upload` | Bulk upload up to 50 CVs into private pool |
| **Company CV** | `GET` | `/api/company/cv` | Paginated company CV pool |
| **Company CV** | `GET` | `/api/company/cv/{id}/download` | Download company pool CV |
| **Company Search**| `POST` | `/api/company/search` | Keyword-based candidate search with relevance ranking |
| **Company Search**| `GET` | `/api/company/search` | List past recruiter searches (capped at 50) |
| **Shortlists** | `POST` | `/api/company/shortlists` | Create recruitment shortlist |
| **Shortlists** | `GET` | `/api/company/shortlists` | List recruitment shortlists (capped at 100) |
| **Shortlists** | `POST` | `/api/company/shortlists/{id}/candidates` | Add candidate to shortlist with notes |
| **Audit Logs** | `GET` | `/api/company/audit` | View immutable audit history of recruiter actions |

---

## 💻 Manual Local Development (Without Docker)

### 1. Run AI Service (FastAPI)
```bash
cd ai-service
py -3.13 -m venv venv
.\venv\Scripts\activate
pip install -e .
uvicorn app.main:app --reload --port 8000
```

### 2. Run Backend (.NET 10)
```bash
cd backend/TalentSyncAI.API
dotnet run
```
Navigate to `http://localhost:5239/swagger`.

---

## 📄 License
This project is licensed under the MIT License.
