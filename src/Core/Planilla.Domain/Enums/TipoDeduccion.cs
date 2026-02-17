namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipos de deducciones que se pueden aplicar a un empleado.
/// </summary>
public enum TipoDeduccion
{
    /// <summary>
    /// Préstamo otorgado por la empresa al empleado
    /// </summary>
    PrestamoInterno = 1,

    /// <summary>
    /// Descuento para pago de préstamo bancario
    /// </summary>
    PrestamoBancario = 2,

    /// <summary>
    /// Pensión alimenticia por orden judicial
    /// </summary>
    PensionAlimenticia = 3,

    /// <summary>
    /// Embargo judicial sobre salario
    /// </summary>
    Embargo = 4,

    /// <summary>
    /// Descuento para seguro médico privado
    /// </summary>
    SeguroMedico = 5,

    /// <summary>
    /// Ahorro voluntario en cooperativa
    /// </summary>
    AhorroVoluntario = 6,

    /// <summary>
    /// Cuota sindical
    /// </summary>
    Sindicato = 7,

    /// <summary>
    /// Descuentos de cooperativas (Ley 17 de 1997)
    /// </summary>
    Cooperativa = 8,

    /// <summary>
    /// Otro tipo de deducción no clasificada
    /// </summary>
    Otro = 99
}
