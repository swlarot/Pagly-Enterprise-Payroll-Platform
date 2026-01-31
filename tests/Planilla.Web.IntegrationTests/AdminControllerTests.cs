using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs.Admin;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Integration tests for AdminController endpoints
/// Verifica que solo SystemAdmins puedan acceder a endpoints de administración
/// </summary>
public class AdminControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Crea un SystemAdmin y retorna su token JWT
    /// </summary>
    private async Task<string> CreateSystemAdminAndGetTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"sysadmin-{Guid.NewGuid()}@vorluno.com";
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = "System Administrator",
            IsSystemAdmin = true
        };

        var result = await userManager.CreateAsync(user, "Admin@1234");
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create system admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Login para obtener token
        var client = _factory.CreateClient();
        var loginDto = new LoginDto
        {
            Email = email,
            Password = "Admin@1234"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        return authResponse!.Token;
    }

    /// <summary>
    /// Crea un tenant regular y retorna el token del Owner
    /// </summary>
    private async Task<(string Token, int TenantId)> CreateRegularTenantAsync()
    {
        var client = _factory.CreateClient();

        // Crear tenant a través del SystemAdmin
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createTenantDto = new CreateTenantDto
        {
            Name = $"Test Company {Guid.NewGuid()}",
            RUC = Guid.NewGuid().ToString().Substring(0, 8),
            DV = "12",
            OwnerEmail = $"owner-{Guid.NewGuid()}@example.com",
            OwnerPassword = "Owner@1234",
            OwnerFullName = "Test Owner",
            CompanyEmail = $"company-{Guid.NewGuid()}@example.com"
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/admin/tenants", createTenantDto);
        createResponse.EnsureSuccessStatusCode();
        var tenantDto = await createResponse.Content.ReadFromJsonAsync<AdminTenantDto>();

        // Login como Owner para obtener token
        var loginDto = new LoginDto
        {
            Email = createTenantDto.OwnerEmail,
            Password = createTenantDto.OwnerPassword
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        return (authResponse!.Token, tenantDto!.Id);
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ========================================================================
    // TEST 1: SystemAdmin puede listar todos los tenants
    // ========================================================================
    [Fact]
    public async Task GetAllTenants_AsSystemAdmin_Returns200WithTenants()
    {
        // Arrange - Crear SystemAdmin y algunos tenants
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var (_, tenantId1) = await CreateRegularTenantAsync();
        var (_, tenantId2) = await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        // Act - Listar todos los tenants
        var response = await adminClient.GetAsync("/api/admin/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<List<AdminTenantDto>>();

        tenants.Should().NotBeNull();
        tenants!.Count.Should().BeGreaterOrEqualTo(2, "should have at least the 2 tenants we created");

        // Verificar que los tenants creados están en la lista
        tenants.Should().Contain(t => t.Id == tenantId1);
        tenants.Should().Contain(t => t.Id == tenantId2);
    }

    // ========================================================================
    // TEST 2: Usuario regular NO puede acceder a endpoints de admin
    // ========================================================================
    [Fact]
    public async Task GetAllTenants_AsRegularUser_Returns403Forbidden()
    {
        // Arrange - Crear tenant regular
        var (ownerToken, _) = await CreateRegularTenantAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);

        // Act - Intentar listar todos los tenants como Owner regular
        var response = await ownerClient.GetAsync("/api/admin/tenants");

        // Assert - Debería retornar 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "regular tenant Owner should not have access to system admin endpoints");
    }

    // ========================================================================
    // TEST 3: SystemAdmin puede ver usuarios de cualquier tenant
    // ========================================================================
    [Fact]
    public async Task GetTenantUsers_AsSystemAdmin_Returns200WithUsers()
    {
        // Arrange - Crear SystemAdmin y tenant
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var (_, tenantId) = await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        // Act - Listar usuarios del tenant
        var response = await adminClient.GetAsync($"/api/admin/tenants/{tenantId}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<AdminTenantUserDto>>();

        users.Should().NotBeNull();
        users!.Count.Should().BeGreaterOrEqualTo(1, "tenant should have at least the Owner user");
        users.Should().Contain(u => u.Role == TenantRole.Owner, "tenant should have an Owner");
    }

    // ========================================================================
    // TEST 4: SystemAdmin puede actualizar suscripción de tenant
    // ========================================================================
    [Fact]
    public async Task UpdateTenantSubscription_AsSystemAdmin_Returns200()
    {
        // Arrange - Crear SystemAdmin y tenant
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var (_, tenantId) = await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        // Act - Actualizar plan a Enterprise
        var updateDto = new UpdateTenantSubscriptionDto
        {
            Plan = SubscriptionPlan.Enterprise,
            ExtendTrialDays = 30
        };

        var response = await adminClient.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/subscription", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTenant = await response.Content.ReadFromJsonAsync<AdminTenantDto>();

        updatedTenant.Should().NotBeNull();
        updatedTenant!.Subscription.Should().NotBeNull();
        updatedTenant.Subscription!.Plan.Should().Be(SubscriptionPlan.Enterprise);
    }

    // ========================================================================
    // TEST 5: Usuario sin autenticación NO puede acceder a endpoints de admin
    // ========================================================================
    [Fact]
    public async Task GetAllTenants_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange - Cliente sin autenticación
        var client = _factory.CreateClient();

        // Act - Intentar acceder a endpoint de admin sin token
        var response = await client.GetAsync("/api/admin/tenants");

        // Assert - Debería retornar 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated requests should be rejected");
    }

    // ========================================================================
    // TEST 6: SystemAdmin puede obtener métricas del sistema
    // ========================================================================
    [Fact]
    public async Task GetSystemMetrics_AsSystemAdmin_Returns200WithMetrics()
    {
        // Arrange - Crear SystemAdmin y algunos tenants
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        await CreateRegularTenantAsync();
        await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        // Act - Obtener métricas del sistema
        var response = await adminClient.GetAsync("/api/admin/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var metrics = await response.Content.ReadFromJsonAsync<SystemMetricsDto>();

        metrics.Should().NotBeNull();
        metrics!.TotalTenants.Should().BeGreaterOrEqualTo(2);
        metrics.PlanDistribution.Should().NotBeNull();
    }

    // ========================================================================
    // TEST 7: SystemAdmin puede crear nuevo tenant con Owner
    // ========================================================================
    [Fact]
    public async Task CreateTenant_AsSystemAdmin_Returns201WithTenant()
    {
        // Arrange
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        var createDto = new CreateTenantDto
        {
            Name = $"New Test Company {Guid.NewGuid()}",
            RUC = "12345678",
            DV = "99",
            OwnerEmail = $"newowner-{Guid.NewGuid()}@example.com",
            OwnerPassword = "NewOwner@1234",
            OwnerFullName = "New Owner Name",
            CompanyEmail = $"newcompany-{Guid.NewGuid()}@example.com",
            Address = "Test Address",
            Phone = "+507 1234-5678"
        };

        // Act
        var response = await adminClient.PostAsJsonAsync("/api/admin/tenants", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenant = await response.Content.ReadFromJsonAsync<AdminTenantDto>();

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be(createDto.Name);
        tenant.RUC.Should().Be(createDto.RUC);
        tenant.Owner.Should().NotBeNull();
        tenant.Owner!.Email.Should().Be(createDto.OwnerEmail);
        tenant.Subscription.Should().NotBeNull();
        tenant.Subscription!.Plan.Should().Be(SubscriptionPlan.Professional, "new tenants should start with Professional trial");
        tenant.Subscription.Status.Should().Be(SubscriptionStatus.Trialing);
    }

    // ========================================================================
    // TEST 8: Usuario regular NO puede crear tenants
    // ========================================================================
    [Fact]
    public async Task CreateTenant_AsRegularUser_Returns403Forbidden()
    {
        // Arrange - Crear tenant regular
        var (ownerToken, _) = await CreateRegularTenantAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);

        var createDto = new CreateTenantDto
        {
            Name = "Unauthorized Tenant",
            RUC = "99999999",
            DV = "88",
            OwnerEmail = $"unauthorized-{Guid.NewGuid()}@example.com",
            OwnerPassword = "Test@1234",
            CompanyEmail = "test@example.com"
        };

        // Act - Intentar crear tenant como Owner regular
        var response = await ownerClient.PostAsJsonAsync("/api/admin/tenants", createDto);

        // Assert - Debería retornar 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "regular users should not be able to create tenants");
    }

    // ========================================================================
    // TEST 9: SystemAdmin puede desactivar tenant
    // ========================================================================
    [Fact]
    public async Task DeactivateTenant_AsSystemAdmin_Returns200()
    {
        // Arrange
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var (_, tenantId) = await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        // Act - Desactivar tenant
        var response = await adminClient.DeleteAsync($"/api/admin/tenants/{tenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verificar que el tenant está desactivado consultándolo
        var getResponse = await adminClient.GetAsync($"/api/admin/tenants/{tenantId}");
        getResponse.EnsureSuccessStatusCode();
        var tenant = await getResponse.Content.ReadFromJsonAsync<AdminTenantDto>();

        tenant.Should().NotBeNull();
        tenant!.IsActive.Should().BeFalse("tenant should be deactivated");
    }

    // ========================================================================
    // TEST 10: SystemAdmin puede actualizar información de tenant
    // ========================================================================
    [Fact]
    public async Task UpdateTenant_AsSystemAdmin_Returns200WithUpdatedData()
    {
        // Arrange
        var adminToken = await CreateSystemAdminAndGetTokenAsync();
        var (_, tenantId) = await CreateRegularTenantAsync();

        var adminClient = CreateAuthenticatedClient(adminToken);

        var updateDto = new UpdateAdminTenantDto
        {
            Name = "Updated Company Name",
            Phone = "+507 9999-9999",
            Email = "updated@example.com"
        };

        // Act
        var response = await adminClient.PutAsJsonAsync($"/api/admin/tenants/{tenantId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTenant = await response.Content.ReadFromJsonAsync<AdminTenantDto>();

        updatedTenant.Should().NotBeNull();
        updatedTenant!.Name.Should().Be(updateDto.Name);
        updatedTenant.Phone.Should().Be(updateDto.Phone);
        updatedTenant.Email.Should().Be(updateDto.Email);
    }
}
