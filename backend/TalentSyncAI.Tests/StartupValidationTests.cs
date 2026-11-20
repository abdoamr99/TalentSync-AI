using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TalentSyncAI.Tests;

public class StartupValidationTests
{
    [Fact]
    public void Startup_WhenJwtSecretKeyMissingOrTooShort_ThrowsInvalidOperationException()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:SecretKey"] = "too_short_key",
                        ["Database:MigrateOnStartup"] = "false"
                    });
                });
            });

        // Attempting to create client/host must fail fast
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
