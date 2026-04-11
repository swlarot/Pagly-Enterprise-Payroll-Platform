using Microsoft.AspNetCore.Hosting;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Variante de <see cref="CustomWebApplicationFactory"/> que override
/// <c>ApiRateLimit:PerMinute</c> a un valor bajo (3) para que los tests
/// del rate limiter puedan exceder el límite con pocas requests.
///
/// Usa su propia fixture para no afectar al rate limiter global que otros
/// tests (CalculatorEndpointTests) comparten en la factory default.
/// </summary>
public class RateLimitedWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Override del rate limit antes de que Program.cs construya el builder.
        // 3 req/min permite que un burst de 4 requests genere al menos 1 x 429
        // sin timing flaky del sliding window.
        builder.UseSetting("ApiRateLimit:PerMinute", "3");
    }
}
