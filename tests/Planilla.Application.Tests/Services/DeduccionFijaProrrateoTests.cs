using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Tests.Services;

/// <summary>
/// Unit tests para el prorrateo de DeduccionFija.Monto (mensual → período).
/// Formula: Math.Round(montoMensual * 12m / periodsPerYear, 2)
/// Equivalente a la formula usada en PayrollProcessingService (DEV-67).
/// </summary>
public class DeduccionFijaProrrateoTests
{
    private static readonly DeduccionPrioridadEngine _engine = new();

    // ====================================================================
    // Formula de prorrateo directa
    // ====================================================================

    [Theory]
    [InlineData(PayPeriodType.Mensual,   100.00, 100.00)]  // 100 * 12/12 = 100.00
    [InlineData(PayPeriodType.Quincenal, 100.00,  50.00)]  // 100 * 12/24 = 50.00
    [InlineData(PayPeriodType.Bisemanal, 100.00,  46.15)]  // 100 * 12/26 = 46.15
    [InlineData(PayPeriodType.Semanal,   100.00,  23.08)]  // 100 * 12/52 = 23.08
    public void ProrrateoDeduccionFija_MontoMensual100_RetornaMontoPeriodoCorrecto(
        PayPeriodType periodType, decimal montoMensual, decimal esperado)
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(periodType);
        var resultado = Math.Round(montoMensual * 12m / periodsPerYear, 2);
        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData(PayPeriodType.Mensual,   650.00, 650.00)]  // 650 * 12/12 = 650.00
    [InlineData(PayPeriodType.Quincenal, 650.00, 325.00)]  // 650 * 12/24 = 325.00
    [InlineData(PayPeriodType.Bisemanal, 650.00, 300.00)]  // 650 * 12/26 = 300.00
    [InlineData(PayPeriodType.Semanal,   650.00, 150.00)]  // 650 * 12/52 = 150.00
    public void ProrrateoDeduccionFija_MontoMensual650_RetornaMontoPeriodoCorrecto(
        PayPeriodType periodType, decimal montoMensual, decimal esperado)
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(periodType);
        var resultado = Math.Round(montoMensual * 12m / periodsPerYear, 2);
        resultado.Should().Be(esperado);
    }

    [Fact]
    public void ProrrateoDeduccionFija_MontoMensualCero_RetornaCero()
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(PayPeriodType.Quincenal);
        var resultado = Math.Round(0m * 12m / periodsPerYear, 2);
        resultado.Should().Be(0m);
    }

    // ====================================================================
    // Integración con DeduccionPrioridadEngine
    // Simula lo que hace PayrollProcessingService: calcula el MontoFijo
    // prorrateado y lo pasa al engine para que lo aplique.
    // ====================================================================

    [Theory]
    [InlineData(PayPeriodType.Mensual,   100.00, 100.00)]
    [InlineData(PayPeriodType.Quincenal, 100.00,  50.00)]
    [InlineData(PayPeriodType.Bisemanal, 100.00,  46.15)]
    [InlineData(PayPeriodType.Semanal,   100.00,  23.08)]
    public void Engine_ConMontoProrrateado_AplicaMontoCorrectoAlNeto(
        PayPeriodType periodType, decimal montoMensual, decimal montoEsperadoPeriodo)
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(periodType);
        var montoFijo = Math.Round(montoMensual * 12m / periodsPerYear, 2);

        var deducciones = new[]
        {
            new DeduccionPendiente
            {
                OrigenDeduccionFijaId = 1,
                TipoDeduccion = TipoDeduccion.AhorroVoluntario,
                Categoria = CategoriaDeduccion.Voluntaria,
                Descripcion = "Ahorro voluntario - test",
                MontoFijo = montoFijo,
                EsPorcentaje = false,
                BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
                Prioridad = 1
            }
        };

        var resultado = _engine.AplicarDeduccionesConPrelacion(
            grossPay: 1_000m,
            netoPostLegal: 800m,
            salarioMinimoPeriodo: 0m,
            deducciones: deducciones);

        resultado.TotalVoluntarias.Should().Be(montoEsperadoPeriodo);
        resultado.Detalle.Should().HaveCount(1);
        resultado.Detalle[0].MontoAplicado.Should().Be(montoEsperadoPeriodo);
        resultado.Detalle[0].MontoSolicitado.Should().Be(montoEsperadoPeriodo);
    }

    // ====================================================================
    // MontoTotalACobrar — el engine limita al remanente pendiente
    // ====================================================================

    [Fact]
    public void Engine_MontoTotalACobrar_LimitaAlRemanente_CuandoMontoProrrateadoEsMayor()
    {
        // Deuda total: $1,000. Ya cobrado: $975. Remanente: $25.
        // Monto prorrateado del período: $50 (100 mensual / 2 quincenal).
        // El engine debe aplicar solo $25 (el remanente).
        var montoFijo = Math.Round(100m * 12m / PayrollConstants.GetPeriodsPerYear(PayPeriodType.Quincenal), 2); // $50

        var deduccion = new DeduccionPendiente
        {
            OrigenDeduccionFijaId = 1,
            TipoDeduccion = TipoDeduccion.Embargo,
            Categoria = CategoriaDeduccion.EmbargoJudicial,
            Descripcion = "Embargo - último período",
            MontoFijo = montoFijo,
            EsPorcentaje = false,
            BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
            Prioridad = 1,
            MontoTotalACobrar = 1_000m,
            MontoCobradoAcumulado = 975m   // remanente = 25
        };

        var resultado = _engine.AplicarDeduccionesConPrelacion(
            grossPay: 1_000m,
            netoPostLegal: 800m,
            salarioMinimoPeriodo: 0m,
            deducciones: new[] { deduccion });

        resultado.TotalEmbargos.Should().Be(25m);
        // El engine limita MontoSolicitado al remanente antes de crear el item
        resultado.Detalle[0].MontoSolicitado.Should().Be(25m);
        resultado.Detalle[0].MontoAplicado.Should().Be(25m);
        resultado.Detalle[0].MontoLimitado.Should().Be(0m);
    }

    [Fact]
    public void Engine_MontoTotalACobrar_DeudaSaldada_NoAplicaNada()
    {
        // Deuda completamente pagada: remanente = 0.
        // El engine no debe aplicar nada.
        var montoFijo = Math.Round(100m * 12m / PayrollConstants.GetPeriodsPerYear(PayPeriodType.Quincenal), 2); // $50

        var deduccion = new DeduccionPendiente
        {
            OrigenDeduccionFijaId = 1,
            TipoDeduccion = TipoDeduccion.Embargo,
            Categoria = CategoriaDeduccion.EmbargoJudicial,
            Descripcion = "Embargo - deuda saldada",
            MontoFijo = montoFijo,
            EsPorcentaje = false,
            BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
            Prioridad = 1,
            MontoTotalACobrar = 1_000m,
            MontoCobradoAcumulado = 1_000m  // remanente = 0
        };

        var resultado = _engine.AplicarDeduccionesConPrelacion(
            grossPay: 1_000m,
            netoPostLegal: 800m,
            salarioMinimoPeriodo: 0m,
            deducciones: new[] { deduccion });

        resultado.TotalEmbargos.Should().Be(0m);
        resultado.Detalle[0].MontoSolicitado.Should().Be(0m);
        resultado.Detalle[0].MontoAplicado.Should().Be(0m);
    }

    // ====================================================================
    // EsPorcentaje = true — MontoFijo es ignorado por el engine
    // ====================================================================

    [Fact]
    public void Engine_EsPorcentajeTrue_IgnoraMontoFijoYUsaPorcentaje()
    {
        // MontoFijo tiene un valor arbitrario; el engine debe ignorarlo
        // y calcular 5% del bruto = $50
        var deduccion = new DeduccionPendiente
        {
            OrigenDeduccionFijaId = 1,
            TipoDeduccion = TipoDeduccion.Sindicato,
            Categoria = CategoriaDeduccion.Voluntaria,
            Descripcion = "Cuota sindical 5%",
            MontoFijo = 999m,   // valor ignorado
            EsPorcentaje = true,
            Porcentaje = 5m,
            BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
            Prioridad = 1
        };

        var resultado = _engine.AplicarDeduccionesConPrelacion(
            grossPay: 1_000m,
            netoPostLegal: 800m,
            salarioMinimoPeriodo: 0m,
            deducciones: new[] { deduccion });

        resultado.TotalVoluntarias.Should().Be(50m);
    }
}
