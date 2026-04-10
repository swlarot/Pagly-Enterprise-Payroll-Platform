namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipo de terminación laboral según el Código de Trabajo de Panamá.
/// Determina qué componentes aplican en la liquidación.
/// </summary>
public enum TipoTerminacion
{
    /// <summary>Despido injustificado — incluye indemnización (Art. 225)</summary>
    Despido = 0,

    /// <summary>Renuncia voluntaria del trabajador</summary>
    Renuncia = 1,

    /// <summary>Terminación por mutuo acuerdo</summary>
    MutuoAcuerdo = 2,

    /// <summary>Despido justificado — NO incluye indemnización</summary>
    DespidoJustificado = 3,

    /// <summary>Jubilación del trabajador</summary>
    Jubilacion = 4
}

public static class TipoTerminacionExtensions
{
    public static string ToNombre(this TipoTerminacion tipo)
    {
        return tipo switch
        {
            TipoTerminacion.Despido => "Despido Injustificado",
            TipoTerminacion.Renuncia => "Renuncia Voluntaria",
            TipoTerminacion.MutuoAcuerdo => "Mutuo Acuerdo",
            TipoTerminacion.DespidoJustificado => "Despido Justificado",
            TipoTerminacion.Jubilacion => "Jubilación",
            _ => tipo.ToString()
        };
    }

    /// <summary>
    /// Indica si el tipo de terminación da derecho a indemnización (Art. 225).
    /// Solo aplica para despido injustificado.
    /// </summary>
    public static bool IncluyeIndemnizacion(this TipoTerminacion tipo)
    {
        return tipo == TipoTerminacion.Despido;
    }
}
