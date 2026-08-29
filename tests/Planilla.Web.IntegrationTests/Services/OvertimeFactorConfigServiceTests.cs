// ====================================================================
// Tests de OvertimeFactorConfigService — factores de horas extra
// configurables por tenant, con fallback al Código de Trabajo.
//
// Incluye la verificación contra una planilla real de cliente
// (quincena 18-01-26) para fijar el comportamiento del recargo dominical.
// ====================================================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Xunit;

namespace Vorluno.Planilla.Web.IntegrationTests.Services;

public class OvertimeFactorConfigServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly OvertimeFactorConfigService _service;
    private const int TenantId = 7;

    public OvertimeFactorConfigServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"overtime-factors-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options, currentUserService: null, tenantContext: new FakeTenantContext(TenantId));
        _service = new OvertimeFactorConfigService(_db, new FakeTenantContext(TenantId));
    }

    public void Dispose() => _db.Dispose();

    // ================== Fallback legal ==================

    [Fact]
    public async Task SinOverrides__DevuelveFactorLegal()
    {
        // Art. 48: jornada ordinaria dominical = 1.50
        (await _service.GetFactorAsync(TipoHoraExtra.DomingoFeriado)).Should().Be(1.50m);
        // Art. 33.1: hora extra diurna = 1.25
        (await _service.GetFactorAsync(TipoHoraExtra.Diurna)).Should().Be(1.25m);
        // Art. 36.4: recargo por exceso = 1.75
        (await _service.GetFactorExcesoAsync()).Should().Be(1.75m);
    }

    [Fact]
    public async Task ConOverride__DevuelveElFactorDelTenant()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.DomingoFeriado, 2.00m) },
                FactorExceso: null),
            userId: "user-1");

        (await _service.GetFactorAsync(TipoHoraExtra.DomingoFeriado)).Should().Be(2.00m);
        // Los tipos no tocados siguen en su valor legal
        (await _service.GetFactorAsync(TipoHoraExtra.Diurna)).Should().Be(1.25m);
    }

    [Fact]
    public async Task GuardarFactorNull__EliminaElOverrideYVuelveALoLegal()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.Diurna, 1.90m) }, null),
            userId: null);
        (await _service.GetFactorAsync(TipoHoraExtra.Diurna)).Should().Be(1.90m);

        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.Diurna, null) }, null),
            userId: null);

        (await _service.GetFactorAsync(TipoHoraExtra.Diurna)).Should().Be(1.25m);
        _db.OvertimeFactorConfigurations.Should().BeEmpty();
    }

    [Fact]
    public async Task GuardarValorIgualAlLegal__NoPersisteOverrideRedundante()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.Nocturna, 1.50m) }, null),
            userId: null);

        _db.OvertimeFactorConfigurations.Should().BeEmpty();
    }

    [Fact]
    public async Task FactorExcesoPersonalizado__SeAplicaYSeRevierte()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(Array.Empty<UpdateOvertimeFactorItem>(), FactorExceso: 2.00m),
            userId: null);
        (await _service.GetFactorExcesoAsync()).Should().Be(2.00m);

        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(Array.Empty<UpdateOvertimeFactorItem>(), FactorExceso: null),
            userId: null);
        (await _service.GetFactorExcesoAsync()).Should().Be(1.75m);
    }

    [Fact]
    public async Task ResetToLegal__BorraTodosLosOverrides()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[]
                {
                    new UpdateOvertimeFactorItem(TipoHoraExtra.Diurna, 1.90m),
                    new UpdateOvertimeFactorItem(TipoHoraExtra.DomingoFeriado, 2.10m)
                },
                FactorExceso: 2.00m),
            userId: null);

        await _service.ResetToLegalAsync();

        _db.OvertimeFactorConfigurations.Should().BeEmpty();
        (await _service.GetFactorAsync(TipoHoraExtra.Diurna)).Should().Be(1.25m);
        (await _service.GetFactorExcesoAsync()).Should().Be(1.75m);
    }

    [Fact]
    public async Task GetConfig__MarcaCualesSonPersonalizadosYExponeElValorLegal()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.DomingoFeriado, 2.00m) }, null),
            userId: null);

        var config = await _service.GetConfigAsync();

        var domingo = config.Factores.Single(f => f.Tipo == TipoHoraExtra.DomingoFeriado);
        domingo.EsPersonalizado.Should().BeTrue();
        domingo.FactorVigente.Should().Be(2.00m);
        domingo.FactorLegal.Should().Be(1.50m);   // la referencia legal sigue visible
        domingo.BaseLegal.Should().Be("Art. 48");

        config.Factores.Single(f => f.Tipo == TipoHoraExtra.Diurna).EsPersonalizado.Should().BeFalse();
    }

    [Fact]
    public async Task OverrideDeOtroTenant__NoAfectaAlTenantActual()
    {
        _db.OvertimeFactorConfigurations.Add(new OvertimeFactorConfiguration
        {
            TenantId = 999,
            Tipo = TipoHoraExtra.DomingoFeriado,
            Factor = 3.00m,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        (await _service.GetFactorAsync(TipoHoraExtra.DomingoFeriado)).Should().Be(1.50m);
    }

    // ================== Planilla real del cliente ==================

    /// <summary>
    /// Quincena 18-01-26. Salario mensual B/.713.44, jornada de 48h semanales.
    /// Tarifa horaria = 713.44 / (48 x 52/12) = 3.43
    /// Domingo (Art. 48) = 3.43 x 1.50 = 5.145 -> 8h = 41.16
    /// </summary>
    [Fact]
    public async Task PlanillaReal__DomingoDe8Horas__Paga41Con16()
    {
        var hourlyRate = Empleado.ComputeHourlyRateFromMonthly(713.44m, hoursPerWeek: 48);
        hourlyRate.Should().Be(3.43m);

        var factorDominical = await _service.GetFactorAsync(TipoHoraExtra.DomingoFeriado);
        var tarifaDominical = hourlyRate * factorDominical;
        tarifaDominical.Should().Be(5.145m);

        (tarifaDominical * 8m).Should().Be(41.16m);
    }

    /// <summary>
    /// Si el tenant decide pagar el domingo al doble en vez del 1.50 legal,
    /// el mismo domingo pasa de 41.16 a 54.88.
    /// </summary>
    [Fact]
    public async Task PlanillaReal__ConFactorDominicalPersonalizado__CambiaElPago()
    {
        await _service.UpdateConfigAsync(
            new UpdateOvertimeFactorsRequest(
                new[] { new UpdateOvertimeFactorItem(TipoHoraExtra.DomingoFeriado, 2.00m) }, null),
            userId: null);

        var hourlyRate = Empleado.ComputeHourlyRateFromMonthly(713.44m, hoursPerWeek: 48);
        var factor = await _service.GetFactorAsync(TipoHoraExtra.DomingoFeriado);

        (hourlyRate * factor * 8m).Should().Be(54.88m);
    }

    // ================== Helper ==================

    private class FakeTenantContext : ITenantContext
    {
        public FakeTenantContext(int tenantId) => TenantId = tenantId;
        public int TenantId { get; }
        public TenantRole TenantRole => TenantRole.Owner;
        public string? UserId => "test-user";
        public bool IsSystemAdmin => false;
        public bool HasTenant => true;
        public Task SetTenantAsync(int tenantId) => Task.CompletedTask;
        public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult<Tenant?>(null);
        public bool HasRole(TenantRole role) => true;
        public bool IsAdminOrOwner() => true;
        public void Clear() { }
    }
}
