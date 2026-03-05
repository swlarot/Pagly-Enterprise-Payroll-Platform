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

    /// <summary>
    /// Salario base MENSUAL del empleado (regla de negocio Panamá - MITRADEL).
    /// Este valor SIEMPRE representa el salario mensual, independientemente del PayPeriodType.
    /// Para obtener el salario del período, usar GetSalarioPeriodo().
    /// </summary>
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
    /// A�os cotizados en CSS. M�nimo 20 a�os (240 cuotas) para jubilaci�n.
    /// 25 a�os → tope pens i�n intermedio ($2,000), 30 a�os → tope pens i�n alto ($2,500).
    /// </summary>
    public int YearsCotized { get; set; } = 0;

    /// <summary>
    /// Salario promedio �ltimos 10 a�os (para determinar tope CSS alto)
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal AverageSalaryLast10Years { get; set; } = 0;

    /// <summary>
    /// Porcentaje de riesgo profesional CSS (Acuerdo N°2 de 1995):
    /// 0.56 = Clase I (Riesgo Mínimo), 0.98 = Clase II,
    /// 2.10 = Clase III (Riesgo Medio), 3.64 = Clase IV, 5.67 = Clase V (Riesgo Máximo)
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
    /// Tasa por hora calculada con base mensual: SalarioBase (mensual) / (HoursPerWeek × 4.3333).
    /// Se almacena para performance pero se recalcula cuando cambia SalarioBase o HoursPerWeek.
    /// NO depende del PayPeriodType - siempre se calcula con base mensual.
    /// Usado para calcular horas extra, dominicales, feriados.
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal HourlyRate { get; set; } = 0m;

    /// <summary>
    /// Semanas por mes (promedio anual): 52/12 ≈ 4.333. Usado para tasa por hora mensual.
    /// </summary>
    public const decimal WeeksPerMonth = 52m / 12m;

    /// <summary>
    /// N�mero de dependientes declarados para deducci�n ISR ($800 c/u).
    /// Sin l�mite legal (Ley 37/2018 + Decreto 368/2018).
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
    /// Períodos por año según tipo de período (para convertir salario del período a mensual).
    /// </summary>
    public static int GetPeriodsPerYear(PayPeriodType periodType)
    {
        return periodType switch
        {
            PayPeriodType.Semanal => 52,
            PayPeriodType.Bisemanal => 26,
            PayPeriodType.Quincenal => 24,
            PayPeriodType.Mensual => 12,
            _ => 24
        };
    }

    /// <summary>
    /// Obtiene el salario mensual. SalarioBase YA es mensual, no necesita conversión.
    /// </summary>
    public decimal GetMonthlySalary()
    {
        return SalarioBase;
    }

    /// <summary>
    /// Calcula la tasa por hora con base mensual: SalarioMensual / (HoursPerWeek × 4.3333).
    /// SalarioBase debe ser el salario MENSUAL del empleado.
    /// Útil para fallbacks cuando HourlyRate no está persistido (ej. en controladores).
    /// </summary>
    public static decimal ComputeHourlyRateFromMonthly(decimal salarioMensual, int hoursPerWeek)
    {
        if (salarioMensual <= 0) return 0m;
        if (hoursPerWeek <= 0) return 0m;
        var horasPorMes = hoursPerWeek * WeeksPerMonth;
        return Math.Round(salarioMensual / horasPorMes, 4);
    }

    /// <summary>
    /// Recalcula HourlyRate con base mensual: SalarioBase (mensual) / (HoursPerWeek × 4.3333).
    /// SalarioBase debe ser el salario MENSUAL del empleado.
    /// Independiente del período de pago (semanal, bisemanal, quincenal, mensual).
    /// </summary>
    public void RecalculateHourlyRate()
    {
        var hoursPerMonth = (decimal)HoursPerWeek * WeeksPerMonth;
        HourlyRate = hoursPerMonth > 0
            ? Math.Round(SalarioBase / hoursPerMonth, 4)
            : 0;
    }

    /// <summary>
    /// Calcula el salario que se paga en cada período (cheque).
    /// SalarioBase (mensual) × 12 / períodos por año.
    /// </summary>
    public decimal GetSalarioPeriodo()
    {
        var periodsPerYear = GetPeriodsPerYear(PayPeriodType);
        return periodsPerYear > 0
            ? Math.Round(SalarioBase * 12m / periodsPerYear, 2)
            : SalarioBase;
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