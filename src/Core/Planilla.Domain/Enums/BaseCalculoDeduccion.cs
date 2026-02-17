namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Base sobre la cual se calcula una deduccion por porcentaje.
/// </summary>
public enum BaseCalculoDeduccion
{
    /// <summary>Porcentaje sobre salario bruto</summary>
    SalarioBruto = 0,

    /// <summary>Porcentaje sobre neto post-legales (despues de CSS+SE+ISR)</summary>
    SalarioNeto = 1
}
