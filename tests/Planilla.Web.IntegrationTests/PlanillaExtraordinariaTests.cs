// ====================================================================
// Planilla extraordinaria, de punta a punta por HTTP: solo para las
// personas elegidas (las que se quedaron fuera), avisa si alguna ya cobró
// ese período, y se calcula como si hubieran estado en la original.
// ====================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class PlanillaExtraordinariaTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PlanillaExtraordinariaTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> CrearEmpleadoAsync(HttpClient client, string nombre)
    {
        var dto = new EmpleadoCrearDto(
            Nombre: nombre, Apellido: "Prueba",
            NumeroIdentificacion: $"8-{Guid.NewGuid().ToString()[..4]}-3",
            Email: null, SalarioBase: 700m, DepartamentoId: null, PosicionId: null);
        var r = await client.PostAsJsonAsync("/api/empleados", dto);
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task SoloLasPersonasElegidas_AvisaSiYaCobraron_YSeCalculaComoLaOriginal()
    {
        var client = await ClienteAsync("Colegio Extraordinaria E2E");
        var ana = await CrearEmpleadoAsync(client, "Ana");
        var beto = await CrearEmpleadoAsync(client, "Beto");
        var carla = await CrearEmpleadoAsync(client, "Carla");

        // La quincena regular se calcula con los tres.
        var regular = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "", PeriodStartDate = "2026-05-01", PeriodEndDate = "2026-05-15", PayPeriodType = 2,
        });
        var regularId = (await regular.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/payrollheaders/{regularId}/calculate", null)).EnsureSuccessStatusCode();
        var brutoRegular = (await client.GetFromJsonAsync<JsonElement>($"/api/payrollheaders/{regularId}/details"))
            .GetProperty("items").EnumerateArray().First().GetProperty("grossPay").GetDecimal();

        // 1) Sin personas no se crea.
        (await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "", PeriodStartDate = "2026-05-01", PeriodEndDate = "2026-05-15", PayPeriodType = 2,
            EsExtraordinaria = true, EmpleadoIds = Array.Empty<int>(),
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2) Antes de crear: avisa que Ana ya está en la quincena regular.
        var verificar = await client.PostAsJsonAsync("/api/payrollheaders/extraordinaria/verificar", new
        {
            PeriodStartDate = "2026-05-01", PeriodEndDate = "2026-05-15", EmpleadoIds = new[] { ana },
        });
        var avisos = (await verificar.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        avisos.Should().ContainSingle();
        avisos[0].GetProperty("planillas")[0].GetString().Should().Contain("01/05–15/05").And.Contain("calculada");

        // 3) Un empleado nuevo que se olvidó: Diego, contratado y fuera de la regular.
        var diego = await CrearEmpleadoAsync(client, "Diego");
        (await (await client.PostAsJsonAsync("/api/payrollheaders/extraordinaria/verificar", new
        {
            PeriodStartDate = "2026-05-01", PeriodEndDate = "2026-05-15", EmpleadoIds = new[] { diego },
        })).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0, "Diego no cobró ese período");

        var crear = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "", PeriodStartDate = "2026-05-01", PeriodEndDate = "2026-05-15", PayPeriodType = 2,
            EsExtraordinaria = true, EmpleadoIds = new[] { diego },
        });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);
        var ext = await crear.Content.ReadFromJsonAsync<JsonElement>();
        ext.GetProperty("esExtraordinaria").GetBoolean().Should().BeTrue();
        ext.GetProperty("horasGeneradas").GetInt32().Should().Be(1, "solo la persona elegida");
        var extId = ext.GetProperty("id").GetInt32();

        // 4) Calcula solo a Diego, igual que si hubiera estado en la regular.
        (await client.PostAsync($"/api/payrollheaders/{extId}/calculate", null)).EnsureSuccessStatusCode();
        var detalles = await client.GetFromJsonAsync<JsonElement>($"/api/payrollheaders/{extId}/details");
        detalles.GetProperty("total").GetInt32().Should().Be(1);
        var fila = detalles.GetProperty("items")[0];
        fila.GetProperty("empleadoId").GetInt32().Should().Be(diego);
        fila.GetProperty("grossPay").GetDecimal().Should().Be(brutoRegular);

        // 5) En la lista del mes aparece marcada como extraordinaria.
        var mes = (await client.GetFromJsonAsync<JsonElement>("/api/payrollheaders?anio=2026&mes=5")).EnumerateArray().ToList();
        mes.Single(p => p.GetProperty("id").GetInt32() == extId).GetProperty("esExtraordinaria").GetBoolean().Should().BeTrue();
        mes.Single(p => p.GetProperty("id").GetInt32() == regularId).GetProperty("esExtraordinaria").GetBoolean().Should().BeFalse();

        // 6) Los demás no entran aunque se pida generar las horas por defecto.
        await client.PostAsync($"/api/payrollheaders/{extId}/hours/generate-defaults", null);
        (await client.GetFromJsonAsync<JsonElement>($"/api/payrollheaders/{extId}/hours")).GetArrayLength()
            .Should().Be(1, $"Beto ({beto}) y Carla ({carla}) no se cuelan en la extraordinaria");
    }
}
