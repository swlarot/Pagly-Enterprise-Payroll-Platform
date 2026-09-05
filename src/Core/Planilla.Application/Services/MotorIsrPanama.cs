// ====================================================================
// Planilla - MotorIsrPanama
// Implementa la "Especificación funcional y técnica del motor de cálculo de ISR
// para software de planilla en Panamá", v1.0, septiembre 2026.
//
// Método acumulativo con regularización:
//   ISR a descontar = MAX(0, ISR debido acumulado − ISR retenido acumulado)
//
// En cada corrida se recalcula cuánto debería llevar retenido el empleado a la
// fecha y se cobra solo la diferencia. Un período con ingreso alto no descuadra
// el año: el siguiente se ajusta solo. Si ya se retuvo de más, la corrida muestra
// B/.0.00 y el exceso queda como saldo a favor del empleado.
// ====================================================================

using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Services;

/// <summary>Un concepto de la corrida, con su tratamiento fiscal.</summary>
public sealed record MovimientoIsr(TratamientoIsr Tratamiento, decimal Monto);

/// <summary>
/// Lo que el empleado trae acumulado en el año fiscal: lo cargado al migrar
/// desde otro sistema más lo que ha generado esta plataforma.
/// </summary>
public sealed class AcumuladoIsr
{
    // Saldos iniciales (migración desde otro sistema)
    public decimal IngresoGravableInicial { get; init; }
    public decimal DecimoInicial { get; init; }
    public decimal IsrRetenidoInicial { get; init; }

    // Acumulados generados por el sistema
    public decimal IngresoGravableProcesado { get; init; }
    public decimal DecimoProcesado { get; init; }
    public decimal IsrRegularProcesado { get; init; }
    public decimal IsrDecimoProcesado { get; init; }

    public decimal IngresoGravableTotal => IngresoGravableInicial + IngresoGravableProcesado;
    public decimal DecimoTotal => DecimoInicial + DecimoProcesado;
    public decimal IsrRetenidoTotal => IsrRetenidoInicial + IsrRegularProcesado + IsrDecimoProcesado;
}

/// <summary>Datos de una corrida para el motor de ISR.</summary>
public sealed class CorridaIsr
{
    public required PayPeriodType Frecuencia { get; init; }

    /// <summary>
    /// Número de período del EMPLEADO dentro del año fiscal (1, 2, 3…).
    /// No es el número de planilla de la empresa: cuenta las corridas del empleado.
    /// </summary>
    public required int NumeroPeriodoEmpleado { get; init; }

    public IReadOnlyList<MovimientoIsr> Movimientos { get; init; } = Array.Empty<MovimientoIsr>();
    public AcumuladoIsr AcumuladoAnterior { get; init; } = new();

    /// <summary>
    /// Deducciones anuales admitidas sobre la renta proyectada.
    /// El motor no decide qué entra aquí: lo recibe ya calculado.
    /// </summary>
    public decimal DeduccionesFiscalesAnuales { get; init; }
}

/// <summary>Resultado del cálculo, con todo el rastro para auditar.</summary>
public sealed class ResultadoIsr
{
    public decimal IngresoGravablePeriodo { get; init; }
    public decimal IngresoGravableAcumulado { get; init; }
    public decimal DecimoPeriodo { get; init; }
    public decimal DecimoAcumulado { get; init; }
    public decimal IngresoAnualProyectado { get; init; }
    public decimal RentaNetaGravableProyectada { get; init; }
    public decimal IsrAnualProyectado { get; init; }
    public decimal IsrDebidoAcumulado { get; init; }
    public decimal IsrRetenidoInicial { get; init; }
    public decimal IsrRetenidoSistemaAnterior { get; init; }
    public decimal IsrRetenidoTotalAnterior { get; init; }

    /// <summary>Lo que se descuenta en esta corrida. Nunca es negativo.</summary>
    public decimal IsrDescontarPeriodo { get; init; }

    public decimal IsrRetenidoTotalNuevo { get; init; }
    public decimal SaldoFavorFisco { get; init; }

    /// <summary>Exceso retenido. Solo para control interno; no se muestra en el comprobante.</summary>
    public decimal SaldoFavorEmpleado { get; init; }
}

/// <summary>Motor de ISR acumulativo con regularización por corrida.</summary>
public static class MotorIsrPanama
{
    // Tarifa del Art. 700 del Código Fiscal.
    private const decimal LimiteExento = 11_000m;
    private const decimal Limite15 = 50_000m;
    private const decimal Tasa15 = 0.15m;
    private const decimal Tasa25 = 0.25m;
    private const decimal IsrHasta50000 = 5_850m;

    /// <summary>Tarifa progresiva anual (Art. 700).</summary>
    public static decimal CalcularIsrAnual(decimal rentaNetaGravable)
    {
        if (rentaNetaGravable <= LimiteExento) return 0m;
        if (rentaNetaGravable <= Limite15)
            return Redondear((rentaNetaGravable - LimiteExento) * Tasa15);
        return Redondear(IsrHasta50000 + (rentaNetaGravable - Limite15) * Tasa25);
    }

