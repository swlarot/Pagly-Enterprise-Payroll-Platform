// ====================================================================
// Tests del SIPE mensual (ReportesService.GenerarReporteSipMensual)
//
// Lo que se declara a la CSS por un mes: planillas aprobadas/pagadas del
// período (no borradores, calculadas ni anuladas), el décimo pagado en el
// mes y la parte cotizable de las liquidaciones. Un empleado sale una vez.
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

public class ReporteSipeMensualTests
{
    private const int TenantId = 1;

    private static (ApplicationDbContext db, ReportesService servicio) Nuevo()
    {
        var tenant = new TenantFijo();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"sipe-{Guid.NewGuid()}").Options,
            currentUserService: null, tenantContext: tenant);
        return (db, new ReportesService(db, tenant));
    }

    private static Empleado Emp(int id) => new()
    {
        Id = id, TenantId = TenantId, Nombre = $"Emp{id}", Apellido = "Test", NumeroIdentificacion = $"8-{id}-{id}",
        SalarioBase = 1000m, EstaActivo = true,
    };

    private static void Planilla(ApplicationDbContext db, int id, DateTime inicio, PayrollStatus estado, params (int emp, decimal bruto, decimal vac)[] detalles)
    {
        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = id, TenantId = TenantId, PayrollNumber = $"2026-{id:D3}",
            PeriodStartDate = inicio, PeriodEndDate = inicio.AddDays(14), PayDate = inicio.AddDays(14),
            PayPeriodType = PayPeriodType.Quincenal, Status = estado,
        });
        var n = 0;
        foreach (var (emp, bruto, vac) in detalles)
        {
            db.PayrollDetails.Add(new PayrollDetail
            {
                Id = id * 100 + n++, TenantId = TenantId, PayrollHeaderId = id, EmpleadoId = emp,
                GrossPay = bruto, MontoVacaciones = vac,
                CssEmployee = Math.Round(bruto * 0.0975m, 2), CssEmployer = Math.Round(bruto * 0.1225m, 2),
                EducationalInsuranceEmployee = Math.Round(bruto * 0.0125m, 2), EducationalInsuranceEmployer = Math.Round(bruto * 0.015m, 2),
                RiskContribution = Math.Round(bruto * 0.0056m, 2),
            });
        }
    }

    [Fact]
    public async Task SoloEntranPlanillasAprobadasOPagadasDelMes__PorPeriodoTrabajado()
    {
        var (db, servicio) = Nuevo();
        db.Empleados.AddRange(Emp(1), Emp(2));
        Planilla(db, 1, new(2026, 4, 1), PayrollStatus.Approved, (1, 500m, 0m), (2, 600m, 0m));
        Planilla(db, 2, new(2026, 4, 16), PayrollStatus.Paid, (1, 500m, 100m));
        Planilla(db, 3, new(2026, 4, 16), PayrollStatus.Calculated, (2, 999m, 0m)); // sin aprobar: fuera
        Planilla(db, 4, new(2026, 4, 16), PayrollStatus.Cancelled, (2, 999m, 0m));  // anulada: fuera
        Planilla(db, 5, new(2026, 5, 1), PayrollStatus.Approved, (1, 999m, 0m));    // otro mes: fuera
        await db.SaveChangesAsync();

        var r = await servicio.GenerarReporteSipMensual(4, 2026);

        r.Empleados.Should().HaveCount(2);
        var e1 = r.Empleados.Single(e => e.Cedula == "8-1-1");
        e1.SalarioBruto.Should().Be(1_000m, "las dos quincenas sumadas en una sola fila");
        e1.BaseCss.Should().Be(1_000m, "la base es el bruto cotizable, no se reconstruye desde la tasa");
        e1.CssEmpleado.Should().Be(97.50m);
        e1.Vacaciones.Should().Be(100m);
        r.Empleados.Single(e => e.Cedula == "8-2-2").SalarioBruto.Should().Be(600m);
        r.Totales.TotalSalarios.Should().Be(1_600m);
        r.TotalVacaciones.Should().Be(100m);
        r.Fuentes.Should().HaveCount(2).And.Contain(f => f.StartsWith("Planilla 2026-001"));
        r.Periodo.Should().Be("abril 2026");
    }

    [Fact]
    public async Task ElDecimoEntraPorFechaDePago__SinRiesgoProfesional()
    {
        var (db, servicio) = Nuevo();
        db.Empleados.Add(Emp(1));
        Planilla(db, 1, new(2026, 4, 1), PayrollStatus.Approved, (1, 500m, 0m));
        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = 1, TenantId = TenantId, Numero = "DEC-2026-01", PeriodoDesde = new(2025, 12, 16), PeriodoHasta = new(2026, 4, 15),
            FechaPago = new(2026, 4, 15), Estado = EstadoDecimo.Pagada,
        });
        db.DetallesDecimo.Add(new DetalleDecimo
        {
            Id = 1, TenantId = TenantId, PlanillaDecimoId = 1, EmpleadoId = 1,
            MontoDecimo = 300m, CssEmpleado = 21.75m, CssPatrono = 32.25m, SeEmpleado = 3.75m, SePatrono = 4.50m,
        });
        // Borrador: fuera.
        db.PlanillasDecimo.Add(new PlanillaDecimo { Id = 2, TenantId = TenantId, Numero = "DEC-X", FechaPago = new(2026, 4, 20), Estado = EstadoDecimo.Borrador });
        db.DetallesDecimo.Add(new DetalleDecimo { Id = 2, TenantId = TenantId, PlanillaDecimoId = 2, EmpleadoId = 1, MontoDecimo = 999m });
        await db.SaveChangesAsync();

        var r = await servicio.GenerarReporteSipMensual(4, 2026);

        var e1 = r.Empleados.Single();
        e1.SalarioBruto.Should().Be(800m);
        e1.CssEmpleado.Should().Be(48.75m + 21.75m);
        e1.RiesgoProfesional.Should().Be(2.80m, "el riesgo solo lo aporta la planilla");
        r.Fuentes.Should().Contain(f => f.StartsWith("Décimo DEC-2026-01"));
    }

    [Fact]
    public async Task LaLiquidacionEntraSoloConSuParteCotizable()
    {
        var (db, servicio) = Nuevo();
        db.Empleados.Add(Emp(1));
        db.Liquidaciones.Add(new Liquidacion
        {
            Id = 1, TenantId = TenantId, EmpleadoId = 1, Numero = "LIQ-001",
            FechaLiquidacion = new(2026, 9, 30), FechaContratacion = new(2013, 6, 1), FechaTerminacion = new(2026, 9, 30),
            TipoTerminacion = default, Estado = EstadoLiquidacion.Aprobada,
            SalarioPendiente = 700.96m, VacacionesProporcionales = 449.97m, DecimoTercerMesProporcional = 147.51m,
            PrimaAntiguedad = 1_828.78m, Indemnizacion = 6_038.46m,
            CssEmpleado = 122.90m, CssPatronal = 154.44m, SeEmpleado = 14.39m, SePatronal = 17.26m,
        });
        await db.SaveChangesAsync();

        var r = await servicio.GenerarReporteSipMensual(9, 2026);

        var e1 = r.Empleados.Single();
        e1.SalarioBruto.Should().Be(1_298.44m, "prima e indemnización no cotizan (Ley 51 Art. 92)");
        e1.BaseCss.Should().Be(1_298.44m);
        e1.Vacaciones.Should().Be(449.97m);
        e1.CssEmpleado.Should().Be(122.90m);
        r.Fuentes.Should().ContainSingle(f => f.StartsWith("Liquidación LIQ-001"));
    }

    [Fact]
    public async Task MesSinNada__ReporteVacioSinFuentes()
    {
        var (db, servicio) = Nuevo();
        await db.SaveChangesAsync();
        var r = await servicio.GenerarReporteSipMensual(2, 2026);
        r.Empleados.Should().BeEmpty();
        r.Fuentes.Should().BeEmpty();
        r.Totales.GranTotalSip.Should().Be(0m);
    }

    private class TenantFijo : ITenantContext
    {
        public int TenantId => TenantId_; // el servicio filtra por el tenant del contexto
        private const int TenantId_ = 1;
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
