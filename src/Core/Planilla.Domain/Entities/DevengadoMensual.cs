// ====================================================================
// Planilla - DevengadoMensual
// Lo que un empleado devengó en un mes que NO viene de planillas de Pagly:
// meses importados al migrar la empresa o escritos a mano.
//
// La DGI y el MITRADEL exigen hasta 60 meses de salarios anteriores por
// empleado, y de ahí salen la prima de antigüedad (60 meses), la
// indemnización (6 meses), las vacaciones (11 meses), el décimo (el
// cuatrimestre) y el saldo inicial de renta (el año en curso).
//
// Decisión: los meses que sí tienen planillas aprobadas en Pagly NUNCA se
// guardan aquí; se derivan de PayrollDetail. Guardar ambos obligaría a
// mantenerlos sincronizados y tarde o temprano divergirían.
// ====================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

public class DevengadoMensual : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required]
    public int EmpleadoId { get; set; }

    [Required]
    public int Anio { get; set; }

    /// <summary>1 a 12.</summary>
    [Required]
    public int Mes { get; set; }

    /// <summary>Salario ordinario del mes.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Salario { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Vacaciones { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Extras { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Comision { get; set; }

    /// <summary>Importado o Manual. Nunca Planilla: esos meses se derivan.</summary>
    [Required]
    public OrigenDevengado Origen { get; set; } = OrigenDevengado.Importado;

    [StringLength(200)]
    public string? Nota { get; set; }

    [NotMapped]
    public decimal Total => Salario + Vacaciones + Extras + Comision;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual Empleado? Empleado { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
