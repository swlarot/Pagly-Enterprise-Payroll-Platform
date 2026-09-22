// ====================================================================
// Tests de DecimoCalculationService
//
// Lo que importa: el devengado del cuatrimestre sale de la fuente única
// (planillas de Pagly y, si un mes no tiene planilla, lo importado o
// escrito a mano), los meses parciales de lo importado se prorratean,
// previsualizar no guarda nada, y al calcular lo escrito a mano queda
// como devengado manual del empleado.
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

public class DecimoCalculationServiceTests
{
    private const int TenantId = 1;
    private const int EmpleadoId = 10;

    // Cuatrimestre típico de la primera partida: 16/12 → 15/04.
    private static readonly DateTime Desde = new(2025, 12, 16);
    private static readonly DateTime Hasta = new(2026, 4, 15);
    private static readonly DateTime Pago = new(2026, 4, 15);

    private static (ApplicationDbContext db, DecimoCalculationService servicio) Nuevo()
    {
        var tenant = new TenantFijo();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"decimo-{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            currentUserService: null, tenantContext: tenant);
        var devengado = new DevengadoMensualService(db, tenant);
        return (db, new DecimoCalculationService(db, new ConfigSinIsr(), devengado));
    }

    private static void Empleado(ApplicationDbContext db, int id = EmpleadoId) => db.Empleados.Add(new Empleado
    {
        Id = id, TenantId = TenantId, Nombre = "Belisa", Apellido = "Belloso", NumeroIdentificacion = "8-809-1415",
        SalarioBase = 700m, EstaActivo = true, IsSubjectToEducationalInsurance = true, IsSubjectToIncomeTax = false,
    });

