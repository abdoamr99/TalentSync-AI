using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalentSyncAI.Application.Common.Interfaces;
using TalentSyncAI.Infrastructure.Data;
using TalentSyncAI.Tests.Fakes;

namespace TalentSyncAI.Tests.Helpers;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeCandidateAiService FakeAiService { get; } = new();
    private readonly string _databaseName = "TalentSyncAITestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "SUPER_SECRET_TESTING_KEY_32_CHARS_LONG_2026_TEST!",
                ["Jwt:Issuer"] = "TalentSyncAI",
                ["Jwt:Audience"] = "TalentSyncAI_Client",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Database:MigrateOnStartup"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all existing DbContext and EF Core provider descriptors to prevent multi-provider conflict
            var efDescriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(IApplicationDbContext) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.StartsWith("Microsoft.EntityFrameworkCore"))
            ).ToList();

            foreach (var d in efDescriptors)
            {
                services.Remove(d);
            }

            // Register single InMemory provider
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Replace ICandidateAiService with our controllable Fake
            services.RemoveAll<ICandidateAiService>();
            services.AddSingleton<ICandidateAiService>(FakeAiService);
        });
    }

    public async Task SeedAsync(Func<ApplicationDbContext, IServiceProvider, Task> seeder)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await seeder(context, scope.ServiceProvider);
    }
}
