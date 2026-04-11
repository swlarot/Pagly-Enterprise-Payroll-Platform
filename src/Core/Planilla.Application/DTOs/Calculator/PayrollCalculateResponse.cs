namespace Vorluno.Planilla.Application.DTOs.Calculator;

/// <summary>
/// Response DTO para <c>POST /v1/payroll/calculate</c>.
/// Contiene el breakdown completo del cálculo + metadatos de versión.
///
/// Los campos siguen la convención camelCase del API (configurado en Program.cs).
/// </summary>
public record PayrollCalculateResponse(
    // ====================================================================
    // Deducciones del empleado
    // ====================================================================

    /// <summary>Salario bruto del período (echo del input).</summary>
    decimal GrossPay,

    /// <summary>Cuota CSS del empleado (9.75% con tope pensional Ley 462 Art. 178).</summary>
    decimal CssEmployee,

    /// <summary>Cuota Seguro Educativo del empleado (1.25% sin tope).</summary>
    decimal EducationalInsuranceEmployee,

    /// <summary>Retención ISR del período (brackets DGI con proyección ×13 meses).</summary>
    decimal IncomeTax,

    /// <summary>Suma CSS+SE+ISR del empleado.</summary>
    decimal TotalDeductions,

    /// <summary>Pago neto al empleado (bruto − deducciones legales).</summary>
    decimal NetPay,

    // ====================================================================
    // Costos patronales
    // ====================================================================

    /// <summary>Cuota CSS patronal (fase Ley 462 aplicable — 13.25% / 14.25% / 15.25%).</summary>
    decimal CssEmployer,

    /// <summary>Cuota Seguro Educativo patronal (1.50% sin tope).</summary>
    decimal EducationalInsuranceEmployer,

    /// <summary>Riesgo profesional (Acuerdo N°2 de 1995, sin tope).</summary>
    decimal RiskContribution,

    /// <summary>Costo patronal total (CSS patronal + riesgo + SE patronal).</summary>
    decimal TotalEmployerCost,

    // ====================================================================
    // Metadata
    // ====================================================================

    /// <summary>Versión del set de reglas usado (<c>DefaultPanamaPayrollConfig.Version</c>).</summary>
    string Version,

    /// <summary>Fecha usada para resolver la fase Ley 462 y los brackets ISR.</summary>
    DateTime CalculationDate
);
