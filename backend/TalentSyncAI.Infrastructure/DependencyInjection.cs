using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Infrastructure.Services;

namespace TalentSyncAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Tenancy & Current User
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Security
        services.Configure<TalentSyncAI.Application.Common.Models.JwtSettings>(
            configuration.GetSection(TalentSyncAI.Application.Common.Models.JwtSettings.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();

        // AI Service (Typed HttpClient with Resilience)
        var aiBaseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
        var internalApiKey = configuration["AiService:InternalApiKey"] ?? "dev-internal-api-key";
        var timeoutSeconds = configuration.GetValue<int>("AiService:TimeoutSeconds", 120);

        services.AddHttpClient<ICandidateAiService, CandidateAiService>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            if (!string.IsNullOrWhiteSpace(internalApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds * 2);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(timeoutSeconds * 2);
        });

        services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();
        services.AddSingleton<IFileStorageService, FileStorageService>();

        // Application Services
        services.AddScoped<ICandidateCvService, CandidateCvService>();
        services.AddScoped<ITargetJobApplicationService, TargetJobApplicationService>();
        services.AddScoped<ICompanyCvService, CompanyCvService>();
        services.AddScoped<ICandidateSearchService, CandidateSearchService>();
        services.AddScoped<ICompanyShortlistService, CompanyShortlistService>();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(name: "database_sqlserver");

        return services;
    }
}
