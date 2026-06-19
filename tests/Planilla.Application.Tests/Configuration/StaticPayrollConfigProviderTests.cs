// ====================================================================
// Planilla - StaticPayrollConfigProviderTests
// Descripción: Tests del provider estático para API Platform stateless.
// Verifica que los valores hardcoded coinciden con los del seed de BD
// (PayrollConfigSeeder.cs) para las 3 fases de la Reforma CSS (Ley 462).
//
// Si estos tests fallan tras un cambio en el seed, es una señal de que
// se perdió sincronía entre la fuente de verdad de la SaaS multi-tenant
// y la config estática del API Platform — revisar ambos archivos.
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Configuration;
using Vorluno.Planilla.Application.Services;

namespace Vorluno.Planilla.Application.Tests.Configuration;

public class StaticPayrollConfigProviderTests
{
    private readonly StaticPayrollConfigProvider _provider = new();

    // ====================================================================
    // Fase 1: hasta feb 2027 (13.25% patronal)
    // ====================================================================

    [Fact]
    public async Task GetTaxConfigAsync__Fase1_2026_06_01__Returns13_25Patronal()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        config.Should().NotBeNull();
        config!.CssEmployeeRate.Should().Be(9.75m);
        config.CssEmployerBaseRate.Should().Be(13.25m);
        config.EffectiveStartDate.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        config.EffectiveEndDate.Should().Be(new DateTime(2027, 2, 28, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetTaxConfigAsync__Fase1_2027_02_28__ReturnsFase1()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2027, 2, 28, 12, 0, 0, DateTimeKind.Utc));

