namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// Desglose detallado del cálculo de planilla para un empleado.
/// Ensamblado a partir de datos almacenados — no requiere re-cálculo.
/// </summary>
public record PayrollBreakdownDto(
    // Ingresos
    IngresosBreakdown Ingresos,

    // Deducciones legales
    CssBreakdownDto Css,
    SeBreakdownDto Se,
    IsrBreakdownDto Isr,

    // Acreedores
    List<AcreedorItemDto> Acreedores,

    // Totales
    decimal TotalDeducciones,
    decimal NetPay
);

public record IngresosBreakdown(
    decimal SalarioBase,
    decimal HorasRegulares,
    decimal HorasDomingo,
    decimal HorasFeriado,
    decimal HorasExtraDiurnas,
    decimal HorasExtraNocturnas,
    decimal HorasExtraFestivos,
    decimal HorasExtraMixtas,
    decimal HorasExtraExceso,
    decimal Comisiones,
    decimal Bonos,
    decimal GrossPay
);

public record CssBreakdownDto(
    decimal BaseUsada,
    decimal TopeSalarial,
    bool SeAplicoTope,
    decimal Tasa,
    decimal Monto
);

public record SeBreakdownDto(
    decimal Base,
    decimal Tasa,
    decimal Monto
);

public record IsrBreakdownDto(
    decimal SalarioPeriodo,
    int PeriodosAlAno,
    decimal SalarioAnualizado,
    decimal DeduccionDependientes,
    decimal IngresoNetoGravable,
    decimal IsrAnual,
    decimal IsrPeriodo
);

public record AcreedorItemDto(
    string Descripcion,
    string Categoria,
    decimal MontoSolicitado,
    decimal MontoAplicado,
    bool FueLimitado,
    string? RazonLimitacion
);
