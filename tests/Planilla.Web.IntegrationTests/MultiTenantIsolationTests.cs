using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.DTOs.Auth;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class MultiTenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MultiTenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> RegisterTenantAsync(string companyName)
    {
        var client = _factory.CreateClient();
        var registerDto = new RegisterDto
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "Test@1234",
            CompanyName = companyName,
            RUC = Guid.NewGuid().ToString().Substring(0, 8),
            DV = "12"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", registerDto);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authResponse!.Token;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Empleados_TenantA_CannotSee_TenantB_Data()
    {
        // Arrange - Create 2 tenants
        var tokenA = await RegisterTenantAsync("Tenant A Company");
        var tokenB = await RegisterTenantAsync("Tenant B Company");

        // Create empleado for Tenant A
        var clientA = CreateAuthenticatedClient(tokenA);
        var empleadoA = new EmpleadoCrearDto(
            Nombre: "Employee A",
            Apellido: "From Tenant A",
            NumeroIdentificacion: "A-001",
            SalarioBase: 1000,
            DepartamentoId: null,
            PosicionId: null
        );

        var createResponseA = await clientA.PostAsJsonAsync("/api/empleados", empleadoA);
        createResponseA.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdA = await createResponseA.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Create empleado for Tenant B
        var clientB = CreateAuthenticatedClient(tokenB);
        var empleadoB = new EmpleadoCrearDto(
            Nombre: "Employee B",
            Apellido: "From Tenant B",
            NumeroIdentificacion: "B-001",
            SalarioBase: 2000,
            DepartamentoId: null,
            PosicionId: null
        );

        var createResponseB = await clientB.PostAsJsonAsync("/api/empleados", empleadoB);
        createResponseB.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdB = await createResponseB.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Act - Tenant A gets all empleados
        var listResponse = await clientA.GetAsync("/api/empleados");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var empleados = await listResponse.Content.ReadFromJsonAsync<List<EmpleadoVerDto>>();

        // Assert - Tenant A only sees their own empleado
        empleados.Should().NotBeNull();
        empleados!.Should().ContainSingle(e => e.Id == createdA!.Id, "Tenant A should see their own employee");
        empleados.Should().NotContain(e => e.Id == createdB!.Id, "Tenant A should NOT see Tenant B's employee");
    }

    [Fact]
    public async Task Empleados_TenantA_CannotAccess_TenantB_EmployeeById()
    {
        // Arrange - Create 2 tenants
        var tokenA = await RegisterTenantAsync("Tenant A");
        var tokenB = await RegisterTenantAsync("Tenant B");

        // Tenant B creates empleado
        var clientB = CreateAuthenticatedClient(tokenB);
        var empleadoB = new EmpleadoCrearDto(
            Nombre: "Employee B",
            Apellido: "Belongs to B",
            NumeroIdentificacion: "B-002",
            SalarioBase: 1500,
            DepartamentoId: null,
            PosicionId: null
        );

        var createResponse = await clientB.PostAsJsonAsync("/api/empleados", empleadoB);
        var createdB = await createResponse.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Act - Tenant A tries to access Tenant B's empleado by ID
        var clientA = CreateAuthenticatedClient(tokenA);
        var getResponse = await clientA.GetAsync($"/api/empleados/{createdB!.Id}");

        // Assert - Should return 404 (not 403) to prevent info leak
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cross-tenant access should return 404 to prevent information leakage");
    }

    [Fact]
    public async Task Empleados_TenantA_CannotUpdate_TenantB_Employee()
    {
        // Arrange - Create 2 tenants and Tenant B's employee
        var tokenA = await RegisterTenantAsync("Tenant A");
        var tokenB = await RegisterTenantAsync("Tenant B");

        var clientB = CreateAuthenticatedClient(tokenB);
        var empleadoB = new EmpleadoCrearDto(
            Nombre: "Original Name",
            Apellido: "Original Apellido",
            NumeroIdentificacion: "B-003",
            SalarioBase: 1200,
            DepartamentoId: null,
            PosicionId: null
        );

        var createResponse = await clientB.PostAsJsonAsync("/api/empleados", empleadoB);
        var createdB = await createResponse.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Act - Tenant A tries to update Tenant B's employee
        var clientA = CreateAuthenticatedClient(tokenA);
        var updateDto = new EmpleadoActualizarDto(
            Nombre: "Hacked Name",
            Apellido: "Hacked Apellido",
            SalarioBase: 99999,
            EstaActivo: true,
            DepartamentoId: null,
            PosicionId: null
        );

        var updateResponse = await clientA.PutAsJsonAsync($"/api/empleados/{createdB!.Id}", updateDto);

        // Assert - Should return 404
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cross-tenant update should return 404");

        // Verify employee was NOT modified
        var verifyResponse = await clientB.GetAsync($"/api/empleados/{createdB.Id}");
        var verifiedEmpleado = await verifyResponse.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        verifiedEmpleado!.Nombre.Should().Be("Original Name", "employee should not be modified by other tenant");
        verifiedEmpleado.SalarioBase.Should().Be(1200, "salary should not be modified");
    }

    [Fact]
    public async Task Empleados_TenantA_CannotDelete_TenantB_Employee()
    {
        // Arrange - Create 2 tenants and Tenant B's employee
        var tokenA = await RegisterTenantAsync("Tenant A");
        var tokenB = await RegisterTenantAsync("Tenant B");

        var clientB = CreateAuthenticatedClient(tokenB);
        var empleadoB = new EmpleadoCrearDto(
            Nombre: "Protected Employee",
            Apellido: "Tenant B",
            NumeroIdentificacion: "B-004",
            SalarioBase: 1300,
            DepartamentoId: null,
            PosicionId: null
        );

        var createResponse = await clientB.PostAsJsonAsync("/api/empleados", empleadoB);
        var createdB = await createResponse.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Act - Tenant A tries to delete Tenant B's employee
        var clientA = CreateAuthenticatedClient(tokenA);
        var deleteResponse = await clientA.DeleteAsync($"/api/empleados/{createdB!.Id}");

        // Assert - Should return 404
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cross-tenant delete should return 404");

        // Verify employee still exists
        var verifyResponse = await clientB.GetAsync($"/api/empleados/{createdB.Id}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, "employee should still exist");
    }
}
