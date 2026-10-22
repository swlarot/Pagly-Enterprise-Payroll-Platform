// ====================================================================
// Reloj de asistencia, de punta a punta por HTTP: subir el archivo,
// ver qué trae, asociar números a personas, importar sin duplicar,
// comparar contra el horario y proponer horas extra al director.
// ====================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class RelojAsistenciaTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RelojAsistenciaTests(CustomWebApplicationFactory factory) => _factory = factory;

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
            Nombre: nombre, Apellido: "Docente",
            NumeroIdentificacion: $"8-{Guid.NewGuid().ToString()[..4]}-9",
            Email: null, SalarioBase: 900m, DepartamentoId: null, PosicionId: null);
        var r = await client.PostAsJsonAsync("/api/empleados", dto);
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private const string Archivo =
        "Reporte de marcaciones\n" +
        "AC-No.;Nombre;Fecha/Hora\n" +
        "1;MARTA;05/10/2026 07:02\n" +   // lunes: entra a tiempo…
        "1;MARTA;05/10/2026 15:20\n" +   // …y sale 50 min después de las 14:30
        "2;LUIS;05/10/2026 07:25\n" +    // llega 25 min tarde
        "2;LUIS;05/10/2026 14:35\n" +
        "1;MARTA;06/10/2026 06:58\n" +   // martes normal
        "1;MARTA;06/10/2026 14:31\n" +   // (Luis no marcó: ausente)
        "99;DESCONOCIDO;06/10/2026 08:00\n";

    private static MultipartFormDataContent Formulario(string contenido, string nombre, string? asignaciones = null)
    {
        var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(archivo, "archivo", nombre);
        if (asignaciones != null) form.Add(new StringContent(asignaciones), "asignaciones");
        return form;
    }

    [Fact]
    public async Task DelArchivoAlDirector()
    {
        var client = await ClienteAsync("Colegio Reloj E2E");
        var marta = await CrearEmpleadoAsync(client, "Marta");
        var luis = await CrearEmpleadoAsync(client, "Luis");

        // Horario de docentes: 07:00–14:30, lunes a viernes, 10 min de tolerancia, extra desde 30 min.
        (await client.PostAsJsonAsync("/api/reloj/horarios", new
        {
            nombre = "Docentes", horaEntrada = "07:00", horaSalida = "14:30", diasLaborables = 31,
            toleranciaMinutos = 10, minutosParaHoraExtra = 30, esPredeterminado = true,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        // 1) Vista previa: 7 marcas de 3 números, nadie asociado todavía, nada guardado.
        var prev = await client.PostAsync("/api/reloj/importar/previsualizar", Formulario(Archivo, "marcas.csv"));
        prev.StatusCode.Should().Be(HttpStatusCode.OK);
        var vp = await prev.Content.ReadFromJsonAsync<JsonElement>();
        vp.GetProperty("totalMarcas").GetInt32().Should().Be(7);
        vp.GetProperty("nuevas").GetInt32().Should().Be(7);
        vp.GetProperty("codigos").GetArrayLength().Should().Be(3);
        vp.GetProperty("columnas").GetString().Should().Contain("AC-No.");

        // 2) Importar asociando 1 → Marta y 2 → Luis; el 99 queda sin asignar.
        var asignaciones = JsonSerializer.Serialize(new Dictionary<string, int> { ["1"] = marta, ["2"] = luis });
        var imp = await client.PostAsync("/api/reloj/importar", Formulario(Archivo, "marcas.csv", asignaciones));
        imp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ri = await imp.Content.ReadFromJsonAsync<JsonElement>();
        ri.GetProperty("nuevas").GetInt32().Should().Be(7);
        ri.GetProperty("sinAsignar").GetInt32().Should().Be(1);

        // 3) Volver a subir el mismo archivo no duplica nada.
        var otra = await (await client.PostAsync("/api/reloj/importar", Formulario(Archivo, "marcas.csv")))
            .Content.ReadFromJsonAsync<JsonElement>();
        otra.GetProperty("nuevas").GetInt32().Should().Be(0);
        otra.GetProperty("repetidas").GetInt32().Should().Be(7);

        // 4) Contra el horario: Marta con 50 min extra el lunes, Luis tarde el lunes y ausente el martes.
        var res = await client.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-06");
        var empleados = res.GetProperty("empleados").EnumerateArray().ToList();
        var m = empleados.Single(e => e.GetProperty("empleadoId").GetInt32() == marta);
        var l = empleados.Single(e => e.GetProperty("empleadoId").GetInt32() == luis);
        m.GetProperty("dias")[0].GetProperty("estado").GetString().Should().Be("Completo");
        m.GetProperty("dias")[0].GetProperty("minutosExtra").GetInt32().Should().Be(50);
        m.GetProperty("horasExtraPorProponer").GetInt32().Should().Be(1);
        l.GetProperty("dias")[0].GetProperty("estado").GetString().Should().Be("Tarde");
        l.GetProperty("dias")[0].GetProperty("minutosTarde").GetInt32().Should().Be(25);
        l.GetProperty("dias")[1].GetProperty("estado").GetString().Should().Be("Ausente");

        // Un día que el archivo no cubre no es ausencia: no hay datos.
        var conMiercoles = await client.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-07");
        var luisTres = conMiercoles.GetProperty("empleados").EnumerateArray().Single(e => e.GetProperty("empleadoId").GetInt32() == luis);
        luisTres.GetProperty("dias")[2].GetProperty("estado").GetString().Should().Be("Sin datos");
        luisTres.GetProperty("ausencias").GetInt32().Should().Be(1);
        res.GetProperty("codigosSinAsignar").EnumerateArray().Select(c => c.GetProperty("codigo").GetString()).Should().Equal("99");

        // 5) Proponer al director: una hora extra pendiente, de 14:30 a 15:20, con el motivo del reloj.
        var prop = await client.PostAsJsonAsync("/api/reloj/proponer-horas-extra", new { desde = "2026-10-05", hasta = "2026-10-06" });
        (await prop.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("creadas").GetInt32().Should().Be(1);
        var pendientes = (await client.GetFromJsonAsync<JsonElement>("/api/horasextra?anio=2026&mes=10&estado=pendiente")).EnumerateArray().ToList();
        pendientes.Should().ContainSingle();
        pendientes[0].GetProperty("empleadoId").GetInt32().Should().Be(marta);
        pendientes[0].GetProperty("motivo").GetString().Should().Contain("15:20");
        pendientes[0].GetProperty("horaInicio").GetString().Should().StartWith("14:30");

        // 6) Proponer otra vez no duplica.
        (await (await client.PostAsJsonAsync("/api/reloj/proponer-horas-extra", new { desde = "2026-10-05", hasta = "2026-10-06" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("creadas").GetInt32().Should().Be(0);

        // 7) Un número es de una sola persona; y el .xls antiguo se pide guardar como .xlsx.
        (await client.PutAsJsonAsync("/api/reloj/codigos", new { codigo = "1", empleadoId = luis }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/reloj/codigos", new { codigo = "99", empleadoId = luis }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var xls = await client.PostAsync("/api/reloj/importar/previsualizar", Formulario("x", "viejo.xls"));
        (await xls.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("problemas")[0].GetProperty("mensaje").GetString()
            .Should().Contain(".xlsx");
    }
}