        config!.CssEmployerBaseRate.Should().Be(13.25m);
    }

    // ====================================================================
    // Fase 2: mar 2027 – feb 2029 (14.25% patronal)
    // ====================================================================

    [Fact]
    public async Task GetTaxConfigAsync__Fase2_2027_06_01__Returns14_25Patronal()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        config!.CssEmployeeRate.Should().Be(9.75m);
        config.CssEmployerBaseRate.Should().Be(14.25m);
        config.EffectiveStartDate.Should().Be(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        config.EffectiveEndDate.Should().Be(new DateTime(2029, 2, 28, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetTaxConfigAsync__Fase2_Boundary_2027_03_01__Returns14_25()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        config!.CssEmployerBaseRate.Should().Be(14.25m);
    }

    // ====================================================================
    // Fase 3: desde mar 2029 (15.25% patronal, sin fin)
    // ====================================================================

    [Fact]
    public async Task GetTaxConfigAsync__Fase3_2029_06_01__Returns15_25Patronal()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2029, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        config!.CssEmployeeRate.Should().Be(9.75m);
        config.CssEmployerBaseRate.Should().Be(15.25m);
        config.EffectiveStartDate.Should().Be(new DateTime(2029, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        config.EffectiveEndDate.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxConfigAsync__Fase3_Boundary_2029_03_01__Returns15_25()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 1, new DateTime(2029, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        config!.CssEmployerBaseRate.Should().Be(15.25m);
    }

    // ====================================================================
    // Valores comunes a las 3 fases — parity con seed de BD
    // ====================================================================

    [Fact]
    public async Task GetTaxConfigAsync__RiskRates_MatchSeed()
    {
        var config = await _provider.GetTaxConfigAsync(1, new DateTime(2026, 6, 1));

        // Acuerdo N°2 de 1995 — mismos valores que PayrollConfigSeeder
        config!.CssRiskRateLow.Should().Be(0.56m);
        config.CssRiskRateMedium.Should().Be(2.10m);
        config.CssRiskRateHigh.Should().Be(5.67m);
    }

    [Fact]
    public async Task GetTaxConfigAsync__CssCaps_MatchSeed()
    {
        var config = await _provider.GetTaxConfigAsync(1, new DateTime(2026, 6, 1));

        config!.CssMaxContributionBaseStandard.Should().Be(1500.00m);
        config.CssMaxContributionBaseIntermediate.Should().Be(2000.00m);
        config.CssMaxContributionBaseHigh.Should().Be(2500.00m);
        config.CssIntermediateMinYears.Should().Be(25);
        config.CssIntermediateMinAvgSalary.Should().Be(2000.00m);
        config.CssHighMinYears.Should().Be(30);
        config.CssHighMinAvgSalary.Should().Be(2500.00m);
    }

    [Fact]
    public async Task GetTaxConfigAsync__SeguroEducativo_MatchSeed()
    {
        var config = await _provider.GetTaxConfigAsync(1, new DateTime(2026, 6, 1));

        config!.EducationalInsuranceEmployeeRate.Should().Be(1.25m);
        config.EducationalInsuranceEmployerRate.Should().Be(1.50m);
    }

    [Fact]
    public async Task GetTaxConfigAsync__IsrDeductions_MatchSeed()
    {
        var config = await _provider.GetTaxConfigAsync(1, new DateTime(2026, 6, 1));

        config!.DependentDeductionAmount.Should().Be(800.00m);
        config.MaxDependents.Should().Be(99); // Ley 37/2018 + Decreto 368/2018
    }

    [Fact]
    public async Task GetTaxConfigAsync__PreservesTenantIdForTraceability()
    {
        var config = await _provider.GetTaxConfigAsync(companyId: 42, new DateTime(2026, 6, 1));

        config!.TenantId.Should().Be(42);
    }

    // ====================================================================
    // Brackets ISR — parity con seed
    // ====================================================================

    [Fact]
    public async Task GetTaxBracketsAsync__2026__Returns3Brackets()
    {
        var brackets = await _provider.GetTaxBracketsAsync(1, 2026);

        brackets.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTaxBracketsAsync__2026_Bracket1_Exento()
    {
        var brackets = await _provider.GetTaxBracketsAsync(1, 2026);

        var exento = brackets[0];
        exento.Order.Should().Be(1);
        exento.MinIncome.Should().Be(0.00m);
        exento.MaxIncome.Should().Be(11000.00m);
        exento.Rate.Should().Be(0.00m);
        exento.FixedAmount.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetTaxBracketsAsync__2026_Bracket2_15Percent()
    {
        var brackets = await _provider.GetTaxBracketsAsync(1, 2026);

        var b2 = brackets[1];
        b2.Order.Should().Be(2);
        b2.MinIncome.Should().Be(11000.01m);
        b2.MaxIncome.Should().Be(50000.00m);
        b2.Rate.Should().Be(15.00m);
        b2.FixedAmount.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetTaxBracketsAsync__2026_Bracket3_25Percent()
    {
        var brackets = await _provider.GetTaxBracketsAsync(1, 2026);

        var b3 = brackets[2];
        b3.Order.Should().Be(3);
        b3.MinIncome.Should().Be(50000.01m);
        b3.MaxIncome.Should().BeNull();
        b3.Rate.Should().Be(25.00m);
        b3.FixedAmount.Should().Be(5850.00m);
    }

    [Fact]
    public async Task GetTaxBracketsAsync__2027__SameStructureAs2026()
    {
        var brackets2026 = await _provider.GetTaxBracketsAsync(1, 2026);
        var brackets2027 = await _provider.GetTaxBracketsAsync(1, 2027);

        brackets2027.Should().HaveCount(brackets2026.Count);
        for (var i = 0; i < brackets2026.Count; i++)
        {
            brackets2027[i].MinIncome.Should().Be(brackets2026[i].MinIncome);
            brackets2027[i].MaxIncome.Should().Be(brackets2026[i].MaxIncome);
            brackets2027[i].Rate.Should().Be(brackets2026[i].Rate);
            brackets2027[i].FixedAmount.Should().Be(brackets2026[i].FixedAmount);
        }
    }

    // ====================================================================
    // Integration con IncomeTaxCalculationServicePortable
    // ====================================================================

    [Fact]
    public async Task IntegratedIsrCalculation__WithStaticProvider__ReturnsCorrectResult()
    {
        // Arrange
        var service = new IncomeTaxCalculationServicePortable(_provider);

        // Salario mensual B/. 2,000 → proyección anual con ×13 = $26,000
        // - Deducción 0 dependientes: $0
        // - Tramo aplicable: $11,000.01 – $50,000 (15% sobre excedente)
        // - Excedente: 26,000 - 11,000 = 15,000 (aplica sobre MinIncome del bracket)
        //   Nota: IncomeTax usa MinIncome del bracket aplicable, no de "piso". Verificar ajuste.
        var grossPay = 2000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var calculationDate = new DateTime(2026, 6, 1);

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            companyId: 1,
            grossPay: grossPay,
            payFrequency: payFrequency,
            dependents: dependents,
            isSubjectToIncomeTax: true,
            isSubjectToEducationalInsurance: true,
            calculationDate: calculationDate
        );

        // Assert — validación de sanidad (con SE deducible, Art. 709 núm. 4)
        // annual = 2,000 * 13 = 26,000; SE = 26,000 * 1.25% = 325 → base 25,675
        // Bracket 2: MinIncome = 11,000.01, Rate = 15%, FixedAmount = 0
        // Excedente = 25,675 - 11,000.01 = 14,674.99
        // AnnualTax = 14,674.99 * 0.15 ≈ 2,201.25 → PeriodTax = 2,201.25 / 12 ≈ 183.44
        result.TaxableIncome.Should().Be(26000m);
        result.DependentDeduction.Should().Be(0m);
        result.SeDeduction.Should().Be(325m);
        result.NetTaxableIncome.Should().Be(25675m);
        result.TaxAmount.Should().BeApproximately(183.44m, 0.02m);
    }

    [Fact]
    public async Task IntegratedIsrCalculation__SalarioExento_ReturnsZero()
    {
        var service = new IncomeTaxCalculationServicePortable(_provider);

        // Salario 800 mensual → 800 * 13 = 10,400 anual; SE = 130 → base 10,270 → exento
        var result = await service.CalculateIncomeTaxAsync(
            companyId: 1,
            grossPay: 800m,
            payFrequency: "Mensual",
            dependents: 0,
            isSubjectToIncomeTax: true,
            isSubjectToEducationalInsurance: true,
            calculationDate: new DateTime(2026, 6, 1)
        );

        result.TaxAmount.Should().Be(0m);
    }
}