    /// <summary>Períodos del año según la frecuencia del EMPLEADO (52 / 26 / 24 / 12).</summary>
    public static int ObtenerPeriodosAnuales(PayPeriodType frecuencia) => frecuencia switch
    {
        PayPeriodType.Semanal => 52,
        PayPeriodType.Bisemanal => 26,
        PayPeriodType.Quincenal => 24,
        PayPeriodType.Mensual => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(frecuencia))
    };

    public static ResultadoIsr Calcular(CorridaIsr corrida)
    {
        Validar(corrida);

        var periodosAnuales = ObtenerPeriodosAnuales(corrida.Frecuencia);
        var movimientos = corrida.Movimientos;

        var ingresoGravablePeriodo = movimientos
            .Where(m => m.Tratamiento == TratamientoIsr.GravableAcumulable)
            .Sum(m => m.Monto);

        var decimoPeriodo = movimientos
            .Where(m => m.Tratamiento == TratamientoIsr.DecimoTercerMes)
            .Sum(m => m.Monto);

        var ingresoGravableAcumulado = corrida.AcumuladoAnterior.IngresoGravableTotal + ingresoGravablePeriodo;
        var decimoAcumulado = corrida.AcumuladoAnterior.DecimoTotal + decimoPeriodo;

        // Proyección desde lo REALMENTE acumulado, no desde el bruto de este período.
        // Así un mes con horas extra se diluye en el promedio y el año converge solo.
        // La fecha de ingreso no interviene: si lo proyectado genera ISR, se retiene, y la
        // regularización de las corridas siguientes corrige cualquier exceso.
        var promedioPorPeriodo = ingresoGravableAcumulado / corrida.NumeroPeriodoEmpleado;
        var salarioAnualProyectado = promedioPorPeriodo * periodosAnuales;

        // El décimo es un mes de salario. Si lo ya pagado supera esa estimación, manda lo real.
        var decimoAnualProyectado = Math.Max(salarioAnualProyectado / 12m, decimoAcumulado);

        var ingresoAnualProyectado = salarioAnualProyectado + decimoAnualProyectado;
        var rentaNeta = Math.Max(0m, ingresoAnualProyectado - corrida.DeduccionesFiscalesAnuales);
        var isrAnual = CalcularIsrAnual(rentaNeta);

        // Parte del impuesto anual que corresponde a los períodos ya corridos.
        //
        // NOTA DE IMPLEMENTACIÓN — única desviación respecto del código de la especificación.
        // Aquel sumaba aquí un "efecto ISR del décimo" adicional en las corridas donde se paga
        // la partida. Ese ajuste cuenta el décimo dos veces: ya está dentro de
        // ingresoAnualProyectado (vía decimoAnualProyectado) y por tanto dentro de isrAnual.
        // Verificado con tres sueldos: retenía 337.50 / 2,256.25 / 6,418.75 cuando lo debido
        // era 300.00 / 2,250.00 / 6,350.00, y hacía saltar el descuento en la quincena de la
        // partida para caer a cero en la siguiente. Sin ese ajuste el año cierra exacto.
        var proporcion = (decimal)corrida.NumeroPeriodoEmpleado / periodosAnuales;
        var isrDebidoAcumulado = Redondear(isrAnual * proporcion);

        var isrInicial = corrida.AcumuladoAnterior.IsrRetenidoInicial;
        var isrSistemaAnterior = corrida.AcumuladoAnterior.IsrRegularProcesado
                               + corrida.AcumuladoAnterior.IsrDecimoProcesado;
        var isrTotalAnterior = isrInicial + isrSistemaAnterior;

        var diferencia = isrDebidoAcumulado - isrTotalAnterior;

        // Nunca se muestra ISR negativo en la planilla.
        var isrDescontar = Math.Max(0m, diferencia);
        var saldoFavorEmpleado = diferencia < 0m ? Math.Abs(diferencia) : 0m;
        var saldoFavorFisco = diferencia > 0m ? diferencia : 0m;

        return new ResultadoIsr
        {
            IngresoGravablePeriodo = Redondear(ingresoGravablePeriodo),
            IngresoGravableAcumulado = Redondear(ingresoGravableAcumulado),
            DecimoPeriodo = Redondear(decimoPeriodo),
            DecimoAcumulado = Redondear(decimoAcumulado),
            IngresoAnualProyectado = Redondear(ingresoAnualProyectado),
            RentaNetaGravableProyectada = Redondear(rentaNeta),
            IsrAnualProyectado = Redondear(isrAnual),
            IsrDebidoAcumulado = isrDebidoAcumulado,
            IsrRetenidoInicial = Redondear(isrInicial),
            IsrRetenidoSistemaAnterior = Redondear(isrSistemaAnterior),
            IsrRetenidoTotalAnterior = Redondear(isrTotalAnterior),
            IsrDescontarPeriodo = Redondear(isrDescontar),
            IsrRetenidoTotalNuevo = Redondear(isrTotalAnterior + isrDescontar),
            SaldoFavorFisco = Redondear(saldoFavorFisco),
            SaldoFavorEmpleado = Redondear(saldoFavorEmpleado)
        };
    }

    private static void Validar(CorridaIsr corrida)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (corrida.NumeroPeriodoEmpleado <= 0)
            throw new ArgumentException(
                "El número de período del empleado debe ser mayor a cero.", nameof(corrida));
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
