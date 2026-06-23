namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipo de duración del contrato — decide prima (Art. 224, Indefinido)
/// vs cesantía (Decreto 60/1995, Definido/PorObra).
/// </summary>
public enum TipoContratoDuracion
{
    Indefinido = 0,
    Definido = 1,
    PorObra = 2
}