namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Causa de terminación de la relación laboral según el Código de Trabajo de Panamá
/// (Arts. 210, 213). Determina qué prestaciones devenga la liquidación.
///
/// Es un catálogo más rico que <see cref="TipoTerminacion"/> (que el usuario elige
/// en la UI) para poder modelar fielmente las reglas legales de indemnización (Art. 225)
/// y preaviso (Art. 211). El mapeo desde <see cref="TipoTerminacion"/> vive en
/// <see cref="CausaTerminacionExtensions.ToCausaTerminacion"/>.
/// </summary>
public enum CausaTerminacion
{
    /// <summary>Renuncia voluntaria (Art. 222) — no devenga indemnización Art. 225.</summary>
    RenunciaSimple = 0,

    /// <summary>Renuncia con justa causa por culpa del empleador (Art. 223) — devenga Art. 225.</summary>
    RenunciaJustaCausa = 1,

    /// <summary>Despido justificado, falta del trabajador probada (Art. 213.A) — no devenga Art. 225.</summary>
    DespidoJustificadoA = 2,

    /// <summary>Despido injustificado (Art. 218) — devenga indemnización Art. 225.</summary>
    DespidoInjustificado = 3,

    /// <summary>Causa económica (Art. 213.C + 215) — devenga Art. 225.</summary>
    CausaEconomicaC = 4,

    /// <summary>Fuerza mayor / caso fortuito (Art. 213.B.7) — devenga Art. 225.</summary>
    FuerzaMayorB = 5,

    /// <summary>Mutuo acuerdo (Art. 210.1) — indemnización pactada en el acuerdo.</summary>
    MutuoAcuerdo = 6,

    /// <summary>Expiración del término pactado (Art. 210.2) — no devenga Art. 225.</summary>
    ExpiracionTerminoPactado = 7,

    /// <summary>Conclusión de la obra (Art. 210.3) — no devenga Art. 225.</summary>
    ConclusionObra = 8,

    /// <summary>Muerte del trabajador (Art. 210.4) — herederos cobran prima; sin Art. 225.</summary>
    MuerteTrabajador = 9,

    /// <summary>Muerte del empleador (Art. 210.5) — equiparable a B.7, devenga Art. 225.</summary>
    MuerteEmpleador = 10,

    /// <summary>Prolongación de la suspensión (Art. 210.6) — interpretación pro-trabajador, devenga Art. 225.</summary>
    ProlongacionSuspension = 11,

    /// <summary>Jubilación del trabajador — paga prima de antigüedad, sin indemnización Art. 225.</summary>
    Jubilacion = 12
}

public static class CausaTerminacionExtensions
{
    /// <summary>
    /// Mapea el <see cref="TipoTerminacion"/> elegido en la UI a la causa legal detallada.
    /// El enum de UI es limitado (5 valores); este mapeo lo traduce al catálogo del
    /// Código de Trabajo usado por las calculadoras de liquidación.
    /// </summary>
    public static CausaTerminacion ToCausaTerminacion(this TipoTerminacion tipo)
    {
        return tipo switch
        {
            TipoTerminacion.Despido => CausaTerminacion.DespidoInjustificado,
            TipoTerminacion.Renuncia => CausaTerminacion.RenunciaSimple,
            TipoTerminacion.MutuoAcuerdo => CausaTerminacion.MutuoAcuerdo,
            TipoTerminacion.DespidoJustificado => CausaTerminacion.DespidoJustificadoA,
            TipoTerminacion.Jubilacion => CausaTerminacion.Jubilacion,
            _ => CausaTerminacion.RenunciaSimple
        };
    }
}
