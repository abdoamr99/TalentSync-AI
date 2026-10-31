using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentSyncAI.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TalentSyncAI_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefreshTokenExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CurrentTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCandidates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyShortlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyShortlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyShortlists_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProcessingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProcessingRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiProcessingRuns_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TargetJobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GitHubUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSearches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NaturalLanguageQuery = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StructuredFiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSavedSearch = table.Column<bool>(type: "bit", nullable: false),
                    SavedSearchTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalResultsCount = table.Column<int>(type: "int", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSearches_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateSearches_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyMembers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecruiterActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecruiterActions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruiterActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCandidateEducations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Degree = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCandidateEducations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCandidateEducations_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCandidateExperiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DurationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCandidateExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCandidateExperiences_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCandidateSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    YearsExperience = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCandidateSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCandidateSkills_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCvDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ParsingStatus = table.Column<int>(type: "int", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedStructuredJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCvDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCvDocuments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCvDocuments_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyCvDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyShortlistCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyShortlistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyShortlistCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyShortlistCandidates_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyShortlistCandidates_CompanyShortlists_CompanyShortlistId",
                        column: x => x.CompanyShortlistId,
                        principalTable: "CompanyShortlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyShortlistCandidates_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateResumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    RawTextContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StructuredJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateResumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateResumes_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSearchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateSearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyCandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MatchScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MatchingStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchingGaps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSearchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateSearchResults_CandidateSearches_CandidateSearchId",
                        column: x => x.CandidateSearchId,
                        principalTable: "CandidateSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidateSearchResults_CompanyCandidates_CompanyCandidateId",
                        column: x => x.CompanyCandidateId,
                        principalTable: "CompanyCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetJobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCvId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetCompany = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JdInputType = table.Column<int>(type: "int", nullable: false),
                    JobPostingUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawJobDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetJobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetJobApplications_CandidateProfiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TargetJobApplications_CandidateResumes_SourceCvId",
                        column: x => x.SourceCvId,
                        principalTable: "CandidateResumes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompanyRoleResearches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetJobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyOverview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CultureAndValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechStackInsights = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypicalInterviewProcess = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResearchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRoleResearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyRoleResearches_TargetJobApplications_TargetJobApplicationId",
                        column: x => x.TargetJobApplicationId,
                        principalTable: "TargetJobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetJobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StarStories = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StudyPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterviewBattlecard = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPreparations_TargetJobApplications_TargetJobApplicationId",
                        column: x => x.TargetJobApplicationId,
                        principalTable: "TargetJobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobMatchAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetJobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MatchingStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkillGaps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallAssessment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnalyzedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobMatchAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobMatchAnalyses_TargetJobApplications_TargetJobApplicationId",
                        column: x => x.TargetJobApplicationId,
                        principalTable: "TargetJobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TailoredResumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetJobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TailoredMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedPdfPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangesExplanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TailoredResumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TailoredResumes_TargetJobApplications_TargetJobApplicationId",
                        column: x => x.TargetJobApplicationId,
                        principalTable: "TargetJobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProcessingRuns_CompanyId_Status",
                table: "AiProcessingRuns",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProcessingRuns_TriggeredByUserId",
                table: "AiProcessingRuns",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_UserId",
                table: "CandidateProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateResumes_CandidateProfileId",
                table: "CandidateResumes",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSearches_CompanyId_PerformedAtUtc",
                table: "CandidateSearches",
                columns: new[] { "CompanyId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSearches_PerformedByUserId",
                table: "CandidateSearches",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSearchResults_CandidateSearchId",
                table: "CandidateSearchResults",
                column: "CandidateSearchId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSearchResults_CompanyCandidateId",
                table: "CandidateSearchResults",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCandidateEducations_CompanyCandidateId",
                table: "CompanyCandidateEducations",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCandidateExperiences_CompanyCandidateId",
                table: "CompanyCandidateExperiences",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCandidates_CompanyId_CreatedAtUtc",
                table: "CompanyCandidates",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCandidateSkills_CompanyCandidateId",
                table: "CompanyCandidateSkills",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCvDocuments_CompanyCandidateId",
                table: "CompanyCvDocuments",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCvDocuments_CompanyId_CreatedAtUtc",
                table: "CompanyCvDocuments",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCvDocuments_UploadedByUserId",
                table: "CompanyCvDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMembers_CompanyId",
                table: "CompanyMembers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMembers_UserId_CompanyId",
                table: "CompanyMembers",
                columns: new[] { "UserId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoleResearches_TargetJobApplicationId",
                table: "CompanyRoleResearches",
                column: "TargetJobApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyShortlistCandidates_AddedByUserId",
                table: "CompanyShortlistCandidates",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyShortlistCandidates_CompanyCandidateId",
                table: "CompanyShortlistCandidates",
                column: "CompanyCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyShortlistCandidates_CompanyShortlistId_CompanyCandidateId",
                table: "CompanyShortlistCandidates",
                columns: new[] { "CompanyShortlistId", "CompanyCandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyShortlists_CompanyId_CreatedAtUtc",
                table: "CompanyShortlists",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPreparations_TargetJobApplicationId",
                table: "InterviewPreparations",
                column: "TargetJobApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobMatchAnalyses_TargetJobApplicationId",
                table: "JobMatchAnalyses",
                column: "TargetJobApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterActions_CompanyId_PerformedAtUtc",
                table: "RecruiterActions",
                columns: new[] { "CompanyId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruiterActions_UserId",
                table: "RecruiterActions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TailoredResumes_TargetJobApplicationId",
                table: "TailoredResumes",
                column: "TargetJobApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetJobApplications_CandidateProfileId",
                table: "TargetJobApplications",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetJobApplications_SourceCvId",
                table: "TargetJobApplications",
                column: "SourceCvId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProcessingRuns");

            migrationBuilder.DropTable(
                name: "CandidateSearchResults");

            migrationBuilder.DropTable(
                name: "CompanyCandidateEducations");

            migrationBuilder.DropTable(
                name: "CompanyCandidateExperiences");

            migrationBuilder.DropTable(
                name: "CompanyCandidateSkills");

            migrationBuilder.DropTable(
                name: "CompanyCvDocuments");

            migrationBuilder.DropTable(
                name: "CompanyMembers");

            migrationBuilder.DropTable(
                name: "CompanyRoleResearches");

            migrationBuilder.DropTable(
                name: "CompanyShortlistCandidates");

            migrationBuilder.DropTable(
                name: "InterviewPreparations");

            migrationBuilder.DropTable(
                name: "JobMatchAnalyses");

            migrationBuilder.DropTable(
                name: "RecruiterActions");

            migrationBuilder.DropTable(
                name: "TailoredResumes");

            migrationBuilder.DropTable(
                name: "CandidateSearches");

            migrationBuilder.DropTable(
                name: "CompanyCandidates");

            migrationBuilder.DropTable(
                name: "CompanyShortlists");

            migrationBuilder.DropTable(
                name: "TargetJobApplications");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "CandidateResumes");

            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
