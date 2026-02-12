using FluentAssertions;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Tests;

/// <summary>
/// Unit tests para PayPeriodType, CalculateSuggestedHoursPerPeriod,
/// RecalculateHourlyRate y PayrollConstants.GetPeriodsPerYear.
/// </summary>
public class PayPeriodAndHoursTests
{
    // ====================================================================
    // CalculateSuggestedHoursPerPeriod
    // ====================================================================

    [Theory]
    [InlineData(PayPeriodType.Semanal, 48, 48)]
    [InlineData(PayPeriodType.Bisemanal, 48, 96)]
    [InlineData(PayPeriodType.Quincenal, 48, 104)]  // Round(48 * 52/24) = 104
    [InlineData(PayPeriodType.Mensual, 48, 208)]     // Round(48 * 52/12) = 208
    public void CalculateSuggestedHoursPerPeriod_StandardWeek_ReturnsCorrectHours(
        PayPeriodType periodType, int hoursPerWeek, decimal expected)
    {
        var result = Empleado.CalculateSuggestedHoursPerPeriod(hoursPerWeek, periodType);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PayPeriodType.Semanal, 40, 40)]
    [InlineData(PayPeriodType.Bisemanal, 40, 80)]
    [InlineData(PayPeriodType.Quincenal, 40, 87)]   // Round(40 * 52/24) = 87
    [InlineData(PayPeriodType.Mensual, 40, 173)]     // Round(40 * 52/12) = 173
    public void CalculateSuggestedHoursPerPeriod_40HourWeek_ReturnsCorrectHours(
        PayPeriodType periodType, int hoursPerWeek, decimal expected)
    {
        var result = Empleado.CalculateSuggestedHoursPerPeriod(hoursPerWeek, periodType);
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateSuggestedHoursPerPeriod_ZeroHours_ReturnsZero()
    {
        var result = Empleado.CalculateSuggestedHoursPerPeriod(0, PayPeriodType.Quincenal);
        result.Should().Be(0);
    }

    // ====================================================================
    // RecalculateHourlyRate
    // ====================================================================

    [Fact]
    public void RecalculateHourlyRate_StandardValues_ReturnsCorrectRate()
    {
        var empleado = new Empleado
        {
            SalarioBase = 1500m,
            HoursPerPeriod = 104m
        };

        empleado.RecalculateHourlyRate();

        empleado.HourlyRate.Should().Be(Math.Round(1500m / 104m, 4));
    }

    [Fact]
    public void RecalculateHourlyRate_ZeroHours_ReturnsZero()
    {
        var empleado = new Empleado
        {
            SalarioBase = 1500m,
            HoursPerPeriod = 0m
        };

        empleado.RecalculateHourlyRate();

        empleado.HourlyRate.Should().Be(0m);
    }

    [Fact]
    public void RecalculateHourlyRate_ZeroSalary_ReturnsZero()
    {
        var empleado = new Empleado
        {
            SalarioBase = 0m,
            HoursPerPeriod = 104m
        };

        empleado.RecalculateHourlyRate();

        empleado.HourlyRate.Should().Be(0m);
    }

    [Fact]
    public void RecalculateHourlyRate_HighSalary_ReturnsCorrectRate()
    {
        var empleado = new Empleado
        {
            SalarioBase = 10000m,
            HoursPerPeriod = 208m  // Mensual
        };

        empleado.RecalculateHourlyRate();

        empleado.HourlyRate.Should().Be(Math.Round(10000m / 208m, 4));
    }

    // ====================================================================
    // SyncPayFrequencyFromType
    // ====================================================================

    [Theory]
    [InlineData(PayPeriodType.Semanal, "Semanal")]
    [InlineData(PayPeriodType.Bisemanal, "Bisemanal")]
    [InlineData(PayPeriodType.Quincenal, "Quincenal")]
    [InlineData(PayPeriodType.Mensual, "Mensual")]
    public void SyncPayFrequencyFromType_AllTypes_SyncsCorrectly(PayPeriodType type, string expected)
    {
        var empleado = new Empleado { PayPeriodType = type };

        empleado.SyncPayFrequencyFromType();

        empleado.PayFrequency.Should().Be(expected);
    }

    // ====================================================================
    // PayrollConstants.GetPeriodsPerYear (enum overload)
    // ====================================================================

    [Theory]
    [InlineData(PayPeriodType.Semanal, 52)]
    [InlineData(PayPeriodType.Bisemanal, 26)]
    [InlineData(PayPeriodType.Quincenal, 24)]
    [InlineData(PayPeriodType.Mensual, 12)]
    public void GetPeriodsPerYear_Enum_ReturnsCorrectPeriods(PayPeriodType type, int expected)
    {
        PayrollConstants.GetPeriodsPerYear(type).Should().Be(expected);
    }

    [Fact]
    public void GetPeriodsPerYear_InvalidEnum_ThrowsArgumentException()
    {
        var act = () => PayrollConstants.GetPeriodsPerYear((PayPeriodType)99);
        act.Should().Throw<ArgumentException>();
    }

    // ====================================================================
    // PayrollConstants.GetPeriodsPerYear (string - con Bisemanal)
    // ====================================================================

    [Fact]
    public void GetPeriodsPerYear_Bisemanal_Returns26()
    {
        PayrollConstants.GetPeriodsPerYear("Bisemanal").Should().Be(26);
    }

    [Fact]
    public void GetPeriodsPerYear_AllStringFrequencies_ReturnCorrectValues()
    {
        PayrollConstants.GetPeriodsPerYear("Semanal").Should().Be(52);
        PayrollConstants.GetPeriodsPerYear("Bisemanal").Should().Be(26);
        PayrollConstants.GetPeriodsPerYear("Quincenal").Should().Be(24);
        PayrollConstants.GetPeriodsPerYear("Mensual").Should().Be(12);
    }

    // ====================================================================
    // Empleado defaults
    // ====================================================================

    [Fact]
    public void Empleado_DefaultValues_AreCorrect()
    {
        var empleado = new Empleado();

        empleado.PayPeriodType.Should().Be(PayPeriodType.Quincenal);
        empleado.HoursPerWeek.Should().Be(48);
        empleado.HoursPerPeriod.Should().Be(104m);
        empleado.HourlyRate.Should().Be(0m);
        empleado.PayFrequency.Should().Be("Quincenal");
    }

    // ====================================================================
    // PayrollEmployeeHours defaults
    // ====================================================================

    [Fact]
    public void PayrollEmployeeHours_DefaultValues_AreZero()
    {
        var hours = new PayrollEmployeeHours();

        hours.SundayHours.Should().Be(0);
        hours.HolidayHours.Should().Be(0);
        hours.OvertimeDayHours.Should().Be(0);
        hours.OvertimeNightHours.Should().Be(0);
        hours.AbsenceHours.Should().Be(0);
        hours.DisabilityHours.Should().Be(0);
    }

    // ====================================================================
    // Integración: flujo completo de configuración de Pay Info
    // ====================================================================

    [Fact]
    public void FullPayInfoFlow_ConfigureEmployee_AllFieldsConsistent()
    {
        var empleado = new Empleado
        {
            SalarioBase = 2400m,
            PayPeriodType = PayPeriodType.Mensual,
            HoursPerWeek = 48
        };

        // Auto-calcular HoursPerPeriod
        empleado.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(
            empleado.HoursPerWeek, empleado.PayPeriodType);

        // Recalcular HourlyRate
        empleado.RecalculateHourlyRate();

        // Sincronizar PayFrequency legacy
        empleado.SyncPayFrequencyFromType();

        // Verificar todo
        empleado.HoursPerPeriod.Should().Be(208m);
        empleado.HourlyRate.Should().Be(Math.Round(2400m / 208m, 4));
        empleado.PayFrequency.Should().Be("Mensual");
    }
}
