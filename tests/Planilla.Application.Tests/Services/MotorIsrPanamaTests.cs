// ====================================================================
// Tests del motor de ISR acumulativo.
// Cubre los nueve casos de aceptación de la especificación funcional v1.0
// más la regresión del doble conteo del décimo.
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Vorluno.Planilla.Application.Tests.Services;

public class MotorIsrPanamaTests
{
    private static CorridaIsr Corrida(
        PayPeriodType frecuencia, int periodo, decimal salario,
        decimal decimo = 0m, AcumuladoIsr? acumulado = null, decimal deducciones = 0m)
        => new()
        {
            Frecuencia = frecuencia,
            NumeroPeriodoEmpleado = periodo,
            DeduccionesFiscalesAnuales = deducciones,
            AcumuladoAnterior = acumulado ?? new AcumuladoIsr(),
            Movimientos = decimo > 0
                ? new[] { new MovimientoIsr(TratamientoIsr.GravableAcumulable, salario),
                          new MovimientoIsr(TratamientoIsr.DecimoTercerMes, decimo) }
                : new[] { new MovimientoIsr(TratamientoIsr.GravableAcumulable, salario) }
        };

    /// <summary>Corre un año completo y devuelve el total retenido.</summary>
    private static decimal CorrerAño(PayPeriodType frecuencia, decimal salarioPeriodo, int[] periodosConDecimo)
    {
        var periodos = MotorIsrPanama.ObtenerPeriodosAnuales(frecuencia);
        var acum = new AcumuladoIsr();
        decimal retenido = 0m;

        for (var n = 1; n <= periodos; n++)
        {
            // Cada partida del décimo es un tercio de un mes de salario.
            var decimoPartida = periodosConDecimo.Contains(n)
                ? salarioPeriodo * periodos / 12m / 3m
                : 0m;

            var r = MotorIsrPanama.Calcular(Corrida(frecuencia, n, salarioPeriodo, decimoPartida, acum));
            retenido += r.IsrDescontarPeriodo;

            acum = new AcumuladoIsr
            {
                IngresoGravableProcesado = r.IngresoGravableAcumulado,
                DecimoProcesado = r.DecimoAcumulado,
                PartidasDecimoProcesadas = acum.PartidasDecimoTotal + (decimoPartida > 0m ? 1 : 0),
                IsrRegularProcesado = r.IsrRetenidoTotalNuevo
            };
        }
        return Math.Round(retenido, 2);
    }

    // ── Tarifa del Art. 700 ───────────────────────────────────────────

    [Theory]
    [InlineData(10_000, 0)]           // exento
    [InlineData(11_000, 0)]           // borde exacto del umbral
    [InlineData(13_000, 300)]         // tramo del 15%
    [InlineData(50_000, 5_850)]       // borde: cierra justo donde arranca el 25%
    [InlineData(52_000, 6_350)]       // tramo del 25%
    public void CalcularIsrAnual__AplicaLaTarifaProgresiva(decimal renta, decimal esperado)
    {
        MotorIsrPanama.CalcularIsrAnual(renta).Should().Be(esperado);
    }

    // ── Casos 4, 5, 6 y 7 de la especificación: frecuencia por empleado ──

    [Theory]
    [InlineData(PayPeriodType.Semanal, 52)]
    [InlineData(PayPeriodType.Bisemanal, 26)]
    [InlineData(PayPeriodType.Quincenal, 24)]
    [InlineData(PayPeriodType.Mensual, 12)]
    public void PeriodosAnuales__SegunLaFrecuenciaDelEmpleado(PayPeriodType f, int esperado)
    {
        MotorIsrPanama.ObtenerPeriodosAnuales(f).Should().Be(esperado);
    }

