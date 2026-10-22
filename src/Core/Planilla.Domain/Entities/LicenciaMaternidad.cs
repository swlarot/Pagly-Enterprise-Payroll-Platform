using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Licencia de maternidad: 14 semanas (6 antes y 8 después del parto) en las
/// que la colaboradora no cobra salario en la planilla, porque la CSS le paga
/// el subsidio. Sigue siendo colaboradora: conserva antigüedad e historial, y
/// vuelve sola a la planilla el día después de <see cref="FechaFin"/>.
/// No se borra: se anula, con quién, cuándo y por qué.
/// </summary>
public class LicenciaMaternidad : ITenantEntity
{
    /// <summary>Duración de la licencia según el Código de Trabajo: 14 semanas.</summary>
    public const int DiasLicencia = 98;

    public int Id { get; set; }
    public int TenantId { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>Primer día de licencia.</summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>Último día de licencia; regresa al día siguiente.</summary>
    public DateTime FechaFin { get; set; }

    public string? Observaciones { get; set; }

    public string? CreadaPor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool Anulada { get; set; }
    public string? AnuladaPor { get; set; }
    public DateTime? AnuladaEn { get; set; }
    public string? MotivoAnulacion { get; set; }

    public virtual Tenant? Tenant { get; set; }

    /// <summary>Último día sugerido: 14 semanas contando el de inicio.</summary>
    public static DateTime FinSugerido(DateTime inicio) => inicio.Date.AddDays(DiasLicencia - 1);

    /// <summary>
    /// Fuero de maternidad: no se la puede despedir sin autorización judicial
    /// hasta un año después de su regreso (Código de Trabajo, Art. 106).
    /// </summary>
    public DateTime FueroHasta => FechaFin.Date.AddDays(1).AddYears(1);

    /// <summary>Días de licencia que caen dentro de un período (ambos extremos incluidos).</summary>
    public int DiasDentro(DateTime desde, DateTime hasta)
    {
        var ini = FechaInicio.Date > desde.Date ? FechaInicio.Date : desde.Date;
        var fin = FechaFin.Date < hasta.Date ? FechaFin.Date : hasta.Date;
        return fin < ini ? 0 : (int)(fin - ini).TotalDays + 1;
    }
}
