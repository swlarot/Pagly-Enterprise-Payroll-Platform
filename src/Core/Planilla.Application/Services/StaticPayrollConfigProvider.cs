// ====================================================================
// Planilla - StaticPayrollConfigProvider
// Descripción: Implementación stateless de IPayrollConfigProvider que
//              retorna configuración hardcoded de Panamá (DefaultPanamaPayrollConfig).
//              Pensado para el API Platform B2B — NO toca ApplicationDbContext
//              ni depende del HttpContext actual.
//
// Convenciones:
//   - Async por interface, pero sin await interno (Task.FromResult).
//   - No lanza excepción si companyId = 0 (es stateless, no hay tenant real).
//     El caller determina la identidad vía API key, no vía companyId.
//   - El parámetro companyId se pasa al DTO como TenantIdForDto solo
//     para preservar trazabilidad en logs.
// ====================================================================

using Vorluno.Planilla.Application.Configuration;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// IPayrollConfigProvider stateless usado por endpoints del API Platform.
/// No lee base de datos. Retorna DefaultPanamaPayrollConfig.
/// </summary>
public class StaticPayrollConfigProvider : IPayrollConfigProvider
{
    /// <summary>
    /// Retorna la configuración de Panamá vigente para la fecha solicitada.
    /// Determina la fase de la Reforma CSS (Ley 462) según effectiveDate.
    /// Nunca retorna null — siempre hay una fase aplicable.
    /// </summary>
    public Task<PayrollTaxConfigDto?> GetTaxConfigAsync(int companyId, DateTime effectiveDate)
    {
        var config = DefaultPanamaPayrollConfig.GetConfigForDate(effectiveDate, companyId);
        return Task.FromResult<PayrollTaxConfigDto?>(config);
    }

    /// <summary>
    /// Retorna los brackets ISR para el año fiscal solicitado.
    /// La estructura de brackets Panamá es estable (exento hasta $11K, 15%, 25%),
    /// aplicable a 2026 y en adelante hasta que cambie la ley.
    /// </summary>
    public Task<List<TaxBracketDto>> GetTaxBracketsAsync(int companyId, int year)
    {
        var brackets = DefaultPanamaPayrollConfig.GetTaxBracketsForYear(year, companyId);
        return Task.FromResult(brackets);
    }
}
