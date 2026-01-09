namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Representa un departamento o área organizacional de la empresa
/// </summary>
public class Departamento
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    public required string Codigo { get; set; } // Código único del departamento (ej: "ADM", "VEN", "OPS")

    public string? Descripcion { get; set; }

    public bool EstaActivo { get; set; } = true;

    /// <summary>
    /// ID del tenant al que pertenece este departamento
    /// </summary>
    public int TenantId { get; set; }

    // Jefe del departamento (opcional)
    public int? ManagerId { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Empleado? Manager { get; set; }
    public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    public virtual ICollection<Posicion> Posiciones { get; set; } = new List<Posicion>();
    public virtual Tenant? Tenant { get; set; }
}
