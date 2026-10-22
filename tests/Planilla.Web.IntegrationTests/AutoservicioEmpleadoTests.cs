// ====================================================================
// Autoservicio: quien solo puede editar su propio perfil cambia sus datos
// básicos, pero no su salario, su cargo ni sus datos de planilla.
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

public class AutoservicioEmpleadoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AutoservicioEmpleadoTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>Usuario vinculado a su ficha de empleado, con solo permisos de autoservicio.</summary>
    private async Task<HttpClient> EmpleadaConAutoservicioAsync(string tokenDueno, int empleadoId)
    {
        var (token, _, _) = await TestTenantHelper.CreateCompanionAsync(_factory, tokenDueno, "Empleada", empleadoId,
            SystemPermission.EmployeeUpdateSelf, SystemPermission.EmployeeViewSelf);
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task NoPuedeSubirseElSalario_NiCambiarSuCargo()
    {
        var tokenDueno = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Colegio Autoservicio E2E");
        var dueno = _factory.CreateClient();
        dueno.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDueno);

        async Task<int> Crear(string nombre)
        {
            var r = await dueno.PostAsJsonAsync("/api/empleados", new EmpleadoCrearDto(nombre, "Prueba", $"8-{Guid.NewGuid().ToString()[..4]}-1", null, 800m, null, null));
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }
        var ana = await Crear("Ana");
        var otro = await Crear("Otro");
        var empleada = await EmpleadaConAutoservicioAsync(tokenDueno, ana);

        // Intenta cambiar su nombre (permitido) y subirse el salario y desactivarse (no permitido).
        var cambio = new EmpleadoActualizarDto(
            Nombre: "Ana María", Apellido: "Prueba", Email: null, SalarioBase: 9999m,
            EstaActivo: false, DepartamentoId: null, PosicionId: null, HoursPerWeek: 10);
        (await empleada.PutAsJsonAsync($"/api/empleados/{ana}", cambio)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ficha = await dueno.GetFromJsonAsync<JsonElement>($"/api/empleados/{ana}");
        ficha.GetProperty("nombre").GetString().Should().Be("Ana María");
        ficha.GetProperty("salarioBase").GetDecimal().Should().Be(800m, "el salario no lo cambia quien lo cobra");
        ficha.GetProperty("estaActivo").GetBoolean().Should().BeTrue();
        ficha.GetProperty("hoursPerWeek").GetInt32().Should().Be(48);

        // Y no puede editar a otra persona.
        (await empleada.PutAsJsonAsync($"/api/empleados/{otro}", cambio)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Quien gestiona empleados sí cambia el salario.
        (await dueno.PutAsJsonAsync($"/api/empleados/{ana}", cambio with { SalarioBase = 850m, EstaActivo = true, HoursPerWeek = 48 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await dueno.GetFromJsonAsync<JsonElement>($"/api/empleados/{ana}")).GetProperty("salarioBase").GetDecimal().Should().Be(850m);
    }
}
