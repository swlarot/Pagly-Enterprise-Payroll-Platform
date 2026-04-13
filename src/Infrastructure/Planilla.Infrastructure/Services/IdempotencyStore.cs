using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación PostgreSQL de <see cref="IIdempotencyStore"/>.
/// Usa la tabla <c>IdempotencyRecords</c> con índice unique (ApiKeyId, IdempotencyKey).
/// </summary>
public class IdempotencyStore : IIdempotencyStore
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<IdempotencyStore> _logger;

    public IdempotencyStore(ApplicationDbContext db, ILogger<IdempotencyStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IdempotencyMatch?> TryGetAsync(
        int apiKeyId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var now = DateTime.UtcNow;

        // Lookup directo por índice unique. Si existe y no expiró → retorna.
        var record = await _db.IdempotencyRecords
            .AsNoTracking()
            .Where(r => r.ApiKeyId == apiKeyId
                     && r.IdempotencyKey == idempotencyKey
                     && r.ExpiresAt > now)
            .Select(r => new IdempotencyMatch(
                r.RequestHash,
                r.Endpoint,
                r.StatusCode,
                r.ResponseJson,
                r.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return record;
    }

    public async Task SaveAsync(
        int apiKeyId,
        string idempotencyKey,
        string endpoint,
        string requestHash,
        int statusCode,
        string responseJson,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var record = new IdempotencyRecord
        {
            ApiKeyId = apiKeyId,
            IdempotencyKey = idempotencyKey,
            Endpoint = endpoint,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseJson = responseJson,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl),
        };

        _db.IdempotencyRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: 2 requests concurrentes con el mismo key.
            // Postgres rechaza la segunda inserción por el índice unique —
            // es el comportamiento correcto (evita duplicados). Logueamos y
            // seguimos; el segundo request verá el record del primero en el
            // próximo TryGet.
            _logger.LogInformation(
                "Idempotency key colisión concurrente ignorada (ApiKeyId={ApiKeyId}, Key={Key})",
                apiKeyId, idempotencyKey);
        }
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Fast path: bulk delete cuando el provider lo soporta (Postgres, SQLite, SQL Server).
        // El InMemory provider no implementa ExecuteDelete — fallback a load+remove.
        try
        {
            var deleted = await _db.IdempotencyRecords
                .Where(r => r.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation("IdempotencyStore purgó {Count} records expirados", deleted);
            }
            return deleted;
        }
        catch (InvalidOperationException)
        {
            // InMemory provider (o similar sin soporte de bulk ops) — fallback manual.
            var expired = await _db.IdempotencyRecords
                .Where(r => r.ExpiresAt <= now)
                .ToListAsync(cancellationToken);
            if (expired.Count == 0) return 0;
            _db.IdempotencyRecords.RemoveRange(expired);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "IdempotencyStore purgó {Count} records expirados (fallback manual)",
                expired.Count);
            return expired.Count;
        }
    }

    /// <summary>
    /// Postgres retorna 23505 en unique constraint violation. Con Npgsql,
    /// ese código llega en PostgresException.SqlState. Para SQLite (tests)
    /// usamos el mensaje ("UNIQUE constraint failed").
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return false;

        // Npgsql.PostgresException tiene prop SqlState; leemos por reflexión
        // para no forzar referencia al paquete Npgsql desde Application.
        var sqlState = inner.GetType()
            .GetProperty("SqlState")
            ?.GetValue(inner) as string;
        if (sqlState == "23505") return true;

        // Fallback para SQLite (tests) y otros providers
        return inner.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || inner.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
