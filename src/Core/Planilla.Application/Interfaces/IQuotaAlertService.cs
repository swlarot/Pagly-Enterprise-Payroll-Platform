namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio que evalúa si un tenant cruzó el 80% o 100% de su cuota mensual
/// de API Platform y, si corresponde, envía email al Owner.
///
/// <para>
/// Dedup: usa la tabla <c>QuotaAlertsSent</c> con unique index
/// (tenantId, year, month, threshold) para garantizar UN email por umbral por mes.
/// </para>
///
/// <para>
/// Se invoca desde <c>ApiUsageTrackingMiddleware</c> después de insertar el record,
/// fire-and-forget (no bloquea el response del cliente).
/// </para>
/// </summary>
public interface IQuotaAlertService
{
    /// <summary>
    /// Evalúa el uso actual del tenant y envía alerta si cruzó un umbral nuevo.
    /// Este método es idempotente — llamarlo 1000 veces en el mismo segundo
    /// no genera 1000 emails (el unique index previene duplicados).
    /// </summary>
    /// <param name="tenantId">ID del tenant que acaba de hacer un request.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task EvaluateAsync(int tenantId, CancellationToken cancellationToken = default);
}
