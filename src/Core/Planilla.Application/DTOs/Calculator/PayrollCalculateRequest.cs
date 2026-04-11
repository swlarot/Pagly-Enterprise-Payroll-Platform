using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Calculator;

/// <summary>
/// Request DTO para <c>POST /v1/payroll/calculate</c>.
/// Contiene todos los parámetros primitivos que necesita el orchestrator
/// para calcular la planilla de UN empleado en un período.
///
/// <para>
/// Diseñado stateless: el caller provee TODOS los datos (no se lee BD).
/// Un cliente del API Platform puede calcular sin tener empleados persistidos.
/// </para>
/// </summary>
public class PayrollCalculateRequest
{
    /// <summary>
    /// Salario bruto del período (devengado). Positivo, sin decimales forzados.
    /// </summary>
    [Required]
    [Range(0.01, 1_000_000.00, ErrorMessage = "grossPay debe estar entre 0.01 y 1,000,000")]
    public decimal GrossPay { get; set; }

    /// <summary>
    /// Frecuencia de pago. Valores válidos: "Semanal", "Bisemanal", "Quincenal", "Mensual".
    /// </summary>
    [Required]
    [RegularExpression("^(Semanal|Bisemanal|Quincenal|Mensual)$",
        ErrorMessage = "payFrequency debe ser uno de: Semanal, Bisemanal, Quincenal, Mensual")]
    public string PayFrequency { get; set; } = "Mensual";

    /// <summary>
    /// Años cotizados por el empleado. Usado para determinar el tope de pensión CSS
    /// según Ley 462 Art. 178 (25+ años → tope intermedio, 30+ → tope alto).
    /// </summary>
    [Range(0, 70, ErrorMessage = "yearsCotized debe estar entre 0 y 70")]
    public int YearsCotized { get; set; }

    /// <summary>
    /// Salario promedio de los últimos 10 años. Usado junto con yearsCotized
    /// para calificar a topes superiores de pensión.
    /// </summary>
    [Range(0, 1_000_000.00)]
    public decimal AverageSalaryLast10Years { get; set; }

    /// <summary>
    /// Tasa de riesgo profesional aplicada (Acuerdo N°2 de 1995).
    /// Típicamente 0.56 (oficinas), 2.10 (manufactura), 5.67 (construcción).
    /// Se usa tal cual como porcentaje — no se valida contra la lista porque
    /// algunos sectores tienen tasas intermedias.
    /// </summary>
    [Range(0, 10.0, ErrorMessage = "cssRiskPercentage debe estar entre 0 y 10")]
    public decimal CssRiskPercentage { get; set; }

    /// <summary>
    /// Número de dependientes declarados por el empleado. Usado para deducción ISR.
    /// No hay límite legal duro (Ley 37/2018 + Decreto 368/2018), pero cap a 99
    /// para evitar valores absurdos.
    /// </summary>
    [Range(0, 99, ErrorMessage = "dependents debe estar entre 0 y 99")]
    public int Dependents { get; set; }

    /// <summary>
    /// ¿Está sujeto a CSS? (default true). Casos típicos de false: contractistas.
    /// </summary>
    public bool IsSubjectToCss { get; set; } = true;

    /// <summary>
    /// ¿Está sujeto a Seguro Educativo? (default true).
    /// </summary>
    public bool IsSubjectToEducationalInsurance { get; set; } = true;

    /// <summary>
    /// ¿Está sujeto a Impuesto Sobre la Renta? (default true).
    /// </summary>
    public bool IsSubjectToIncomeTax { get; set; } = true;

    /// <summary>
    /// Fecha del período para determinar la configuración vigente
    /// (ej: fase Ley 462 aplicable, año fiscal de brackets ISR).
    /// Si es null, se usa DateTime.UtcNow.
    /// </summary>
    public DateTime? CalculationDate { get; set; }
}
