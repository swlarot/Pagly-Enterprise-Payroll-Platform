// ====================================================================
// Reloj conectado por internet (ADMS / PUSH de ZKTeco), de punta a punta:
// se registra el número de serie, su primera conexión queda pendiente con
// su IP hasta que alguien de la empresa la confirma, y desde entonces solo
// se aceptan envíos desde esa IP. Cada marca cae en la empresa del reloj y
// en la ficha de la persona. Un reloj desconocido, sin vincular, desde otra
// IP o desactivado no entra.
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

    /// <summary>El reloj no inicia sesión; la IP la añade el proxy en X-Forwarded-For.</summary>
    private HttpClient Reloj(string ipSegunElProxy)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Forwarded-For", ipSegunElProxy);
        return c;
    }

    private static StringContent Lineas(string texto) => new(texto, Encoding.UTF8, "text/plain");

    private static async Task<JsonElement> DispositivoAsync(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/reloj/dispositivos")).EnumerateArray().Single();

    [Fact]
    public async Task ElRelojSeVinculaPorIpYCadaMarcaCaeEnSuEmpresa()
    {
        var colegio = await ClienteAsync("Colegio ADMS E2E");
        var otra = await ClienteAsync("Otra Empresa ADMS E2E");
        var desdeElColegio = Reloj("190.140.10.20");

        var r = await colegio.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto(
            "Marta", "Herrera", $"8-{Guid.NewGuid().ToString()[..4]}-5", null, 900m, null, null));
        var marta = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await colegio.PutAsJsonAsync("/api/reloj/codigos", new { codigo = "7", empleadoId = marta })).EnsureSuccessStatusCode();

        var sn = $"ZK{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        var cuerpo = "7\t2026-10-05 07:01:00\t0\t1\t0\t0\t0\n7\t2026-10-05 14:40:00\t1\t1\t0\t0\t0\n88\t2026-10-05 08:00:00\t0\t1\t0\t0\t0\n";

        // 1) Sin registrar, no entra.
        (await desdeElColegio.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2) Registrado: su primera conexión queda pendiente con la IP a la vista; todavía no entra nada.
        (await colegio.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn.ToLowerInvariant(), nombre = "Entrada principal" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await otra.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn, nombre = "Copia" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "un reloj pertenece a una sola empresa");

        (await desdeElColegio.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await desdeElColegio.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas(cuerpo))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var pendiente = await DispositivoAsync(colegio);
        pendiente.GetProperty("ipPendiente").GetString().Should().Be("190.140.10.20");
        pendiente.GetProperty("marcasRecibidas").GetInt32().Should().Be(0);

        // 3) Confirmada la IP: el saludo le pide solo marcaciones y el envío entra.
        var id = pendiente.GetProperty("id").GetInt32();
        (await colegio.PostAsync($"/api/reloj/dispositivos/{id}/confirmar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saludo = await desdeElColegio.GetAsync($"/iclock/cdata?SN={sn}&options=all&pushver=2.4.1");
        saludo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saludo.Content.ReadAsStringAsync()).Should().Contain($"GET OPTION FROM: {sn}").And.Contain("TransFlag=TransData AttLog");

        var envio = await desdeElColegio.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo));
        (await envio.Content.ReadAsStringAsync()).Should().Be("OK: 3");
        await desdeElColegio.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo));
        (await (await desdeElColegio.PostAsync($"/iclock/cdata?SN={sn}&table=OPERLOG", Lineas("OPLOG 4\t0\t2026-10-05 07:00:00")))
            .Content.ReadAsStringAsync()).Should().Be("OK");

        // 4) Alguien que conoce el número de serie, desde otra IP, no entra; ni inventando X-Forwarded-For.
        var intruso = Reloj("200.50.60.70");
        (await intruso.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-05 03:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var disfrazado = Reloj("190.140.10.20, 200.50.60.70"); // el proxy añade la IP real al final
        (await disfrazado.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-05 03:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await DispositivoAsync(colegio)).GetProperty("ipRechazada").GetString().Should().Be("200.50.60.70");

        // 5) Las marcas están en el colegio, en la ficha de Marta, sin duplicar, y en ningún otro lado.
        var res = await colegio.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        var m = res.GetProperty("empleados").EnumerateArray().Single(e => e.GetProperty("empleadoId").GetInt32() == marta);
        m.GetProperty("dias")[0].GetProperty("entrada").GetString().Should().Be("07:01");
        m.GetProperty("dias")[0].GetProperty("salida").GetString().Should().Be("14:40");
        m.GetProperty("dias")[0].GetProperty("marcas").GetInt32().Should().Be(2, "reenviar no duplica y el intruso no entró");
        res.GetProperty("codigosSinAsignar").EnumerateArray().Select(c => c.GetProperty("codigo").GetString()).Should().Equal("88");

        var ajena = await otra.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        ajena.GetProperty("empleados").GetArrayLength().Should().Be(0);
        ajena.GetProperty("codigosSinAsignar").GetArrayLength().Should().Be(0);

        // 6) Si el colegio cambia de internet, se acepta la nueva dirección a mano.
        (await colegio.PostAsync($"/api/reloj/dispositivos/{id}/aceptar-ip", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await intruso.GetAsync($"/iclock/getrequest?SN={sn}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await desdeElColegio.GetAsync($"/iclock/getrequest?SN={sn}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "solo la IP aceptada");

        // 7) Desactivado, deja de aceptar envíos.
        (await colegio.PostAsync($"/api/reloj/dispositivos/{id}/desactivar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await intruso.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-06 07:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
