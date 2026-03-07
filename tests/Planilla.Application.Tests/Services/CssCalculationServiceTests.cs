// ====================================================================
// Planilla - CssCalculationServiceTests
// Source: Core360 Stage 5, Sección 2.2
// Creado: 2025-12-26
// Descripción: Tests unitarios del servicio de cálculo CSS
// Valida topes escalonados, tasas, riesgo profesional según Ley 462
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Application.Tests.Helpers;

namespace Vorluno.Planilla.Application.Tests.Services;

/// <summary>
/// Tests unitarios del servicio de cálculo CSS (Caja de Seguro Social).
/// Valida topes escalonados según Ley 462 de Panamá.
/// </summary>
public class CssCalculationServiceTests
{
    private const int DefaultCompanyId = 1;
    private readonly DateTime _calculationDate = new(2025, 1, 15);

    [Fact]
    public async Task CalculateEmployeeCss__TopeEstandar__ReturnsCorrectAmount()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 1800m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = true;

        // Act
        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(1500m);
        result.MaxContributionBase.Should().Be(1500m);
        result.TipoTope.Should().Be("Estándar");
        result.Rate.Should().Be(9.75m);
        result.Amount.Should().Be(146.25m); // 1500 * 9.75% = 146.25
    }

    [Fact]
    public async Task CalculateEmployeeCss__TopeIntermedio__ReturnsCorrectAmount()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 2500m;
        var yearsCotized = 27; // ≥ 25 años
        var avgSalary = 2100m; // ≥ B/. 2,000
        var isSubject = true;

        // Act
        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(2000m);
        result.MaxContributionBase.Should().Be(2000m);
        result.TipoTope.Should().Be("Intermedio");
        result.Rate.Should().Be(9.75m);
        result.Amount.Should().Be(195.00m); // 2000 * 9.75% = 195.00
    }

    [Fact]
    public async Task CalculateEmployeeCss__TopeAlto__ReturnsCorrectAmount()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 3000m;
        var yearsCotized = 32; // ≥ 30 años
        var avgSalary = 2600m; // ≥ B/. 2,500
        var isSubject = true;

        // Act
        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(2500m);
        result.MaxContributionBase.Should().Be(2500m);
        result.TipoTope.Should().Be("Alto");
        result.Rate.Should().Be(9.75m);
        result.Amount.Should().Be(243.75m); // 2500 * 9.75% = 243.75
    }

    [Fact]
    public async Task CalculateEmployerCss__TasaBase__ReturnsCorrectAmount()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 1500m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = true;

        // Act
        var result = await service.CalculateEmployerCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(1500m);
        result.Rate.Should().Be(13.25m); // Tasa base 2025
        result.Amount.Should().Be(198.75m); // 1500 * 13.25% = 198.75
    }

    [Fact]
    public async Task CalculateEmployerCss__SalarioSobreTope__AplicaTopeAlContributionBase()
    {
        // grossPay = 2000 > tope estándar (1500) → contribución debe basarse en 1500
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var result = await service.CalculateEmployerCssAsync(
            DefaultCompanyId,
            grossPay: 2000m,
            payFrequency: "Mensual",
            yearsCotized: 10,
            averageSalaryLast10Years: 1200m,
            isSubjectToCss: true,
            calculationDate: _calculationDate
        );

        result.ContributionBase.Should().Be(1500m);  // capped
        result.Rate.Should().Be(13.25m);
        result.Amount.Should().Be(198.75m); // 1500 * 13.25% = 198.75 (no 2000 * 13.25% = 265)
    }

    // ====================================================================
    // CSS por frecuencia de pago no-mensual (DEV-75)
    // Verifica que periodCap se prorratee y se aplique al contributionBase
    // ====================================================================

    [Theory]
    [InlineData("Quincenal", 900.00, 750.00,  73.13)]  // cap=1500/2=750;  750*9.75%=73.125→73.13
    [InlineData("Bisemanal", 800.00, 692.31,  67.50)]  // cap=1500*12/26=692.31; 692.31*9.75%=67.500225→67.50
    [InlineData("Semanal",   400.00, 346.15,  33.75)]  // cap=1500*12/52=346.15; *9.75%=33.75
    public async Task CalculateEmployeeCss__FrecuenciaNivel_ProrrateoTope_ReturnsCorrectAmount(
        string payFrequency, decimal grossPay, decimal expectedCap, decimal expectedAmount)
    {
        // En todas las frecuencias: grossPay > periodCap → contributionBase = periodCap
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            payFrequency,
            yearsCotized: 10,
            averageSalaryLast10Years: 1200m,
            isSubjectToCss: true,
            calculationDate: _calculationDate
        );

        result.ContributionBase.Should().Be(expectedCap);
        result.MaxContributionBase.Should().Be(expectedCap);
        result.TipoTope.Should().Be("Estándar");
        result.Rate.Should().Be(9.75m);
        result.Amount.Should().Be(expectedAmount);
    }

    [Theory]
    [InlineData(0.56, 0.56, 8.40)]  // Riesgo bajo
    [InlineData(2.50, 2.50, 37.50)] // Riesgo medio
    [InlineData(5.39, 5.39, 80.85)] // Riesgo alto
    public async Task CalculateRiskContribution__DifferentLevels__ReturnsCorrectAmount(
        decimal cssRiskPercentage,
        decimal expectedRate,
        decimal expectedAmount)
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 1500m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = true;

        // Act
        var (amount, rate) = await service.CalculateRiskContributionAsync(
            DefaultCompanyId,
            grossPay,
            cssRiskPercentage,
            isSubject,
            _calculationDate
        );

        // Assert
        rate.Should().Be(expectedRate);
        amount.Should().Be(expectedAmount); // 1500 * rate%
    }

    [Fact]
    public async Task CalculateEmployeeCss__NotSubject__ReturnsZero()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 1500m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = false; // NO sujeto a CSS

        // Act
        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(0);
        result.MaxContributionBase.Should().Be(0);
        result.TipoTope.Should().Be("N/A");
        result.Rate.Should().Be(0);
        result.Amount.Should().Be(0);
    }

    [Fact]
    public async Task CalculateEmployeeCss__NoConfig__ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = MockPayrollConfigProvider.WithMissingConfig();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 1500m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = true;

        // Act
        Func<Task> act = async () => await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se encontró configuración de CSS*");
    }

    [Fact]
    public async Task CalculateRiskContribution__NegativeRate__ThrowsInvalidOperationException()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        // Act
        Func<Task> act = async () => await service.CalculateRiskContributionAsync(
            DefaultCompanyId,
            grossPay: 1500m,
            cssRiskPercentage: -1m, // tasa inválida
            isSubjectToCss: true,
            calculationDate: _calculationDate
        );

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CssRiskPercentage no puede ser negativo*");
    }

    [Fact]
    public async Task CalculateEmployeeCss__SalarioCero__ReturnsZero()
    {
        // Arrange
        var mockProvider = new MockPayrollConfigProvider();
        var service = new CssCalculationServicePortable(mockProvider);

        var grossPay = 0m;
        var yearsCotized = 10;
        var avgSalary = 1200m;
        var isSubject = true;

        // Act
        var result = await service.CalculateEmployeeCssAsync(
            DefaultCompanyId,
            grossPay,
            "Mensual",
            yearsCotized,
            avgSalary,
            isSubject,
            _calculationDate
        );

        // Assert
        result.ContributionBase.Should().Be(0);
        result.Amount.Should().Be(0);
    }
}
