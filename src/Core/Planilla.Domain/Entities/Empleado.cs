using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vorluno.Planilla.Domain.Entities;

public class Empleado
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede tener m�s de 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede tener m�s de 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El n�mero de identificaci�n es obligatorio.")]
    [StringLength(20, ErrorMessage = "El n�mero de identificaci�n no puede tener m�s de 20 caracteres.")]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 2)")]
    [Range(0, double.MaxValue, ErrorMessage = "El salario base no puede ser negativo.")]
    public decimal SalarioBase { get; set; }

    public DateTime FechaContratacion { get; set; }

    public bool EstaActivo { get; set; } = true;

    // ====================================================================
    // Phase E: Campos para c�lculo de planilla
    // ====================================================================

    /// <summary>
    /// ID del tenant al que pertenece el empleado (multi-tenancy)
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Departamento al que pertenece el empleado (opcional)
    /// </summary>
    public int? DepartamentoId { get; set; }

    /// <summary>
    /// Posici�n o cargo del empleado (opcional)
    /// </summary>
    public int? PosicionId { get; set; }

    /// <summary>
    /// A�os cotizados en CSS (determina tope CSS: 25 a�os ? intermedio, 30 a�os ? alto)
    /// </summary>
    public int YearsCotized { get; set; } = 0;

    /// <summary>
    /// Salario promedio �ltimos 10 a�os (para determinar tope CSS alto)
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal AverageSalaryLast10Years { get; set; } = 0;

    /// <summary>
    /// Porcentaje de riesgo profesional CSS: 0.56 (bajo), 2.50 (medio), 5.39 (alto)
    /// </summary>
    [Column(TypeName = "decimal(5, 2)")]
    public decimal CssRiskPercentage { get; set; } = 0.56m;

    /// <summary>
    /// Frecuencia de pago: "Quincenal", "Mensual", "Semanal"
    /// </summary>
    [StringLength(20)]
    public string PayFrequency { get; set; } = "Quincenal";

    /// <summary>
    /// N�mero de dependientes declarados (m�ximo 3 para deducci�n ISR)
    /// </summary>
    public int Dependents { get; set; } = 0;

    /// <summary>
    /// Indica si el empleado est� sujeto a CSS
    /// </summary>
    public bool IsSubjectToCss { get; set; } = true;

    /// <summary>
    /// Indica si el empleado est� sujeto a Seguro Educativo
    /// </summary>
    public bool IsSubjectToEducationalInsurance { get; set; } = true;

    /// <summary>
    /// Indica si el empleado est� sujeto a Impuesto Sobre la Renta (ISR)
    /// </summary>
    public bool IsSubjectToIncomeTax { get; set; } = true;

    // Propiedad de navegaci�n: un empleado puede tener muchos recibos de sueldo.
    // La clase ReciboDeSueldo ya est� implementada y representa cada uno de ellos.
    public virtual ICollection<ReciboDeSueldo> RecibosDeSueldo { get; set; } = new List<ReciboDeSueldo>();

    // Navigation properties para Departamento y Posicion
    public virtual Departamento? Departamento { get; set; }
    public virtual Posicion? Posicion { get; set; }

    // Navigation property para Tenant
    public virtual Tenant? Tenant { get; set; }
}