    private static void Planilla(ApplicationDbContext db, int id, DateTime inicio, DateTime fin, decimal bruto)
    {
        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = id, TenantId = TenantId, PayrollNumber = $"2026-{id:D3}",
            PeriodStartDate = inicio, PeriodEndDate = fin, PayDate = fin,
            PayPeriodType = PayPeriodType.Quincenal, Status = PayrollStatus.Approved,
        });
        db.PayrollDetails.Add(new PayrollDetail
        {
            Id = id * 100, TenantId = TenantId, PayrollHeaderId = id, EmpleadoId = EmpleadoId, GrossPay = bruto,
        });
    }

    [Fact]
    public async Task ElDevengadoSumaPlanillasYMesesImportados__ConLosParcialesProrrateados()
    {
        var (db, servicio) = Nuevo();
        Empleado(db);
        // Enero y febrero con planillas de Pagly (dos quincenas cada uno).
        Planilla(db, 1, new(2026, 1, 1), new(2026, 1, 15), 350m);
        Planilla(db, 2, new(2026, 1, 16), new(2026, 1, 31), 350m);
        Planilla(db, 3, new(2026, 2, 1), new(2026, 2, 15), 350m);
        // Diciembre y marzo vienen de la importación (meses completos).
        db.DevengadosMensuales.AddRange(
            new DevengadoMensual { TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = 2025, Mes = 12, Salario = 620m, Origen = OrigenDevengado.Importado },
            new DevengadoMensual { TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = 2026, Mes = 3, Salario = 700m, Origen = OrigenDevengado.Importado });
        await db.SaveChangesAsync();

        var p = await servicio.PrevisualizarAsync(Desde, Hasta, Pago, TenantId);
        var e = p.Empleados.Single();

        e.Meses.Should().HaveCount(5, "diciembre, enero, febrero, marzo y abril");
        var dic = e.Meses.Single(m => m.Anio == 2025 && m.Mes == 12);
        dic.Origen.Should().Be(OrigenDevengado.Importado);
        dic.Monto.Should().Be(319.98m, "del 16 al 31 son 16 de 31 días: 620 × 0.5161");
        e.Meses.Single(m => m.Mes == 1).Monto.Should().Be(700m, "las dos quincenas de enero");
        e.Meses.Single(m => m.Mes == 1).Planillas.Should().HaveCount(2);
        e.Meses.Single(m => m.Mes == 3).Monto.Should().Be(700m, "marzo cae completo dentro del período");
        e.Meses.Single(m => m.Mes == 4).Origen.Should().Be(OrigenDevengado.SinDatos);
        e.TieneMesesSinDatos.Should().BeTrue();

        e.TotalDevengado.Should().Be(319.98m + 700m + 350m + 700m);
        e.MontoDecimo.Should().Be(Math.Round(e.TotalDevengado / 12m, 2));
        e.CssEmpleado.Should().Be(Math.Round(e.MontoDecimo * 0.0725m, 2));
        e.SeEmpleado.Should().Be(Math.Round(e.MontoDecimo * 0.0125m, 2));
        e.NetoPago.Should().Be(e.MontoDecimo - e.CssEmpleado - e.SeEmpleado);
    }

    [Fact]
    public async Task PrevisualizarNoGuardaNada__YElMesEscritoAManoManda()
    {
        var (db, servicio) = Nuevo();
        Empleado(db);
        Planilla(db, 1, new(2026, 1, 1), new(2026, 1, 15), 350m);
        await db.SaveChangesAsync();

        var ajustes = new[] { new AjusteMesDecimo(EmpleadoId, 2026, 3, 700m) };
        var p = await servicio.PrevisualizarAsync(Desde, Hasta, Pago, TenantId, ajustes);
        var marzo = p.Empleados.Single().Meses.Single(m => m.Mes == 3);

        marzo.Monto.Should().Be(700m);
        marzo.Origen.Should().Be(OrigenDevengado.Manual);
        (await db.DevengadosMensuales.CountAsync()).Should().Be(0, "previsualizar no escribe");
        (await db.DetallesDecimo.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnMesDePlanillaNoSeDejaPisarAMano()
    {
        var (db, servicio) = Nuevo();
        Empleado(db);
        Planilla(db, 1, new(2026, 1, 1), new(2026, 1, 15), 350m);
        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = 1, TenantId = TenantId, Numero = "DEC-2026-01",
            PeriodoDesde = Desde, PeriodoHasta = Hasta, FechaPago = Pago, Estado = EstadoDecimo.Borrador,
        });
        await db.SaveChangesAsync();

        var acto = () => servicio.CalcularAsync(1, TenantId, new[] { new AjusteMesDecimo(EmpleadoId, 2026, 1, 9_999m) });

        await acto.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya tiene planilla en Pagly*");
    }

    [Fact]
    public async Task CalcularGuardaDetallesYDejaElMesManualComoDevengado()
    {
        var (db, servicio) = Nuevo();
        Empleado(db);
        Planilla(db, 1, new(2026, 1, 1), new(2026, 1, 15), 350m);
        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = 1, TenantId = TenantId, Numero = "DEC-2026-01",
            PeriodoDesde = Desde, PeriodoHasta = Hasta, FechaPago = Pago, Estado = EstadoDecimo.Borrador,
        });
        await db.SaveChangesAsync();

        var r = await servicio.CalcularAsync(1, TenantId, new[] { new AjusteMesDecimo(EmpleadoId, 2026, 3, 700m) });

        r.EmpleadosProcesados.Should().Be(1);
        var detalle = await db.DetallesDecimo.SingleAsync();
        detalle.TotalDevengado.Should().Be(1_050m, "350 de la quincena + 700 escritos a mano");
        detalle.MontoDecimo.Should().Be(87.50m);

        var planilla = await db.PlanillasDecimo.SingleAsync();
        planilla.Estado.Should().Be(EstadoDecimo.Calculada);
        planilla.TotalDecimo.Should().Be(87.50m);

        var manual = await db.DevengadosMensuales.SingleAsync();
        manual.Mes.Should().Be(3);
        manual.Salario.Should().Be(700m);
        manual.Origen.Should().Be(OrigenDevengado.Manual);
    }

    [Fact]
    public async Task UnaPartidaPagadaNoSeRecalcula()
    {
        var (db, servicio) = Nuevo();
        Empleado(db);
        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = 1, TenantId = TenantId, Numero = "DEC-2026-01",
            PeriodoDesde = Desde, PeriodoHasta = Hasta, FechaPago = Pago, Estado = EstadoDecimo.Pagada,
        });
        await db.SaveChangesAsync();

        var acto = () => servicio.CalcularAsync(1, TenantId);

        await acto.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reábrela primero*");
    }

    [Theory]
    [InlineData(2025, 12, "0.5161")]  // del 16 al 31: 16 de 31 días
    [InlineData(2026, 1, "1.0000")]   // mes entero dentro del período
    [InlineData(2026, 4, "0.5000")]   // del 1 al 15: 15 de 30 días
    public void LaFraccionDelMesSaleDeLosDiasDentroDelPeriodo(int anio, int mes, string esperada)
    {
        DecimoCalculationService.FraccionDelMesDentroDelPeriodo(anio, mes, Desde, Hasta)
            .Should().Be(decimal.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Sin tramos de ISR: el décimo de estos tests no lleva renta.</summary>
    private class ConfigSinIsr : IPayrollConfigProvider
    {
        public Task<PayrollTaxConfigDto?> GetTaxConfigAsync(int companyId, DateTime effectiveDate)
            => Task.FromResult<PayrollTaxConfigDto?>(null);
        public Task<List<TaxBracketDto>> GetTaxBracketsAsync(int companyId, int year)
            => Task.FromResult(new List<TaxBracketDto>());
    }

    private class TenantFijo : ITenantContext
    {
        public int TenantId => 1;
        public TenantRole TenantRole => TenantRole.Owner;
        public string? UserId => null;
        public bool IsSystemAdmin => false;
        public bool HasTenant => true;
        public Task SetTenantAsync(int tenantId) => Task.CompletedTask;
        public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult<Tenant?>(null);
        public bool HasRole(TenantRole role) => true;
        public bool IsAdminOrOwner() => true;
        public void Clear() { }
    }
}
