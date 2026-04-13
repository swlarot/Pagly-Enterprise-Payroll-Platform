using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs.Calculator;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Tests del filter <c>IdempotentAttribute</c> contra el endpoint real
/// /v1/payroll/calculate. Cubre: opt-in (sin header pasa), replay exacto,
/// reuso con payload distinto (422), expiración.
/// </summary>
public class IdempotencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdempotencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Calculate_WithoutIdempotencyKey_PassesThrough()
    {
        // Sin header Idempotency-Key, el endpoint se ejecuta normalmente.
        var key = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Idempotent-Replay").Should().BeFalse();

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_SameIdempotencyKey_ReplaysResponse()
    {
        var key = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var idempotencyKey = $"test-{Guid.NewGuid()}";
        var request = BuildValidRequest();

        // Primera llamada — ejecuta y cachea
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(request),
        };
        req1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await _client.SendAsync(req1);
        var body1 = await response1.Content.ReadAsStringAsync();
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response1.Headers.Contains("Idempotent-Replay").Should().BeFalse();

        // Segunda llamada con el MISMO key y MISMO payload — replay
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(request),
        };
        req2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(req2);
        var body2 = await response2.Content.ReadAsStringAsync();

        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.Headers.Contains("Idempotent-Replay").Should().BeTrue();
        response2.Headers.GetValues("Idempotent-Replay").First().Should().Be("true");

        // El body debe ser byte-a-byte idéntico
        body2.Should().Be(body1);

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_SameKeyDifferentPayload_Returns422()
    {
        var key = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var idempotencyKey = $"test-{Guid.NewGuid()}";

        // Primera llamada con payload A
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(BuildValidRequest()),
        };
        req1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await _client.SendAsync(req1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Segunda llamada con el MISMO key pero payload B (distinto grossPay)
        var payloadB = BuildValidRequest();
        payloadB.GrossPay = 3500m; // distinto de 2000
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(payloadB),
        };
        req2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(req2);

        response2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response2.Content.ReadAsStringAsync();
        body.Should().Contain("IDEMPOTENCY_KEY_REUSED");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_EmptyIdempotencyKey_Returns400()
    {
        var key = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(BuildValidRequest()),
        };
        req.Headers.Add("Idempotency-Key", "   "); // whitespace only

        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("INVALID_IDEMPOTENCY_KEY");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_TooLongIdempotencyKey_Returns400()
    {
        var key = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/payroll/calculate")
        {
            Content = JsonContent.Create(BuildValidRequest()),
        };
        req.Headers.Add("Idempotency-Key", new string('x', 256));

        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task PurgeExpired_RemovesOnlyExpiredRecords()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        // Seed: uno expirado, uno vigente
        db.IdempotencyRecords.Add(new Vorluno.Planilla.Domain.Entities.IdempotencyRecord
        {
            ApiKeyId = 999999,
            IdempotencyKey = "expired-" + Guid.NewGuid(),
            Endpoint = "/v1/payroll/calculate",
            RequestHash = new string('0', 64),
            StatusCode = 200,
            ResponseJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-48),
            ExpiresAt = DateTime.UtcNow.AddHours(-24),
        });
        db.IdempotencyRecords.Add(new Vorluno.Planilla.Domain.Entities.IdempotencyRecord
        {
            ApiKeyId = 999998,
            IdempotencyKey = "valid-" + Guid.NewGuid(),
            Endpoint = "/v1/payroll/calculate",
            RequestHash = new string('1', 64),
            StatusCode = 200,
            ResponseJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddHours(23),
        });
        await db.SaveChangesAsync();

        var before = await db.IdempotencyRecords.CountAsync();

        var deleted = await store.PurgeExpiredAsync();

        var after = await db.IdempotencyRecords.CountAsync();
        deleted.Should().BeGreaterThanOrEqualTo(1);
        after.Should().Be(before - deleted);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private async Task<string> SeedApiKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        var (_, plaintext) = await service.GenerateAsync(
            tenantId: 888,
            name: $"idempotency-test-{Guid.NewGuid()}",
            mode: "Live",
            expiresAt: null,
            createdByUserId: null);

        return plaintext;
    }

    private static PayrollCalculateRequest BuildValidRequest()
    {
        return new PayrollCalculateRequest
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
}
