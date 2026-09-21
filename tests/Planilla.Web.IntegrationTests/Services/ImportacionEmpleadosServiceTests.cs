// ====================================================================
// Tests de la importación de empleados, de punta a punta:
// plantilla generada → llenada → leída → validada → confirmada.
// ====================================================================

using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vorluno.Planilla.Application.DTOs.Importacion;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Xunit;

namespace Vorluno.Planilla.Web.IntegrationTests.Services;

public class ImportacionEmpleadosServiceTests
{
    private const int TenantId = 1;
    private static readonly DateTime Hoy = new(2026, 9, 21);

    private static (ApplicationDbContext db, ImportacionEmpleadosService servicio) Nuevo()
    {
        var tenant = new TenantUno();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"importacion-{Guid.NewGuid()}")
                // La base en memoria no tiene transacciones; en producción (PostgreSQL) sí.
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            currentUserService: null, tenantContext: tenant);
        return (db, new ImportacionEmpleadosService(db, tenant, new AuditSilenciosa(), NullLogger<ImportacionEmpleadosService>.Instance));
    }

    /// <summary>Toma la plantilla real y la llena como lo haría el contador.</summary>
    private static MemoryStream PlantillaLlena(Action<IXLWorksheet, IXLWorksheet, IXLWorksheet> llenar)
    {
        var bytes = PlantillaImportacion.Generar(Hoy);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        llenar(wb.Worksheet(PlantillaImportacion.HojaEmpleados),
               wb.Worksheet(PlantillaImportacion.HojaSalarios),
               wb.Worksheet(PlantillaImportacion.HojaSaldos));
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void FilaBelisa(IXLWorksheet emp, IXLWorksheet sal, IXLWorksheet saldos, int fila = 3)
    {
        emp.Cell(fila, 1).Value = "8-809-1415";
        emp.Cell(fila, 2).Value = "Belisa";
        emp.Cell(fila, 3).Value = "Belloso";
        emp.Cell(fila, 5).Value = 700.96;
        emp.Cell(fila, 6).Value = new DateTime(2013, 6, 1);
        emp.Cell(fila, 7).Value = "Quincenal";
        emp.Cell(fila, 9).Value = "Producción";
        emp.Cell(fila, 12).Value = 2.10;

        // Los 60 meses de la hoja "Salarios Acumulados" del contador (oct-2021 → sep-2026)
        sal.Cell(fila, 1).Value = "8-809-1415";
        var montos = new[]
        {
            443.16, 667.50, 526.56,                                                                       // 2021 oct-dic
            396.92, 344.58, 365.65, 515.61, 542.57, 506.34, 482.75, 554.37, 467.59, 467.59, 446.53, 465.53, // 2022
            592.28, 647.88, 324.36, 652.94, 635.25, 590.59, 604.07, 534.15, 534.15, 663.90, 659.70, 700.96, // 2023
            639.46, 647.04, 642.80, 657.15, 636.09, 651.25, 656.31, 638.62, 625.14, 628.51, 652.94, 678.21, // 2024
            679.90, 639.46, 645.36, 647.04, 647.88, 640.30, 577.11, 616.71, 593.10, 588.07, 633.56, 575.43, // 2025
            700.96, 615.87, 648.73, 602.39, 627.66, 572.06, 562.79, 619.24, 700.96                          // 2026 ene-sep
        };
        for (var i = 0; i < montos.Length; i++) sal.Cell(fila, 3 + i).Value = montos[i];

        saldos.Cell(fila, 1).Value = "8-809-1415";
        saldos.Cell(fila, 3).Value = 120.50;   // ISR retenido en 2026
        saldos.Cell(fila, 4).Value = 300.00;   // décimo pagado
        saldos.Cell(fila, 5).Value = "2";      // partidas
    }

    [Fact]
    public void Plantilla__TraeLos60MesesTerminandoEnElMesActual()
    {
        var meses = PlantillaImportacion.MesesDeLaPlantilla(Hoy);
        meses.Should().HaveCount(60);
        meses.First().Should().Be(new DateTime(2021, 10, 1));
        meses.Last().Should().Be(new DateTime(2026, 9, 1));
    }

    [Fact]
    public async Task Validar__LeeLasTresHojasYUneLosDatosPorCedula()
    {
        var (_, servicio) = Nuevo();
        using var archivo = PlantillaLlena((e, s, z) => FilaBelisa(e, s, z));

        var r = await servicio.ValidarAsync(archivo, Hoy);

        r.ProblemasDelArchivo.Should().BeEmpty();
        r.MesesDelArchivo.Should().HaveCount(60);
        r.Filas.Should().HaveCount(1, "la fila de ejemplo de la plantilla se ignora");
        var f = r.Filas[0];
        f.TieneErrores.Should().BeFalse();
        f.Datos.Cedula.Should().Be("8-809-1415");
        f.Datos.SalarioBase.Should().Be(700.96m);
        f.Datos.FechaContratacion.Should().Be(new DateTime(2013, 6, 1));
        f.Datos.RiesgoProfesional.Should().Be(2.10m);
        f.Datos.Meses.Count(m => m.Monto is not null).Should().Be(60);
        // La hoja del contador da 35,671.55 como "Total de Salario Acumulados", pero ese total
        // ya lleva sumadas las vacaciones proporcionales (449.97). Los 60 meses puros suman esto:
        f.Datos.Meses.Sum(m => m.Monto ?? 0).Should().Be(35_221.58m);
        f.Datos.Saldos!.IsrRetenido.Should().Be(120.50m);
        f.Datos.Saldos.PartidasDecimo.Should().Be(2);
    }

    [Fact]
    public async Task Validar__ArchivoSinHojaEmpleados_LoDice()
    {
        var (_, servicio) = Nuevo();
        using var wb = new XLWorkbook();
        wb.Worksheets.Add("Otra");
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;

        var r = await servicio.ValidarAsync(ms, Hoy);
        r.ProblemasDelArchivo.Should().ContainMatch("*hoja «Empleados»*");
        r.Filas.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmar__CreaEmpleadoMesesYSaldos()
    {
        var (db, servicio) = Nuevo();
        using var archivo = PlantillaLlena((e, s, z) => FilaBelisa(e, s, z));
        var validado = await servicio.ValidarAsync(archivo, Hoy);

        var resumen = await servicio.ConfirmarAsync(new ConfirmarImportacionRequest
        {
            Filas = validado.Filas.Select(f => f.Datos).ToList(),
            NombreArchivo = "prueba.xlsx"
        }, Hoy);

        resumen.Creados.Should().Be(1);
        resumen.Actualizados.Should().Be(0);
        resumen.MesesGuardados.Should().Be(60);
        resumen.SaldosGuardados.Should().Be(1);

        var e = await db.Empleados.Include(x => x.Departamento).Include(x => x.HistorialSalarial).SingleAsync();
        e.NumeroIdentificacion.Should().Be("8-809-1415");
        e.PayPeriodType.Should().Be(PayPeriodType.Quincenal);
        e.CssRiskPercentage.Should().Be(2.10m);
        e.Departamento!.Nombre.Should().Be("Producción");
        e.HourlyRate.Should().BeGreaterThan(0, "se calcula igual que en el alta manual");
        e.HistorialSalarial.Should().ContainSingle(h => h.Motivo.StartsWith("Contratación"));

        (await db.DevengadosMensuales.CountAsync()).Should().Be(60);
        (await db.DevengadosMensuales.Where(d => d.Anio == 2021).SumAsync(d => d.Salario)).Should().Be(1_637.22m);

        var saldo = await db.AcumuladosFiscalesEmpleados.SingleAsync();
        saldo.IsrRetenidoInicial.Should().Be(120.50m);
        saldo.DecimoInicial.Should().Be(300m);
        saldo.PartidasDecimoInicial.Should().Be(2);
        // Ingreso del año: enero a agosto (el mes actual, septiembre, no cuenta como saldo)
        saldo.IngresoGravableInicial.Should().Be(700.96m + 615.87m + 648.73m + 602.39m + 627.66m + 572.06m + 562.79m + 619.24m);
    }

    [Fact]
    public async Task Confirmar__ReimportarLaMismaCedula_ActualizaSinDuplicar()
    {
        var (db, servicio) = Nuevo();
        using var a1 = PlantillaLlena((e, s, z) => FilaBelisa(e, s, z));
        var v1 = await servicio.ValidarAsync(a1, Hoy);
        await servicio.ConfirmarAsync(new ConfirmarImportacionRequest { Filas = v1.Filas.Select(f => f.Datos).ToList() }, Hoy);

        using var a2 = PlantillaLlena((e, s, z) => { FilaBelisa(e, s, z); e.Cell(3, 5).Value = 750.00; });
        var v2 = await servicio.ValidarAsync(a2, Hoy);
        v2.Filas[0].Datos.YaExiste.Should().BeTrue();
        v2.Filas[0].Problemas.Should().ContainSingle(p => p.Campo == "cedula" && p.Tipo == TipoProblema.Aviso);

        var resumen = await servicio.ConfirmarAsync(new ConfirmarImportacionRequest { Filas = v2.Filas.Select(f => f.Datos).ToList() }, Hoy);

        resumen.Creados.Should().Be(0);
        resumen.Actualizados.Should().Be(1);
        (await db.Empleados.CountAsync()).Should().Be(1);
        (await db.Empleados.SingleAsync()).SalarioBase.Should().Be(750m);
        (await db.HistorialSalarial.CountAsync()).Should().Be(2, "contratación más el ajuste");
        (await db.DevengadosMensuales.CountAsync()).Should().Be(60, "los meses se actualizan, no se duplican");
        (await db.AcumuladosFiscalesEmpleados.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Confirmar__UnaFilaConError_NoEscribeNada()
    {
        var (db, servicio) = Nuevo();
        var buena = new FilaImportacionDto
        {
            Cedula = "4-111-2222", Nombre = "Ana", Apellido = "Pérez", SalarioBase = 800m,
            FechaContratacion = new DateTime(2024, 1, 1), TipoPeriodo = "Quincenal"
        };
        var mala = new FilaImportacionDto { Cedula = "??", Nombre = "X", Apellido = "Y", SalarioBase = 100m, FechaContratacion = new(2024, 1, 1), TipoPeriodo = "Quincenal" };

        var acto = () => servicio.ConfirmarAsync(new ConfirmarImportacionRequest { Filas = new() { buena, mala } }, Hoy);

        await acto.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no se guardó nada*");
        (await db.Empleados.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Confirmar__LasOmitidasNoSeImportan()
    {
        var (db, servicio) = Nuevo();
        var a = new FilaImportacionDto { Cedula = "4-111-2222", Nombre = "Ana", Apellido = "Pérez", SalarioBase = 800m, FechaContratacion = new(2024, 1, 1), TipoPeriodo = "Quincenal" };
        var b = new FilaImportacionDto { Cedula = "4-333-4444", Nombre = "Luis", Apellido = "Gómez", SalarioBase = 900m, FechaContratacion = new(2024, 1, 1), TipoPeriodo = "Mensual" };

        var resumen = await servicio.ConfirmarAsync(new ConfirmarImportacionRequest
        {
            Filas = new() { a, b }, Omitidas = new() { "4-333-4444" }
        }, Hoy);

        resumen.Creados.Should().Be(1);
        resumen.Omitidos.Should().Be(1);
        (await db.Empleados.SingleAsync()).NumeroIdentificacion.Should().Be("4-111-2222");
    }

    private class AuditSilenciosa : IAuditLogService
    {
        public Task LogAsync(string action, string entityType, string? entityId = null, Dictionary<string, string>? metadata = null) => Task.CompletedTask;
        public Task<Application.Common.Result<Application.DTOs.Tenant.PagedResultDto<Application.DTOs.Tenant.AuditLogDto>>> GetAuditLogAsync(Application.DTOs.Tenant.AuditLogFilterDto filter) => throw new NotImplementedException();
        public Task<Application.Common.Result<List<Application.DTOs.Tenant.AuditLogDto>>> GetEntityAuditLogAsync(string entityType, string entityId) => throw new NotImplementedException();
    }

    private class TenantUno : ITenantContext
    {
        public int TenantId => TenantId_;
        private const int TenantId_ = 1;
        public TenantRole TenantRole => TenantRole.Owner;
        public string? UserId => "u1";
        public bool IsSystemAdmin => false;
        public bool HasTenant => true;
        public Task SetTenantAsync(int tenantId) => Task.CompletedTask;
        public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult<Tenant?>(null);
        public bool HasRole(TenantRole role) => true;
        public bool IsAdminOrOwner() => true;
        public void Clear() { }
    }
}
