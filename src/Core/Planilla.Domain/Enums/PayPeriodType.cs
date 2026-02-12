namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Tipos de período de pago soportados.
/// Orden por frecuencia de uso en Panamá: Quincenal > Bisemanal > Semanal > Mensual
/// </summary>
public enum PayPeriodType
{
    /// <summary>Semanal — 52 períodos/año</summary>
    Semanal = 0,

    /// <summary>Bisemanal (cada 2 semanas) — 26 períodos/año</summary>
    Bisemanal = 1,

    /// <summary>Quincenal (1-15 y 16-fin de mes) — 24 períodos/año</summary>
    Quincenal = 2,

    /// <summary>Mensual — 12 períodos/año</summary>
    Mensual = 3
}
