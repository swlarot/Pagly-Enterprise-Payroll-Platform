// ====================================================================
// Reloj conectado por internet (ADMS / PUSH de ZKTeco), de punta a punta:
// se registra el número de serie, el reloj saluda y envía sus marcas sin
// iniciar sesión, y cada marca cae en la empresa del reloj y en la ficha
// de la persona. Un reloj desconocido o desactivado no entra.
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

public class RelojConexionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RelojConexionTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Lineas(string texto) => new(texto, Encoding.UTF8, "text/plain");

    [Fact]
    public async Task ElRelojEnviaSoloYCadaMarcaCaeEnSuEmpresa()
    {
        var colegio = await ClienteAsync("Colegio ADMS E2E");
        var otra = await ClienteAsync("Otra Empresa ADMS E2E");
        var reloj = _factory.CreateClient(); // el reloj no inicia sesión

        var r = await colegio.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto(
            "Marta", "Herrera", $"8-{Guid.NewGuid().ToString()[..4]}-5", null, 900m, null, null));
        var marta = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await colegio.PutAsJsonAsync("/api/reloj/codigos", new { codigo = "7", empleadoId = marta })).EnsureSuccessStatusCode();

        var sn = $"ZK{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

        // 1) Sin registrar, el reloj no entra (y conserva sus marcas para reintentar).
        (await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2) Registrado: el saludo le pide solo marcaciones.
        (await colegio.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn.ToLowerInvariant(), nombre = "Entrada principal" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await otra.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn, nombre = "Copia" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "un reloj pertenece a una sola empresa");

        var saludo = await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all&pushver=2.4.1");
        saludo.StatusCode.Should().Be(HttpStatusCode.OK);
        var texto = await saludo.Content.ReadAsStringAsync();
        texto.Should().Contain($"GET OPTION FROM: {sn}").And.Contain("TransFlag=TransData AttLog");

        // 3) Envía dos marcas de Marta (número 7) y una de alguien sin asociar.
        var cuerpo = "7\t2026-10-05 07:01:00\t0\t1\t0\t0\t0\n7\t2026-10-05 14:40:00\t1\t1\t0\t0\t0\n88\t2026-10-05 08:00:00\t0\t1\t0\t0\t0\n";
        var envio = await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo));
        (await envio.Content.ReadAsStringAsync()).Should().Be("OK: 3");

        // Reenviar lo mismo no duplica; otras tablas se ignoran.
        await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo));
        (await (await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=OPERLOG", Lineas("OPLOG 4\t0\t2026-10-05 07:00:00")))
            .Content.ReadAsStringAsync()).Should().Be("OK");
        (await reloj.GetAsync($"/iclock/getrequest?SN={sn}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // 4) Las marcas están en el colegio, en la ficha de Marta, y en ningún otro lado.
        var res = await colegio.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        var m = res.GetProperty("empleados").EnumerateArray().Single(e => e.GetProperty("empleadoId").GetInt32() == marta);
        m.GetProperty("dias")[0].GetProperty("entrada").GetString().Should().Be("07:01");
        m.GetProperty("dias")[0].GetProperty("salida").GetString().Should().Be("14:40");
        m.GetProperty("dias")[0].GetProperty("marcas").GetInt32().Should().Be(2, "reenviar no duplica");
        res.GetProperty("codigosSinAsignar").EnumerateArray().Select(c => c.GetProperty("codigo").GetString()).Should().Equal("88");

        var ajena = await otra.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        ajena.GetProperty("empleados").GetArrayLength().Should().Be(0);
        ajena.GetProperty("codigosSinAsignar").GetArrayLength().Should().Be(0);

        var dispositivos = (await colegio.GetFromJsonAsync<JsonElement>("/api/reloj/dispositivos")).EnumerateArray().ToList();
        dispositivos.Single().GetProperty("marcasRecibidas").GetInt32().Should().Be(3);

        // 5) Desactivado, deja de aceptar envíos.
        var id = dispositivos.Single().GetProperty("id").GetInt32();
        (await colegio.PostAsync($"/api/reloj/dispositivos/{id}/desactivar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-06 07:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
