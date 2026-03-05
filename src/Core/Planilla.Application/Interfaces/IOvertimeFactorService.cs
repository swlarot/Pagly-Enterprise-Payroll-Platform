using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IOvertimeFactorService
{
    TipoHoraExtra DetermineOvertimeType(DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin);
    decimal CalculateBaseFactor(TipoHoraExtra tipo);
    decimal CalculateFactor(TipoHoraExtra tipo, bool esExceso);
    Task<(bool esExceso, string mensaje)> ValidateOvertimeLimits(int empleadoId, DateTime fecha, decimal horasNuevas);
}
