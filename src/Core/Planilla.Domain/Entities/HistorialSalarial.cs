using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

public class HistorialSalarial : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Empleado al que pertenece este registro salarial.</summary>
    public int EmpleadoId { get; set; }

    /// <summary>Salario mensual vigente a partir de FechaVigencia.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Range(0, double.MaxValue, ErrorMessage = "El salario no puede ser negativo.")]
    public decimal SalarioMensual { get; set; }

    /// <summary>Fecha desde la cual este salario entró en vigencia.</summary>
    public DateTime FechaVigencia { get; set; }

    /// <summary>Motivo del cambio (opcional): contratación, aumento, ajuste…</summary>
    [StringLength(200)]
    public string? Motivo { get; set; }

    /// <summary>Fecha y hora de registro del cambio (auditoría).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>ID del tenant (multi-tenancy).</summary>
    public int TenantId { get; set; }

    // Navegación
    public virtual Empleado? Empleado { get; set; }
}