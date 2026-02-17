namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Estado de una orden judicial asociada a una deduccion (pension alimenticia o embargo).
/// </summary>
public enum EstadoOrdenJudicial
{
    /// <summary>Orden vigente y activa</summary>
    Vigente = 1,

    /// <summary>Orden temporalmente suspendida</summary>
    Suspendida = 2,

    /// <summary>Orden levantada definitivamente</summary>
    Levantada = 3
}
