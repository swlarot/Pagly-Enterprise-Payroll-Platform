using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs.Calculator;
using Vorluno.Planilla.Application.Interfaces;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Tests del rate limiter del API Platform. Usa una factory dedicada con
/// <c>ApiRateLimit:PerMinute=3</c> para poder exceder el límite con 4 requests
/// sin depender de timing (si fuera 60, necesitaríamos 61 requests).
///
/// La política <c>"calculator-v1"</c> particiona por el claim <c>api_key_id</c>,
/// lo que significa que cada key tiene su propio bucket.
/// </summary>
public class RateLimitingTests : IClassFixture<RateLimitedWebApplicationFactory>
{
    private readonly RateLimitedWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RateLimitingTests(RateLimitedWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Calculate_WithinLimit_AllReturn200()
    {
        // Arrange: con límite=3, 3 requests consecutivas deben pasar.
        var plaintextKey = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", plaintextKey);

        // Act
        var statusCodes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Should().OnlyContain(s => s == HttpStatusCode.OK,
            "las primeras 3 requests deben pasar bajo el límite de 3");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_ExceedsLimit_Returns429WithProblemDetails()
    {
        // Arrange: con límite=3, la 4ta request debe ser rechazada.
        var plaintextKey = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", plaintextKey);

        // Agotar el bucket con 3 requests.
        for (var i = 0; i < 3; i++)
        {
            var ok = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());
            ok.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} debería pasar");
        }

        // La 4ta debe ser rechazada.
        var rejected = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // Retry-After header presente
        rejected.Headers.Should().Contain(h => h.Key == "Retry-After");

        // Body tiene shape RFC 7807 con errorCode=rate_limit_error
        var body = await rejected.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limit_error");
        body.Should().Contain("Too many requests");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_DifferentKeys_HaveSeparateBuckets()
    {
        // Arrange: dos keys distintas, cada una agota su propio bucket.
        var keyA = await SeedApiKeyAsync();
        var keyB = await SeedApiKeyAsync();

        // Act: con keyA, llega al límite (3 OK) y el 4to es rechazado.
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", keyA);
        for (var i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());
        }
        var keyARejected = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());
        keyARejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // keyB debe seguir funcionando porque tiene su propio bucket.
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", keyB);
        var keyBFirst = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());
        keyBFirst.StatusCode.Should().Be(HttpStatusCode.OK,
            "keyB tiene su propio partition y no debería estar afectado por el rate limit de keyA");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private async Task<string> SeedApiKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        var (_, plaintext) = await service.GenerateAsync(
            tenantId: 999,
            name: $"rate-limit-test-{Guid.NewGuid():N}",
            mode: "Live",
            expiresAt: null,
            createdByUserId: null);

        return plaintext;
    }

    private static PayrollCalculateRequest BuildValidRequest() => new()
    {
        GrossPay = 2000m,
        PayFrequency = "Mensual",
        YearsCotized = 5,
        AverageSalaryLast10Years = 1800m,
        CssRiskPercentage = 0m,
        Dependents = 0,
        IsSubjectToCss = true,
        IsSubjectToEducationalInsurance = true,
        IsSubjectToIncomeTax = true,
        CalculationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
