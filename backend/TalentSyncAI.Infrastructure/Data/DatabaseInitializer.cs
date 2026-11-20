using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Domain.Entities;
using TalentSyncAI.Domain.Enums;

namespace TalentSyncAI.Infrastructure.Data;

public static class DatabaseInitializer
{
    public const string DefaultDevPassword = "DevPassword123!";
    public static readonly Guid DevCandidateId = Guid.Parse("01a1925a-1825-7eaa-acb4-416264bf7202");
    public static readonly Guid DevCompanyId = Guid.Parse("01A1920B-BBBB-7000-8000-000000000001");
    public static readonly Guid DevRecruiterId = Guid.Parse("01A1920B-BB54-78EE-AA25-895CE7C1248C");

    public static async Task InitializeDatabaseAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        var migrateOnStartup = configuration.GetValue<bool?>("Database:MigrateOnStartup") 
            ?? environment.IsDevelopment();

        var context = services.GetRequiredService<ApplicationDbContext>();

        if (migrateOnStartup)
        {
            try
            {
                logger.LogInformation("Applying EF Core database migrations on startup...");
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply database migrations on startup.");
                throw;
            }
        }

        if (environment.IsDevelopment())
        {
            await SeedDevelopmentDataAsync(context, services.GetRequiredService<IPasswordHasher>(), logger);
        }
    }

    private static async Task SeedDevelopmentDataAsync(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILogger logger)
    {
        var passwordHash = passwordHasher.HashPassword(DefaultDevPassword);

        // Candidate
        if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == DevCandidateId))
        {
            var candidate = new User
            {
                Id = DevCandidateId,
                Email = "candidate@talentsync.ai",
                NormalizedEmail = "CANDIDATE@TALENTSYNC.AI",
                PasswordHash = passwordHash,
                FirstName = "Dev",
                LastName = "Candidate",
                UserType = UserType.Candidate,
                IsActive = true
            };
            context.Users.Add(candidate);

            var profile = new CandidateProfile
            {
                UserId = DevCandidateId,
                FirstName = "Dev",
                LastName = "Candidate",
                Email = "candidate@talentsync.ai"
            };
            context.CandidateProfiles.Add(profile);
        }

        // Company
        if (!await context.Companies.IgnoreQueryFilters().AnyAsync(c => c.Id == DevCompanyId))
        {
            context.Companies.Add(new Company
            {
                Id = DevCompanyId,
                Name = "TechCorp Solutions",
                Industry = "Technology",
                Description = "Default company for local development and testing"
            });
        }

        // Recruiter
        if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == DevRecruiterId))
        {
            context.Users.Add(new User
            {
                Id = DevRecruiterId,
                Email = "sara.recruiter@techcorp.com",
                NormalizedEmail = "SARA.RECRUITER@TECHCORP.COM",
                PasswordHash = passwordHash,
                FirstName = "Sara",
                LastName = "Recruiter",
                UserType = UserType.Recruiter,
                IsActive = true
            });

            context.CompanyMembers.Add(new CompanyMember
            {
                Id = Guid.NewGuid(),
                CompanyId = DevCompanyId,
                UserId = DevRecruiterId,
                Role = CompanyMemberRole.Admin
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Development database seeding completed.");
    }
}
