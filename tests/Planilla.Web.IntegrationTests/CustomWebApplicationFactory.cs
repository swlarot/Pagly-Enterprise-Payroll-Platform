using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));

            // Add test DbContext with in-memory database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // Use in-memory database (each test gets fresh database)
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                // Ignore warnings about in-memory limitations
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });

        // Override JWT configuration for tests
        builder.UseSetting("Jwt:Key", "test-secret-key-must-be-at-least-32-characters-long-for-hmacsha256");
        builder.UseSetting("Jwt:Issuer", "https://test.planilla.vorluno.dev");
        builder.UseSetting("Jwt:Audience", "https://test.planilla.vorluno.dev");

        // Set Testing environment BEFORE Build() is called
        builder.UseEnvironment("Testing");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => Task.CompletedTask;
}
