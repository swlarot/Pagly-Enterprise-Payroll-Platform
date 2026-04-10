using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para visualización de liquidaciones laborales.
/// </summary>
public record LiquidacionDto(
    int Id,
    int EmpleadoId,
    string EmpleadoNombre,
    string Numero,
    DateTime FechaLiquidacion,
    DateTime FechaContratacion,
    DateTime FechaTerminacion,
    TipoTerminacion TipoTerminacion,
    string TipoTerminacionNombre,
    decimal SalarioBase,
    decimal SalarioPromedio,
    decimal AnosServicio,
    // Componentes
    decimal Indemnizacion,
    decimal Preaviso,
    decimal VacacionesProporcionales,
    decimal DiasVacacionesProporcionales,
    decimal DecimoTercerMesProporcional,
    decimal SalarioPendiente,
    decimal DiasSalarioPendiente,
    bool IncluyePreaviso,
    // Deducciones
    decimal CssEmpleado,
    decimal SeEmpleado,
    decimal Isr,
    // Costos patronales
    decimal CssPatronal,
    decimal SePatronal,
    // Totales
    decimal TotalBruto,
    decimal TotalDeducciones,
    decimal TotalNeto,
    // Estado
    EstadoLiquidacion Estado,
    string EstadoNombre,
    string? Observaciones,
    // Auditoría
    DateTime CreatedAt,
    string? CreatedBy,
    string? ApprovedBy,
    DateTime? ApprovedDate
);
