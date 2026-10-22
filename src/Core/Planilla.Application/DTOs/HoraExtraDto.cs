using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

public record HoraExtraDto(
    int Id,
    int EmpleadoId,
    string EmpleadoNombre,
    DateTime Fecha,
    TipoHoraExtra TipoHoraExtra,
    string TipoNombre,
    TimeSpan HoraInicio,
    TimeSpan HoraFin,
    decimal CantidadHoras,
    decimal FactorMultiplicador,
    bool EsExceso,
    decimal? FactorExceso,
    decimal? MontoCalculado,
    bool EstaAprobada,
    string? AprobadoPor,
    string Motivo,
    string? Observaciones,
    int? PlanillaDetailId,
    // "Pendiente" | "Aprobada" | "Rechazada" | "Pagada"
    string Estado,
    DateTime? FechaAprobacion,
    DateTime? FechaRechazo,
    string? RechazadoPor,
    string? MotivoRechazo
);

/// <summary>
/// Decisión del director sobre varias horas extra a la vez.
/// Decision: "aprobar" | "rechazar" | "pendiente".
/// </summary>
public record DecidirHorasExtraRequest(List<int> Ids, string Decision, string? Motivo);

public record RechazarHoraExtraRequest(string? Motivo);
