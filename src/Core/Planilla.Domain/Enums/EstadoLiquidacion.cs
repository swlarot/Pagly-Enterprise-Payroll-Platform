namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Estado del flujo de trabajo de una liquidación laboral.
/// </summary>
public enum EstadoLiquidacion
{
    /// <summary>Liquidación creada, pendiente de cálculo o revisión</summary>
    Borrador = 0,

    /// <summary>Liquidación calculada, lista para aprobación</summary>
    Calculada = 1,

    /// <summary>Liquidación aprobada por un autorizado</summary>
    Aprobada = 2,

    /// <summary>Liquidación pagada al empleado</summary>
    Pagada = 3
}

public static class EstadoLiquidacionExtensions
{
    public static string ToNombre(this EstadoLiquidacion estado)
    {
        return estado switch
        {
            EstadoLiquidacion.Borrador => "Borrador",
            EstadoLiquidacion.Calculada => "Calculada",
            EstadoLiquidacion.Aprobada => "Aprobada",
            EstadoLiquidacion.Pagada => "Pagada",
            _ => estado.ToString()
        };
    }
}
