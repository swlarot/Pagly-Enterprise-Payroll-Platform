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

/// <summary>
/// De dónde sale la renta de este empleado en esta planilla, con las mismas
/// columnas de la ficha anual: así se puede comprobar "si la renta pegó" sin
/// salir de la planilla.
/// </summary>
public record IsrBreakdownDto(
    decimal IngresoGravablePeriodo,
    int NumeroPeriodo,
    decimal PeriodoEquivalente,
    decimal PeriodosDePago,
    decimal Acumulado,
    decimal IngresoAnualProyectado,
    decimal RentaAnual,
    decimal RentaPorPeriodo,
    decimal ImpuestoCausado,
    decimal RetenidoAntes,
    decimal ADescontar,
    decimal GastoRepresentacion,
    decimal IsrGastoRepresentacion,
    decimal IsrPeriodo,
    bool TieneSaldoInicial,
    bool TieneMesesImportados
);

public record AcreedorItemDto(
    string Descripcion,
    string Categoria,
    decimal MontoSolicitado,
    decimal MontoAplicado,
    bool FueLimitado,
    string? RazonLimitacion
);
