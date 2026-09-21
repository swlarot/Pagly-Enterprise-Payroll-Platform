namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// De dónde sale el devengado de un mes de un empleado.
/// </summary>
public enum OrigenDevengado
{
    /// <summary>Calculado por Pagly a partir de las planillas aprobadas del mes. Nunca se guarda: se deriva.</summary>
    Planilla = 0,

    /// <summary>Cargado desde la plantilla de importación al migrar la empresa.</summary>
    Importado = 1,

    /// <summary>Escrito a mano por el usuario (por ejemplo, un mes que faltaba para el décimo).</summary>
    Manual = 2,

    /// <summary>No hay planilla ni dato cargado para ese mes.</summary>
    SinDatos = 3
}
