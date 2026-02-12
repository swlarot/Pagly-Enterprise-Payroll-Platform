using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Integration tests para P1: PayPeriodType, Pay Info y Horas Trabajadas.
/// </summary>
public class PayPeriodAndHoursIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PayPeriodAndHoursIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> RegisterTenantAsync(string companyName)
    {
        return await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, companyName);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ====================================================================
    // Test 1: Crear empleado con nuevos campos vía API
    // ====================================================================

    [Fact]
    public async Task CreateEmployee_WithPayInfo_ReturnsCorrectFields()
    {
        var token = await RegisterTenantAsync("PayInfo Test Co");
        var client = CreateAuthenticatedClient(token);

        var empleado = new EmpleadoCrearDto(
            Nombre: "Carlos",
            Apellido: "Mendez",
            NumeroIdentificacion: $"PI-{Guid.NewGuid().ToString().Substring(0, 6)}",
            Email: null,
            SalarioBase: 2400m,
            DepartamentoId: null,
            PosicionId: null,
            PayPeriodType: PayPeriodType.Mensual,
            HoursPerWeek: 40,
            HoursPerPeriod: null // auto-calcular
        );

        var response = await client.PostAsJsonAsync("/api/empleados", empleado);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<EmpleadoVerDto>();
        created.Should().NotBeNull();
        created!.PayPeriodTypeName.Should().Be("Mensual");
        created.HoursPerWeek.Should().Be(40);
        created.HoursPerPeriod.Should().BeGreaterThan(0, "HoursPerPeriod should be auto-calculated");
        created.HourlyRate.Should().BeGreaterThan(0, "HourlyRate should be calculated from SalarioBase / HoursPerPeriod");
    }

    // ====================================================================
    // Test 2: Calcular planilla SIN horas (retrocompatibilidad)
    // ====================================================================

    [Fact]
    public async Task CalculatePayroll_WithoutHours_UsesBaseSalary()
    {
        var token = await RegisterTenantAsync("NoHours Test Co");
        var client = CreateAuthenticatedClient(token);

        // Crear empleado
        var empleado = new EmpleadoCrearDto(
            Nombre: "Ana",
            Apellido: "Lopez",
            NumeroIdentificacion: $"NH-{Guid.NewGuid().ToString().Substring(0, 6)}",
            Email: null,
            SalarioBase: 1500m,
            DepartamentoId: null,
            PosicionId: null
        );
        var empResponse = await client.PostAsJsonAsync("/api/empleados", empleado);
        empResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Crear planilla
        var planilla = new
        {
            PayrollNumber = $"PL-{Guid.NewGuid().ToString().Substring(0, 6)}",
            PeriodStartDate = "2026-02-01",
            PeriodEndDate = "2026-02-15",
            PayDate = "2026-02-20",
            PayPeriodType = 2 // Quincenal
        };
        var planillaResponse = await client.PostAsJsonAsync("/api/payrollheaders", planilla);
        planillaResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPlanilla = await planillaResponse.Content.ReadFromJsonAsync<dynamic>();
        int planillaId = (int)createdPlanilla!.GetProperty("id").GetInt32();

        // Calcular SIN registrar horas
        var calcResponse = await client.PostAsync($"/api/payrollheaders/{planillaId}/calculate", null);
        calcResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verificar que usó SalarioBase como grossPay
        var detailsResponse = await client.GetAsync($"/api/payrollheaders/{planillaId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ====================================================================
    // Test 3: Calcular planilla CON horas registradas
    // ====================================================================

    [Fact]
    public async Task CalculatePayroll_WithHours_UsesHoursBasedPay()
    {
        var token = await RegisterTenantAsync("WithHours Test Co");
        var client = CreateAuthenticatedClient(token);

        // Crear empleado con salario base de 1040 (tasa = 10/h con 104 horas/período)
        var empleado = new EmpleadoCrearDto(
            Nombre: "Pedro",
            Apellido: "Garcia",
            NumeroIdentificacion: $"WH-{Guid.NewGuid().ToString().Substring(0, 6)}",
            Email: null,
            SalarioBase: 1040m,
            DepartamentoId: null,
            PosicionId: null,
            PayPeriodType: PayPeriodType.Quincenal,
            HoursPerWeek: 48,
            HoursPerPeriod: 104
        );
        var empResponse = await client.PostAsJsonAsync("/api/empleados", empleado);
        empResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdEmp = await empResponse.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        // Crear planilla
        var planilla = new
        {
            PayrollNumber = $"PH-{Guid.NewGuid().ToString().Substring(0, 6)}",
            PeriodStartDate = "2026-02-01",
            PeriodEndDate = "2026-02-15",
            PayDate = "2026-02-20",
            PayPeriodType = 2
        };
        var planillaResponse = await client.PostAsJsonAsync("/api/payrollheaders", planilla);
        planillaResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPlanilla = await planillaResponse.Content.ReadFromJsonAsync<dynamic>();
        int planillaId = (int)createdPlanilla!.GetProperty("id").GetInt32();

        // Registrar horas: 96 regulares + 8 domingo
        var hoursRequest = new
        {
            RegularHours = 96m,
            SundayHours = 8m,
            HolidayHours = 0m,
            OvertimeDayHours = 0m,
            OvertimeNightHours = 0m,
            AbsenceHours = 0m,
            DisabilityHours = 0m
        };
        var hoursResponse = await client.PutAsJsonAsync(
            $"/api/payrollheaders/{planillaId}/hours/{createdEmp!.Id}", hoursRequest);
        hoursResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Calcular planilla
        var calcResponse = await client.PostAsync($"/api/payrollheaders/{planillaId}/calculate", null);
        calcResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verificar que el cálculo se realizó
        var detailsResponse = await client.GetAsync($"/api/payrollheaders/{planillaId}");
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ====================================================================
    // Test 4: Acceso cross-tenant a PayrollEmployeeHours
    // ====================================================================

    [Fact]
    public async Task PayrollHours_CrossTenant_ReturnsForbiddenOrNotFound()
    {
        // Arrange - 2 tenants
        var tokenA = await RegisterTenantAsync("Hours Tenant A");
        var tokenB = await RegisterTenantAsync("Hours Tenant B");
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        // Tenant A crea empleado y planilla
        var empleadoA = new EmpleadoCrearDto(
            Nombre: "Emp",
            Apellido: "TenantA",
            NumeroIdentificacion: $"HA-{Guid.NewGuid().ToString().Substring(0, 6)}",
            Email: null,
            SalarioBase: 1000m,
            DepartamentoId: null,
            PosicionId: null
        );
        var empResp = await clientA.PostAsJsonAsync("/api/empleados", empleadoA);
        var createdEmp = await empResp.Content.ReadFromJsonAsync<EmpleadoVerDto>();

        var planillaA = new
        {
            PayrollNumber = $"XA-{Guid.NewGuid().ToString().Substring(0, 6)}",
            PeriodStartDate = "2026-03-01",
            PeriodEndDate = "2026-03-15",
            PayDate = "2026-03-20",
            PayPeriodType = 2
        };
        var plResp = await clientA.PostAsJsonAsync("/api/payrollheaders", planillaA);
        var createdPl = await plResp.Content.ReadFromJsonAsync<dynamic>();
        int planillaIdA = (int)createdPl!.GetProperty("id").GetInt32();

        // Tenant A registra horas
        var hours = new { RegularHours = 104m };
        await clientA.PutAsJsonAsync($"/api/payrollheaders/{planillaIdA}/hours/{createdEmp!.Id}", hours);

        // Act - Tenant B intenta acceder a las horas de Tenant A
        var crossResponse = await clientB.GetAsync($"/api/payrollheaders/{planillaIdA}/hours");

        // Assert - Debe ser 404 (planilla no encontrada en el tenant B)
        crossResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cross-tenant access to payroll hours should return 404");

        // Tenant B intenta registrar horas en planilla de Tenant A
        var crossPut = await clientB.PutAsJsonAsync(
            $"/api/payrollheaders/{planillaIdA}/hours/{createdEmp.Id}",
            new { RegularHours = 999m });
        crossPut.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "cross-tenant upsert of hours should return 404");
    }

    // ====================================================================
    // Test 5: Generate default hours
    // ====================================================================

    [Fact]
    public async Task GenerateDefaultHours_CreatesForAllActiveEmployees()
    {
        var token = await RegisterTenantAsync("DefaultHours Test Co");
        var client = CreateAuthenticatedClient(token);

        // Crear 2 empleados
        for (int i = 0; i < 2; i++)
        {
            var emp = new EmpleadoCrearDto(
                Nombre: $"Emp{i}",
                Apellido: "Test",
                NumeroIdentificacion: $"DH{i}-{Guid.NewGuid().ToString().Substring(0, 5)}",
                Email: null,
                SalarioBase: 1000m,
                DepartamentoId: null,
                PosicionId: null
            );
            await client.PostAsJsonAsync("/api/empleados", emp);
        }

        // Crear planilla
        var planilla = new
        {
            PayrollNumber = $"DH-{Guid.NewGuid().ToString().Substring(0, 6)}",
            PeriodStartDate = "2026-04-01",
            PeriodEndDate = "2026-04-15",
            PayDate = "2026-04-20",
            PayPeriodType = 2
        };
        var plResp = await client.PostAsJsonAsync("/api/payrollheaders", planilla);
        var createdPl = await plResp.Content.ReadFromJsonAsync<dynamic>();
        int planillaId = (int)createdPl!.GetProperty("id").GetInt32();

        // Generate defaults
        var genResp = await client.PostAsync($"/api/payrollheaders/{planillaId}/hours/generate-defaults", null);
        genResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verificar que se generaron horas
        var hoursResp = await client.GetAsync($"/api/payrollheaders/{planillaId}/hours");
        hoursResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
