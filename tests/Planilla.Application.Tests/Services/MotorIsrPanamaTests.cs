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
        r.IsrDescontarPeriodo.Should().Be(12.50m);   // 300 x 1/24
    }

    // ── Caso 2: saldos iniciales de migración ────────────────────────

    [Fact]
    public void SaldoInicialRetenido__SeRestaDelDebidoAcumulado()
    {
        var acum = new AcumuladoIsr
        {
            IngresoGravableInicial = 5_500m,   // 11 quincenas ya pagadas en el otro sistema
            IsrRetenidoInicial = 137.50m       // y ya le retuvieron esto
        };

        var r = MotorIsrPanama.Calcular(Corrida(PayPeriodType.Quincenal, 12, 500m, acumulado: acum));

        r.IsrRetenidoInicial.Should().Be(137.50m);
        r.IsrDebidoAcumulado.Should().Be(150m);      // 300 x 12/24
        r.IsrDescontarPeriodo.Should().Be(12.50m);   // solo la diferencia, sin duplicar
    }

    // ── Caso 3: nunca se muestra ISR negativo ────────────────────────

    [Fact]
    public void YaSeRetuvoDeMas__DescuentaCeroYGuardaElSaldoAFavor()
    {
        // El ejemplo literal de la especificación: debido 900, retenido 975, saldo a favor 75.
        // Salario mensual 1,769.23 → proyectado 23,000 → ISR anual 1,800 → debido a mitad de año 900.
        var acum = new AcumuladoIsr
        {
            IngresoGravableInicial = 8_846.15m,   // cinco meses ya devengados
            IsrRetenidoInicial = 975m             // pero ya le retuvieron de más
        };
        var r = MotorIsrPanama.Calcular(
            Corrida(PayPeriodType.Mensual, 6, 1_769.23m, acumulado: acum));

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
}
