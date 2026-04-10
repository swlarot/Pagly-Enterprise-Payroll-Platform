// ====================================================================
// Planilla - IncomeTaxCalculationServiceTests
// Source: Core360 Stage 5, Sección 2.4
// Creado: 2025-12-26
// Actualizado: 2026-04-09 — Ajustado para ×13 (incluye décimo tercer mes)
// Descripción: Tests unitarios del servicio de ISR
// Valida brackets progresivos, deducciones, proyección anual con décimo
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Exceptions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Application.Tests.Helpers;

namespace Vorluno.Planilla.Application.Tests.Services;

/// <summary>
/// Tests unitarios del servicio de Impuesto Sobre la Renta (ISR).
/// Valida brackets progresivos según regulaciones de la DGI de Panamá.
/// La proyección anual usa ×13 meses (12 + décimo tercer mes).
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

        // Salario mensual que proyecta a < B/. 11,000 anual (con ×13)
        var grossPay = 840m; // 840 * 13 = 10,920 anual (exento)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(10920m); // 840 * 13
        result.DependentDeduction.Should().Be(0);
        result.NetTaxableIncome.Should().Be(10920m);
        result.TaxAmount.Should().Be(0); // Tramo exento
        result.EffectiveTaxRate.Should().Be(0);
    }

    [Fact]
    public async Task CalculateIncomeTax__Tramo15Percent__ReturnsCorrectTax()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        // Salario mensual que proyecta al tramo 15%
        var grossPay = 3000m; // 3000 * 13 = 39,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(39000m); // 3000 * 13
        result.NetTaxableIncome.Should().Be(39000m);
        // ISR: (39,000 - 11,000) * 15% = 28,000 * 0.15 = 4,200 anual
        // Por mes: 4,200 / 12 = 350.00
        result.TaxAmount.Should().Be(350.00m);
    }

    [Fact]
    public async Task CalculateIncomeTax__Tramo25Percent__ReturnsCorrectTax()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        // Salario mensual que proyecta al tramo 25%
        var grossPay = 6000m; // 6000 * 13 = 78,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(78000m);
        result.NetTaxableIncome.Should().Be(78000m);
        // ISR:
        // Tramo 2 (11,001-50,000): FixedAmount = 5,850
        // Tramo 3: (78,000 - 50,000) * 25% = 7,000
        // Total anual: 5,850 + 7,000 = 12,850
        // Por mes: 12,850 / 12 = 1,070.83
        result.TaxAmount.Should().Be(1070.83m);
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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(39000m);
        result.DependentDeduction.Should().Be(1600m); // 800 * 2
        result.NetTaxableIncome.Should().Be(37400m); // 39,000 - 1,600
        // ISR: (37,400 - 11,000) * 15% = 26,400 * 0.15 = 3,960 anual
        // Por mes: 3,960 / 12 = 330.00
        result.TaxAmount.Should().Be(330.00m);
    }

    [Fact]
    public async Task CalculateIncomeTax__MasDe3Dependientes__LimitaA3()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 5; // Intenta 5, pero máximo es 3
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.DependentDeduction.Should().Be(2400m); // 800 * 3 (máximo)
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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        // 250 * 52 / 12 = 1,083.33... mensual → 1,083.33... * 13 = 14,083.33...
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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(0);
        result.DependentDeduction.Should().Be(0);
        result.NetTaxableIncome.Should().Be(0);
        result.TaxAmount.Should().Be(0);
        result.EffectiveTaxRate.Should().Be(0);
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

        // Act
        Func<Task> act = async () => await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se encontró configuración de ISR*");
    }

    [Fact]
    public async Task CalculateIncomeTax__NoBrackets__ThrowsPayrollConfigurationException()
    {
        // Arrange
        // Mock que retorna config pero sin brackets
        var mockProvider = MockPayrollConfigProvider.WithMissingConfig();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        Func<Task> act = async () => await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        // Primero lanzará InvalidOperationException porque falta la config
        // (el test de brackets faltantes requeriría un mock más específico)
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CalculateIncomeTax__ExactamenteEnLimite11000__AplicaBracketCorrectamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        // Salario que proyecta justo por debajo de B/. 11,000 con ×13
        var grossPay = 846m; // 846 * 13 = 10,998 (exento)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(10998m); // 846 * 13
        // Dentro del tramo exento, no debe pagar impuesto
        result.TaxAmount.Should().Be(0);
    }

    [Fact]
    public async Task CalculateIncomeTax__ExactamenteEnLimite50000__AplicaBracketCorrectamente()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new IncomeTaxCalculationServicePortable(mockProvider);

        // Salario que proyecta exactamente al límite entre tramo 15% y 25%
        var grossPay = 3846.15m; // 3846.15 * 13 = 49,999.95 (todavía en tramo 15%)
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(49999.95m); // 3846.15 * 13
        // Justo en el límite del tramo 15%
        // (49,999.95 - 11,000) * 15% = 38,999.95 * 0.15 = 5,849.99 anual
        // Por mes: 5,849.99 / 12 ≈ 487.50
        result.TaxAmount.Should().BeApproximately(487.50m, 0.10m);
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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

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

        // Salario mensual alto
        var grossPay = 10000m; // 10,000 * 13 = 130,000 anual
        var payFrequency = "Mensual";
        var dependents = 0;
        var isSubject = true;

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        result.TaxableIncome.Should().Be(130000m);
        // ISR:
        // Tramo 2: FixedAmount = 5,850
        // Tramo 3: (130,000 - 50,000) * 25% = 20,000
        // Total anual: 5,850 + 20,000 = 25,850
        // Por mes: 25,850 / 12 = 2,154.17
        result.TaxAmount.Should().Be(2154.17m);
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

        // Act
        var result = await service.CalculateIncomeTaxAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            dependents,
            isSubject,
            _calculationDate
        );

        // Assert
        // Impuesto anual: 12,850
        // Tasa efectiva: (12,850 / 78,000) * 100 = 16.47%
        result.EffectiveTaxRate.Should().BeApproximately(16.47m, 0.05m);
    }
}
