// ====================================================================
// Tests de HorasPlanillaService
//
// Al crear una planilla, las horas se preparan solas: una fila por empleado
// activo con sus regulares, más las horas extra y ausencias aprobadas del
// período. Los inactivos y lo no aprobado se quedan fuera; lo ya escrito se
// respeta según el modo.
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

public class HorasPlanillaServiceTests
{
    private const int TenantId = 1;

    private static (ApplicationDbContext db, HorasPlanillaService servicio) Nuevo()
    {
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"horas-{Guid.NewGuid()}").Options,
            currentUserService: null, tenantContext: new TenantFijo());
        return (db, new HorasPlanillaService(db));
    }

    private static Empleado Empleado(int id, decimal horas = 104m, bool activo = true) => new()
    {
        Id = id, TenantId = TenantId, Nombre = $"Emp{id}", Apellido = "Test",
        NumeroIdentificacion = $"8-{id}-{id}", SalarioBase = 1000m, HoursPerPeriod = horas,
        EstaActivo = activo, IsDeleted = false,
    };

    private static PayrollHeader Planilla(int id = 1) => new()
    {
        Id = id, TenantId = TenantId, PayrollNumber = "2026-001",
        PeriodStartDate = new(2026, 4, 1), PeriodEndDate = new(2026, 4, 15), PayDate = new(2026, 4, 15),
        PayPeriodType = PayPeriodType.Quincenal, Status = PayrollStatus.Draft,
    };

    [Fact]
    public async Task GenerarHorasPorDefecto__UnaFilaPorActivoConSusRegulares()
    {
        var (db, servicio) = Nuevo();
        var planilla = Planilla();
        db.PayrollHeaders.Add(planilla);
        db.Empleados.AddRange(Empleado(1, 104m), Empleado(2, 96m), Empleado(3, activo: false));
        await db.SaveChangesAsync();

        var n = await servicio.GenerarHorasPorDefectoAsync(planilla);
        await db.SaveChangesAsync();

        n.Should().Be(2);
        var filas = await db.PayrollEmployeeHours.Where(h => h.PayrollHeaderId == 1).ToListAsync();
        filas.Should().HaveCount(2);
        filas.Single(h => h.EmpleadoId == 1).RegularHours.Should().Be(104m);
        filas.Single(h => h.EmpleadoId == 2).RegularHours.Should().Be(96m);
    }

    [Fact]
    public async Task GenerarHorasPorDefecto__NoDuplicaNiPisaLoQueYaHay()
    {
        var (db, servicio) = Nuevo();
        var planilla = Planilla();
        db.PayrollHeaders.Add(planilla);
        db.Empleados.AddRange(Empleado(1), Empleado(2));
        db.PayrollEmployeeHours.Add(new PayrollEmployeeHours { PayrollHeaderId = 1, EmpleadoId = 1, TenantId = TenantId, RegularHours = 80m });
        await db.SaveChangesAsync();

        var n = await servicio.GenerarHorasPorDefectoAsync(planilla);
        await db.SaveChangesAsync();

        n.Should().Be(1);
        var filas = await db.PayrollEmployeeHours.Where(h => h.PayrollHeaderId == 1).ToListAsync();
        filas.Should().HaveCount(2);
        filas.Single(h => h.EmpleadoId == 1).RegularHours.Should().Be(80m, "lo escrito a mano se respeta");
    }

    [Fact]
    public async Task ImportarNovedades__TraeExtrasAprobadasYAusenciasDelPeriodo()
    {
        var (db, servicio) = Nuevo();
        var planilla = Planilla();
        db.PayrollHeaders.Add(planilla);
        db.Empleados.AddRange(Empleado(1), Empleado(2));
        db.HorasExtra.AddRange(
            new HoraExtra { Id = 1, TenantId = TenantId, EmpleadoId = 1, Fecha = new(2026, 4, 3), TipoHoraExtra = TipoHoraExtra.Diurna, CantidadHoras = 2m, EstaAprobada = true },
            new HoraExtra { Id = 2, TenantId = TenantId, EmpleadoId = 1, Fecha = new(2026, 4, 5), TipoHoraExtra = TipoHoraExtra.Nocturna, CantidadHoras = 3m, EstaAprobada = true },
            new HoraExtra { Id = 3, TenantId = TenantId, EmpleadoId = 1, Fecha = new(2026, 4, 6), TipoHoraExtra = TipoHoraExtra.Diurna, CantidadHoras = 9m, EstaAprobada = false },   // sin aprobar
            new HoraExtra { Id = 4, TenantId = TenantId, EmpleadoId = 2, Fecha = new(2026, 4, 20), TipoHoraExtra = TipoHoraExtra.Diurna, CantidadHoras = 5m, EstaAprobada = true },  // fuera del período
            new HoraExtra { Id = 5, TenantId = TenantId, EmpleadoId = 2, Fecha = new(2026, 4, 10), TipoHoraExtra = TipoHoraExtra.FiestaNacionalDiurna, CantidadHoras = 4m, EstaAprobada = true, PlanillaDetailId = 99 }); // ya pagada
        // Ausencia de 3 días que empieza antes del período: solo cuentan los días dentro (1–2 abril = 2 días).
        db.Ausencias.Add(new Ausencia { Id = 1, TenantId = TenantId, EmpleadoId = 2, FechaInicio = new(2026, 3, 31), FechaFin = new(2026, 4, 2), AfectaSalario = true, DiasAusencia = 3m });
        await db.SaveChangesAsync();

        await servicio.GenerarHorasPorDefectoAsync(planilla);
        var r = await servicio.ImportarNovedadesAsync(planilla, ModoNovedades.Sobrescribir);
        await db.SaveChangesAsync();

        r.RequiereConfirmacion.Should().BeFalse();
        r.EmpleadosConNovedades.Should().Be(2);
        r.HorasExtra.Should().Be(5m);
        r.HorasAusencia.Should().Be(16m);

        var f1 = await db.PayrollEmployeeHours.SingleAsync(h => h.EmpleadoId == 1);
        f1.RegularHours.Should().Be(104m, "las regulares no se tocan");
        f1.OvertimeDayHours.Should().Be(2m);
        f1.OvertimeNightHours.Should().Be(3m);
        var f2 = await db.PayrollEmployeeHours.SingleAsync(h => h.EmpleadoId == 2);
        f2.OvertimeDayHours.Should().Be(0m);
        f2.AbsenceHours.Should().Be(16m);
    }

    [Fact]
    public async Task ImportarNovedades__PreguntarNoTocaNadaSiYaHayValores__SumarAcumula()
    {
        var (db, servicio) = Nuevo();
        var planilla = Planilla();
        db.PayrollHeaders.Add(planilla);
        db.Empleados.Add(Empleado(1));
        db.PayrollEmployeeHours.Add(new PayrollEmployeeHours { PayrollHeaderId = 1, EmpleadoId = 1, TenantId = TenantId, RegularHours = 104m, OvertimeDayHours = 1m });
        db.HorasExtra.Add(new HoraExtra { Id = 1, TenantId = TenantId, EmpleadoId = 1, Fecha = new(2026, 4, 3), TipoHoraExtra = TipoHoraExtra.Diurna, CantidadHoras = 2m, EstaAprobada = true });
        await db.SaveChangesAsync();

        var pregunta = await servicio.ImportarNovedadesAsync(planilla, ModoNovedades.Preguntar);
        pregunta.RequiereConfirmacion.Should().BeTrue();
        pregunta.EmpleadosConValoresPrevios.Should().Be(1);
        (await db.PayrollEmployeeHours.SingleAsync()).OvertimeDayHours.Should().Be(1m, "preguntar no escribe");

        await servicio.ImportarNovedadesAsync(planilla, ModoNovedades.Sumar);
        await db.SaveChangesAsync();
        (await db.PayrollEmployeeHours.SingleAsync()).OvertimeDayHours.Should().Be(3m);

        await servicio.ImportarNovedadesAsync(planilla, ModoNovedades.Sobrescribir);
        await db.SaveChangesAsync();
        (await db.PayrollEmployeeHours.SingleAsync()).OvertimeDayHours.Should().Be(2m);
    }

    private class TenantFijo : ITenantContext
    {
        public int TenantId => 0;
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
