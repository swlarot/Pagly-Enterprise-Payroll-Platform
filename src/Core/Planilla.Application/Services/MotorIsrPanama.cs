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

    // Gastos de representación: van por cuenta aparte porque tienen tarifa propia
    // y no entran en la proyección del salario.
    public decimal GastoRepresentacionInicial { get; init; }
    public decimal GastoRepresentacionProcesado { get; init; }
    public decimal IsrGastoRepresentacionInicial { get; init; }
    public decimal IsrGastoRepresentacionProcesado { get; init; }

    // Partidas de décimo ya pagadas en el año. El contador de períodos avanza una
    // fracción fija por partida, no por monto, así que hace falta saber cuántas van.
    public int PartidasDecimoInicial { get; init; }
    public int PartidasDecimoProcesadas { get; init; }

    public decimal IngresoGravableTotal => IngresoGravableInicial + IngresoGravableProcesado;
    public decimal DecimoTotal => DecimoInicial + DecimoProcesado;
    public int PartidasDecimoTotal => PartidasDecimoInicial + PartidasDecimoProcesadas;
    public decimal GastoRepresentacionTotal => GastoRepresentacionInicial + GastoRepresentacionProcesado;

    public decimal IsrGastoRepresentacionTotal =>
        IsrGastoRepresentacionInicial + IsrGastoRepresentacionProcesado;

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
    /// <summary>Columna PERIODOS del libro del contador.</summary>
    public decimal PeriodoEquivalente { get; init; }

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

    // ========== Gastos de representación (tarifa propia) ==========

    public decimal GastoRepresentacionPeriodo { get; init; }
    public decimal GastoRepresentacionAcumulado { get; init; }

    /// <summary>Retención sobre gastos de representación de esta corrida.</summary>
    public decimal IsrGastoRepresentacionPeriodo { get; init; }

    public decimal IsrGastoRepresentacionAcumulado { get; init; }

    /// <summary>
    /// Lo que se descuenta en total en esta corrida: el ISR del salario más el de
    /// los gastos de representación. Es el número que va al comprobante.
    /// </summary>
    public decimal IsrTotalDescontarPeriodo => IsrDescontarPeriodo + IsrGastoRepresentacionPeriodo;
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

    // Tarifa de gastos de representación (Código Fiscal Art. 732, reformado por la
    // Ley 6 de 2005 y vigente en su forma actual desde julio de 2010).
    private const decimal GastoRepresentacionTramo1 = 25_000m;
    private const decimal GastoRepresentacionTasa1 = 0.10m;
    private const decimal GastoRepresentacionTasa2 = 0.15m;
    private const decimal GastoRepresentacionImpuestoTramo1 = 2_500m;  // 25,000 x 10%

    /// <summary>
    /// Impuesto anual sobre gastos de representación: 10% hasta B/.25,000 y, de ahí
    /// en adelante, B/.2,500 más 15% sobre el excedente.
    ///
    /// No comparte tarifa ni deducciones con el salario: es una retención propia
    /// sobre lo efectivamente pagado en el año, sin proyección. Por eso tampoco
    /// entra en la renta neta gravable del Art. 700.
    /// </summary>
    public static decimal CalcularIsrGastosRepresentacion(decimal acumuladoAnual)
    {
        if (acumuladoAnual <= 0m) return 0m;

        if (acumuladoAnual <= GastoRepresentacionTramo1)
            return Redondear(acumuladoAnual * GastoRepresentacionTasa1);

        return Redondear(GastoRepresentacionImpuestoTramo1
            + (acumuladoAnual - GastoRepresentacionTramo1) * GastoRepresentacionTasa2);
    }

    /// <summary>
    /// Períodos EQUIVALENTES del año: los del calendario más los que aporta el décimo.
    ///
    /// El décimo tercer mes también tributa, así que entra en el reparto. Equivale a un mes
    /// de salario, o sea P/12 períodos, de modo que el total es P x 13/12:
    ///
    ///   Semanal 52 -> 56.33     Quincenal 24 -> 26.00
    ///   Bisemanal 26 -> 28.17   Mensual   12 -> 13.00
    ///
    /// El caso quincenal da 26, que es el divisor que usa el contador: "no son 24 quincenas,
    /// son 26". La planilla regular retiene 24/26 del impuesto anual y el décimo el 2/26
    /// restante, repartido entre sus tres partidas.
    /// </summary>
    public static decimal ObtenerPeriodosEquivalentesAnuales(PayPeriodType frecuencia)
        => ObtenerPeriodosAnuales(frecuencia) * 13m / 12m;

    /// <summary>
    /// Columna PERIODOS del libro del contador: el número de corrida más una fracción
    /// FIJA por cada partida de décimo ya pagada. Cada partida vale un tercio de mes,
    /// o sea P/36 períodos: en una quincenal, 2/3.
    ///
    ///   q7 con 1 partida  -> 7.667     q15 con 2 -> 16.333
    ///   q23 con 3 partidas -> 25.000    q24 con 3 -> 26.000
    ///
    /// Es una constante, no depende del monto del décimo ni del salario. Antes se
    /// ponderaba por monto y con salario fijo daba lo mismo, pero con salario variable
    /// se separaba del libro (7.685 donde el contador pone 7.667).
    ///
    /// Se redondea a TRES decimales porque así lo escribe el contador en su hoja
    /// (7.667, no 7.6667) y con la fracción exacta la celda IMPUESTO CAUSADO se
    /// separa ocho centavos de la suya.
    /// </summary>
    public static decimal CalcularPeriodoEquivalente(PayPeriodType frecuencia, int numeroPeriodo, int partidasDecimo)
        => Math.Round(numeroPeriodo + partidasDecimo * (ObtenerPeriodosAnuales(frecuencia) / 36m), 3,
            MidpointRounding.AwayFromZero);

    /// <summary>Períodos del calendario según la frecuencia del EMPLEADO (52 / 26 / 24 / 12).</summary>
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

        var periodosEquivalentes = ObtenerPeriodosEquivalentesAnuales(corrida.Frecuencia);
        var movimientos = corrida.Movimientos;

        var ingresoGravablePeriodo = movimientos
            .Where(m => m.Tratamiento == TratamientoIsr.GravableAcumulable)
            .Sum(m => m.Monto);

        var decimoPeriodo = movimientos
            .Where(m => m.Tratamiento == TratamientoIsr.DecimoTercerMes)
            .Sum(m => m.Monto);

        var gastoRepresentacionPeriodo = movimientos
            .Where(m => m.Tratamiento == TratamientoIsr.GastoRepresentacion)
            .Sum(m => m.Monto);

        var ingresoGravableAcumulado = corrida.AcumuladoAnterior.IngresoGravableTotal + ingresoGravablePeriodo;
        var decimoAcumulado = corrida.AcumuladoAnterior.DecimoTotal + decimoPeriodo;

        // Una corrida que trae décimo cuenta como una partida más.
        var partidasDecimo = corrida.AcumuladoAnterior.PartidasDecimoTotal + (decimoPeriodo > 0m ? 1 : 0);
        var periodoEquivalente = CalcularPeriodoEquivalente(
            corrida.Frecuencia, corrida.NumeroPeriodoEmpleado, partidasDecimo);

        // Proyección del ACUMULADO bruto, sin restar nada —criterio del contador— y
        // desde lo realmente acumulado, no desde el bruto de este período: un período con
        // horas extra se diluye en el promedio y el año converge solo. Es la columna
        // INGRESO GRAVABLE del libro: ACUMULADO / PERIODOS x 26.
        var totalAcumulado = ingresoGravableAcumulado + decimoAcumulado;
        var ingresoAnualProyectado = totalAcumulado / periodoEquivalente * periodosEquivalentes;
        var rentaNeta = Math.Max(0m, ingresoAnualProyectado - corrida.DeduccionesFiscalesAnuales);
        var isrAnual = CalcularIsrAnual(rentaNeta);

        // Parte del impuesto anual que corresponde a lo ya corrido, medido en períodos
        // equivalentes. Al llegar el décimo el contador salta y con él la retención debida,
        // que es justamente cómo el décimo paga su parte del impuesto.
        //
        // NOTA — desviación respecto del código de la especificación: aquel sumaba aquí un
        // "efecto ISR del décimo" adicional. Ese ajuste lo contaba dos veces, porque el décimo
        // ya entra en la proyección anual. Verificado con tres sueldos: retenía
        // 337.50 / 2,256.25 / 6,418.75 cuando lo debido era 300.00 / 2,250.00 / 6,350.00.
        var proporcion = periodoEquivalente / periodosEquivalentes;
        var isrDebidoAcumulado = Redondear(isrAnual * proporcion);

        // Gastos de representación: cuenta aparte. La retención del período es lo que
        // falta para completar el impuesto del acumulado del año, así que un pago que
        // cruza los 25,000 se parte solo entre el 10% y el 15%.
        var grAcumulado = corrida.AcumuladoAnterior.GastoRepresentacionTotal + gastoRepresentacionPeriodo;
        var isrGrAcumulado = CalcularIsrGastosRepresentacion(grAcumulado);
        var isrGrAnterior = corrida.AcumuladoAnterior.IsrGastoRepresentacionTotal;
        var isrGrPeriodo = Math.Max(0m, isrGrAcumulado - isrGrAnterior);

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
            PeriodoEquivalente = Math.Round(periodoEquivalente, 3, MidpointRounding.AwayFromZero),
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
            SaldoFavorEmpleado = Redondear(saldoFavorEmpleado),
            GastoRepresentacionPeriodo = Redondear(gastoRepresentacionPeriodo),
            GastoRepresentacionAcumulado = Redondear(grAcumulado),
            IsrGastoRepresentacionPeriodo = Redondear(isrGrPeriodo),
            IsrGastoRepresentacionAcumulado = Redondear(isrGrAcumulado)
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
