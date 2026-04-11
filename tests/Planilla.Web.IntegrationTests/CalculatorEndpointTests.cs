using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs.Calculator;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Tests end-to-end del endpoint POST /v1/payroll/calculate usando
/// WebApplicationFactory + EF InMemory + API key real generada en setup.
/// Valida el flujo completo: auth por header X-Api-Key → orchestrator
/// con StaticPayrollConfigProvider → response shape.
/// </summary>
public class CalculatorEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CalculatorEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ====================================================================
    // Auth
    // ====================================================================

    [Fact]
    public async Task Calculate_WithoutApiKey_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Calculate_WithInvalidApiKey_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", "pk_live_deadbeef" + new string('0', 32));

        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    // ====================================================================
    // Happy path
    // ====================================================================

    [Fact]
    public async Task Calculate_WithValidKey_ReturnsBreakdown()
    {
        var plaintextKey = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", plaintextKey);

        var request = BuildValidRequest();

        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"response body: {body}");

        var result = JsonSerializer.Deserialize<PayrollCalculateResponse>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        result.Should().NotBeNull();
        result!.GrossPay.Should().Be(2000m);
        result.Version.Should().Be("2026.1");

        // Sanidad: para salario 2000 mensual con 0 dependientes, sujeto a todo,
        // fase 1 (13.25% patronal), 0% riesgo, 5 años cotizados, promedio $1800.
        //
        // CssCalculationServicePortable aplica el tope de Ley 462 Art. 178 como tope
        // de COTIZACIÓN (Math.Min(grossPay, periodCap)). Para mensual periodCap=1500
        // (no califica a intermedio 2000/25yrs ni alto 2500/30yrs).
        //   CSS empleado:  1500 * 9.75%  = 146.25
        //   CSS patronal:  1500 * 13.25% = 198.75
        //
        // Seguro educativo NO tiene tope, aplica sobre bruto completo:
        //   SE empleado:   2000 * 1.25% = 25.00
        //   SE patronal:   2000 * 1.50% = 30.00
        //
        // ISR se proyecta anual con ×13 meses: 2000 * 13 = 26,000.
        //   Bracket 2: 15% de (26,000 - 11,000.01) ≈ 2250 anual / 12 ≈ 187.50
        result.CssEmployee.Should().BeApproximately(146.25m, 0.01m);
        result.EducationalInsuranceEmployee.Should().Be(25.00m);
        result.IncomeTax.Should().BeApproximately(187.50m, 0.01m);
        result.NetPay.Should().BeApproximately(2000m - 146.25m - 25m - 187.50m, 0.01m);
        result.CssEmployer.Should().BeApproximately(198.75m, 0.01m);
        result.EducationalInsuranceEmployer.Should().Be(30.00m);

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    [Fact]
    public async Task Calculate_ResponseHasRequestIdHeader()
    {
        var plaintextKey = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", plaintextKey);

        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", BuildValidRequest());

        // HttpResponseHeaders.TryGetValues es case-insensitive por contract.
        // ASP.NET puede normalizar el header a "X-Request-ID" en transit.
        response.Headers.TryGetValues("X-Request-Id", out var values).Should().BeTrue();
        values!.First().Should().NotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    /// <summary>
    /// Siembra un tenant (stub) y una API key usando el service real.
    /// Retorna la key plaintext para usar en los headers.
    /// </summary>
    private async Task<string> SeedApiKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

        var (_, plaintext) = await service.GenerateAsync(
            tenantId: 999,
            name: "integration-test",
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
