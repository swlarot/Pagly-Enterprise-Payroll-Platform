using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.ApiKeys;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Panel de analytics del API Platform para el System Admin.
/// A diferencia de ApiKeysController.GetUsageStats (per-tenant), estos endpoints
/// cruzan TODA la plataforma y están restringidos al policy "RequireSystemAdmin".
///
/// <para>
/// No aplica filtro de TenantContext: el SystemAdmin necesita ver uso across tenants.
/// ApiUsageRecord NO tiene global query filter, así que las queries son directas.
/// </para>
/// </summary>
[ApiController]
[Route("api/system-admin/api-usage")]
[Authorize(Policy = "RequireSystemAdmin")]
public class SystemApiUsageController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SystemApiUsageController> _logger;

    // Umbrales para detectar señales. Conservadores en v0.
    private const decimal HighErrorRateThresholdPercent = 15m;
    private const int TrafficSpikeThresholdMultiplier = 3;      // 3x promedio del período
    private const int TrafficSpikeMinimumVolume = 50;           // solo si volumen total > 50 req
    private const int ChurnDaysWithoutActivity = 14;

    public SystemApiUsageController(
        ApplicationDbContext db,
        ILogger<SystemApiUsageController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Analytics global agregado. Core del dashboard del System Admin.
    /// </summary>
    /// <param name="since">Inicio del período (default: hace 30 días).</param>
    /// <param name="until">Fin del período (default: ahora).</param>
    /// <param name="topN">Cantidad de tenants a retornar en el ranking (default 20, max 100).</param>
    [HttpGet("global")]
    [ProducesResponseType(typeof(SystemApiUsageDto), 200)]
    public async Task<ActionResult<SystemApiUsageDto>> GetGlobalUsage(
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null,
        [FromQuery] int topN = 20,
        CancellationToken cancellationToken = default)
    {
        var periodEnd = until ?? DateTime.UtcNow;
        var periodStart = since ?? periodEnd.AddDays(-30);
        topN = Math.Clamp(topN, 1, 100);

        var records = _db.ApiUsageRecords
            .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd);

        // ====================================================================
        // 1. Summary — contadores globales agregados
        // ====================================================================
        var totalRequests = await records.CountAsync(cancellationToken);
        var successfulRequests = await records.CountAsync(
            r => r.StatusCode >= 200 && r.StatusCode < 300, cancellationToken);
        var clientErrors = await records.CountAsync(
            r => r.StatusCode >= 400 && r.StatusCode < 500, cancellationToken);
        var serverErrors = await records.CountAsync(
            r => r.StatusCode >= 500, cancellationToken);

        var errorRate = totalRequests > 0
            ? Math.Round(((decimal)(clientErrors + serverErrors) / totalRequests) * 100m, 2)
            : 0m;

        // Tenants distintos + keys distintas activas en el período
        var activeTenants = await records
            .Select(r => r.TenantId).Distinct().CountAsync(cancellationToken);

        var activeKeyIds = await records
            .Where(r => r.ApiKeyId != null)
            .Select(r => r.ApiKeyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var activeKeys = activeKeyIds.Count;

        // Latencia promedio y p95 — materializar para calcular p95 en memoria
        var responseTimes = await records
            .Select(r => r.ResponseTimeMs)
            .ToListAsync(cancellationToken);
        var avgMs = responseTimes.Count > 0 ? (int)responseTimes.Average() : 0;
        var p95Ms = responseTimes.Count > 0
            ? responseTimes.OrderBy(t => t).ElementAt((int)(responseTimes.Count * 0.95))
            : 0;

        // Pico requests por minuto — agrupar por (año,mes,día,hora,minuto) y tomar max
        var perMinuteRaw = await records
            .GroupBy(r => new
            {
                r.CreatedAt.Year,
                r.CreatedAt.Month,
                r.CreatedAt.Day,
                r.CreatedAt.Hour,
                r.CreatedAt.Minute
            })
            .Select(g => g.Count())
            .ToListAsync(cancellationToken);
        var peakPerMinute = perMinuteRaw.Count > 0 ? perMinuteRaw.Max() : 0;

        var summary = new SystemUsageSummaryDto(
            TotalRequests: totalRequests,
            ActiveTenants: activeTenants,
            ActiveKeys: activeKeys,
            SuccessfulRequests: successfulRequests,
            ClientErrors: clientErrors,
            ServerErrors: serverErrors,
            ErrorRatePercent: errorRate,
            AvgResponseTimeMs: avgMs,
            P95ResponseTimeMs: p95Ms,
            PeakRequestsPerMinute: peakPerMinute,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd
        );

        // ====================================================================
        // 2. Daily usage (series de tiempo agregadas)
        // ====================================================================
        var dailyRaw = await records
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count(),
                SuccessCount = g.Count(r => r.StatusCode >= 200 && r.StatusCode < 300),
                ErrorCount = g.Count(r => r.StatusCode >= 400)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);
        var dailyUsage = dailyRaw
            .Select(d => new DailyUsageDto(d.Date, d.Count, d.SuccessCount, d.ErrorCount))
            .ToList();

        // ====================================================================
        // 3. Status breakdown global
        // ====================================================================
        var statusRaw = await records
            .GroupBy(r => r.StatusCode)
            .Select(g => new { StatusCode = g.Key, Count = g.Count() })
            .OrderByDescending(s => s.Count)
            .ToListAsync(cancellationToken);
        var statusBreakdown = statusRaw
            .Select(s => new StatusBreakdownDto(
                s.StatusCode,
                s.Count,
                s.StatusCode switch
                {
                    200 => "OK",
                    400 => "Bad Request",
                    401 => "Unauthorized",
                    403 => "Forbidden",
                    429 => "Rate Limited",
                    >= 500 => "Server Error",
                    _ => $"HTTP {s.StatusCode}"
                }))
            .ToList();

        // ====================================================================
        // 4. Ranking de tenants + joins con Tenant + Subscription
        // ====================================================================
        // Primero agregamos por tenant
        var tenantAggRaw = await records
            .GroupBy(r => r.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                TotalRequests = g.Count(),
                SuccessfulRequests = g.Count(r => r.StatusCode >= 200 && r.StatusCode < 300),
                ErrorRequests = g.Count(r => r.StatusCode >= 400),
                AvgResponseTimeMs = (int)g.Average(r => r.ResponseTimeMs),
                FirstRequestAt = g.Min(r => r.CreatedAt),
                LastRequestAt = g.Max(r => r.CreatedAt)
            })
            .OrderByDescending(t => t.TotalRequests)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var tenantIdsInRanking = tenantAggRaw.Select(t => t.TenantId).ToList();

        // Enriquecer con nombre + plan
        var tenantInfos = await _db.Tenants
            .Where(t => tenantIdsInRanking.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Subdomain,
                Plan = _db.Subscriptions
                    .Where(s => s.TenantId == t.Id)
                    .Select(s => (SubscriptionPlan?)s.Plan)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var tenantInfoDict = tenantInfos.ToDictionary(t => t.Id);

        // Contar keys activas por tenant (una query sola)
        var keysByTenant = await _db.ApiKeys
            .Where(k => tenantIdsInRanking.Contains(k.TenantId) && k.RevokedAt == null)
            .GroupBy(k => k.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // P95 por tenant — materializa las latencias por tenant y calcula en memoria
        var latenciesByTenant = await records
            .Where(r => tenantIdsInRanking.Contains(r.TenantId))
            .Select(r => new { r.TenantId, r.ResponseTimeMs })
            .ToListAsync(cancellationToken);
        var p95ByTenant = latenciesByTenant
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var sorted = g.Select(x => x.ResponseTimeMs).OrderBy(v => v).ToList();
                    return sorted.Count > 0 ? sorted.ElementAt((int)(sorted.Count * 0.95)) : 0;
                });

        var avgPerTenant = tenantAggRaw.Any()
            ? tenantAggRaw.Average(t => t.TotalRequests)
            : 0d;

        var ranking = tenantAggRaw.Select(row =>
        {
            var info = tenantInfoDict.TryGetValue(row.TenantId, out var i) ? i : null;
            var errorRatePct = row.TotalRequests > 0
                ? Math.Round(((decimal)row.ErrorRequests / row.TotalRequests) * 100m, 2)
                : 0m;

            var signals = new List<string>();
            if (errorRatePct >= HighErrorRateThresholdPercent)
                signals.Add("high-error-rate");
            if (row.TotalRequests >= TrafficSpikeMinimumVolume
                && avgPerTenant > 0
                && row.TotalRequests >= avgPerTenant * TrafficSpikeThresholdMultiplier)
                signals.Add("traffic-spike");

            return new TenantUsageRowDto(
                TenantId: row.TenantId,
                TenantName: info?.Name ?? $"Tenant #{row.TenantId}",
                Subdomain: info?.Subdomain ?? "",
                PlanName: info?.Plan?.ToString() ?? "Unknown",
                TotalRequests: row.TotalRequests,
                SuccessfulRequests: row.SuccessfulRequests,
                ErrorRequests: row.ErrorRequests,
                ErrorRatePercent: errorRatePct,
                AvgResponseTimeMs: row.AvgResponseTimeMs,
                P95ResponseTimeMs: p95ByTenant.TryGetValue(row.TenantId, out var p) ? p : 0,
                ActiveKeysCount: keysByTenant.TryGetValue(row.TenantId, out var kc) ? kc : 0,
                FirstRequestAt: row.FirstRequestAt,
                LastRequestAt: row.LastRequestAt,
                Signals: signals
            );
        }).ToList();

        // ====================================================================
        // 5. Plan distribution (agrupa todos los tenants con actividad)
        // ====================================================================
        var tenantPlanMap = await _db.Subscriptions
            .Select(s => new { s.TenantId, s.Plan })
            .ToListAsync(cancellationToken);
        var tenantPlanDict = tenantPlanMap.ToDictionary(x => x.TenantId, x => x.Plan);

        var allTenantAgg = await records
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var planDistribution = allTenantAgg
            .GroupBy(t => tenantPlanDict.TryGetValue(t.TenantId, out var p)
                ? p.ToString()
                : "Unknown")
            .Select(g => new PlanUsageSliceDto(
                PlanName: g.Key,
                TotalRequests: g.Sum(x => x.Count),
                TenantCount: g.Count()))
            .OrderByDescending(p => p.TotalRequests)
            .ToList();

        // ====================================================================
        // 6. Signals / alertas
        // ====================================================================
        var signalsData = await ComputeSignalsAsync(
            periodStart, periodEnd, allTenantAgg.Select(a => a.TenantId).ToList(),
            tenantPlanDict, avgPerTenant, cancellationToken);

        return Ok(new SystemApiUsageDto(
            Summary: summary,
            TenantRanking: ranking,
            DailyUsage: dailyUsage,
            StatusBreakdown: statusBreakdown,
            PlanDistribution: planDistribution,
            Signals: signalsData
        ));
    }

    /// <summary>
    /// Exporta ranking completo de tenants como CSV para análisis offline.
    /// Incluye todos los tenants con actividad (no limitado a topN).
    /// </summary>
    [HttpGet("export.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null,
        CancellationToken cancellationToken = default)
    {
        var periodEnd = until ?? DateTime.UtcNow;
        var periodStart = since ?? periodEnd.AddDays(-30);

        var records = _db.ApiUsageRecords
            .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd);

        var tenantAggRaw = await records
            .GroupBy(r => r.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                TotalRequests = g.Count(),
                SuccessfulRequests = g.Count(r => r.StatusCode >= 200 && r.StatusCode < 300),
                ErrorRequests = g.Count(r => r.StatusCode >= 400),
                AvgResponseTimeMs = (int)g.Average(r => r.ResponseTimeMs),
                FirstRequestAt = g.Min(r => r.CreatedAt),
                LastRequestAt = g.Max(r => r.CreatedAt)
            })
            .OrderByDescending(t => t.TotalRequests)
            .ToListAsync(cancellationToken);

        var tenantIds = tenantAggRaw.Select(t => t.TenantId).ToList();
        var tenantInfos = await _db.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Subdomain,
                Plan = _db.Subscriptions
                    .Where(s => s.TenantId == t.Id)
                    .Select(s => (SubscriptionPlan?)s.Plan)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var tenantInfoDict = tenantInfos.ToDictionary(t => t.Id);

        var keysByTenant = await _db.ApiKeys
            .Where(k => tenantIds.Contains(k.TenantId) && k.RevokedAt == null)
            .GroupBy(k => k.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // Build CSV
        var sb = new StringBuilder();
        sb.AppendLine("TenantId,TenantName,Subdomain,Plan,TotalRequests,SuccessfulRequests,ErrorRequests,ErrorRatePercent,AvgResponseTimeMs,ActiveKeysCount,FirstRequestAt,LastRequestAt");

        foreach (var row in tenantAggRaw)
        {
            var info = tenantInfoDict.TryGetValue(row.TenantId, out var i) ? i : null;
            var errorRatePct = row.TotalRequests > 0
                ? Math.Round(((decimal)row.ErrorRequests / row.TotalRequests) * 100m, 2)
                : 0m;

            sb.Append(row.TenantId).Append(',');
            sb.Append(CsvEscape(info?.Name ?? "")).Append(',');
            sb.Append(CsvEscape(info?.Subdomain ?? "")).Append(',');
            sb.Append(CsvEscape(info?.Plan?.ToString() ?? "Unknown")).Append(',');
            sb.Append(row.TotalRequests).Append(',');
            sb.Append(row.SuccessfulRequests).Append(',');
            sb.Append(row.ErrorRequests).Append(',');
            sb.Append(errorRatePct.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.AvgResponseTimeMs).Append(',');
            sb.Append(keysByTenant.TryGetValue(row.TenantId, out var kc) ? kc : 0).Append(',');
            sb.Append(row.FirstRequestAt.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(row.LastRequestAt.ToString("O", CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var filename = $"api-usage-{periodStart:yyyyMMdd}-{periodEnd:yyyyMMdd}.csv";

        _logger.LogInformation(
            "SystemAdmin exported API usage CSV: {TenantRows} rows, period {Start}..{End}",
            tenantAggRaw.Count, periodStart, periodEnd);

        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private async Task<SystemUsageSignalsDto> ComputeSignalsAsync(
        DateTime periodStart,
        DateTime periodEnd,
        List<int> activeTenantIds,
        Dictionary<int, SubscriptionPlan> tenantPlanDict,
        double avgPerTenant,
        CancellationToken cancellationToken)
    {
        // 1. High error rate por tenant en este período
        var errorRateRaw = await _db.ApiUsageRecords
            .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd)
            .GroupBy(r => r.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Total = g.Count(),
                Errors = g.Count(r => r.StatusCode >= 400)
            })
            .Where(t => t.Total >= 20) // mínimo volumen para que el % sea significativo
            .ToListAsync(cancellationToken);

        var highErrorRate = errorRateRaw
            .Select(t => new
            {
                t.TenantId,
                Rate = t.Total > 0 ? ((decimal)t.Errors / t.Total) * 100m : 0m
            })
            .Where(t => t.Rate >= HighErrorRateThresholdPercent)
            .OrderByDescending(t => t.Rate)
            .Take(10)
            .ToList();

        // 2. Traffic spikes: tenants con > 3x promedio y volumen mínimo
        var spikeIds = new List<int>();
        if (avgPerTenant > 0)
        {
            var spikeRaw = await _db.ApiUsageRecords
                .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd)
                .GroupBy(r => r.TenantId)
                .Select(g => new { TenantId = g.Key, Count = g.Count() })
                .Where(t => t.Count >= TrafficSpikeMinimumVolume
                         && t.Count >= avgPerTenant * TrafficSpikeThresholdMultiplier)
                .OrderByDescending(t => t.Count)
                .Take(10)
                .ToListAsync(cancellationToken);
            spikeIds = spikeRaw.Select(s => s.TenantId).ToList();
        }

        // 3. Possible churn: tenants que tienen keys pero 0 requests en últimos N días
        var churnCutoff = DateTime.UtcNow.AddDays(-ChurnDaysWithoutActivity);
        var recentlyActiveTenantIds = await _db.ApiUsageRecords
            .Where(r => r.CreatedAt >= churnCutoff)
            .Select(r => r.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var tenantsWithKeys = await _db.ApiKeys
            .Where(k => k.RevokedAt == null)
            .Select(k => k.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var churnTenantIds = tenantsWithKeys
            .Except(recentlyActiveTenantIds)
            .Take(10)
            .ToList();

        // 4. No active keys: tenants con plan que permite API pero 0 keys activas
        var tenantsWithApiAccess = tenantPlanDict
            .Where(kv => kv.Value == SubscriptionPlan.Professional
                      || kv.Value == SubscriptionPlan.Enterprise)
            .Select(kv => kv.Key)
            .ToList();
        var noKeyTenantIds = tenantsWithApiAccess
            .Except(tenantsWithKeys)
            .Take(10)
            .ToList();

        // Enriquecer todos los tenant ids con nombre
        var allSignalTenantIds = highErrorRate.Select(t => t.TenantId)
            .Concat(spikeIds)
            .Concat(churnTenantIds)
            .Concat(noKeyTenantIds)
            .Distinct()
            .ToList();

        var signalTenantInfo = await _db.Tenants
            .Where(t => allSignalTenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        string PlanName(int tid) => tenantPlanDict.TryGetValue(tid, out var p) ? p.ToString() : "Unknown";
        string TenantName(int tid) => signalTenantInfo.TryGetValue(tid, out var n) ? n : $"Tenant #{tid}";

        var errorRateDto = highErrorRate.Select(t => new TenantSignalDto(
            TenantId: t.TenantId,
            TenantName: TenantName(t.TenantId),
            PlanName: PlanName(t.TenantId),
            Metric: $"{t.Rate:0.0}%",
            Reason: "Error-rate por encima del umbral (≥15%)"
        )).ToList();

        // Para spikes necesitamos el count — lo reusamos del query
        var spikeCounts = await _db.ApiUsageRecords
            .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd
                     && spikeIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);
        var spikesDto = spikeIds.Select(tid => new TenantSignalDto(
            TenantId: tid,
            TenantName: TenantName(tid),
            PlanName: PlanName(tid),
            Metric: $"{(spikeCounts.TryGetValue(tid, out var c) ? c : 0)} req",
            Reason: $"Volumen ≥ {TrafficSpikeThresholdMultiplier}x el promedio de la plataforma"
        )).ToList();

        var churnDto = churnTenantIds.Select(tid => new TenantSignalDto(
            TenantId: tid,
            TenantName: TenantName(tid),
            PlanName: PlanName(tid),
            Metric: $"{ChurnDaysWithoutActivity}+ días sin uso",
            Reason: "Tenant tiene keys activas pero 0 requests recientes"
        )).ToList();

        var noKeysDto = noKeyTenantIds.Select(tid => new TenantSignalDto(
            TenantId: tid,
            TenantName: TenantName(tid),
            PlanName: PlanName(tid),
            Metric: "0 keys",
            Reason: "Plan permite API pero no hay keys configuradas (onboarding incompleto)"
        )).ToList();

        return new SystemUsageSignalsDto(
            HighErrorRate: errorRateDto,
            TrafficSpikes: spikesDto,
            PossibleChurn: churnDto,
            NoActiveKeys: noKeysDto
        );
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuoting) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
