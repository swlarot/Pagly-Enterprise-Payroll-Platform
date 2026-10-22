// ====================================================================
// Reloj conectado por internet (ADMS / PUSH de ZKTeco), de punta a punta.
// El protocolo no tiene contraseña: el reloj se vincula a una IP.
//   · Registrado desde la red del colegio, se vincula solo al conectarse
//     desde esa misma red.
//   · Desde otra red, queda pendiente hasta que alguien confirma la IP que
//     vio (si cambió mientras tanto, no se acepta).
// Cada marca cae en la empresa del reloj y en la ficha de la persona.
// Un reloj desconocido, sin vincular, desde otra IP o desactivado no entra.
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
    private const string RedDelColegio = "190.140.10.20";

    private readonly CustomWebApplicationFactory _factory;

    public RelojConexionTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAsync(string empresa, string? ip = null)
    {
        var token = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, empresa);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ip != null) client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    /// <summary>El reloj no inicia sesión; su IP la añade el proxy en X-Forwarded-For.</summary>
    private HttpClient Reloj(string ipSegunElProxy)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Forwarded-For", ipSegunElProxy);
        return c;
    }

    private static StringContent Lineas(string texto) => new(texto, Encoding.UTF8, "text/plain");
    private static string NuevoSn() => $"ZK{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<JsonElement> DispositivoAsync(HttpClient c, string sn) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/reloj/dispositivos")).EnumerateArray()
            .Single(d => d.GetProperty("numeroSerie").GetString() == sn);

    [Fact]
    public async Task RegistradoDesdeElColegio_SeVinculaSolo_YCadaMarcaCaeEnSuEmpresa()
    {
        var colegio = await ClienteAsync("Colegio ADMS E2E", ip: RedDelColegio);
        var otra = await ClienteAsync("Otra Empresa ADMS E2E");
        var reloj = Reloj(RedDelColegio);

        var r = await colegio.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto(
            "Marta", "Herrera", $"8-{Guid.NewGuid().ToString()[..4]}-5", null, 900m, null, null));
        var marta = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await colegio.PutAsJsonAsync("/api/reloj/codigos", new { codigo = "7", empleadoId = marta })).EnsureSuccessStatusCode();

        var sn = NuevoSn();
        var cuerpo = "7\t2026-10-05 07:01:00\t0\t1\t0\t0\t0\n7\t2026-10-05 14:40:00\t1\t1\t0\t0\t0\n88\t2026-10-05 08:00:00\t0\t1\t0\t0\t0\n";

        // 1) Sin registrar, no entra.
        (await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2) Registrado desde la red del colegio: al conectarse desde esa red se vincula solo.
        (await colegio.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn.ToLowerInvariant(), nombre = "Entrada principal" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await otra.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn, nombre = "Copia" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "un reloj pertenece a una sola empresa");

        var saludo = await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all&pushver=2.4.1");
        saludo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saludo.Content.ReadAsStringAsync()).Should().Contain($"GET OPTION FROM: {sn}").And.Contain("TransFlag=TransData AttLog");
        (await DispositivoAsync(colegio, sn)).GetProperty("ipVinculada").GetString().Should().Be(RedDelColegio);

        (await (await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo))).Content.ReadAsStringAsync())
            .Should().Be("OK: 3");
        await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG&Stamp=9999", Lineas(cuerpo));
        (await (await reloj.PostAsync($"/iclock/cdata?SN={sn}&table=OPERLOG", Lineas("OPLOG 4\t0\t2026-10-05 07:00:00")))
            .Content.ReadAsStringAsync()).Should().Be("OK");

        // 3) Quien conoce el número de serie, desde otra IP, no entra; ni inventando X-Forwarded-For.
        var intruso = Reloj("200.50.60.70");
        (await intruso.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-05 03:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var disfrazado = Reloj($"{RedDelColegio}, 200.50.60.70"); // el proxy añade la IP real al final
        (await disfrazado.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-05 03:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4) Las marcas están en el colegio, en la ficha de Marta, sin duplicar, y en ningún otro lado.
        var res = await colegio.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        var m = res.GetProperty("empleados").EnumerateArray().Single(e => e.GetProperty("empleadoId").GetInt32() == marta);
        m.GetProperty("dias")[0].GetProperty("entrada").GetString().Should().Be("07:01");
        m.GetProperty("dias")[0].GetProperty("salida").GetString().Should().Be("14:40");
        m.GetProperty("dias")[0].GetProperty("marcas").GetInt32().Should().Be(2, "reenviar no duplica y el intruso no entró");
        res.GetProperty("codigosSinAsignar").EnumerateArray().Select(c => c.GetProperty("codigo").GetString()).Should().Equal("88");

        var ajena = await otra.GetFromJsonAsync<JsonElement>("/api/reloj/resumen?desde=2026-10-05&hasta=2026-10-05");
        ajena.GetProperty("empleados").GetArrayLength().Should().Be(0);
        ajena.GetProperty("codigosSinAsignar").GetArrayLength().Should().Be(0);

        // 5) Si el colegio cambia de internet, se acepta la nueva IP, pero solo la que se vio en pantalla.
        var dispositivo = await DispositivoAsync(colegio, sn);
        var id = dispositivo.GetProperty("id").GetInt32();
        dispositivo.GetProperty("ipRechazada").GetString().Should().Be("200.50.60.70");
        (await colegio.PostAsJsonAsync($"/api/reloj/dispositivos/{id}/aceptar-ip", new { ip = "1.2.3.4" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "la dirección que se vio no es la que está");
        (await colegio.PostAsJsonAsync($"/api/reloj/dispositivos/{id}/aceptar-ip", new { ip = "200.50.60.70" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await intruso.GetAsync($"/iclock/getrequest?SN={sn}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reloj.GetAsync($"/iclock/getrequest?SN={sn}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "solo la IP aceptada");

        // 6) Desactivado, deja de aceptar envíos.
        (await colegio.PostAsync($"/api/reloj/dispositivos/{id}/desactivar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await intruso.PostAsync($"/iclock/cdata?SN={sn}&table=ATTLOG", Lineas("7\t2026-10-06 07:00:00\t0\t1\t0\t0\t0\n")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConIpPrivada_NoSeVinculaSolo()
    {
        // Si la IP fuera la de la red interna (o el proxy no añadiera la real), no se adivina.
        var colegio = await ClienteAsync("Colegio ADMS IP Privada E2E", ip: "10.0.3.7");
        var reloj = Reloj("10.0.3.7");
        var sn = NuevoSn();
        (await colegio.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn, nombre = "Entrada" })).EnsureSuccessStatusCode();

        (await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await DispositivoAsync(colegio, sn)).GetProperty("ipPendiente").GetString().Should().Be("10.0.3.7");

        Vorluno.Planilla.Infrastructure.Services.RelojAsistenciaService.EsIpPublica("190.140.10.20").Should().BeTrue();
        foreach (var privada in new[] { "10.1.2.3", "172.20.0.1", "192.168.1.10", "127.0.0.1", "::1", "100.64.0.9", "desconocida", "::ffff:10.0.0.1" })
            Vorluno.Planilla.Infrastructure.Services.RelojAsistenciaService.EsIpPublica(privada).Should().BeFalse(privada);
    }

    [Fact]
    public async Task RegistradoDesdeOtraRed_QuedaPendienteHastaConfirmarLaIpVista()
    {
        var colegio = await ClienteAsync("Colegio ADMS Pendiente E2E", ip: "181.1.1.1"); // registra desde su casa
        var reloj = Reloj(RedDelColegio);
        var sn = NuevoSn();

        (await colegio.PostAsJsonAsync("/api/reloj/dispositivos", new { numeroSerie = sn, nombre = "Entrada" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // El reloj se conecta desde otra red: no entra, y su IP queda a la vista para confirmarla.
        (await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var d = await DispositivoAsync(colegio, sn);
        d.GetProperty("ipPendiente").GetString().Should().Be(RedDelColegio);
        d.GetProperty("ipEsperada").GetString().Should().Be("181.1.1.1");
        var id = d.GetProperty("id").GetInt32();

        // Confirmar una IP distinta de la que está no vale; aceptar-ip no sirve para un reloj sin vincular.
        (await colegio.PostAsJsonAsync($"/api/reloj/dispositivos/{id}/confirmar", new { ip = "9.9.9.9" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await colegio.PostAsJsonAsync($"/api/reloj/dispositivos/{id}/aceptar-ip", new { ip = RedDelColegio })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await colegio.PostAsJsonAsync($"/api/reloj/dispositivos/{id}/confirmar", new { ip = RedDelColegio })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await reloj.GetAsync($"/iclock/cdata?SN={sn}&options=all")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
