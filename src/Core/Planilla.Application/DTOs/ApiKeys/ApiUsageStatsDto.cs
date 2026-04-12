namespace Vorluno.Planilla.Application.DTOs.ApiKeys;

/// <summary>
/// Response del endpoint de analytics <c>GET /api/api-keys/usage</c>.
/// Datos agregados del uso del API Platform para el tenant actual.
/// </summary>
public record ApiUsageStatsDto(
    /// <summary>Requests por día en el rango solicitado (para line chart).</summary>
    List<DailyUsageDto> DailyUsage,

    /// <summary>Breakdown por status code (para donut chart).</summary>
    List<StatusBreakdownDto> StatusBreakdown,

    /// <summary>Top keys por uso (para bar chart — ya existía pero ahora con data real).</summary>
    List<KeyUsageDto> TopKeys,

    /// <summary>Estadísticas generales del período.</summary>
    UsageSummaryDto Summary
);

public record DailyUsageDto(
    /// <summary>Fecha del día (UTC, sin hora).</summary>
    DateTime Date,
    /// <summary>Total de requests ese día.</summary>
    int Count,
    /// <summary>Requests exitosas (2xx).</summary>
    int SuccessCount,
    /// <summary>Requests con error (4xx + 5xx).</summary>
    int ErrorCount
);

public record StatusBreakdownDto(
    int StatusCode,
    int Count,
    /// <summary>Label legible: "OK", "Bad Request", "Unauthorized", "Rate Limited", "Server Error".</summary>
    string Label
);

public record KeyUsageDto(
    int KeyId,
    string KeyName,
    string KeyPrefix,
    int TotalRequests,
    int AvgResponseTimeMs
);

public record UsageSummaryDto(
    /// <summary>Total de requests en el período.</summary>
    int TotalRequests,
    /// <summary>Requests exitosas (2xx).</summary>
    int SuccessfulRequests,
    /// <summary>Requests con error del cliente (4xx).</summary>
    int ClientErrors,
    /// <summary>Requests con error del servidor (5xx).</summary>
    int ServerErrors,
    /// <summary>Latencia promedio en ms.</summary>
    int AvgResponseTimeMs,
    /// <summary>Latencia p95 en ms (el 95% de los requests fue más rápido que esto).</summary>
    int P95ResponseTimeMs,
    /// <summary>Inicio del período consultado.</summary>
    DateTime PeriodStart,
    /// <summary>Fin del período consultado.</summary>
    DateTime PeriodEnd
);
