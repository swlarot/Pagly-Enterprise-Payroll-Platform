// ====================================================================
// Datos sensibles con confirmación de otra persona, de punta a punta:
// con la opción activa, un cambio de salario o una baja no se aplican al
// guardarlos; otra persona con permiso los confirma o los rechaza con su
// razón. Quien los pidió no puede confirmarlos. Sin la opción, todo sigue
// como antes.
// ====================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class CambiosSensiblesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CambiosSensiblesTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient Cliente(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static EmpleadoActualizarDto Datos(string nombre, decimal salario) =>
        new(Nombre: nombre, Apellido: "Prueba", Email: null, SalarioBase: salario, EstaActivo: true, DepartamentoId: null, PosicionId: null);

    private static async Task<decimal> SalarioAsync(HttpClient c, int id) =>
        (await c.GetFromJsonAsync<JsonElement>($"/api/empleados/{id}")).GetProperty("salarioBase").GetDecimal();

    private static async Task<JsonElement> CambioAsync(HttpClient c, int empleadoId, string tipo) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/cambios-pendientes")).EnumerateArray()
            .First(x => x.GetProperty("empleadoId").GetInt32() == empleadoId && x.GetProperty("tipo").GetString() == tipo);

    [Fact]
    public async Task LaPersonaAfectadaNoConfirmaLoSuyo_YQuienSoloDaDeBajaNoVeSalarios()
    {
        var tokenDueno = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Colegio Separación E2E");
        var dueno = Cliente(tokenDueno);
        (await dueno.PutAsJsonAsync("/api/cambios-pendientes/opcion", new { activo = true })).EnsureSuccessStatusCode();

        var r = await dueno.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto("Carla", "Prueba", $"8-{Guid.NewGuid().ToString()[..4]}-4", null, 1000m, null, null));
        var carla = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Carla también administra empleados, y su usuario es el de su ficha.
        var (tokenCarla, userCarla, _) = await TestTenantHelper.CreateCompanionAsync(_factory, tokenDueno, "Carla Prueba", carla,
            SystemPermission.EmployeesRead, SystemPermission.EmployeesUpdate);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Vorluno.Planilla.Infrastructure.Data.ApplicationDbContext>();
            var ficha = await db.Empleados.FindAsync(carla);
            ficha!.UserId = userCarla;
            await db.SaveChangesAsync();
        }
        var (tokenBajas, _, _) = await TestTenantHelper.CreateCompanionAsync(_factory, tokenDueno, "Solo Bajas", null, SystemPermission.EmployeesDelete);

        (await dueno.PutAsJsonAsync($"/api/empleados/{carla}", Datos("Carla", 1500m))).StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Carla ve el cambio pero no puede confirmar su propio aumento, ni le llega el aviso para hacerlo.
        var carlaCliente = Cliente(tokenCarla);
        var cambio = await CambioAsync(carlaCliente, carla, "Salario");
        cambio.GetProperty("puedoConfirmar").GetBoolean().Should().BeFalse();
        (await carlaCliente.PostAsync($"/api/cambios-pendientes/{cambio.GetProperty("id").GetInt32()}/confirmar", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await carlaCliente.GetFromJsonAsync<JsonElement>("/api/avisos")).GetProperty("noLeidos").GetInt32().Should().Be(0);
        (await SalarioAsync(dueno, carla)).Should().Be(1000m);

        // Quien solo da de baja no ve cambios de salario (ni sus montos).
        (await Cliente(tokenBajas).GetFromJsonAsync<JsonElement>("/api/cambios-pendientes")).EnumerateArray()
            .Should().NotContain(x => x.GetProperty("tipo").GetString() == "Salario");
    }

    [Fact]
    public async Task SalarioYBaja_EsperanAOtraPersona()
    {
        var tokenDueno = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Colegio Cambios Sensibles E2E");
        var dueno = Cliente(tokenDueno);
        var (tokenAdmin, _, correoAdmin) = await TestTenantHelper.CreateCompanionAsync(_factory, tokenDueno, "Administradora Gómez", null,
            SystemPermission.EmployeesRead, SystemPermission.EmployeesUpdate, SystemPermission.EmployeesDelete);
        var admin = Cliente(tokenAdmin);

        async Task<int> Crear(string nombre)
        {
            var r = await dueno.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto(nombre, "Prueba", $"8-{Guid.NewGuid().ToString()[..4]}-2", null, 800m, null, null));
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }
        var ana = await Crear("Ana");
        var beto = await Crear("Beto");

        // Sin la opción, el salario cambia al guardar (como siempre).
        (await dueno.PutAsJsonAsync($"/api/empleados/{ana}", Datos("Ana", 810m))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SalarioAsync(dueno, ana)).Should().Be(810m);

        // Solo el dueño activa la opción.
        (await admin.PutAsJsonAsync("/api/cambios-pendientes/opcion", new { activo = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await dueno.PutAsJsonAsync("/api/cambios-pendientes/opcion", new { activo = true })).StatusCode.Should().Be(HttpStatusCode.OK);

        // 1) El dueño sube el salario: se guarda lo demás, el salario queda pendiente.
        var pide = await dueno.PutAsJsonAsync($"/api/empleados/{ana}", Datos("Ana María", 900m));
        pide.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await pide.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cambioPendiente").GetBoolean().Should().BeTrue();
        var ficha = await dueno.GetFromJsonAsync<JsonElement>($"/api/empleados/{ana}");
        ficha.GetProperty("salarioBase").GetDecimal().Should().Be(810m);
        ficha.GetProperty("nombre").GetString().Should().Be("Ana María");
        (await dueno.PutAsJsonAsync($"/api/empleados/{ana}", Datos("Ana María", 950m))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "ya hay uno esperando");

        // 2) Quien lo pidió no lo confirma; la administradora sí, avisada en la campana y por correo.
        var cambio = await CambioAsync(dueno, ana, "Salario");
        cambio.GetProperty("puedoConfirmar").GetBoolean().Should().BeFalse();
        (await dueno.PostAsync($"/api/cambios-pendientes/{cambio.GetProperty("id").GetInt32()}/confirmar", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await CambioAsync(admin, ana, "Salario")).GetProperty("puedoConfirmar").GetBoolean().Should().BeTrue();
        (await admin.GetFromJsonAsync<JsonElement>("/api/avisos")).GetProperty("noLeidos").GetInt32().Should().Be(1);
        _factory.Correos.Avisos.Should().Contain(c => c.Para == correoAdmin && c.Asunto.Contains("Cambio de salario por confirmar"));

        (await admin.PostAsync($"/api/cambios-pendientes/{cambio.GetProperty("id").GetInt32()}/confirmar", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SalarioAsync(dueno, ana)).Should().Be(900m);
        (await dueno.GetFromJsonAsync<JsonElement>("/api/avisos")).GetProperty("items")[0]
            .GetProperty("titulo").GetString().Should().Contain("confirmado");

        // 3) La baja de Beto: pendiente, la administradora la rechaza con su razón y Beto sigue activo.
        var baja = await dueno.PostAsJsonAsync($"/api/empleados/{beto}/force-delete", new { confirmed = true });
        baja.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await dueno.GetFromJsonAsync<JsonElement>($"/api/empleados/{beto}")).GetProperty("estaActivo").GetBoolean().Should().BeTrue();
        var cambioBaja = (await CambioAsync(admin, beto, "Baja")).GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync($"/api/cambios-pendientes/{cambioBaja}/rechazar", new { comentario = " " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "hay que decir por qué");
        (await admin.PostAsJsonAsync($"/api/cambios-pendientes/{cambioBaja}/rechazar", new { comentario = "Tiene un préstamo sin cerrar" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await dueno.GetFromJsonAsync<JsonElement>($"/api/empleados/{beto}")).GetProperty("estaActivo").GetBoolean().Should().BeTrue();
        (await CambioAsync(dueno, beto, "Baja")).GetProperty("comentario").GetString().Should().Be("Tiene un préstamo sin cerrar");

        // 4) Quien pidió un cambio lo puede retirar; el salario no se toca.
        await dueno.PutAsJsonAsync($"/api/empleados/{ana}", Datos("Ana María", 950m));
        var otro = (await CambioAsync(dueno, ana, "Salario")).GetProperty("id").GetInt32();
        (await admin.PostAsync($"/api/cambios-pendientes/{otro}/retirar", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await dueno.PostAsync($"/api/cambios-pendientes/{otro}/retirar", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SalarioAsync(dueno, ana)).Should().Be(900m);
    }
}
