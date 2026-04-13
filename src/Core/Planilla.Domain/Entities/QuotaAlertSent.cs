namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Marca que una alerta de cuota fue enviada. Previene spam — se manda UN email
/// por umbral por período (mes calendario UTC) por tenant.
///
/// <para>
/// Ejemplo: un tenant que cruza 80% el día 15 del mes no recibe otro email 80%
/// hasta el mes siguiente; si también cruza 100% el día 28, recibe un segundo
/// email (threshold distinto). El siguiente mes empieza la cuenta de nuevo.
/// </para>
///
/// <para>
/// Schema key: (<c>TenantId</c>, <c>PeriodYear</c>, <c>PeriodMonth</c>, <c>Threshold</c>) unique.
/// Así el insert es idempotente — si dos requests concurrentes cruzan el umbral
/// al mismo tiempo, solo uno logra insertar y solo uno envía email.
/// </para>
/// </summary>
public class QuotaAlertSent
{
    public int Id { get; set; }

    /// <summary>
    /// Tenant al que se le envió la alerta.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Año del período (UTC). Ej: 2026.
    /// </summary>
    public int PeriodYear { get; set; }

    /// <summary>
    /// Mes del período (UTC). 1-12.
    /// </summary>
    public int PeriodMonth { get; set; }

    /// <summary>
    /// Umbral cruzado: 80 o 100 (percentil entero, sin decimales). Si en el
    /// futuro se agregan más umbrales (50? 90?), solo se añaden aquí sin
    /// cambiar schema.
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Total de requests del tenant en el mes al momento de la alerta.
    /// Snapshot útil para debuggear (¿por qué se mandó el email?).
    /// </summary>
    public int RequestsAtAlert { get; set; }

    /// <summary>
    /// Límite del plan al momento de la alerta. Si el tenant cambia de plan
    /// (upgrade), el próximo umbral se calcula con el nuevo límite.
    /// </summary>
    public int LimitAtAlert { get; set; }

    /// <summary>
    /// Timestamp UTC del envío del email.
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
