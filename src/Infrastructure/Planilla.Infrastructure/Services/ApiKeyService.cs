using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Security;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación de IApiKeyService con patrón Stripe-like:
/// formato <c>pk_{mode}_{prefix8}{secret32}</c>, lookup O(1) por prefix,
/// hash SHA256 del secret, comparación constant-time.
///
/// Los helpers de formato/parsing/hashing viven en <see cref="ApiKeyFormat"/>
/// para ser testeables sin dependencias de EF Core.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    // Máximo de reintentos si hay colisión de prefix (astronómicamente raro).
    private const int MaxGenerationAttempts = 5;

    private readonly ApplicationDbContext _db;

    public ApiKeyService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(ApiKey entity, string plaintextKey)> GenerateAsync(
        int tenantId,
        string name,
        string mode,
        DateTime? expiresAt,
        string? createdByUserId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0) throw new ArgumentException("tenantId debe ser > 0", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name es requerido", nameof(name));
        if (mode != "Test" && mode != "Live")
            throw new ArgumentException("mode debe ser 'Test' o 'Live'", nameof(mode));

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var prefix = ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.PrefixLength);

            // IgnoreQueryFilters porque un prefix es GLOBAL-unique, no por tenant —
            // el filtro por TenantId bloquearía la verificación de colisión.
            var collision = await _db.ApiKeys
                .IgnoreQueryFilters()
                .AnyAsync(k => k.KeyPrefix == prefix, cancellationToken);

            if (collision) continue;

            var secret = ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.SecretLength);
            var hash = ApiKeyFormat.ComputeSha256Hex(secret);
            var plaintextKey = ApiKeyFormat.Compose(mode, prefix, secret);

            var entity = new ApiKey
            {
                TenantId = tenantId,
                Name = name.Trim(),
                KeyPrefix = prefix,
                KeyHash = hash,
                Mode = mode,
                ExpiresAt = expiresAt,
                CreatedByUserId = createdByUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.ApiKeys.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return (entity, plaintextKey);
        }

        throw new InvalidOperationException(
            $"No se pudo generar una API key única tras {MaxGenerationAttempts} intentos. " +
            "Esto indica un problema con el generador de entropía del sistema.");
    }

    public async Task<ApiKey?> ValidateAsync(string plaintextKey, CancellationToken cancellationToken = default)
    {
        if (!ApiKeyFormat.TryParse(plaintextKey, out var mode, out var prefix, out var secret))
            return null;

        // Lookup por prefix (índice único). IgnoreQueryFilters porque el handler
        // no conoce el tenant todavía — esta búsqueda lo determina.
        var key = await _db.ApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix, cancellationToken);

        if (key is null) return null;

        // Defensa en profundidad: verificar que el mode coincida (test vs live).
        if (!string.Equals(key.Mode, mode, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!key.IsUsableNow())
            return null;

        // Comparación constant-time del hash del secret.
        var computedHash = ApiKeyFormat.ComputeSha256Hex(secret);
        if (!ApiKeyFormat.FixedTimeEquals(computedHash, key.KeyHash))
            return null;

        return key;
    }

    public async Task<bool> RevokeAsync(
        int keyId,
        int tenantId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, cancellationToken);

        if (key is null) return false;
        if (key.RevokedAt.HasValue) return true; // idempotente

        key.RevokedAt = DateTime.UtcNow;
        key.IsActive = false;
        key.RevocationReason = string.IsNullOrWhiteSpace(reason) ? "revoked" : reason.Trim();
        key.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
