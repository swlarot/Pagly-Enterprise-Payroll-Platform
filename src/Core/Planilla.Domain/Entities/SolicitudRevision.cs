using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Pedido de que otra persona revise algo antes de seguir: una planilla, un
/// décimo, una liquidación o las horas extra de un mes. Queda como historial
/// de quién pidió, a quién, qué respondió y cuándo; nunca se borra.
/// </summary>
public class SolicitudRevision : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public TipoRevision Tipo { get; set; }

    /// <summary>
    /// Id de lo revisado. Para horas extra es el mes, como AAAAMM (202610).
    /// </summary>
    public int EntidadId { get; set; }

    /// <summary>Cómo se llama lo revisado en pantalla («Planilla PL-2026-017»).</summary>
    public string Referencia { get; set; } = string.Empty;

    /// <summary>Ruta de la app donde se revisa («/planillas?anio=2026&amp;mes=10»).</summary>
    public string Enlace { get; set; } = string.Empty;

    public string SolicitadoPor { get; set; } = string.Empty;
    public string RevisorId { get; set; } = string.Empty;

    /// <summary>Lo que quien pide quiere que el revisor mire.</summary>
    public string? Comentario { get; set; }

    public EstadoRevision Estado { get; set; } = EstadoRevision.Pendiente;

    /// <summary>Lo que respondió el revisor (obligatorio al devolver).</summary>
    public string? Respuesta { get; set; }
    public DateTime? RespondidaEn { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
