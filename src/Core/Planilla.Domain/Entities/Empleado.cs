using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

public class Empleado : ITenantEntity
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

    /// <summary>
    /// Email del empleado (para contacto y acceso al sistema si se crea usuario)
    /// </summary>
    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    [Range(0, double.MaxValue, ErrorMessage = "El salario base no puede ser negativo.")]
    public decimal SalarioBase { get; set; }

    public DateTime FechaContratacion { get; set; }

    public bool EstaActivo { get; set; } = true;

    // ====================================================================
    // Soft Delete - NUNCA hard delete (CSS requiere retención 5+ años)
    // ====================================================================

    /// <summary>
    /// Indica si el empleado fue eliminado (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Fecha y hora en que el empleado fue eliminado
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// ID del usuario que eliminó el empleado
    /// </summary>
    [StringLength(450)]
    public string? DeletedBy { get; set; }

    // ====================================================================
    // Phase E: Campos para c�lculo de planilla
    // ====================================================================

    /// <summary>
    /// ID del tenant al que pertenece el empleado (multi-tenancy)
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// ID del usuario vinculado (para permitir que empleados accedan como usuarios)
    /// </summary>
    [StringLength(450)]
    public string? UserId { get; set; }

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

    // ====================================================================
    // Pay Info — Configuración de pago por horas
    // ====================================================================

    /// <summary>
    /// Tipo de período de pago (reemplaza PayFrequency string).
    /// Determina cómo se anualiza el salario para ISR.
    /// </summary>
    public PayPeriodType PayPeriodType { get; set; } = PayPeriodType.Quincenal;

    /// <summary>
    /// Horas semanales del contrato laboral (Panamá estándar: 48 horas = 8h × 6 días)
    /// Código de Trabajo Art. 31: máximo 48 horas semanales diurnas
    /// </summary>
    public int HoursPerWeek { get; set; } = 48;

    /// <summary>
    /// Horas del período, calculadas según PayPeriodType:
    /// - Semanal: HoursPerWeek (48)
    /// - Bisemanal: HoursPerWeek × 2 (96)
    /// - Quincenal: HoursPerWeek × 2.167 (~104)
    /// - Mensual: HoursPerWeek × 4.333 (~208)
    /// El usuario puede overridear este cálculo.
    /// </summary>
    [Column(TypeName = "decimal(8, 2)")]
    public decimal HoursPerPeriod { get; set; } = 104m;

    /// <summary>
    /// Tasa por hora calculada: SalarioBase / HoursPerPeriod.
    /// Se almacena para performance pero se recalcula cuando cambia SalarioBase o HoursPerPeriod.
    /// Usado para calcular horas extra, dominicales, feriados.
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal HourlyRate { get; set; } = 0m;

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

    // Navigation property para Usuario (si está vinculado)
    public virtual AppUser? User { get; set; }

    // ====================================================================
    // Métodos helper para Pay Info
    // ====================================================================

    /// <summary>
    /// Sincroniza PayFrequency (legacy string) con PayPeriodType (nuevo enum).
    /// </summary>
    public void SyncPayFrequencyFromType()
    {
        PayFrequency = PayPeriodType switch
        {
            PayPeriodType.Semanal => "Semanal",
            PayPeriodType.Bisemanal => "Bisemanal",
            PayPeriodType.Quincenal => "Quincenal",
            PayPeriodType.Mensual => "Mensual",
            _ => "Quincenal"
        };
    }

    /// <summary>
    /// Recalcula HourlyRate basado en SalarioBase y HoursPerPeriod.
    /// </summary>
    public void RecalculateHourlyRate()
    {
        HourlyRate = HoursPerPeriod > 0 ? Math.Round(SalarioBase / HoursPerPeriod, 4) : 0;
    }

    /// <summary>
    /// Calcula HoursPerPeriod sugerido basado en HoursPerWeek y PayPeriodType.
    /// </summary>
    public static decimal CalculateSuggestedHoursPerPeriod(int hoursPerWeek, PayPeriodType periodType)
    {
        return periodType switch
        {
            PayPeriodType.Semanal => hoursPerWeek,
            PayPeriodType.Bisemanal => hoursPerWeek * 2m,
            PayPeriodType.Quincenal => Math.Round(hoursPerWeek * (52m / 24m), 0),
            PayPeriodType.Mensual => Math.Round(hoursPerWeek * (52m / 12m), 0),
            _ => hoursPerWeek * 2m
        };
    }
}