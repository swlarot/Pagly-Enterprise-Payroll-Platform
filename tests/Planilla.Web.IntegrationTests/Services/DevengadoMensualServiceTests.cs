// ====================================================================
// Tests de DevengadoMensualService
//
// La regla que importa: las planillas de Pagly mandan sobre lo importado,
// y un mes sin nada se devuelve igual, marcado como SinDatos, para que
// quien calcule sepa qué le falta.
// ====================================================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Xunit;

namespace Vorluno.Planilla.Web.IntegrationTests.Services;

public class DevengadoMensualServiceTests
{
    private const int TenantId = 1;
    private const int EmpleadoId = 10;

    private static (ApplicationDbContext db, DevengadoMensualService servicio) Nuevo()
    {
        var tenant = new TenantFijo();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"devengado-{Guid.NewGuid()}").Options,
            currentUserService: null, tenantContext: tenant);
        return (db, new DevengadoMensualService(db, tenant));
    }

    private static void SembrarPlanilla(ApplicationDbContext db, int id, DateTime inicio, DateTime fin,
        decimal bruto, decimal extras = 0m, decimal comision = 0m, decimal vacaciones = 0m,
        PayrollStatus estado = PayrollStatus.Approved)
    {
        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = id, TenantId = TenantId, PayrollNumber = $"P-{id}",
            PeriodStartDate = inicio, PeriodEndDate = fin, PayDate = fin,
            PayPeriodType = PayPeriodType.Quincenal, Status = estado
        });
        db.PayrollDetails.Add(new PayrollDetail
        {
            Id = id * 100, TenantId = TenantId, PayrollHeaderId = id, EmpleadoId = EmpleadoId,
            GrossPay = bruto, OvertimePay = extras, Commissions = comision, MontoVacaciones = vacaciones
        });
    }

    [Fact]
    public async Task MesConDosQuincenas__SumaLasDosYSeparaLosConceptos()
    {
        var (db, servicio) = Nuevo();
        SembrarPlanilla(db, 1, new(2026, 3, 1), new(2026, 3, 15), bruto: 600m, extras: 50m, comision: 30m);
        SembrarPlanilla(db, 2, new(2026, 3, 16), new(2026, 3, 31), bruto: 500m, vacaciones: 100m);
        await db.SaveChangesAsync();

        var meses = await servicio.ObtenerMesesAsync(EmpleadoId, new(2026, 3, 1), new(2026, 3, 31));

        meses.Should().HaveCount(1);
        var marzo = meses[0];
        marzo.Origen.Should().Be(OrigenDevengado.Planilla);
        marzo.Total.Should().Be(1_100m, "la suma es siempre el bruto real");
        marzo.Extras.Should().Be(50m);
        marzo.Comision.Should().Be(30m);
        marzo.Vacaciones.Should().Be(100m);
        marzo.Salario.Should().Be(920m);
    }

    [Fact]
    public async Task ElMesEsElDelPeriodoTrabajado__NoElDePago()
    {
        var (db, servicio) = Nuevo();
        // Quincena 16-31 de enero, pagada el 2 de febrero.
        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = 1, TenantId = TenantId, PayrollNumber = "P-1",
            PeriodStartDate = new(2026, 1, 16), PeriodEndDate = new(2026, 1, 31), PayDate = new(2026, 2, 2),
            PayPeriodType = PayPeriodType.Quincenal, Status = PayrollStatus.Paid
        });
        db.PayrollDetails.Add(new PayrollDetail
        {
            Id = 100, TenantId = TenantId, PayrollHeaderId = 1, EmpleadoId = EmpleadoId, GrossPay = 500m
        });
        await db.SaveChangesAsync();

        var meses = await servicio.ObtenerMesesAsync(EmpleadoId, new(2026, 1, 1), new(2026, 2, 28));

        meses[0].Should().Match<MesDevengado>(m => m.Mes == 1 && m.Total == 500m);
        meses[1].Should().Match<MesDevengado>(m => m.Mes == 2 && m.Origen == OrigenDevengado.SinDatos);
    }

    [Fact]
    public async Task PlanillaMandaSobreImportado__YLoImportadoLlenaLosMesesQueFaltan()
    {
        var (db, servicio) = Nuevo();
        SembrarPlanilla(db, 1, new(2026, 2, 1), new(2026, 2, 15), bruto: 700m);
        db.DevengadosMensuales.AddRange(
            new DevengadoMensual { TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = 2026, Mes = 1, Salario = 650m, Origen = OrigenDevengado.Importado },
            new DevengadoMensual { TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = 2026, Mes = 2, Salario = 999m, Origen = OrigenDevengado.Importado });
        await db.SaveChangesAsync();

        var meses = await servicio.ObtenerMesesAsync(EmpleadoId, new(2026, 1, 1), new(2026, 3, 31));

        meses.Should().HaveCount(3);
        meses[0].Should().Match<MesDevengado>(m => m.Origen == OrigenDevengado.Importado && m.Total == 650m);
        meses[1].Should().Match<MesDevengado>(m => m.Origen == OrigenDevengado.Planilla && m.Total == 700m,
            "febrero tiene planilla: lo importado se ignora");
        meses[2].Should().Match<MesDevengado>(m => m.Origen == OrigenDevengado.SinDatos && m.Total == 0m);
    }

    [Fact]
    public async Task PlanillaAnulada__NoCuentaComoPlanilla()
    {
        var (db, servicio) = Nuevo();
        SembrarPlanilla(db, 1, new(2026, 2, 1), new(2026, 2, 15), bruto: 700m, estado: PayrollStatus.Cancelled);
        db.DevengadosMensuales.Add(new DevengadoMensual
            { TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = 2026, Mes = 2, Salario = 650m, Origen = OrigenDevengado.Importado });
        await db.SaveChangesAsync();

        var meses = await servicio.ObtenerMesesAsync(EmpleadoId, new(2026, 2, 1), new(2026, 2, 28));

        meses[0].Origen.Should().Be(OrigenDevengado.Importado);
        meses[0].Total.Should().Be(650m);
    }

    [Fact]
    public async Task UltimosMeses__TerminaEnElMesDeLaFechaYCuentaHaciaAtras()
    {
        var (db, servicio) = Nuevo();

        // Los 60 meses anteriores al 30/09/2026 van de octubre 2021 a septiembre 2026,
        // como en la hoja "Salarios Acumulados" del contador.
        var meses = await servicio.UltimosMesesAsync(EmpleadoId, new(2026, 9, 30), 60);

        meses.Should().HaveCount(60);
        meses.First().Should().Match<MesDevengado>(m => m.Anio == 2021 && m.Mes == 10);
        meses.Last().Should().Match<MesDevengado>(m => m.Anio == 2026 && m.Mes == 9);
    }

    [Fact]
    public async Task GuardarMes__CreaYLuegoActualizaSinDuplicar()
    {
        var (db, servicio) = Nuevo();

        await servicio.GuardarMesAsync(EmpleadoId, 2025, 12, 575.43m, 0m, 0m, 0m, OrigenDevengado.Importado);
        await servicio.GuardarMesAsync(EmpleadoId, 2025, 12, 600m, 0m, 0m, 0m, OrigenDevengado.Manual, "corregido");

        var guardados = await db.DevengadosMensuales.ToListAsync();
        guardados.Should().HaveCount(1);
        guardados[0].Salario.Should().Be(600m);
        guardados[0].Origen.Should().Be(OrigenDevengado.Manual);
        guardados[0].Nota.Should().Be("corregido");
    }

    [Fact]
    public async Task GuardarMes__RechazaUnMesQueYaTienePlanilla()
    {
        var (db, servicio) = Nuevo();
        SembrarPlanilla(db, 1, new(2026, 2, 1), new(2026, 2, 15), bruto: 700m);
        await db.SaveChangesAsync();

        var acto = () => servicio.GuardarMesAsync(EmpleadoId, 2026, 2, 999m, 0m, 0m, 0m, OrigenDevengado.Manual);

        await acto.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya tiene planilla*");
    }

    [Fact]
    public async Task GuardarMes__RechazaNegativosYOrigenesDerivados()
    {
        var (_, servicio) = Nuevo();

        await FluentActions.Awaiting(() => servicio.GuardarMesAsync(EmpleadoId, 2026, 1, -1m, 0m, 0m, 0m, OrigenDevengado.Importado))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => servicio.GuardarMesAsync(EmpleadoId, 2026, 1, 100m, 0m, 0m, 0m, OrigenDevengado.Planilla))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => servicio.GuardarMesAsync(EmpleadoId, 2026, 13, 100m, 0m, 0m, 0m, OrigenDevengado.Manual))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private class TenantFijo : ITenantContext
    {
        public int TenantId => 0;   // 0 desactiva el filtro global; las entidades se siembran con TenantId 1
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
