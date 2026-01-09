using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vorluno.Planilla.Domain.Entities;

public class ReciboDeSueldo
{
    [Key]
    public int Id { get; set; }

    // Clave for�nea que establece la relaci�n con la tabla Empleado
    [ForeignKey("Empleado")]
    public int EmpleadoId { get; set; }

    /// <summary>
    /// ID del tenant al que pertenece este recibo
    /// </summary>
    public int TenantId { get; set; }

    public DateTime FechaGeneracion { get; set; }

    public DateTime PeriodoInicio { get; set; }

    public DateTime PeriodoFin { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SalarioBruto { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalDeducciones { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SalarioNeto { get; set; }

    // Propiedad de navegaci�n para acceder al objeto Empleado completo
    // La palabra clave 'virtual' permite a EF Core optimizar la carga (Lazy Loading).
    public virtual Empleado Empleado { get; set; } = null!;

    // Navigation property para Tenant
    public virtual Tenant? Tenant { get; set; }
}