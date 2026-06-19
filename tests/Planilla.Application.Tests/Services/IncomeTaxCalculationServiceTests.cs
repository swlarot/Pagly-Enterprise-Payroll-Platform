// ====================================================================
// Planilla - IncomeTaxCalculationServiceTests
// Source: Core360 Stage 5, Sección 2.4
// Creado: 2025-12-26
// Actualizado: 2026-04-09 — Ajustado para ×13 (incluye décimo tercer mes)
// Actualizado: 2026-06-18 — ISR descuenta el Seguro Educativo (Art. 709 núm. 4
//   + Art. 704 Código Fiscal). Números validados contra el oráculo de Talento
//   (isr-calculator.spec.ts). SE empleado = 1.25% del ingreso anual proyectado.
// Descripción: Tests unitarios del servicio de ISR
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Application.Tests.Helpers;

namespace Vorluno.Planilla.Application.Tests.Services;

/// <summary>
/// Tests unitarios del servicio de Impuesto Sobre la Renta (ISR).
/// Valida brackets progresivos según regulaciones de la DGI de Panamá.
/// La proyección anual usa ×13 meses (12 + décimo tercer mes) y la base
/// imponible descuenta dependientes (Art. 709 núm. 3) y el Seguro Educativo
/// del empleado (Art. 709 núm. 4), nunca la CSS (no listada en el Art. 709).
/// </summary>
public class IncomeTaxCalculationServiceTests
{
    private const int DefaultCompanyId = 1;
    private readonly DateTime _calculationDate = new(2025, 1, 15);

