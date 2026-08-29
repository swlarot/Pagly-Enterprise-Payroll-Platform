// ====================================================================
// Planilla - OvertimeFactorConfigService
// Descripción: Resuelve los factores de horas extra del tenant actual.
//              Si el tenant no definió un override para un tipo, devuelve
//              el factor legal de OvertimeClassifier (Arts. 33, 36, 48-50).
//
// Los overrides se cachean por instancia (scoped) para evitar una query
// por cada hora extra al procesar una planilla completa.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <inheritdoc cref="IOvertimeFactorConfigService"/>
public class OvertimeFactorConfigService : IOvertimeFactorConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    // Caché por scope: una planilla procesa muchas horas extra con la misma config.
    private Dictionary<TipoHoraExtra, decimal>? _cacheFactores;
    private decimal? _cacheFactorExceso;

    public OvertimeFactorConfigService(ApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<decimal> GetFactorAsync(TipoHoraExtra tipo, CancellationToken ct = default)
    {
        var overrides = await LoadOverridesAsync(ct);
        return overrides.TryGetValue(tipo, out var factor)
            ? factor
            : OvertimeClassifier.FactorBase(tipo);
    }

    public async Task<decimal> GetFactorExcesoAsync(CancellationToken ct = default)
    {
        await LoadOverridesAsync(ct);
        return _cacheFactorExceso ?? OvertimeClassifier.FactorExcesoLegal;
    }

    public async Task<OvertimeFactorConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var overrides = await LoadOverridesAsync(ct);

        var factores = OvertimeClassifier.TodosLosTipos
            .Select(tipo =>
            {
                var legal = OvertimeClassifier.FactorBase(tipo);
                var personalizado = overrides.TryGetValue(tipo, out var propio);
                return new OvertimeFactorDto(
                    Tipo: tipo,
                    Nombre: OvertimeClassifier.Nombre(tipo),
                    BaseLegal: OvertimeClassifier.BaseLegal(tipo),
                    FactorLegal: legal,
                    FactorVigente: personalizado ? propio : legal,
                    EsPersonalizado: personalizado
                );
            })
            .ToList();

        return new OvertimeFactorConfigDto(
            TenantId: _tenantContext.TenantId,
            Factores: factores,
            FactorExcesoLegal: OvertimeClassifier.FactorExcesoLegal,
            FactorExcesoVigente: _cacheFactorExceso ?? OvertimeClassifier.FactorExcesoLegal,
            FactorExcesoEsPersonalizado: _cacheFactorExceso.HasValue
        );
    }

    public async Task UpdateConfigAsync(
        UpdateOvertimeFactorsRequest request, string? userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _tenantContext.TenantId;
        var existentes = await _context.OvertimeFactorConfigurations
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var item in request.Factores ?? Array.Empty<UpdateOvertimeFactorItem>())
        {
            var fila = existentes.FirstOrDefault(o => o.Tipo == item.Tipo && !o.EsFactorExceso);

            // Factor null (o igual al legal) ⇒ el tenant vuelve al valor de ley: se borra el override.
            if (item.Factor is null || item.Factor == OvertimeClassifier.FactorBase(item.Tipo))
            {
                if (fila is not null) _context.OvertimeFactorConfigurations.Remove(fila);
                continue;
            }

            if (fila is null)
            {
                _context.OvertimeFactorConfigurations.Add(new OvertimeFactorConfiguration
                {
                    TenantId = tenantId,
                    Tipo = item.Tipo,
                    Factor = item.Factor.Value,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedByUserId = userId
                });
            }
            else
            {
                fila.Factor = item.Factor.Value;
                fila.IsActive = true;
                fila.UpdatedAt = DateTime.UtcNow;
                fila.UpdatedByUserId = userId;
            }
        }

        // Recargo por exceso: se guarda en una fila marcada, no ligada a un tipo puntual.
        var filaExceso = existentes.FirstOrDefault(o => o.EsFactorExceso);
        if (request.FactorExceso is null || request.FactorExceso == OvertimeClassifier.FactorExcesoLegal)
        {
            if (filaExceso is not null) _context.OvertimeFactorConfigurations.Remove(filaExceso);
        }
        else if (filaExceso is null)
        {
            _context.OvertimeFactorConfigurations.Add(new OvertimeFactorConfiguration
            {
                TenantId = tenantId,
                Tipo = TipoHoraExtra.Diurna, // valor de relleno: la fila se identifica por EsFactorExceso
                Factor = 0m,
                FactorExceso = request.FactorExceso.Value,
                EsFactorExceso = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedByUserId = userId
            });
        }
        else
        {
            filaExceso.FactorExceso = request.FactorExceso.Value;
            filaExceso.UpdatedAt = DateTime.UtcNow;
            filaExceso.UpdatedByUserId = userId;
        }

        await _context.SaveChangesAsync(ct);
        InvalidarCache();
    }

    public async Task ResetToLegalAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var filas = await _context.OvertimeFactorConfigurations
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct);

        if (filas.Count > 0)
        {
            _context.OvertimeFactorConfigurations.RemoveRange(filas);
            await _context.SaveChangesAsync(ct);
        }

        InvalidarCache();
    }

    // ================== Helpers ==================

    private async Task<Dictionary<TipoHoraExtra, decimal>> LoadOverridesAsync(CancellationToken ct)
    {
        if (_cacheFactores is not null) return _cacheFactores;

        var tenantId = _tenantContext.TenantId;
        var filas = await _context.OvertimeFactorConfigurations
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .ToListAsync(ct);

        _cacheFactorExceso = filas.FirstOrDefault(o => o.EsFactorExceso)?.FactorExceso;

        _cacheFactores = filas
            .Where(o => !o.EsFactorExceso)
            .GroupBy(o => o.Tipo)
            .ToDictionary(g => g.Key, g => g.First().Factor);

        return _cacheFactores;
    }

    private void InvalidarCache()
    {
        _cacheFactores = null;
        _cacheFactorExceso = null;
    }
}
