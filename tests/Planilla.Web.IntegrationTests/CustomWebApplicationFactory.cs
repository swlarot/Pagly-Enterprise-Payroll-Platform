using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Shared database name for all requests within this factory instance
    // Each test class gets its own factory instance, thus its own database
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set Testing environment FIRST so Program.cs can conditionally skip PostgreSQL registration
        builder.UseEnvironment("Testing");

        // Override JWT configuration for tests
        builder.UseSetting("Jwt:Key", "test-secret-key-must-be-at-least-32-characters-long-for-hmacsha256");
        builder.UseSetting("Jwt:Issuer", "https://test.planilla.vorluno.dev");
        builder.UseSetting("Jwt:Audience", "https://test.planilla.vorluno.dev");

        builder.ConfigureServices(services =>
        {
            // Add test DbContext with in-memory database
            // Program.cs will NOT register PostgreSQL DbContext when Environment is "Testing"
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // Use SAME in-memory database name for all requests in this test
                // Each test class fixture gets a unique database via Guid
                options.UseInMemoryDatabase(_dbName);
                // Ignore warnings about in-memory limitations
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => Task.CompletedTask;
}
