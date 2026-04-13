namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Store de respuestas cacheadas por Idempotency-Key.
/// Provee 3 operaciones: buscar existente, guardar nueva y purgar expiradas.
///
/// <para>
/// La lógica del filter HTTP no está aquí — ver IdempotencyFilter en la capa Web.
/// Este store solo maneja el CRUD de IdempotencyRecord contra la DB.
/// </para>
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Busca una respuesta cacheada para el par (<paramref name="apiKeyId"/>,
    /// <paramref name="idempotencyKey"/>). Retorna null si no existe o expiró.
    /// </summary>
    Task<IdempotencyMatch?> TryGetAsync(
        int apiKeyId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda una nueva respuesta cacheada. Si el par ya existe (unique constraint),
    /// lanza — pero el filter debe chequear con <see cref="TryGetAsync"/> antes.
    /// </summary>
    Task SaveAsync(
        int apiKeyId,
        string idempotencyKey,
        string endpoint,
        string requestHash,
        int statusCode,
        string responseJson,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purga records expirados. Retorna cantidad eliminada.
    /// Llamarla desde un background job periódico.
    /// </summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de buscar un idempotency record. Separa el hash para que el filter
/// pueda validar el body del request nuevo contra el original sin leer todo el registro.
/// </summary>
public record IdempotencyMatch(
    string RequestHash,
    string Endpoint,
    int StatusCode,
    string ResponseJson,
    DateTime CreatedAt);
