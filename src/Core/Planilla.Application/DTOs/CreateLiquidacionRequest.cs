using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para crear una nueva liquidación laboral.
/// </summary>
public record CreateLiquidacionRequest(
    int EmpleadoId,
    DateTime FechaTerminacion,
    TipoTerminacion TipoTerminacion,
    bool IncluyePreaviso = false,
    decimal? DiasVacacionesPendientes = null,
    decimal? DiasSalarioPendiente = null,
    string? Observaciones = null
);
