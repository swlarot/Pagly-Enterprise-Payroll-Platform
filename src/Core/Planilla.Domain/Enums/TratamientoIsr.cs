namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Cómo trata el motor de ISR a cada concepto de la planilla.
/// Especificación funcional del motor de ISR, sección 8.
/// </summary>
public enum TratamientoIsr
{
    /// <summary>
    /// Grava y se acumula: entra en la base del año y se proyecta.
    /// Salario, horas extra, recargos, comisiones y bonificaciones.
    /// </summary>
    GravableAcumulable = 0,

    /// <summary>No entra en la base del ISR.</summary>
    NoGravable = 1,

    /// <summary>
    /// Décimo tercer mes. Se acumula por separado para poder conciliarlo,
    /// aunque forme parte de la base anual.
    /// </summary>
    DecimoTercerMes = 2,

    /// <summary>
    /// Gasto de representación. Tiene tarifa propia (10% hasta 25,000 y 15% sobre
    /// el excedente) y no se mezcla con el salario ordinario.
    /// </summary>
    GastoRepresentacion = 3,

    /// <summary>Conceptos de liquidación con tratamiento fiscal especial.</summary>
    LiquidacionEspecial = 4
}