    [Fact]
    public async Task CalculateIncomeTax__TramoExento__ReturnsZeroTax()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        // Salario mensual que proyecta a < B/. 11,000 anual (con ×13), incluso tras el SE
        var grossPay = 840m; // 840 * 13 = 10,920 anual; SE = 136.50 → base 10,783.50 (exento)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(10920m);          // 840 * 13
        result.DependentDeduction.Should().Be(0);
        result.SeDeduction.Should().Be(136.50m);           // 10,920 * 1.25%
        result.NetTaxableIncome.Should().Be(10783.50m);    // 10,920 - 136.50
        result.TaxAmount.Should().Be(0);                   // Tramo exento
        result.EffectiveTaxRate.Should().Be(0);
    }

    [Fact]
    public async Task CalculateIncomeTax__Tramo15Percent__ReturnsCorrectTax()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m; // 3000 * 13 = 39,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(39000m);          // 3000 * 13
        result.SeDeduction.Should().Be(487.50m);           // 39,000 * 1.25%
        result.NetTaxableIncome.Should().Be(38512.50m);    // 39,000 - 487.50
        // ISR: (38,512.50 - 11,000) * 15% = 4,126.875 ≈ 4,126.88 anual
        // Por mes: 4,126.88 / 12 = 343.91
        result.TaxAmount.Should().Be(343.91m);
    }

    [Fact]
    public async Task CalculateIncomeTax__Tramo25Percent__ReturnsCorrectTax()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 6000m; // 6000 * 13 = 78,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(78000m);
        result.SeDeduction.Should().Be(975.00m);           // 78,000 * 1.25%
        result.NetTaxableIncome.Should().Be(77025m);       // 78,000 - 975
        // ISR: 5,850 (fixed tramo 3) + (77,025 - 50,000) * 25% = 5,850 + 6,756.25 = 12,606.25 anual
        // Por mes: 12,606.25 / 12 = 1,050.52
        result.TaxAmount.Should().Be(1050.52m);
    }

    [Fact]
    public async Task CalculateIncomeTax__ConDependientes__AplicaDeduccion()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m; // 3000 * 13 = 39,000 anual
        var payFrequency = "Mensual";
        var dependents = 2; // 2 dependientes = B/. 1,600 deducción
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(39000m);
        result.DependentDeduction.Should().Be(1600m);      // 800 * 2
        result.SeDeduction.Should().Be(487.50m);           // 39,000 * 1.25%
        result.NetTaxableIncome.Should().Be(36912.50m);    // 39,000 - 1,600 - 487.50
        // ISR: (36,912.50 - 11,000) * 15% = 3,886.875 ≈ 3,886.88 anual
        // Por mes: 3,886.88 / 12 = 323.91
        result.TaxAmount.Should().Be(323.91m);
    }

    [Fact]
    public async Task CalculateIncomeTax__MasDe3Dependientes__LimitaA3()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 5; // Intenta 5, pero el mock limita a 3
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.DependentDeduction.Should().Be(2400m); // 800 * 3 (máximo del mock)
    }

    [Fact]
    public async Task CalculateIncomeTax__FrecuenciaMensual__ProyectaCon13Meses()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 1000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(13000m); // 1000 * 13
    }

    [Fact]
    public async Task CalculateIncomeTax__FrecuenciaQuincenal__ProyectaCon13Meses()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 500m; // quincenal
        var payFrequency = "Quincenal";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        // 500 * 24 / 12 = 1,000 mensual → 1,000 * 13 = 13,000 anual
        result.TaxableIncome.Should().Be(13000m);
    }

    [Fact]
    public async Task CalculateIncomeTax__FrecuenciaSemanal__ProyectaCon13Meses()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 250m; // semanal
        var payFrequency = "Semanal";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        // 250 * 52 / 12 = 1,083.33... mensual → * 13 = 14,083.33...
        result.TaxableIncome.Should().BeApproximately(14083.33m, 0.01m);
    }

    [Fact]
    public async Task CalculateIncomeTax__NotSubject__ReturnsZero()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = false; // NO sujeto a ISR
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(0);
        result.DependentDeduction.Should().Be(0);
        result.SeDeduction.Should().Be(0);
        result.NetTaxableIncome.Should().Be(0);
        result.TaxAmount.Should().Be(0);
        result.EffectiveTaxRate.Should().Be(0);
    }

    [Fact]
    public async Task CalculateIncomeTax__SinSeguroEducativo__NoDescuentaSe()
    {
        // Arrange — empleado sujeto a ISR pero NO al Seguro Educativo
        // (ej. servicios profesionales sin CSS). El Art. 709 núm. 4 no aplica.
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m; // 39,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = false;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert — sin SE deducible, la base es el bruto íntegro
        result.SeDeduction.Should().Be(0m);
        result.NetTaxableIncome.Should().Be(39000m);
        // ISR: (39,000 - 11,000) * 15% = 4,200 anual → / 12 = 350.00
        result.TaxAmount.Should().Be(350.00m);
    }

    [Fact]
    public async Task CalculateIncomeTax__NoConfig__ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = MockPayrollConfigProvider.WithMissingConfig();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        Func<Task> act = async () => await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se encontró configuración de ISR*");
    }

    [Fact]
    public async Task CalculateIncomeTax__NoBrackets__ThrowsException()
    {
        // Arrange — mock que retorna config null (sin brackets)
        var mockProvider = MockPayrollConfigProvider.WithMissingConfig();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        Func<Task> act = async () => await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CalculateIncomeTax__ExactamenteEnLimite11000__AplicaBracketCorrectamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 846m; // 846 * 13 = 10,998; SE = 137.475 → base 10,860.53 (exento)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(10998m); // 846 * 13
        result.TaxAmount.Should().Be(0);          // base tras SE < 11,000 → exento
    }

    [Fact]
    public async Task CalculateIncomeTax__IngresoTramo15ConSe__AplicaBracketCorrectamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3846.15m; // 3846.15 * 13 = 49,999.95 anual (tramo 15%)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(49999.95m); // 3846.15 * 13
        // SE = 49,999.95 * 1.25% = 624.999375 → base 49,374.95
        // ISR: (49,374.95 - 11,000) * 15% = 5,756.24 anual → / 12 = 479.69
        result.TaxAmount.Should().BeApproximately(479.69m, 0.05m);
    }

    [Fact]
    public async Task CalculateIncomeTax__SalarioCero__ReturnsZero()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 0m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(0);
        result.TaxAmount.Should().Be(0);
    }

    [Fact]
    public async Task CalculateIncomeTax__IngresoAlto__AplicaTramo25Correctamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 10000m; // 10,000 * 13 = 130,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        result.TaxableIncome.Should().Be(130000m);
        result.SeDeduction.Should().Be(1625.00m);          // 130,000 * 1.25%
        result.NetTaxableIncome.Should().Be(128375m);      // 130,000 - 1,625
        // ISR: 5,850 + (128,375 - 50,000) * 25% = 5,850 + 19,593.75 = 25,443.75 anual
        // Por mes: 25,443.75 / 12 = 2,120.31
        result.TaxAmount.Should().Be(2120.31m);
    }

    [Fact]
    public async Task CalculateIncomeTax__ValidarTasaEfectiva__CalculaCorrectamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 6000m; // 78,000 anual (con ×13)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;
        var subjectToSe = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId, grossPay, payFrequency, dependents, isSubject, subjectToSe, _calculationDate);

        // Assert
        // Impuesto anual: 12,606.25 (con SE deducido)
        // Tasa efectiva: (12,606.25 / 78,000) * 100 ≈ 16.16%
        result.EffectiveTaxRate.Should().BeApproximately(16.16m, 0.05m);
    }
}
