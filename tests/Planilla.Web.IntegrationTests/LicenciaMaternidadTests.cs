// ====================================================================
// Licencia de maternidad, de punta a punta por HTTP: los días de licencia
// no se pagan en la planilla (los paga la CSS), la fila dice por qué, la
// colaboradora vuelve sola al terminar, y liquidarla por despido con
// fuero pide confirmar la autorización.
// ====================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class LicenciaMaternidadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LicenciaMaternidadTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> CrearEmpleadaAsync(HttpClient client)
    {
        var dto = new EmpleadoCrearDto(
            Nombre: "Laura", Apellido: "Docente",
            NumeroIdentificacion: $"8-{Guid.NewGuid().ToString()[..4]}-7",
            Email: null, SalarioBase: 700m, DepartamentoId: null, PosicionId: null);
        var r = await client.PostAsJsonAsync("/api/empleados", dto);
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<int> PlanillaCalculadaAsync(HttpClient client, string inicio, string fin)
    {
        var crear = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "", PeriodStartDate = inicio, PeriodEndDate = fin, PayPeriodType = 2,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/payrollheaders/{id}/calculate", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<JsonElement> FilaAsync(HttpClient client, int planillaId) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/payrollheaders/{planillaId}/details")).GetProperty("items")[0];

    [Fact]
    public async Task LosDiasDeLicenciaNoSePagan_LaFilaDicePorQue_YVuelveAlAnularla()
    {
        var client = await ClienteAsync("Colegio Maternidad E2E");
        var empleadaId = await CrearEmpleadaAsync(client);

        // 1) Licencia desde el 8 de abril: el regreso se propone a las 14 semanas.
        var crear = await client.PostAsJsonAsync("/api/licencias-maternidad", new { empleadoId = empleadaId, fechaInicio = "2026-04-08" });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var licencia = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("licencia");
        licencia.GetProperty("dias").GetInt32().Should().Be(98);
        licencia.GetProperty("fechaFin").GetDateTime().Date.Should().Be(new DateTime(2026, 7, 14));
        licencia.GetProperty("regreso").GetDateTime().Date.Should().Be(new DateTime(2026, 7, 15));
        licencia.GetProperty("fueroHasta").GetDateTime().Date.Should().Be(new DateTime(2027, 7, 15));
        var licenciaId = licencia.GetProperty("id").GetInt32();

        (await client.PostAsJsonAsync("/api/licencias-maternidad", new { empleadoId = empleadaId, fechaInicio = "2026-05-01" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "se cruza con la que ya tiene");

        // 2) Primera quincena de abril: cobra 7 de 15 días, al centavo.
        var q1 = await PlanillaCalculadaAsync(client, "2026-04-01", "2026-04-15");
        var fila1 = await FilaAsync(client, q1);
        fila1.GetProperty("grossPay").GetDecimal().Should().Be(163.33m, "350 × 7/15");
        fila1.GetProperty("nota").GetString().Should().Contain("8 de 15 días");

        // 3) Segunda quincena: todo el período en licencia; sigue en la planilla, en cero, con su motivo.
        var q2 = await PlanillaCalculadaAsync(client, "2026-04-16", "2026-04-30");
        var fila2 = await FilaAsync(client, q2);
        fila2.GetProperty("grossPay").GetDecimal().Should().Be(0m);
        fila2.GetProperty("cssEmployee").GetDecimal().Should().Be(0m);
        fila2.GetProperty("nota").GetString().Should().Contain("todo el período");

        // 4) Al regresar vuelve sola: la quincena siguiente al 14 de julio se paga completa.
        var q3 = await PlanillaCalculadaAsync(client, "2026-07-16", "2026-07-31");
        var fila3 = await FilaAsync(client, q3);
        fila3.GetProperty("grossPay").GetDecimal().Should().Be(350m);
        fila3.GetProperty("nota").ValueKind.Should().Be(JsonValueKind.Null);

        // 5) Anulada (registrada por error), recalcular paga el período completo y la licencia se conserva.
        (await client.PostAsJsonAsync($"/api/licencias-maternidad/{licenciaId}/anular", new { motivo = "fecha equivocada" })).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/payrollheaders/{q2}/calculate", null)).EnsureSuccessStatusCode();
        (await FilaAsync(client, q2)).GetProperty("grossPay").GetDecimal().Should().Be(350m);
        (await client.GetFromJsonAsync<JsonElement>("/api/licencias-maternidad?incluirAnuladas=true"))
            .EnumerateArray().Single().GetProperty("estado").GetString().Should().Be("Anulada");
    }

    [Fact]
    public async Task LiquidarPorDespidoConFuero_PideConfirmarLaAutorizacion()
    {
        var client = await ClienteAsync("Colegio Fuero E2E");
        var empleadaId = await CrearEmpleadaAsync(client);
        (await client.PostAsJsonAsync("/api/licencias-maternidad", new
        {
            empleadoId = empleadaId, fechaInicio = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"),
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var fin = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        var despido = new { empleadoId = empleadaId, fechaTerminacion = fin, tipoTerminacion = (int)TipoTerminacion.Despido, diasSalarioPendiente = 0m };

        var prev = await client.PostAsJsonAsync("/api/liquidaciones/previsualizar", despido);
        prev.StatusCode.Should().Be(HttpStatusCode.OK);
        (await prev.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fueroMaternidadHasta").ValueKind
            .Should().Be(JsonValueKind.String, "la pantalla lo avisa antes de crear");

        var sinConfirmar = await client.PostAsJsonAsync("/api/liquidaciones", despido);
        sinConfirmar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sinConfirmar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Contain("fuero de maternidad");

        var confirmado = await client.PostAsJsonAsync("/api/liquidaciones", new
        {
            empleadoId = empleadaId, fechaTerminacion = fin, tipoTerminacion = (int)TipoTerminacion.Despido,
            diasSalarioPendiente = 0m, confirmaFueroMaternidad = true,
        });
        confirmado.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
