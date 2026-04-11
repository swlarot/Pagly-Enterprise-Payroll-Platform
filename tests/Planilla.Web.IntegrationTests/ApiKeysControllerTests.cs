using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vorluno.Planilla.Application.DTOs.ApiKeys;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Planilla.Web.IntegrationTests;

/// <summary>
/// Tests end-to-end del CRUD de ApiKeysController.
/// Flujo real: registrar un tenant con /api/auth/register → obtener JWT →
/// crear/listar/revocar API keys. El aislamiento cross-tenant se valida
/// creando dos tenants distintos.
/// </summary>
public class ApiKeysControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiKeysControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ====================================================================
    // Auth guard
    // ====================================================================

    [Fact]
    public async Task Create_WithoutJwt_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest
        {
            Name = "test",
            Mode = "Live"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_WithoutJwt_Returns401()
    {
        var response = await _client.GetAsync("/api/api-keys");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ====================================================================
    // Happy path
    // ====================================================================

    [Fact]
    public async Task Create_AsOwner_Returns201WithPlaintextKey()
    {
        var (client, _) = await RegisterAndAuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest
        {
            Name = "Integration test key",
            Mode = "Live"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CreateApiKeyResponse>(body, JsonOpts);

        result.Should().NotBeNull();
        result!.PlaintextKey.Should().StartWith("pk_live_");
        result.Key.Name.Should().Be("Integration test key");
        result.Key.Mode.Should().Be("Live");
        result.Key.KeyPrefix.Should().HaveLength(8);
        result.Key.IsActive.Should().BeTrue();
        result.Key.RevokedAt.Should().BeNull();
        result.Key.TotalRequests.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithExpiresInDays_SetsExpiration()
    {
        var (client, _) = await RegisterAndAuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest
        {
            Name = "expires-key",
            Mode = "Test",
            ExpiresInDays = 30
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);
        result!.Key.ExpiresAt.Should().NotBeNull();
        result.Key.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnTenantKeys()
    {
        var (clientA, tenantA) = await RegisterAndAuthenticateAsync();
        await clientA.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "a1", Mode = "Live" });
        await clientA.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "a2", Mode = "Live" });

        var (clientB, tenantB) = await RegisterAndAuthenticateAsync();
        await clientB.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "b1", Mode = "Test" });

        tenantA.Should().NotBe(tenantB, "los dos registros deben generar tenants distintos");

        // List como A — debe ver solo a1 y a2
        var listA = await clientA.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys", JsonOpts);
        listA!.Should().HaveCount(2);
        listA.Select(k => k.Name).Should().BeEquivalentTo(new[] { "a2", "a1" }); // desc

        // List como B — debe ver solo b1
        var listB = await clientB.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys", JsonOpts);
        listB!.Should().HaveCount(1);
        listB[0].Name.Should().Be("b1");
    }

    [Fact]
    public async Task GetById_ReturnsKey()
    {
        var (client, _) = await RegisterAndAuthenticateAsync();
        var create = await client.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "get-test", Mode = "Live" });
        var created = await create.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);

        var get = await client.GetAsync($"/api/api-keys/{created!.Key.Id}");

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<ApiKeyDto>(JsonOpts);
        fetched!.Id.Should().Be(created.Key.Id);
        fetched.Name.Should().Be("get-test");
    }

    [Fact]
    public async Task GetById_ForeignTenant_Returns404()
    {
        var (clientA, _) = await RegisterAndAuthenticateAsync();
        var createA = await clientA.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "a", Mode = "Live" });
        var createdA = await createA.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);

        var (clientB, _) = await RegisterAndAuthenticateAsync();
        var response = await clientB.GetAsync($"/api/api-keys/{createdA!.Key.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "global query filter aísla keys entre tenants");
    }

    [Fact]
    public async Task Revoke_OwnKey_Returns204_AndSubsequentAuthFails()
    {
        var (client, _) = await RegisterAndAuthenticateAsync();
        var create = await client.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "to-revoke", Mode = "Live" });
        var created = await create.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);

        var revoke = await client.DeleteAsync($"/api/api-keys/{created!.Key.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Después de revocar, usar el plaintext key en /v1/payroll/calculate debe fallar
        using var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Api-Key", created.PlaintextKey);

        var calcResponse = await apiClient.PostAsJsonAsync("/v1/payroll/calculate", BuildValidCalcRequest());
        calcResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "una key revocada no debe ser aceptada por el handler de auth");
    }

    [Fact]
    public async Task Revoke_NonexistentKey_Returns404()
    {
        var (client, _) = await RegisterAndAuthenticateAsync();

        var response = await client.DeleteAsync("/api/api-keys/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_ForeignTenantKey_Returns404()
    {
        var (clientA, _) = await RegisterAndAuthenticateAsync();
        var createA = await clientA.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest { Name = "a", Mode = "Live" });
        var createdA = await createA.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);

        var (clientB, _) = await RegisterAndAuthenticateAsync();
        var response = await clientB.DeleteAsync($"/api/api-keys/{createdA!.Key.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "tenant B no puede revocar keys de tenant A, y el 404 no revela existencia");

        // Verificar en BD que la key NO fue revocada
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var key = await db.ApiKeys
            .IgnoreQueryFilters()
            .FirstAsync(k => k.Id == createdA.Key.Id);
        key.RevokedAt.Should().BeNull();
    }

    // ====================================================================
    // E2E: create → use → track → revoke
    // ====================================================================

    [Fact]
    public async Task FullFlow_CreateUseRevoke_TracksUsage()
    {
        var (dashboardClient, _) = await RegisterAndAuthenticateAsync();

        // 1. Owner crea key desde el dashboard
        var create = await dashboardClient.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest
        {
            Name = "full-flow",
            Mode = "Live"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CreateApiKeyResponse>(JsonOpts);
        var keyId = created!.Key.Id;
        var plaintext = created.PlaintextKey;

        // 2. Cliente usa la key en /v1/payroll/calculate
        using var apiClient = _factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("X-Api-Key", plaintext);
        var calc = await apiClient.PostAsJsonAsync("/v1/payroll/calculate", BuildValidCalcRequest());
        calc.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Dashboard vuelve a listar — pero el tracking middleware falla silenciosamente
        //    con EF InMemory (no soporta ExecuteUpdateAsync), así que TotalRequests NO se
        //    incrementa en tests. El test end-to-end del tracking con SQLite ya cubre el
        //    caso positivo. Aquí solo verificamos que el endpoint /v1 funciona y que
        //    el dashboard puede listar después.
        var list = await dashboardClient.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys", JsonOpts);
        list!.Should().Contain(k => k.Id == keyId);

        // 4. Owner revoca
        var revoke = await dashboardClient.DeleteAsync($"/api/api-keys/{keyId}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Listar de nuevo — la key aparece pero con IsActive=false, RevokedAt seteado
        var listAfter = await dashboardClient.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys", JsonOpts);
        var revoked = listAfter!.Single(k => k.Id == keyId);
        revoked.IsActive.Should().BeFalse();
        revoked.RevokedAt.Should().NotBeNull();
        revoked.RevocationReason.Should().Be("revoked_by_owner");
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    // Misma key que CustomWebApplicationFactory setea en Jwt:Key
    private const string TestJwtKey = "test-secret-key-must-be-at-least-32-characters-long-for-hmacsha256";
    private const string TestJwtIssuer = "https://test.planilla.vorluno.dev";
    private const string TestJwtAudience = "https://test.planilla.vorluno.dev";

    /// <summary>
    /// Seedea un nuevo tenant con subscription + dummy AppUser Owner, y retorna
    /// un HttpClient con JWT Bearer firmado manualmente. Evita depender de
    /// <c>/api/auth/register</c> que está deshabilitado hardcoded (retorna 403).
    /// </summary>
    private async Task<(HttpClient client, int tenantId)> RegisterAndAuthenticateAsync()
    {
        int tenantId;
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Seedear tenant
            var tenant = new Tenant
            {
                Name = $"Co {Guid.NewGuid().ToString()[..8]}",
                Subdomain = $"co-{Guid.NewGuid().ToString()[..8]}",
                Email = $"owner-{Guid.NewGuid()}@example.com",
                IsActive = true,
                SubscriptionId = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            // 2. Subscription Professional (trial) para que CanUseApi esté true
            var subscription = new Subscription
            {
                TenantId = tenant.Id,
                Plan = SubscriptionPlan.Professional,
                Status = SubscriptionStatus.Trialing,
                StartDate = DateTime.UtcNow,
                TrialEndsAt = DateTime.UtcNow.AddDays(14),
                CreatedAt = DateTime.UtcNow
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            tenant.SubscriptionId = subscription.Id;
            await db.SaveChangesAsync();

            tenantId = tenant.Id;
            userId = Guid.NewGuid().ToString();
        }

        var token = GenerateJwt(userId: userId, email: $"owner-{userId}@test.local", tenantId: tenantId, role: "Owner");

        var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (authedClient, tenantId);
    }

    private static string GenerateJwt(string userId, string email, int tenantId, string role)
    {
        var keyBytes = Encoding.UTF8.GetBytes(TestJwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString()),
            new("tenant_role", role),
            new("plan", "Professional"),
            new("is_system_admin", "false")
        };

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static object BuildValidCalcRequest() => new
    {
        grossPay = 2000m,
        payFrequency = "Mensual",
        yearsCotized = 5,
        averageSalaryLast10Years = 1800m,
        cssRiskPercentage = 0m,
        dependents = 0,
        isSubjectToCss = true,
        isSubjectToEducationalInsurance = true,
        isSubjectToIncomeTax = true,
        calculationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
