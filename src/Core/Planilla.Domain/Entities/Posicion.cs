namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Nivel de riesgo profesional de una posición (afecta tasa CSS Riesgo Profesional)
/// </summary>
public enum NivelRiesgo
{
    Bajo = 0,     // 0.56% - Actividades administrativas, oficina
    Medio = 1,    // 2.50% - Actividades con riesgo moderado
    Alto = 2      // 5.39% - Actividades de alto riesgo (construcción, maquinaria pesada, etc.)
}

/// <summary>
/// Representa una posición o cargo dentro de un departamento
/// </summary>
public class Posicion
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    public required string Codigo { get; set; } // Código único (ej: "GER-VEN", "ASIST-ADM")

    public string? Descripcion { get; set; }

    public bool EstaActivo { get; set; } = true;

    /// <summary>
    /// ID del tenant al que pertenece esta posición
    /// </summary>
    public int TenantId { get; set; }

    // Departamento al que pertenece
    public int DepartamentoId { get; set; }

    // Rango salarial
    public decimal SalarioMinimo { get; set; }
    public decimal SalarioMaximo { get; set; }

    // Nivel de riesgo para cálculo CSS Riesgo Profesional
    public NivelRiesgo NivelRiesgo { get; set; } = NivelRiesgo.Bajo;

    // Auditoría
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Departamento Departamento { get; set; } = null!;
    public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// Obtiene el porcentaje de riesgo profesional según el nivel
    /// </summary>
    public decimal GetCssRiskPercentage()
    {
        return NivelRiesgo switch
        {
            NivelRiesgo.Bajo => 0.56m,
            NivelRiesgo.Medio => 2.50m,
            NivelRiesgo.Alto => 5.39m,
            _ => 0.56m
        };
    }
}
