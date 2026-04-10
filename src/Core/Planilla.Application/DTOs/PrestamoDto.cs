using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para visualización de préstamos con información calculada.
/// </summary>
public record PrestamoDto(
    int Id,
    int EmpleadoId,
    string EmpleadoNombre,
    string Descripcion,
    decimal MontoOriginal,
    decimal MontoPendiente,
    decimal CuotaMensual,
    decimal TasaInteres,
    DateTime FechaInicio,
    DateTime? FechaFin,
    int NumeroCuotas,
    int CuotasPagadas,
    int CuotasRestantes,  // Calculado
    EstadoPrestamo Estado,
    string EstadoNombre,  // Nombre legible del estado
    string? Referencia,
    decimal PorcentajePagado,  // Calculado
    PayPeriodType FrecuenciaCuota,
    string FrecuenciaCuotaNombre
);
