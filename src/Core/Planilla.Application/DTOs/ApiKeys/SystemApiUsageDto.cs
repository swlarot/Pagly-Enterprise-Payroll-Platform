namespace Vorluno.Planilla.Application.DTOs.ApiKeys;

/// <summary>
/// Response del endpoint de analytics GLOBAL del System Admin.
/// A diferencia de ApiUsageStatsDto (per-tenant), este cruza TODA la plataforma:
/// agrega contadores across tenants, calcula rankings, detecta señales de abuso.
///
/// <para>
/// Uso: panel interno del operador (no expuesto a clientes).
/// Endpoint: GET /api/system-admin/api-usage/global
/// </para>
///
/// <para>
/// Nota explícita: NO incluye comparación vs período anterior (ej. "+23% WoW").
/// En v0 con poca data real, el % produce artefactos (+∞%, -100%, +400% sobre 1→5).
/// Se agregará en una fase posterior con umbrales mínimos de volumen base.
/// </para>
/// </summary>
public record SystemApiUsageDto(
    /// <summary>Métricas globales del período.</summary>
    SystemUsageSummaryDto Summary,

    /// <summary>Ranking de tenants (Top N) por volumen de uso.</summary>
    List<TenantUsageRowDto> TenantRanking,

    /// <summary>Series de tiempo agregadas (total por día).</summary>
    List<DailyUsageDto> DailyUsage,

    /// <summary>Breakdown global por status code.</summary>
    List<StatusBreakdownDto> StatusBreakdown,

    /// <summary>Distribución de requests por plan (stacked area chart).</summary>
    List<PlanUsageSliceDto> PlanDistribution,

    /// <summary>Alertas / señales operacionales detectadas en el período.</summary>
    SystemUsageSignalsDto Signals
);

public record SystemUsageSummaryDto(
    /// <summary>Total de requests al API Platform en el período.</summary>
    int TotalRequests,
    /// <summary>Tenants distintos que consumieron el API (al menos 1 request).</summary>
    int ActiveTenants,
    /// <summary>API keys distintas activas (no revocadas + al menos 1 request).</summary>
    int ActiveKeys,
    /// <summary>Requests 2xx.</summary>
    int SuccessfulRequests,
    /// <summary>Requests 4xx.</summary>
    int ClientErrors,
    /// <summary>Requests 5xx.</summary>
    int ServerErrors,
    /// <summary>% de requests con error (4xx+5xx) sobre el total. 0-100.</summary>
    decimal ErrorRatePercent,
    /// <summary>Latencia promedio global en ms.</summary>
    int AvgResponseTimeMs,
    /// <summary>Latencia p95 global en ms.</summary>
    int P95ResponseTimeMs,
    /// <summary>Pico de requests por minuto observado en el período.</summary>
    int PeakRequestsPerMinute,
    /// <summary>Inicio del período consultado.</summary>
    DateTime PeriodStart,
    /// <summary>Fin del período consultado.</summary>
    DateTime PeriodEnd
);

/// <summary>
/// Una fila del ranking de tenants por uso del API Platform.
/// Ordenada desc por TotalRequests.
/// </summary>
public record TenantUsageRowDto(
    int TenantId,
    string TenantName,
    string Subdomain,
    /// <summary>Nombre del plan ("Free", "Starter", "Professional", "Enterprise").</summary>
    string PlanName,
    /// <summary>Total de requests del tenant en el período.</summary>
    int TotalRequests,
    /// <summary>Requests exitosas (2xx).</summary>
    int SuccessfulRequests,
    /// <summary>Requests con error (4xx+5xx).</summary>
    int ErrorRequests,
    /// <summary>% de error-rate (4xx+5xx / total). 0-100.</summary>
    decimal ErrorRatePercent,
    /// <summary>Latencia promedio en ms para este tenant.</summary>
    int AvgResponseTimeMs,
    /// <summary>Latencia p95 en ms para este tenant.</summary>
    int P95ResponseTimeMs,
    /// <summary>Keys activas (no revocadas) que tiene el tenant.</summary>
    int ActiveKeysCount,
    /// <summary>Fecha del primer request del tenant en el período (null si no hay data).</summary>
    DateTime? FirstRequestAt,
    /// <summary>Fecha del último request del tenant en el período.</summary>
    DateTime? LastRequestAt,
    /// <summary>Flags operacionales: posibles abusos, cerca de cuota, alto error-rate.</summary>
    List<string> Signals
);

/// <summary>Distribución de requests por plan (para stacked area chart).</summary>
public record PlanUsageSliceDto(
    string PlanName,
    int TotalRequests,
    int TenantCount
);

/// <summary>
/// Alertas operacionales que el System Admin debe revisar.
/// Cada lista es una advertencia derivada — el frontend las muestra como cards
/// con call-to-action (drill-down al tenant).
/// </summary>
public record SystemUsageSignalsDto(
    /// <summary>Tenants con error-rate ≥ 15% en el período (posible bug o abuso).</summary>
    List<TenantSignalDto> HighErrorRate,
    /// <summary>Tenants con crecimiento anormal de volumen (posible abuso o integración nueva).</summary>
    List<TenantSignalDto> TrafficSpikes,
    /// <summary>Tenants con 0 requests en los últimos 14 días (candidatos a churn).</summary>
    List<TenantSignalDto> PossibleChurn,
    /// <summary>Tenants sin keys activas pese a plan que permite API (onboarding incompleto).</summary>
    List<TenantSignalDto> NoActiveKeys
);

/// <summary>
/// Representación compacta de un tenant que dispara una alerta.
/// El frontend usa TenantId para linkear a /system-admin/tenants/{id}.
/// </summary>
public record TenantSignalDto(
    int TenantId,
    string TenantName,
    string PlanName,
    /// <summary>Métrica relevante formateada ("23 req", "22%", "14 días sin uso").</summary>
    string Metric,
    /// <summary>Descripción humana del por qué disparó la señal.</summary>
    string Reason
);
