using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentSyncAI.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateCvFileHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CandidateResumes_CandidateProfileId",
                table: "CandidateResumes");

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "CandidateResumes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidateResumes_CandidateProfileId_FileHash",
                table: "CandidateResumes",
                columns: new[] { "CandidateProfileId", "FileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateResumes_CandidateProfileId_IsPrimary",
                table: "CandidateResumes",
                columns: new[] { "CandidateProfileId", "IsPrimary" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CandidateResumes_CandidateProfileId_FileHash",
                table: "CandidateResumes");

            migrationBuilder.DropIndex(
                name: "IX_CandidateResumes_CandidateProfileId_IsPrimary",
                table: "CandidateResumes");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "CandidateResumes");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateResumes_CandidateProfileId",
                table: "CandidateResumes",
                column: "CandidateProfileId");
        }
    }
}
