// ====================================================================
// Planilla - LiquidacionCalculatorsTests
// Creado: 2026-06-18 — Casos-oráculo portados de Talento
//   (liquidacion-calculator.spec.ts), validados contra MITRADEL.
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Tests.Services;

public class LiquidacionCalculatorsTests
{
    // ── Prima de antigüedad (Art. 224) — 1 semana/año, sin escalones ──
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(30, 30)]   // sin escalón de 25 ni multiplicador 1.5
    [InlineData(7.5, 7.5)] // respeta fracciones
    public void PrimaAntiguedadWeeks__SinEscalones(decimal years, decimal expected)
    {
        LiquidacionCalculator.PrimaAntiguedadWeeks(years).Should().Be(expected);
    }

    // ── Indemnización Art. 225 — escala 3.4 (≤10 años) + 1 (>10 años) ──
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 17)]    // 5 × 3.4
    [InlineData(10, 34)]   // 10 × 3.4
    [InlineData(15, 39)]   // 10 × 3.4 + 5 × 1
    [InlineData(25, 49)]   // 10 × 3.4 + 15 × 1
    public void IndemnizacionDespidoWeeks__Escala(decimal years, decimal expected)
    {
        LiquidacionCalculator.IndemnizacionDespidoWeeks(years).Should().BeApproximately(expected, 0.001m);
    }

    // ── Cobertura de causas Art. 210/213 ──
    [Theory]
    [InlineData(CausaTerminacion.RenunciaSimple, false)]
    [InlineData(CausaTerminacion.DespidoJustificadoA, false)]
    [InlineData(CausaTerminacion.ExpiracionTerminoPactado, false)]
    [InlineData(CausaTerminacion.ConclusionObra, false)]
    [InlineData(CausaTerminacion.MuerteTrabajador, false)]
    [InlineData(CausaTerminacion.Jubilacion, false)]
    [InlineData(CausaTerminacion.RenunciaJustaCausa, true)]
    [InlineData(CausaTerminacion.DespidoInjustificado, true)]
    [InlineData(CausaTerminacion.CausaEconomicaC, true)]
    [InlineData(CausaTerminacion.FuerzaMayorB, true)]
    [InlineData(CausaTerminacion.MuerteEmpleador, true)]
    [InlineData(CausaTerminacion.MutuoAcuerdo, true)]
    [InlineData(CausaTerminacion.ProlongacionSuspension, true)]
    public void PagaIndemnizacionArt225__PorCausa(CausaTerminacion causa, bool expected)
    {
        LiquidacionCalculator.PagaIndemnizacionArt225(causa).Should().Be(expected);
    }

    private static LiquidacionCalcInput Base(decimal salario = 1000m, decimal years = 5m,
        CausaTerminacion causa = CausaTerminacion.RenunciaSimple, decimal vacDays = 0m) => new()
    {
        MonthlySalaryForPrima = salario,
        MonthlySalaryForIndemnization = salario,
        MonthlySalaryForDailyRate = salario,
        YearsWorked = years,
        Causa = causa,
        UnpaidVacationDays = vacDays
    };

    [Fact]
    public void Calcular__RenunciaSimple__SoloPrimaSinIndemnizacion()
    {
        var r = LiquidacionCalculator.Calcular(Base(causa: CausaTerminacion.RenunciaSimple));
        r.PrimaAntiguedadAmount.Should().BeApproximately(1153.85m, 0.05m); // 5 × 230.77
        r.IndemnizacionWeeks.Should().Be(0);
        r.IndemnizacionDespidoAmount.Should().Be(0);
    }

    [Fact]
    public void Calcular__DespidoInjustificado__PrimaMasIndemnizacion17Sem()
    {
        var r = LiquidacionCalculator.Calcular(Base(causa: CausaTerminacion.DespidoInjustificado));
        r.PrimaAntiguedadAmount.Should().BeApproximately(1153.85m, 0.05m);
        r.IndemnizacionWeeks.Should().BeApproximately(17m, 0.01m);
        r.IndemnizacionDespidoAmount.Should().BeApproximately(3923.08m, 0.05m);
    }

    [Fact]
    public void Calcular__DespidoJustificado__SoloPrima()
    {
        var r = LiquidacionCalculator.Calcular(Base(causa: CausaTerminacion.DespidoJustificadoA));
        r.IndemnizacionDespidoAmount.Should().Be(0);
        r.PrimaAntiguedadAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calcular__15Anos_CausaEconomica__Indemnizacion39Sem()
    {
        var r = LiquidacionCalculator.Calcular(Base(years: 15m, causa: CausaTerminacion.CausaEconomicaC));
        r.PrimaAntiguedadAmount.Should().BeApproximately(15 * 230.77m, 0.10m);
        r.IndemnizacionWeeks.Should().BeApproximately(39m, 0.01m);
    }

    [Fact]
    public void Calcular__BasesSalarialesDistintas__Art226VsArt149()
    {
        // Prima usa promedio 5 años (1000); indemnización usa 6m/30d (1500).
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 1000m,
            MonthlySalaryForIndemnization = 1500m,
            MonthlySalaryForDailyRate = 1500m,
            YearsWorked = 5m,
            Causa = CausaTerminacion.DespidoInjustificado
        });
        r.PrimaAntiguedadAmount.Should().BeApproximately(1153.85m, 0.05m);   // 5 × 230.77
        r.IndemnizacionDespidoAmount.Should().BeApproximately(5884.62m, 0.10m); // 17 × 346.15
        r.WeeklySalaryForPrima.Should().BeApproximately(230.77m, 0.05m);
        r.WeeklySalaryForIndemnization.Should().BeApproximately(346.15m, 0.05m);
    }

    [Fact]
    public void Calcular__RecargoArt219_50Porciento()
    {
        var input = Base(causa: CausaTerminacion.DespidoInjustificado) with { RecargoArt219Percentage = 0.5m };
        var r = LiquidacionCalculator.Calcular(input);
        r.RecargoArt219Percentage.Should().Be(0.5m);
        r.RecargoArt219Amount.Should().BeApproximately(r.IndemnizacionDespidoAmount * 0.5m, 0.05m);
    }

    [Fact]
    public void Calcular__RecargoArt219_NoAplicaSinIndemnizacion()
    {
        var input = Base(causa: CausaTerminacion.RenunciaSimple) with { RecargoArt219Percentage = 0.5m };
        var r = LiquidacionCalculator.Calcular(input);
        r.IndemnizacionDespidoAmount.Should().Be(0);
        r.RecargoArt219Amount.Should().Be(0);
    }

    [Fact]
    public void Calcular__Cesantia_ContratoPorObra__SustituyePrima()
    {
        // Decreto 60/1995: 1500 × 24 meses × 6% = 2,160 (oráculo cesantia Caso 5).
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 1500m,
            MonthlySalaryForIndemnization = 1500m,
            MonthlySalaryForDailyRate = 1500m,
            YearsWorked = 2m,
            Causa = CausaTerminacion.ConclusionObra,
            ContractDurationType = TipoContratoDuracion.PorObra
        });
        r.PrimaAntiguedadAmount.Should().Be(0);          // por obra no paga prima
        r.CesantiaAmount.Should().BeApproximately(2160m, 0.05m);
    }

    [Fact]
    public void Calcular__MenosDe1Ano__SinPrimaPeroIndemnizacionProporcional()
    {
        var r = LiquidacionCalculator.Calcular(Base(years: 0.5m, causa: CausaTerminacion.DespidoInjustificado)
            with { PreavisoOtorgado = true });
        r.PrimaAntiguedadAmount.Should().Be(0);          // prima exige ≥ 1 año
        r.IndemnizacionWeeks.Should().BeApproximately(1.7m, 0.01m); // 0.5 × 3.4
    }

    // ── Paridad con MITRADEL — casos del plan de auditoría ──

    [Fact]
    public void CasoA__5Anos_Despido_1000_1MesVac_PreavisoOtorgado__6300_42()
    {
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 1000m,
            MonthlySalaryForIndemnization = 1000m,
            MonthlySalaryForDailyRate = 1000m,
            YearsWorked = 5m,
            Causa = CausaTerminacion.DespidoInjustificado,
            UnpaidVacationDays = 30m,
            PreavisoOtorgado = true
        });
        r.PrimaAntiguedadAmount.Should().BeApproximately(1153.85m, 0.05m);
        r.IndemnizacionDespidoAmount.Should().BeApproximately(3923.08m, 0.05m);
        r.VacacionesNoPagadasAmount.Should().BeApproximately(1000m, 0.01m);
        r.VacacionesProporcionalAmount.Should().BeApproximately(90.91m, 0.05m);
        r.DecimoProporcionalAmount.Should().BeApproximately(132.58m, 0.05m);
        r.PreavisoCompensacionAmount.Should().Be(0); // absorbido en indemnización
        r.TotalAmount.Should().BeApproximately(6300.42m, 0.05m);
    }

    [Fact]
    public void CasoC__15Anos_Despido_2000__25006_41()
    {
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 2000m,
            MonthlySalaryForIndemnization = 2000m,
            MonthlySalaryForDailyRate = 2000m,
            YearsWorked = 15m,
            Causa = CausaTerminacion.DespidoInjustificado,
            PreavisoOtorgado = true
        });
        r.PrimaAntiguedadAmount.Should().BeApproximately(6923.08m, 0.05m);
        r.IndemnizacionWeeks.Should().BeApproximately(39m, 0.01m);
        r.IndemnizacionDespidoAmount.Should().BeApproximately(18000m, 0.05m);
        r.DecimoProporcionalAmount.Should().BeApproximately(83.33m, 0.05m);
        r.VacacionesProporcionalAmount.Should().Be(0);
        r.TotalAmount.Should().BeApproximately(25006.41m, 0.05m);
    }

    [Fact]
    public void CasoD__Domestica_3Anos_Despido_350__SinPreavisoCompensable()
    {
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 350m,
            MonthlySalaryForIndemnization = 350m,
            MonthlySalaryForDailyRate = 350m,
            YearsWorked = 3m,
            Causa = CausaTerminacion.DespidoInjustificado,
            IsDomesticWorker = true,
            PreavisoOtorgado = false
        });
        r.PreavisoCompensacionAmount.Should().Be(0);
        r.PreavisoSource.Should().Contain("Art. 233");
        r.DecimoProporcionalAmount.Should().BeApproximately(14.58m, 0.05m);
        r.VacacionesProporcionalAmount.Should().Be(0);
    }

    [Fact]
    public void CasoE__28Anos_Recargo25__Recargo9000()
    {
        var r = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = 3000m,
            MonthlySalaryForIndemnization = 3000m,
            MonthlySalaryForDailyRate = 3000m,
            YearsWorked = 28m,
            Causa = CausaTerminacion.DespidoInjustificado,
            RecargoArt219Percentage = 0.25m,
            PreavisoOtorgado = true
        });
        r.IndemnizacionWeeks.Should().BeApproximately(52m, 0.01m); // 10×3.4 + 18×1
        r.IndemnizacionDespidoAmount.Should().BeApproximately(36000m, 0.10m);
        r.RecargoArt219Amount.Should().BeApproximately(9000m, 0.10m);
    }
}
