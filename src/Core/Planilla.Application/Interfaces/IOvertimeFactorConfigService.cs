using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Resuelve los factores de horas extra de un tenant, cayendo al valor legal
/// cuando el tenant no ha definido un override propio.
/// </summary>
public interface IOvertimeFactorConfigService
{
    /// <summary>Factor vigente para un tipo (override del tenant o valor legal).</summary>
    Task<decimal> GetFactorAsync(TipoHoraExtra tipo, CancellationToken ct = default);

    /// <summary>Recargo vigente por exceso de límites (Art. 36.4).</summary>
    Task<decimal> GetFactorExcesoAsync(CancellationToken ct = default);

    /// <summary>Todos los factores del tenant, con su referencia legal, para la UI.</summary>
    Task<OvertimeFactorConfigDto> GetConfigAsync(CancellationToken ct = default);

    /// <summary>Guarda overrides. Un factor null elimina el override y vuelve al valor legal.</summary>
    Task UpdateConfigAsync(UpdateOvertimeFactorsRequest request, string? userId, CancellationToken ct = default);

    /// <summary>Elimina todos los overrides del tenant y vuelve a los factores legales.</summary>
    Task ResetToLegalAsync(CancellationToken ct = default);
}
