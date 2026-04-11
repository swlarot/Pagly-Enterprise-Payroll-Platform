using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio de gestión de API keys para el API Platform B2B.
/// Responsable de generar, validar y revocar keys siguiendo el patrón
/// de Stripe/GitHub/AWS: prefijo público para lookup indexado + hash
/// SHA256 del secret para comparación constant-time.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Genera una nueva API key para un tenant.
    /// La key plaintext solo se retorna UNA vez — nunca se puede recuperar después.
    /// </summary>
    /// <param name="tenantId">Tenant owner.</param>
    /// <param name="name">Nombre amigable (ej: "Urbis Production").</param>
    /// <param name="mode">"Test" o "Live".</param>
    /// <param name="expiresAt">Fecha de expiración opcional (null = sin expiración).</param>
    /// <param name="createdByUserId">UserId del creador (auditoría).</param>
    /// <returns>
    /// Tupla con la entity persistida y el plaintext completo
    /// (<c>pk_live_abc12345xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx</c>).
    /// El plaintext NO se almacena en ningún lado.
    /// </returns>
    Task<(ApiKey entity, string plaintextKey)> GenerateAsync(
        int tenantId,
        string name,
        string mode,
        DateTime? expiresAt,
        string? createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida una API key plaintext contra la tabla.
    /// Hace lookup por prefijo (O(1) índice único), compara hash con
    /// constant-time comparison, y verifica activa/no-revocada/no-expirada.
    /// </summary>
    /// <param name="plaintextKey">La key completa que envía el cliente.</param>
    /// <returns>
    /// La entity ApiKey si es válida, null si no existe, fue revocada, o el hash no coincide.
    /// Siempre usar el mismo código de error 401 en el caller para no filtrar el motivo.
    /// </returns>
    Task<ApiKey?> ValidateAsync(string plaintextKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca una key por id. Setea RevokedAt, IsActive=false, RevocationReason.
    /// </summary>
    Task<bool> RevokeAsync(
        int keyId,
        int tenantId,
        string? reason,
        CancellationToken cancellationToken = default);
}
