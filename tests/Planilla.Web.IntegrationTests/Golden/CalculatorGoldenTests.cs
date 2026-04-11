using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.Interfaces;

namespace Planilla.Web.IntegrationTests.Golden;

/// <summary>
/// Contract test del endpoint <c>POST /v1/payroll/calculate</c>.
///
/// <para>
/// Hace un request con parámetros canónicos fijos y compara el JSON de respuesta
/// contra un snapshot guardado en disco. Protege el <b>shape</b> del response:
/// nombres de campos (camelCase), orden, tipos, precisión decimal, valores calculados.
/// </para>
///
/// <para>
/// Si alguien renombra <c>cssEmployee</c> → <c>cssEmpleado</c> por accidente,
/// o cambia el orden de los campos en <see cref="Vorluno.Planilla.Application.DTOs.Calculator.PayrollCalculateResponse"/>,
/// este test falla y el CI lo atrapa antes de romper clientes en producción.
/// </para>
///
/// <para>
/// Parity tests (chunks anteriores) verifican que los <b>números</b> sean correctos.
/// Este test verifica que el <b>contrato</b> (shape) sea estable. Los dos son complementarios.
/// </para>
///
/// <para>
/// <b>Flujo de actualización intencional del golden</b>: si agregas un campo nuevo al
/// response (ej: <c>calculationTimeMs</c>), este test va a fallar. El fix es:
/// <list type="number">
///   <item>Confirmar que el cambio es intencional (no un rename accidental).</item>
///   <item>Borrar el archivo <c>Golden/Files/calculator-2026-fase1-mensual.json</c>.</item>
///   <item>Re-correr el test una vez — regenera el golden con el shape nuevo.</item>
///   <item>Revisar que el golden regenerado esté correcto y commitearlo.</item>
/// </list>
/// </para>
/// </summary>
public class CalculatorGoldenTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string GoldenFileName = "calculator-2026-fase1-mensual.json";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CalculatorGoldenTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Calculate_CanonicalRequest_MatchesGoldenSnapshot()
    {
        // Arrange: seed API key + build canonical request
        var plaintextKey = await SeedApiKeyAsync();
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", plaintextKey);

        var request = new
        {
            grossPay = 2000m,
            payFrequency = "Mensual",
            yearsCotized = 5,
            averageSalaryLast10Years = 1800m,
            cssRiskPercentage = 0m,
            dependents = 0,
            isSubjectToCss = true,
            isSubjectToEducationalInsurance = true,
            isSubjectToIncomeTax = true,
            // Fecha fija y explícita: evita non-determinismo de DateTime.UtcNow.
            // 2026-06-01 cae en fase 1 de Ley 462 (13.25% patronal hasta feb 2027).
            calculationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/payroll/calculate", request);

        // Assert: status + body shape
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();

        // Normalize: re-serializamos con WriteIndented=true para formato estable de diff.
        // Esto no cambia el orden de los campos — System.Text.Json preserva el orden del
        // JsonDocument original, que a su vez refleja el orden de serialización del record.
        var normalizedActual = NormalizeJson(rawJson);

        var goldenPath = GetGoldenPath();
        if (!File.Exists(goldenPath))
        {
            // Primera corrida: crear el golden. Fail con mensaje instructivo para que
            // el developer revise el contenido antes de aceptar.
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            await File.WriteAllTextAsync(goldenPath, normalizedActual);

            throw new InvalidOperationException(
                $"Golden file created at:\n  {goldenPath}\n\n" +
                "This is the first run. Review the file contents carefully and commit it " +
                "to version control. Re-run the test — it should pass now.\n\n" +
                "If the content looks wrong, fix the calculator code and DELETE the golden " +
                "file before re-running.");
        }

        var expectedJson = await File.ReadAllTextAsync(goldenPath);

        // Comparación byte-a-byte del JSON normalizado.
        // Normaliza line endings para ser cross-platform (CRLF en Windows, LF en Linux).
        var actualClean = normalizedActual.Replace("\r\n", "\n").Trim();
        var expectedClean = expectedJson.Replace("\r\n", "\n").Trim();

        actualClean.Should().Be(expectedClean,
            "the response shape must match the golden snapshot. " +
            "If this change is intentional, delete the golden file at " +
            $"{goldenPath} and re-run the test to regenerate it. " +
            "Otherwise, revert the change that modified the response structure.");
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
            name: "golden-test",
            mode: "Live",
            expiresAt: null,
            createdByUserId: null);

        return plaintext;
    }

    /// <summary>
    /// Re-serializa el JSON con indentación para que el diff sea legible cuando el test
    /// falla. Preserva el orden de campos original (System.Text.Json lo mantiene).
    /// </summary>
    private static string NormalizeJson(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true
            // NO seteamos PropertyNamingPolicy — queremos que los nombres salgan tal cual
            // los generó el endpoint original. Si cambia el naming del controller, el test
            // lo detecta.
        });
    }

    /// <summary>
    /// Resuelve la ruta al golden file en el source directory del proyecto de tests
    /// (no el bin output). Así el archivo está bajo control de versiones y es editable
    /// directamente desde el IDE.
    ///
    /// <para>
    /// <c>AppContext.BaseDirectory</c> apunta a <c>bin/Debug/net10.0/</c> — subir 3
    /// niveles nos deja en el project root del test (<c>Planilla.Web.IntegrationTests/</c>).
    /// </para>
    /// </summary>
    private static string GetGoldenPath()
    {
        var binDir = AppContext.BaseDirectory;
        var projectDir = Path.GetFullPath(Path.Combine(binDir, "..", "..", ".."));
        return Path.Combine(projectDir, "Golden", "Files", GoldenFileName);
    }
}
