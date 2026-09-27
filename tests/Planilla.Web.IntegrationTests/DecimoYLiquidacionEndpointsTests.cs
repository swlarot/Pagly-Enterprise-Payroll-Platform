// ====================================================================
// De punta a punta por HTTP: el décimo y la liquidación tal como los usa
// la pantalla —previsualizar sin guardar, crear con lo que se vio, borrar
// el décimo escribiendo su número y anular una liquidación.
// ====================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class DecimoYLiquidacionEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DecimoYLiquidacionEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> CrearEmpleadoAsync(HttpClient client, string sufijo)
    {
        var dto = new EmpleadoCrearDto(
            Nombre: "Belisa", Apellido: $"Belloso{sufijo}",
            NumeroIdentificacion: $"8-{sufijo}-{Guid.NewGuid().ToString()[..4]}",
            Email: null, SalarioBase: 700m, DepartamentoId: null, PosicionId: null);
        var r = await client.PostAsJsonAsync("/api/empleados", dto);
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        return j.GetProperty("id").GetInt32();
    }

    /// <summary>Una quincena aprobada, para que el empleado tenga devengado real.</summary>
    private static async Task PlanillaAprobadaAsync(HttpClient client, int empleadoId, string inicio, string fin)
    {
        var crear = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "",
            PeriodStartDate = inicio,
            PeriodEndDate = fin,
            PayPeriodType = 2,
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Las horas ya se generan solas al crear; se calcula y se aprueba.
        (await client.PostAsync($"/api/payrollheaders/{id}/calculate", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/payrollheaders/{id}/approve", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Decimo__PrevisualizarNoGuarda_CalcularGuarda_BorrarExigeElNumero()
    {
        var client = await ClienteAsync("Décimo E2E Co");
        var empleadoId = await CrearEmpleadoAsync(client, "DEC");
        await PlanillaAprobadaAsync(client, empleadoId, "2026-01-01", "2026-01-15");

        var periodo = new { periodoDesde = "2025-12-16", periodoHasta = "2026-04-15", fechaPago = "2026-04-15" };

        // 1) Previsualizar: hay empleados con sus meses y no se guardó nada.
        var prev = await client.PostAsJsonAsync("/api/decimo/previsualizar", periodo);
        prev.StatusCode.Should().Be(HttpStatusCode.OK);
        var pj = await prev.Content.ReadFromJsonAsync<JsonElement>();
        var empleados = pj.GetProperty("empleados").EnumerateArray().ToList();
        empleados.Should().NotBeEmpty();
        empleados[0].GetProperty("meses").GetArrayLength().Should().Be(5, "diciembre a abril");
        (await client.GetFromJsonAsync<JsonElement>("/api/decimo?ano=2026&mes=4")).GetArrayLength()
            .Should().Be(0, "previsualizar no crea la partida");

        // 2) Crear y calcular con un mes escrito a mano.
        var creada = await client.PostAsJsonAsync("/api/decimo", periodo);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        var partida = await creada.Content.ReadFromJsonAsync<JsonElement>();
        var partidaId = partida.GetProperty("id").GetInt32();
        var numero = partida.GetProperty("numero").GetString()!;

        var calc = await client.PostAsJsonAsync($"/api/decimo/{partidaId}/calcular", new
        {
            ajustes = new[] { new { empleadoId, anio = 2026, mes = 3, monto = 700m } },
        });
        calc.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3) Aparece en el mes de su fecha de pago y no en otro.
        var deAbril = await client.GetFromJsonAsync<JsonElement>("/api/decimo?ano=2026&mes=4");
        deAbril.GetArrayLength().Should().Be(1);
        deAbril[0].GetProperty("totalDecimo").GetDecimal().Should().BeGreaterThan(0m);
        (await client.GetFromJsonAsync<JsonElement>("/api/decimo?ano=2026&mes=8")).GetArrayLength().Should().Be(0);

        // 4) El mes escrito a mano quedó como devengado del empleado.
        var meses = await client.GetFromJsonAsync<JsonElement>($"/api/empleados/{empleadoId}/devengado-mensual?desde=2026-03&hasta=2026-03");
        meses.ToString().Should().Contain("700");

        // 5) Borrar exige el número exacto.
        var mal = await BorrarAsync(client, partidaId, "no-es-el-numero");
        mal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<JsonElement>("/api/decimo?ano=2026&mes=4")).GetArrayLength()
            .Should().Be(1, "con la confirmación mala no se borra nada");

        var bien = await BorrarAsync(client, partidaId, numero);
        bien.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/decimo?ano=2026&mes=4")).GetArrayLength().Should().Be(0);
    }

    private static Task<HttpResponseMessage> BorrarAsync(HttpClient client, int id, string confirmacion)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/decimo/{id}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { confirmacion }), Encoding.UTF8, "application/json"),
        };
        return client.SendAsync(req);
    }

    [Fact]
    public async Task Liquidacion__PrevisualizaConSusBases_SeCreaYSeAnula()
    {
        var client = await ClienteAsync("Liquidación E2E Co");
        var empleadoId = await CrearEmpleadoAsync(client, "LIQ");
        await PlanillaAprobadaAsync(client, empleadoId, "2026-01-01", "2026-01-15");

        var cuerpo = new
        {
            empleadoId,
            fechaTerminacion = "2026-09-30",
            tipoTerminacion = (int)TipoTerminacion.MutuoAcuerdo,
            incluyePreaviso = false,
            diasSalarioPendiente = 15m,
        };

        // 1) Previsualizar: trae el cálculo y de dónde sale cada partida.
        var prev = await client.PostAsJsonAsync("/api/liquidaciones/previsualizar", cuerpo);
        prev.StatusCode.Should().Be(HttpStatusCode.OK);
        var pj = await prev.Content.ReadFromJsonAsync<JsonElement>();
        pj.GetProperty("usaDevengadoReal").GetBoolean().Should().BeTrue("el empleado tiene una planilla aprobada");
        pj.GetProperty("bases").GetProperty("meses").GetArrayLength().Should().BeGreaterThan(0);
        pj.GetProperty("calculo").GetProperty("salarioPendiente").GetDecimal().Should().BeGreaterThan(0m);
        (await client.GetFromJsonAsync<JsonElement>("/api/liquidaciones?anio=2026&mes=9")).GetArrayLength()
            .Should().Be(0, "previsualizar no guarda");

        // 2) Crear: queda en el mes de su último día de trabajo.
        var creada = await client.PostAsJsonAsync("/api/liquidaciones", cuerpo);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        var liq = await creada.Content.ReadFromJsonAsync<JsonElement>();
        var liqId = liq.GetProperty("id").GetInt32();

        var deSeptiembre = await client.GetFromJsonAsync<JsonElement>("/api/liquidaciones?anio=2026&mes=9");
        deSeptiembre.GetArrayLength().Should().Be(1);
        (await client.GetFromJsonAsync<JsonElement>("/api/liquidaciones?anio=2026&mes=8")).GetArrayLength().Should().Be(0);

        // 3) Anular: se conserva, marcada, y el empleado queda libre para liquidarse otra vez.
        var anular = await client.PostAsJsonAsync($"/api/liquidaciones/{liqId}/anular", new { motivo = "fecha equivocada" });
        anular.StatusCode.Should().Be(HttpStatusCode.OK);

        var tras = await client.GetFromJsonAsync<JsonElement>("/api/liquidaciones?anio=2026&mes=9");
        tras.GetArrayLength().Should().Be(1);
        tras[0].GetProperty("estado").GetInt32().Should().Be((int)EstadoLiquidacion.Anulada);

        (await client.PostAsJsonAsync("/api/liquidaciones", cuerpo)).StatusCode
            .Should().Be(HttpStatusCode.Created, "una anulada no bloquea una nueva liquidación");
    }

    [Fact]
    public async Task ElSipeMensualIncluyeLaPlanillaAprobadaDelMes()
    {
        var client = await ClienteAsync("SIPE E2E Co");
        var empleadoId = await CrearEmpleadoAsync(client, "SIP");
        await PlanillaAprobadaAsync(client, empleadoId, "2026-04-01", "2026-04-15");

        var r = await client.GetFromJsonAsync<JsonElement>("/api/reportes/sipe?mes=4&anio=2026");

        r.GetProperty("empleados").GetArrayLength().Should().Be(1);
        r.GetProperty("fuentes").GetArrayLength().Should().Be(1);
        r.GetProperty("totales").GetProperty("totalSalarios").GetDecimal().Should().BeGreaterThan(0m);
        r.GetProperty("periodo").GetString().Should().Be("abril 2026");

        // Un mes sin nada no trae a nadie.
        var vacio = await client.GetFromJsonAsync<JsonElement>("/api/reportes/sipe?mes=2&anio=2026");
        vacio.GetProperty("empleados").GetArrayLength().Should().Be(0);
    }
}