    [Fact]
    public void EmpleadosDeDistintaFrecuencia__CadaUnoCalculaConLaSuya()
    {
        // Mismo ingreso anual (13,000) por caminos distintos: los tres deben coincidir.
        var quincenal = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m));
        var mensual = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Mensual, 1, 1000m));
        var semanal = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Semanal, 1, 12_000m / 52m));

        quincenal.IngresoAnualProyectado.Should().Be(13_000m);
        mensual.IngresoAnualProyectado.Should().Be(13_000m);
        semanal.IngresoAnualProyectado.Should().Be(13_000m);
        quincenal.IsrAnualProyectado.Should().Be(300m);
        mensual.IsrAnualProyectado.Should().Be(300m);
    }

    // ── Caso 1: la fecha de ingreso no bloquea el cálculo ─────────────

    [Fact]
    public void PrimeraCorridaDelEmpleado__YaRetieneSiElProyectadoGeneraIsr()
    {
        // Entra a mitad de año: en su primera quincena ya se le proyecta el año
        // y se le retiene la parte que toca. La regularización ajusta después.
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m));

        r.IngresoAnualProyectado.Should().Be(13_000m);
        r.IsrAnualProyectado.Should().Be(300m);
        r.IsrDescontarPeriodo.Should().Be(11.54m);   // 300 x 1/26 — el 11.53 del contador
    }

    // ── Caso 2: saldos iniciales de migración ────────────────────────

    [Fact]
    public void SaldoInicialRetenido__SeRestaDelDebidoAcumulado()
    {
        var acum = new AcumuladoIsr
        {
            IngresoGravableInicial = 5_500m,   // 11 quincenas ya pagadas en el otro sistema
            IsrRetenidoInicial = 127m          // y ya le retuvieron esto
        };

        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 12, 500m, acumulado: acum));

        r.IsrRetenidoInicial.Should().Be(127m);
        r.IsrDebidoAcumulado.Should().Be(138.46m);   // 300 x 12/26
        r.IsrDescontarPeriodo.Should().Be(11.46m);   // solo la diferencia, sin duplicar
    }

    // ── Caso 3: nunca se muestra ISR negativo ────────────────────────

    [Fact]
    public void YaSeRetuvoDeMas__DescuentaCeroYGuardaElSaldoAFavor()
    {
        // El ejemplo literal de la especificación: debido 900, retenido 975, saldo a favor 75.
        // Salario mensual 1,846.15 → proyectado 24,000 → ISR anual 1,950 → debido a los
        // 6 de 13 períodos equivalentes = 900.
        var acum = new AcumuladoIsr
        {
            IngresoGravableInicial = 9_230.77m,   // cinco meses ya devengados
            IsrRetenidoInicial = 975m             // pero ya le retuvieron de más
        };
        var r = MotorIsrPanama.Calcular(
            Corrida(PayPeriodType.Mensual, 6, 1_846.15m, acumulado: acum));

        r.IsrDebidoAcumulado.Should().BeApproximately(900m, 0.05m);
        r.IsrDescontarPeriodo.Should().Be(0m);
        r.SaldoFavorEmpleado.Should().BeApproximately(75m, 0.05m);
        r.IsrRetenidoTotalNuevo.Should().Be(975m);   // no crece: no se retuvo nada más
    }

    [Fact]
    public void ElSaldoAFavorNoSeMuestraComoNegativo()
    {
        // Retencion inicial desproporcionada: el descuento se topa en cero y el resto
        // queda registrado como saldo a favor, nunca como un negativo en la planilla.
        var acum = new AcumuladoIsr { IngresoGravableInicial = 500m, IsrRetenidoInicial = 5_000m };
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 2, 500m, acumulado: acum));

        r.IsrDescontarPeriodo.Should().Be(0m);
        r.SaldoFavorEmpleado.Should().Be(5_000m - r.IsrDebidoAcumulado);
        r.SaldoFavorEmpleado.Should().BeGreaterThan(0m);
    }

    // ── Caso 9: sin renta gravable no hay retención ──────────────────

    [Fact]
    public void IngresoBajoElUmbral__NoRetieneNada()
    {
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 400m));

        r.IngresoAnualProyectado.Should().Be(10_400m);   // bajo los 11,000
        r.IsrAnualProyectado.Should().Be(0m);
        r.IsrDescontarPeriodo.Should().Be(0m);
        r.SaldoFavorEmpleado.Should().Be(0m);
    }

    // ── Caso 8: el décimo se acumula por separado ────────────────────

    [Fact]
    public void Decimo__SeAcumulaAparteDelSalario()
    {
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 7, 500m, decimo: 333.33m));

        r.DecimoPeriodo.Should().Be(333.33m);
        r.IngresoGravablePeriodo.Should().Be(500m);   // el décimo NO se mezcla con el salario
    }

    // ── Regresión: el doble conteo del décimo ────────────────────────

    [Theory]
    [InlineData(500, 300)]        // 12,000 + 1,000 de décimo = 13,000 → 300
    [InlineData(1000, 2250)]      // 24,000 + 2,000 = 26,000 → 2,250
    [InlineData(2000, 6350)]      // 48,000 + 4,000 = 52,000 → 6,350
    public void AñoCompleto__ElTotalRetenidoEsExactamenteElIsrDelAño(decimal salarioQuincena, decimal isrEsperado)
    {
        // El código de la especificación sumaba un "efecto ISR del décimo" en las corridas
        // de la partida, contándolo dos veces: ya estaba dentro de la proyección anual.
        // Con ese ajuste retenía 337.50 / 2,256.25 / 6,418.75. Sin él, el año cierra exacto.
        CorrerAño(PayPeriodType.Quincenal, salarioQuincena, new[] { 7, 15, 23 })
            .Should().BeApproximately(isrEsperado, 0.05m);
    }

    [Fact]
    public void AñoCompleto__LaRetencionNoDaSaltosEnLaQuincenaDelDecimo()
    {
        var acum = new AcumuladoIsr();
        var descuentos = new List<decimal>();

        for (var n = 1; n <= 24; n++)
        {
            var decimoPartida = new[] { 7, 15, 23 }.Contains(n) ? 333.33m : 0m;
            var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, n, 500m, decimoPartida, acum));
            descuentos.Add(r.IsrDescontarPeriodo);
            acum = new AcumuladoIsr
            {
                IngresoGravableProcesado = r.IngresoGravableAcumulado,
                DecimoProcesado = r.DecimoAcumulado,
                PartidasDecimoProcesadas = acum.PartidasDecimoTotal + (decimoPartida > 0m ? 1 : 0),
                IsrRegularProcesado = r.IsrRetenidoTotalNuevo
            };
        }

        // Ninguna quincena debe cobrar mas del doble de la media ni caer a cero de golpe.
        var media = descuentos.Average();
        descuentos.Should().OnlyContain(d => d <= media * 2m);
        descuentos.Should().OnlyContain(d => d > 0m);
    }

    // ── Deducciones: el motor las recibe, no las decide ──────────────

    [Fact]
    public void DeduccionesAnuales__SeRestanDeLaRentaProyectada()
    {
        var sin = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m));
        var con = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m, deducciones: 1_000m));

        sin.RentaNetaGravableProyectada.Should().Be(13_000m);
        con.RentaNetaGravableProyectada.Should().Be(12_000m);
        con.IsrAnualProyectado.Should().BeLessThan(sin.IsrAnualProyectado);
    }

    [Fact]
    public void DeduccionesMayoresQueLaRenta__NoProducenBaseNegativa()
    {
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m, deducciones: 99_000m));

        r.RentaNetaGravableProyectada.Should().Be(0m);
        r.IsrDescontarPeriodo.Should().Be(0m);
    }

    // ── Validación ───────────────────────────────────────────────────

    [Fact]
    public void NumeroDePeriodoInvalido__Falla()
    {
        var accion = () => MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 0, 500m));
        accion.Should().Throw<ArgumentException>();
    }

    // ── Trazabilidad: el resultado permite reconstruir el cálculo ────

    [Fact]
    public void Resultado__ExponeTodoElRastroDelCalculo()
    {
        var acum = new AcumuladoIsr { IngresoGravableInicial = 2_000m, IsrRetenidoInicial = 50m };
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 5, 500m, acumulado: acum));

        r.IngresoGravablePeriodo.Should().Be(500m);
        r.IngresoGravableAcumulado.Should().Be(2_500m);
        r.IsrRetenidoInicial.Should().Be(50m);
        r.IsrRetenidoTotalAnterior.Should().Be(50m);
        r.IsrRetenidoTotalNuevo.Should().Be(r.IsrRetenidoTotalAnterior + r.IsrDescontarPeriodo);
    }
    // ── El reparto que describe el contador ──────────────────────────

    [Fact]
    public void RepartoDelContador__24QuincenasMasLasDosDelDecimo()
    {
        // "No son 24 quincenas, son 26."  500 x 26 = 13,000 → ISR 300 → 300/26 = 11.54.
        // La planilla retiene 24 x 11.54 = 276.92 y el decimo el resto: 23.08, en 3 partidas.
        MotorIsrPanama.ObtenerPeriodosEquivalentesAnuales(PayPeriodType.Quincenal).Should().Be(26m);

        var porQuincena = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 500m));
        porQuincena.IsrAnualProyectado.Should().Be(300m);
        porQuincena.IsrDescontarPeriodo.Should().Be(11.54m);

        var enPlanilla = 11.54m * 24m;
        var enDecimo = 300m - enPlanilla;
        enPlanilla.Should().BeApproximately(276.96m, 0.10m);
        enDecimo.Should().BeApproximately(23.08m, 0.10m);
        (enDecimo / 3m).Should().BeApproximately(7.69m, 0.05m);
    }

    [Theory]
    [InlineData(PayPeriodType.Semanal, 56.3333333333333333333333333)]
    [InlineData(PayPeriodType.Bisemanal, 28.1666666666666666666666667)]
    [InlineData(PayPeriodType.Quincenal, 26)]
    [InlineData(PayPeriodType.Mensual, 13)]
    public void PeriodosEquivalentes__MismoCriterioEnTodaFrecuencia(PayPeriodType f, decimal esperado)
    {
        MotorIsrPanama.ObtenerPeriodosEquivalentesAnuales(f)
            .Should().BeApproximately(esperado, 0.0001m);
    }

    [Fact]
    public void PeriodoEquivalente__AvanzaConElDecimoIgualQueEnElLibroDelContador()
    {
        // La columna PERIODOS del Excel da 7.667 en la quincena de la primera partida.
        var acum = new AcumuladoIsr { IngresoGravableProcesado = 3_000m };   // 6 quincenas
        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 7, 500m, decimo: 333.33m, acumulado: acum));

        r.PeriodoEquivalente.Should().Be(7.667m);
        r.IngresoGravableAcumulado.Should().Be(3_500m);
        r.DecimoAcumulado.Should().Be(333.33m);
        r.IngresoAnualProyectado.Should().BeApproximately(13_000m, 1m);
    }

    [Theory]
    [InlineData(PayPeriodType.Quincenal, 7, 1, 7.667)]
    [InlineData(PayPeriodType.Quincenal, 15, 2, 16.333)]
    [InlineData(PayPeriodType.Quincenal, 23, 3, 25.000)]
    [InlineData(PayPeriodType.Quincenal, 24, 3, 26.000)]
    [InlineData(PayPeriodType.Mensual, 12, 3, 13.000)]
    public void PeriodoEquivalente__EsUnaConstantePorPartidaNoDependeDelMonto(
        PayPeriodType frecuencia, int periodo, int partidas, decimal esperado)
    {
        // Columna PERIODOS del libro del contador: constantes 7.667 / 16.333 / 25 / 26.
        // Antes se ponderaba por monto y con salario variable daba 7.685 donde el
        // contador escribe 7.667.
        Math.Round(MotorIsrPanama.CalcularPeriodoEquivalente(frecuencia, periodo, partidas), 3)
            .Should().Be(esperado);
    }

    // ====================================================================
    // Reproducción de las hojas del contador, celda por celda
    //
    // Libro1.xlsx (Orlando_Barroso) y CALCULO RENTA.xlsx (CRIS, Marianela_Barroso).
    // Las fórmulas: ACUMULADO = suma bruta; INGRESO GRAVABLE = ACUMULADO/PERIODOS*26;
    // RENTA ANUAL = Art. 700; IMPUESTO CAUSADO = RENTA ANUAL/26*PERIODOS.
    // La base es el BRUTO: no se resta el Seguro Social en ninguna celda.
    // ====================================================================

    [Fact]
    public void Libro1_OrlandoBarroso__IngresoGravableYRentaAlCentavo()
    {
        // Fila 1: 356.72 / 1 x 26 = 9,274.72. Con el Seguro Social restado daba 8,370.44.
        var acum = new AcumuladoIsr();
        var q1 = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 1, 356.72m, acumulado: acum));
        q1.IngresoAnualProyectado.Should().Be(9_274.72m);
        q1.IsrAnualProyectado.Should().Be(0m);

        acum = new AcumuladoIsr { IngresoGravableProcesado = 356.72m };
        var q2 = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 2, 445.90m, acumulado: acum));
        q2.IngresoAnualProyectado.Should().Be(10_434.06m);

        acum = new AcumuladoIsr { IngresoGravableProcesado = 802.62m };
        var q3 = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 3, 466.48m, acumulado: acum));
        q3.IngresoAnualProyectado.Should().Be(10_998.87m);

        acum = new AcumuladoIsr { IngresoGravableProcesado = 1_269.10m };
        var q4 = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 4, 466.48m, acumulado: acum));
        q4.IngresoAnualProyectado.Should().Be(11_281.27m);
        q4.IsrAnualProyectado.Should().Be(42.19m, "RENTA ANUAL");
        q4.IsrDebidoAcumulado.Should().Be(6.49m, "IMPUESTO CAUSADO = 42.19 / 26 x 4");
    }

    [Fact]
    public void CalculoRenta_Cris__ExtrasYTresDecimosAlCentavo()
    {
        // Salario 1,000 por quincena con extras variables y décimo en q7, q15 y q23.
        var extras = new Dictionary<int, decimal>
        {
            [1] = 3_711.21m, [3] = 2_717.30m, [5] = 3_939.97m, [7] = 3_095.62m, [9] = 4_835.96m,
            [11] = 3_771.71m, [13] = 3_721.53m, [15] = 3_603.20m, [17] = 3_185.45m, [19] = 1_612.46m,
            [21] = 1_952.76m, [23] = 1_694.51m
        };
        var decimos = new Dictionary<int, decimal> { [7] = 1_530.62m, [15] = 2_442.82m, [23] = 1_370.43m };

        var causadoPorQuincena = new Dictionary<int, decimal>();
        var acum = new AcumuladoIsr();
        for (var n = 1; n <= 24; n++)
        {
            var bruto = 1_000m + extras.GetValueOrDefault(n);
            var dec = decimos.GetValueOrDefault(n);
            var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, n, bruto, dec, acum));
            causadoPorQuincena[n] = r.IsrDebidoAcumulado;

            if (n == 7) r.PeriodoEquivalente.Should().Be(7.667m);
            if (n == 15) r.PeriodoEquivalente.Should().Be(16.333m);
            if (n == 23) r.PeriodoEquivalente.Should().Be(25.000m);
            if (n == 24) r.PeriodoEquivalente.Should().Be(26.000m);

            acum = new AcumuladoIsr
            {
                IngresoGravableProcesado = r.IngresoGravableAcumulado,
                DecimoProcesado = r.DecimoAcumulado,
                PartidasDecimoProcesadas = acum.PartidasDecimoTotal + (dec > 0m ? 1 : 0),
                IsrRegularProcesado = r.IsrRetenidoTotalNuevo
            };
        }

        // Celdas de IMPUESTO CAUSADO de la hoja CRIS. El Excel calcula con la fracción
        // exacta 7.667 escrita a mano y sin redondeo intermedio; un centavo de tolerancia.
        causadoPorQuincena[1].Should().BeApproximately(922.03m, 0.01m);
        causadoPorQuincena[7].Should().BeApproximately(3_537.70m, 0.01m);
        causadoPorQuincena[15].Should().BeApproximately(7_915.01m, 0.02m);
        causadoPorQuincena[24].Should().BeApproximately(10_146.39m, 0.01m);
    }

    [Fact]
    public void CalculoRenta_MarianelaBarroso__SalarioFijoCierraEn6350()
    {
        // 2,000 por quincena, décimo 1,333.33 x 3: 52,000 al año -> RENTA ANUAL 6,350.
        var acum = new AcumuladoIsr();
        decimal causadoFinal = 0m;
        for (var n = 1; n <= 24; n++)
        {
            var dec = new[] { 7, 15, 23 }.Contains(n) ? 1_333.33m : 0m;
            var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, n, 2_000m, dec, acum));
            r.IsrAnualProyectado.Should().BeApproximately(6_350m, 0.60m, "RENTA ANUAL fila {0}", n);
            causadoFinal = r.IsrDebidoAcumulado;
            acum = new AcumuladoIsr
            {
                IngresoGravableProcesado = r.IngresoGravableAcumulado,
                DecimoProcesado = r.DecimoAcumulado,
                PartidasDecimoProcesadas = acum.PartidasDecimoTotal + (dec > 0m ? 1 : 0),
                IsrRegularProcesado = r.IsrRetenidoTotalNuevo
            };
        }
        causadoFinal.Should().Be(6_350.00m, "IMPUESTO CAUSADO de la quincena 24");
    }


    // ====================================================================
    // Gastos de representación
    //
    // Tienen tarifa propia (Código Fiscal Art. 732): 10% hasta B/.25,000 y
    // B/.2,500 más 15% sobre el excedente. No se proyectan ni se mezclan con
    // el salario: es una retención sobre lo efectivamente pagado en el año.
    // ====================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10_000, 1_000)]
    [InlineData(25_000, 2_500)]      // el tope del primer tramo
    [InlineData(30_000, 3_250)]      // 2,500 + 15% de 5,000
    [InlineData(100_000, 13_750)]    // 2,500 + 15% de 75,000
    public void TarifaDeGastosDeRepresentacion__SigueLosDosTramosDelArticulo732(
        decimal acumuladoAnual, decimal esperado)
    {
        MotorIsrPanama.CalcularIsrGastosRepresentacion(acumuladoAnual).Should().Be(esperado);
    }

    [Fact]
    public void GastoQueCruzaLos25000__SeParteSoloEntreElDiezYElQuincePorCiento()
    {
        // Ya lleva 20,000 pagados (2,000 retenidos) y ahora le pagan 10,000 más.
        // Del pago nuevo, 5,000 completan el primer tramo al 10% y 5,000 van al 15%.
        var resultado = MotorIsrPanama.Calcular(new CorridaIsr
        {
            Frecuencia = PayPeriodType.Mensual,
            NumeroPeriodoEmpleado = 6,
            AcumuladoAnterior = new AcumuladoIsr
            {
                GastoRepresentacionProcesado = 20_000m,
                IsrGastoRepresentacionProcesado = 2_000m
            },
            Movimientos = new[]
            {
                new MovimientoIsr(TratamientoIsr.GastoRepresentacion, 10_000m)
            }
        });

        resultado.GastoRepresentacionAcumulado.Should().Be(30_000m);
        resultado.IsrGastoRepresentacionAcumulado.Should().Be(3_250m);
        resultado.IsrGastoRepresentacionPeriodo.Should().Be(1_250m, "500 al 10% más 750 al 15%");
    }

    [Fact]
    public void GastosDeRepresentacion__NoAlteranElIsrDelSalario()
    {
        var acumulado = new AcumuladoIsr { IngresoGravableProcesado = 11_000m };

        var movimientosSalario = new[]
        {
            new MovimientoIsr(TratamientoIsr.GravableAcumulable, 1_000m)
        };

        var sinGasto = MotorIsrPanama.Calcular(new CorridaIsr
        {
            Frecuencia = PayPeriodType.Mensual,
            NumeroPeriodoEmpleado = 12,
            AcumuladoAnterior = acumulado,
            Movimientos = movimientosSalario
        });

        var conGasto = MotorIsrPanama.Calcular(new CorridaIsr
        {
            Frecuencia = PayPeriodType.Mensual,
            NumeroPeriodoEmpleado = 12,
            AcumuladoAnterior = acumulado,
            Movimientos = movimientosSalario
                .Append(new MovimientoIsr(TratamientoIsr.GastoRepresentacion, 5_000m))
                .ToArray()
        });

        conGasto.IngresoAnualProyectado.Should().Be(sinGasto.IngresoAnualProyectado,
            "el gasto de representación no entra en la proyección del salario");
        conGasto.IsrDescontarPeriodo.Should().Be(sinGasto.IsrDescontarPeriodo,
            "tampoco altera la renta neta gravable del Art. 700");

        conGasto.IsrGastoRepresentacionPeriodo.Should().Be(500m, "10% de 5,000");
        conGasto.IsrTotalDescontarPeriodo.Should()
            .Be(sinGasto.IsrDescontarPeriodo + 500m, "el comprobante suma las dos retenciones");
    }

    [Fact]
    public void GastosDeRepresentacion__ElAnioSeRetieneCompletoMesAMes()
    {
        // 2,500 mensuales durante 12 meses: 30,000 en el año.
        var grProcesado = 0m;
        var isrGrProcesado = 0m;

        for (var mes = 1; mes <= 12; mes++)
        {
            var resultado = MotorIsrPanama.Calcular(new CorridaIsr
            {
                Frecuencia = PayPeriodType.Mensual,
                NumeroPeriodoEmpleado = mes,
                AcumuladoAnterior = new AcumuladoIsr
                {
                    GastoRepresentacionProcesado = grProcesado,
                    IsrGastoRepresentacionProcesado = isrGrProcesado
                },
                Movimientos = new[]
                {
                    new MovimientoIsr(TratamientoIsr.GastoRepresentacion, 2_500m)
                }
            });

            grProcesado += 2_500m;
            isrGrProcesado += resultado.IsrGastoRepresentacionPeriodo;
        }

        grProcesado.Should().Be(30_000m);
        isrGrProcesado.Should().Be(MotorIsrPanama.CalcularIsrGastosRepresentacion(30_000m),
            "mes a mes se llega exactamente al impuesto del año, sin sobrantes de redondeo");
        isrGrProcesado.Should().Be(3_250m);
    }

    [Fact]
    public void SaldosDeMigracion__TambienCuentanParaLosGastosDeRepresentacion()
    {
        // Migró a mitad de año con 25,000 ya pagados y 2,500 ya retenidos.
        var resultado = MotorIsrPanama.Calcular(new CorridaIsr
        {
            Frecuencia = PayPeriodType.Mensual,
            NumeroPeriodoEmpleado = 1,
            AcumuladoAnterior = new AcumuladoIsr
            {
                GastoRepresentacionInicial = 25_000m,
                IsrGastoRepresentacionInicial = 2_500m
            },
            Movimientos = new[]
            {
                new MovimientoIsr(TratamientoIsr.GastoRepresentacion, 1_000m)
            }
        });

        resultado.IsrGastoRepresentacionPeriodo.Should().Be(150m,
            "el primer tramo ya se agotó antes de migrar: el pago nuevo va todo al 15%");
    }
}
