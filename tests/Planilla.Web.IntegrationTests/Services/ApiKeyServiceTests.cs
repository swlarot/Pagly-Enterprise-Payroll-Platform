using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Security;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;

namespace Planilla.Web.IntegrationTests.Services;

/// <summary>
/// Tests del ApiKeyService usando EF InMemory.
/// Valida el flujo generate → validate → revoke con persistencia real
/// pero sin Postgres (para velocidad de CI).
///
/// Nota: el ApplicationDbContext aplica global query filters que accesan
/// <c>_tenantContext.TenantId</c>. En InMemory el short-circuit del
/// expression tree no siempre funciona con <c>null</c>, así que pasamos
/// un stub <see cref="BypassTenantContext"/> con TenantId=0 — el filter
/// lo interpreta como bypass y no aplica filtrado.
/// </summary>
public class ApiKeyServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ApiKeyService _service;

    public ApiKeyServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"apikey-tests-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new ApplicationDbContext(options, currentUserService: null, tenantContext: new BypassTenantContext());
        _service = new ApiKeyService(_db);
    }

    /// <summary>
    /// Stub de ITenantContext que retorna TenantId=0 — dispara el bypass del
    /// global query filter sin causar NullReferenceException en InMemory.
    /// </summary>
    private class BypassTenantContext : ITenantContext
    {
        public int TenantId => 0;
        public TenantRole TenantRole => TenantRole.User;
        public string? UserId => null;
        public bool IsSystemAdmin => false;
        public bool HasTenant => false;
        public Task SetTenantAsync(int tenantId) => Task.CompletedTask;
        public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult<Tenant?>(null);
        public bool HasRole(TenantRole role) => false;
        public bool IsAdminOrOwner() => false;
        public void Clear() { }
    }

    public void Dispose() => _db.Dispose();

    // ====================================================================
    // Generate
    // ====================================================================

    [Fact]
    public async Task Generate_HappyCase_ReturnsEntityAndPlaintext()
    {
        var (entity, plaintext) = await _service.GenerateAsync(
            tenantId: 1,
            name: "Test Key",
            mode: "Live",
            expiresAt: null,
            createdByUserId: "user-xyz");

        entity.Should().NotBeNull();
        entity.TenantId.Should().Be(1);
        entity.Name.Should().Be("Test Key");
        entity.Mode.Should().Be("Live");
        entity.KeyPrefix.Should().HaveLength(ApiKeyFormat.PrefixLength);
        entity.KeyHash.Should().HaveLength(64); // SHA256 hex
        entity.CreatedByUserId.Should().Be("user-xyz");
        entity.IsActive.Should().BeTrue();
        entity.RevokedAt.Should().BeNull();

        plaintext.Should().StartWith("pk_live_");
        plaintext.Should().HaveLength("pk_live_".Length + ApiKeyFormat.PrefixLength + ApiKeyFormat.SecretLength);
    }

    [Fact]
    public async Task Generate_PlaintextNotStored_OnlyHash()
    {
        var (entity, plaintext) = await _service.GenerateAsync(1, "k", "Live", null, null);

        // Ningún campo de la entity contiene el secret en plaintext
        entity.KeyHash.Should().NotContain(plaintext);
        entity.KeyPrefix.Should().NotBe(plaintext);

        // Pero la relación prefix-secret es derivable del plaintext
        ApiKeyFormat.TryParse(plaintext, out _, out var prefix, out var secret).Should().BeTrue();
        entity.KeyPrefix.Should().Be(prefix);
        ApiKeyFormat.ComputeSha256Hex(secret).Should().Be(entity.KeyHash);
    }

    [Fact]
    public async Task Generate_InvalidTenantId_Throws()
    {
        await FluentActions.Invoking(() => _service.GenerateAsync(0, "name", "Live", null, null))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Generate_InvalidMode_Throws()
    {
        await FluentActions.Invoking(() => _service.GenerateAsync(1, "name", "Sandbox", null, null))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ====================================================================
    // Validate
    // ====================================================================

    [Fact]
    public async Task Validate_CorrectKey_ReturnsEntity()
    {
        var (created, plaintext) = await _service.GenerateAsync(42, "k", "Live", null, null);

        var validated = await _service.ValidateAsync(plaintext);

        validated.Should().NotBeNull();
        validated!.Id.Should().Be(created.Id);
        validated.TenantId.Should().Be(42);
    }

    [Fact]
    public async Task Validate_TestKeyAgainstLiveRequest_StillValidates()
    {
        // El service valida que el mode coincida entre key y lookup.
        // No hay concepto de "prod lookup" todavía — solo validamos el key completo.
        var (_, plaintext) = await _service.GenerateAsync(1, "k", "Test", null, null);

        var result = await _service.ValidateAsync(plaintext);
        result.Should().NotBeNull();
        result!.Mode.Should().Be("Test");
    }

    [Fact]
    public async Task Validate_MalformedKey_ReturnsNull()
    {
        var result = await _service.ValidateAsync("garbage_not_a_key");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_NonexistentPrefix_ReturnsNull()
    {
        var fakeKey = ApiKeyFormat.Compose(
            "Live",
            ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.PrefixLength),
            ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.SecretLength));

        var result = await _service.ValidateAsync(fakeKey);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_CorrectPrefixWrongSecret_ReturnsNull()
    {
        var (entity, plaintext) = await _service.GenerateAsync(1, "k", "Live", null, null);

        // Swap el secret por otro random — el prefix sigue siendo válido pero el hash falla
        ApiKeyFormat.TryParse(plaintext, out _, out var prefix, out _).Should().BeTrue();
        var forged = ApiKeyFormat.Compose("Live", prefix, ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.SecretLength));

        var result = await _service.ValidateAsync(forged);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_RevokedKey_ReturnsNull()
    {
        var (entity, plaintext) = await _service.GenerateAsync(1, "k", "Live", null, null);
        await _service.RevokeAsync(entity.Id, entity.TenantId, "test revoke");

        var result = await _service.ValidateAsync(plaintext);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_ExpiredKey_ReturnsNull()
    {
        var past = DateTime.UtcNow.AddMinutes(-1);
        var (_, plaintext) = await _service.GenerateAsync(1, "k", "Live", expiresAt: past, null);

        var result = await _service.ValidateAsync(plaintext);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_FutureExpiration_Works()
    {
        var future = DateTime.UtcNow.AddDays(365);
        var (_, plaintext) = await _service.GenerateAsync(1, "k", "Live", expiresAt: future, null);

        var result = await _service.ValidateAsync(plaintext);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Validate_InactiveKey_ReturnsNull()
    {
        var (entity, plaintext) = await _service.GenerateAsync(1, "k", "Live", null, null);
        entity.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await _service.ValidateAsync(plaintext);
        result.Should().BeNull();
    }

    // ====================================================================
    // Revoke
    // ====================================================================

    [Fact]
    public async Task Revoke_ValidKey_ReturnsTrue_SetsFields()
    {
        var (entity, _) = await _service.GenerateAsync(1, "k", "Live", null, null);

        var ok = await _service.RevokeAsync(entity.Id, entity.TenantId, "compromised");

        ok.Should().BeTrue();
        var refreshed = await _db.ApiKeys.FirstAsync(k => k.Id == entity.Id);
        refreshed.RevokedAt.Should().NotBeNull();
        refreshed.IsActive.Should().BeFalse();
        refreshed.RevocationReason.Should().Be("compromised");
    }

    [Fact]
    public async Task Revoke_NonexistentKey_ReturnsFalse()
    {
        var ok = await _service.RevokeAsync(keyId: 9999, tenantId: 1, "x");
        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_CrossTenantAttempt_ReturnsFalse()
    {
        var (entity, _) = await _service.GenerateAsync(1, "k", "Live", null, null);

        // Otro tenant intenta revocar — debe fallar silenciosamente (retorna false)
        var ok = await _service.RevokeAsync(entity.Id, tenantId: 2, "x");
        ok.Should().BeFalse();

        var refreshed = await _db.ApiKeys.FirstAsync(k => k.Id == entity.Id);
        refreshed.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Revoke_Idempotent()
    {
        var (entity, _) = await _service.GenerateAsync(1, "k", "Live", null, null);

        (await _service.RevokeAsync(entity.Id, 1, "first")).Should().BeTrue();
        (await _service.RevokeAsync(entity.Id, 1, "second")).Should().BeTrue();

        var refreshed = await _db.ApiKeys.FirstAsync(k => k.Id == entity.Id);
        refreshed.RevocationReason.Should().Be("first"); // no sobrescribe
    }

    // ====================================================================
    // Unique prefix
    // ====================================================================

    [Fact]
    public async Task Generate_ManyKeys_AllUniquePrefixes()
    {
        // Generar 50 keys y verificar que todos los prefixes son distintos
        var prefixes = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var (entity, _) = await _service.GenerateAsync(1, $"k{i}", "Live", null, null);
            prefixes.Add(entity.KeyPrefix).Should().BeTrue($"el prefix '{entity.KeyPrefix}' se repitió en la iteración {i}");
        }
        prefixes.Count.Should().Be(50);
    }
}
