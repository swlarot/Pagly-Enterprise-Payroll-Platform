using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IOvertimeFactorService
{
    TipoHoraExtra DetermineOvertimeType(DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin);

    /// <summary>
    /// Factor legal del Código de Trabajo, SIN aplicar los overrides del tenant.
    /// Para cálculos de nómina usar <see cref="CalculateBaseFactorAsync"/>.
    /// </summary>
    decimal CalculateBaseFactor(TipoHoraExtra tipo);

    /// <summary>
    /// Factor legal completo (con recargo por exceso), SIN overrides del tenant.
    /// Para cálculos de nómina usar <see cref="CalculateFactorAsync"/>.
    /// </summary>
    decimal CalculateFactor(TipoHoraExtra tipo, bool esExceso);

    /// <summary>Factor base vigente para el tenant actual (override configurado o valor legal).</summary>
    Task<decimal> CalculateBaseFactorAsync(TipoHoraExtra tipo, CancellationToken ct = default);

    /// <summary>Factor completo vigente para el tenant, incluyendo el recargo por exceso (Art. 36.4).</summary>
    Task<decimal> CalculateFactorAsync(TipoHoraExtra tipo, bool esExceso, CancellationToken ct = default);

    /// <summary>Recargo por exceso vigente para el tenant (legal: 1.75).</summary>
    Task<decimal> GetFactorExcesoAsync(CancellationToken ct = default);

    Task<(bool esExceso, string mensaje)> ValidateOvertimeLimits(int empleadoId, DateTime fecha, decimal horasNuevas);
}
