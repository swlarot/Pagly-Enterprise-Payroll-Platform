// ====================================================================
// Horas extra con decisión del director, de punta a punta por HTTP:
// rechazar no borra, la planilla paga solo lo aprobado, aprobar la
// planilla las marca como pagadas y el reporte interno lo cuenta todo.
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

public class HorasExtraDirectorTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HorasExtraDirectorTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> CrearEmpleadoAsync(HttpClient client)
    {
        var dto = new EmpleadoCrearDto(
            Nombre: "Marta", Apellido: "Docente",
            NumeroIdentificacion: $"8-{Guid.NewGuid().ToString()[..4]}-1",
            Email: null, SalarioBase: 700m, DepartamentoId: null, PosicionId: null);
        var r = await client.PostAsJsonAsync("/api/empleados", dto);
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> RegistrarAsync(HttpClient client, int empleadoId, string fecha, string inicio, string fin)
    {
        var r = await client.PostAsJsonAsync("/api/horasextra", new
        {
            empleadoId,
            fecha,
            tipoHoraExtra = (int)TipoHoraExtra.Diurna,
            horaInicio = inicio,
            horaFin = fin,
            motivo = "Reunión de padres",
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<List<JsonElement>> DelMesAsync(HttpClient client, string estado = "")
    {
        var url = "/api/horasextra?anio=2026&mes=4" + (estado == "" ? "" : $"&estado={estado}");
        return (await client.GetFromJsonAsync<JsonElement>(url)).EnumerateArray().ToList();
    }

    [Fact]
    public async Task ElDirectorDecide_RechazarNoBorra_LaPlanillaPagaSoloLoAprobado()
    {
        var client = await ClienteAsync("Colegio Armonía E2E");
        var empleadoId = await CrearEmpleadoAsync(client);

        var a = await RegistrarAsync(client, empleadoId, "2026-04-06", "15:00:00", "17:00:00");
        var b = await RegistrarAsync(client, empleadoId, "2026-04-07", "15:00:00", "16:00:00");
        var c = await RegistrarAsync(client, empleadoId, "2026-04-08", "15:00:00", "16:30:00");
        var idA = a.GetProperty("id").GetInt32();
        var idB = b.GetProperty("id").GetInt32();
        var idC = c.GetProperty("id").GetInt32();
        a.GetProperty("estado").GetString().Should().Be("Pendiente");

        // 1) Aprueba una, rechaza otra con motivo y deja la tercera pendiente.
        (await client.PostAsync($"/api/horasextra/{idA}/aprobar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync($"/api/horasextra/{idB}/rechazar", new { motivo = "No fue autorizada antes" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delMes = await DelMesAsync(client);
        delMes.Should().HaveCount(3, "rechazar no borra el registro");
        var rechazada = delMes.Single(h => h.GetProperty("id").GetInt32() == idB);
        rechazada.GetProperty("estado").GetString().Should().Be("Rechazada");
        rechazada.GetProperty("motivoRechazo").GetString().Should().Be("No fue autorizada antes");
        rechazada.GetProperty("rechazadoPor").GetString().Should().NotBeNullOrEmpty();

        (await DelMesAsync(client, "pendiente")).Select(h => h.GetProperty("id").GetInt32()).Should().Equal(idC);
        (await client.GetFromJsonAsync<JsonElement>("/api/horasextra/pendientes")).GetArrayLength()
            .Should().Be(1, "las rechazadas no esperan decisión");

        // 2) Una rechazada no se puede eliminar: se conserva como constancia.
        (await client.DeleteAsync($"/api/horasextra/{idB}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3) La planilla de la quincena paga el salario más solo lo aprobado.
        var montoA = delMes.Single(h => h.GetProperty("id").GetInt32() == idA).GetProperty("montoCalculado").GetDecimal();
        var crear = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "",
            PeriodStartDate = "2026-04-01",
            PeriodEndDate = "2026-04-15",
            PayPeriodType = 2,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var planillaId = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/payrollheaders/{planillaId}/calculate", null)).EnsureSuccessStatusCode();

        var detalles = await client.GetFromJsonAsync<JsonElement>($"/api/payrollheaders/{planillaId}/details");
        var bruto = detalles.GetProperty("items")[0].GetProperty("grossPay").GetDecimal();
        bruto.Should().BeApproximately(350m + montoA, 0.05m, "quincena de 700 más la única hora extra aprobada");

        // 4) Si el director cambia de opinión después del cálculo, no se aprueba sin recalcular.
        (await client.PostAsJsonAsync("/api/horasextra/decidir", new { ids = new[] { idC }, decision = "aprobar" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/payrollheaders/{planillaId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsync($"/api/payrollheaders/{planillaId}/calculate", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/payrollheaders/{planillaId}/approve", null)).EnsureSuccessStatusCode();

        // 5) Aprobada la planilla, lo aprobado queda pagado y ya no se cambia.
        var pagadas = await DelMesAsync(client, "pagada");
        pagadas.Select(h => h.GetProperty("id").GetInt32()).Should().BeEquivalentTo(new[] { idA, idC });
        (await client.PostAsJsonAsync($"/api/horasextra/{idA}/rechazar", new { motivo = "tarde" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 6) El reporte interno cuenta todo: registradas, decididas, pagadas y el costo.
        var rep = await client.GetFromJsonAsync<JsonElement>("/api/reportes/horas-extra?mes=4&anio=2026");
        var t = rep.GetProperty("totales");
        t.GetProperty("horasRegistradas").GetDecimal().Should().Be(4.5m);
        t.GetProperty("horasAprobadas").GetDecimal().Should().Be(3.5m);
        t.GetProperty("horasRechazadas").GetDecimal().Should().Be(1m);
        t.GetProperty("horasPendientes").GetDecimal().Should().Be(0m);
        t.GetProperty("montoPagado").GetDecimal().Should().Be(t.GetProperty("montoAprobado").GetDecimal());
        t.GetProperty("cuotasPatronales").GetDecimal().Should().BeGreaterThan(0m);
        t.GetProperty("costoTotal").GetDecimal().Should()
            .Be(t.GetProperty("montoAprobado").GetDecimal() + t.GetProperty("cuotasPatronales").GetDecimal());
        rep.GetProperty("empleados")[0].GetProperty("registros").GetArrayLength().Should().Be(3);

        var excel = await client.GetAsync("/api/reportes/horas-extra/excel?mes=4&anio=2026");
        excel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await excel.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);

        // 7) Anular la planilla libera las horas: vuelven a aprobadas sin pagar.
        (await client.PostAsJsonAsync($"/api/payrollheaders/{planillaId}/cancel", new { reason = "prueba" })).EnsureSuccessStatusCode();
        (await DelMesAsync(client, "aprobada")).Should().HaveCount(2);
    }

    [Fact]
    public async Task DevolverAPendiente_YDecisionInvalida()
    {
        var client = await ClienteAsync("Colegio Pendientes E2E");
        var empleadoId = await CrearEmpleadoAsync(client);
        var h = await RegistrarAsync(client, empleadoId, "2026-04-13", "15:00:00", "16:00:00");
        var id = h.GetProperty("id").GetInt32();

        (await client.PostAsJsonAsync($"/api/horasextra/{id}/rechazar", new { motivo = "duplicada" })).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/horasextra/{id}/pendiente", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tras = (await DelMesAsync(client)).Single();
        tras.GetProperty("estado").GetString().Should().Be("Pendiente");
        tras.GetProperty("motivoRechazo").ValueKind.Should().Be(JsonValueKind.Null);

        (await client.PostAsJsonAsync("/api/horasextra/decidir", new { ids = new[] { id }, decision = "quizas" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/horasextra/decidir", new { ids = new[] { id, 999999 }, decision = "aprobar" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "todo o nada: si una no existe no se decide ninguna");
        (await DelMesAsync(client)).Single().GetProperty("estado").GetString().Should().Be("Pendiente");
    }
}
