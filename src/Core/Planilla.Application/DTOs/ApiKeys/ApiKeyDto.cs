namespace Vorluno.Planilla.Application.DTOs.ApiKeys;

/// <summary>
/// DTO de lectura de una API key para el dashboard del tenant.
/// Nunca incluye el hash ni el plaintext — solo el prefijo (público)
/// para que el usuario identifique visualmente sus keys.
///
/// Los contadores <c>TotalRequests</c> y <c>LastUsedAt</c> son actualizados
/// por <c>ApiUsageTrackingMiddleware</c> en cada request 2xx al API Platform.
/// </summary>
public record ApiKeyDto(
    int Id,
    string Name,
    string KeyPrefix,
    string Mode,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt,
    string? RevocationReason,
    bool IsActive,
    long TotalRequests,
    DateTime CreatedAt
);
