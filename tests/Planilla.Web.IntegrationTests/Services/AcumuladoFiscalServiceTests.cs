// ====================================================================
// Tests de AcumuladoFiscalService
//
// Lo que importa acá no es la aritmética (esa la cubre MotorIsrPanamaTests)
// sino QUÉ entra al acumulado: el servicio lo deriva de las planillas
// guardadas, así que un recálculo, una anulación o una planilla de otro año
// no deben ensuciar el año fiscal del empleado.
// ====================================================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Xunit;

namespace Vorluno.Planilla.Web.IntegrationTests.Services;

public class AcumuladoFiscalServiceTests
{
    private const int TenantId = 1;
    private const int EmpleadoId = 10;
    private const int Anio = 2026;

    private static ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"acumulado-{Guid.NewGuid()}")
                .Options,
            currentUserService: null,
            tenantContext: new BypassTenantContext());

    /// <summary>Crea una planilla con un detalle para el empleado indicado.</summary>
    private static void SembrarPlanilla(
        ApplicationDbContext db,
        int headerId,
        DateTime fechaPago,
        decimal bruto,
        decimal css,
        decimal isr,
        PayrollStatus estado = PayrollStatus.Approved,
        int empleadoId = EmpleadoId,
        decimal gastoRepresentacion = 0m,
        decimal isrGastoRepresentacion = 0m)
    {
        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = headerId,
            TenantId = TenantId,
            PayrollNumber = $"P-{headerId}",
            // Inicio real de la quincena (día 1 o 16): el mes de una planilla lo
            // define PeriodStartDate, y "fechaPago - 15 días" caía en el mes anterior.
            PeriodStartDate = fechaPago.Day <= 15 ? new DateTime(fechaPago.Year, fechaPago.Month, 1) : new DateTime(fechaPago.Year, fechaPago.Month, 16),
            PeriodEndDate = fechaPago,
            PayDate = fechaPago,
            PayPeriodType = PayPeriodType.Quincenal,
            Status = estado
        });

        db.PayrollDetails.Add(new PayrollDetail
        {
            Id = headerId * 100,
            TenantId = TenantId,
            PayrollHeaderId = headerId,
            EmpleadoId = empleadoId,
            GrossPay = bruto,
            CssEmployee = css,
            IncomeTax = isr,
            GastoRepresentacion = gastoRepresentacion,
            IsrGastoRepresentacion = isrGastoRepresentacion
        });
    }

    [Fact]
    public async Task EmpleadoNuevo__AcumuladoEnCeroYPrimerPeriodo()
    {
        using var db = NuevoContexto();
        var servicio = new AcumuladoFiscalService(db);

        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);
        var periodo = await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio);

        acumulado.IngresoGravableTotal.Should().Be(0m);
        acumulado.IsrRetenidoTotal.Should().Be(0m);
        periodo.Should().Be(1, "la corrida que se está calculando es la primera del año");
    }

    [Fact]
    public async Task ConPlanillasDelAnio__SumaBrutoMenosSeguroSocialYElIsrRetenido()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio, 1, 15), bruto: 1000m, css: 97.50m, isr: 20m);
        SembrarPlanilla(db, 2, new DateTime(Anio, 1, 31), bruto: 1000m, css: 97.50m, isr: 20m);
        SembrarPlanilla(db, 3, new DateTime(Anio, 2, 15), bruto: 1200m, css: 117m, isr: 25m);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);

        // La base es el bruto: no se resta el Seguro Social (criterio del contador).
        acumulado.IngresoGravableTotal.Should().Be(3200m);
        acumulado.IsrRetenidoTotal.Should().Be(65m);
        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio)).Should().Be(4);
    }

    [Fact]
    public async Task AlRecalcularUnaPlanilla__NoSeCuentaASiMisma()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio, 1, 15), bruto: 1000m, css: 97.50m, isr: 20m);
        SembrarPlanilla(db, 2, new DateTime(Anio, 1, 31), bruto: 1000m, css: 97.50m, isr: 20m);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio, excluirPayrollHeaderId: 2);
        var periodo = await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio, excluirPayrollHeaderId: 2);

        acumulado.IngresoGravableTotal.Should().Be(1000m);
        acumulado.IsrRetenidoTotal.Should().Be(20m);
        periodo.Should().Be(2, "recalcular la segunda planilla la deja siendo la segunda, no la tercera");
    }

    [Fact]
    public async Task PlanillaAnulada__QuedaFueraDelAcumulado()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio, 1, 15), bruto: 1000m, css: 97.50m, isr: 20m);
        SembrarPlanilla(db, 2, new DateTime(Anio, 1, 31), bruto: 1000m, css: 97.50m, isr: 20m,
            estado: PayrollStatus.Cancelled);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);

        acumulado.IsrRetenidoTotal.Should().Be(20m);
        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio)).Should().Be(2);
    }

    [Fact]
    public async Task PlanillasDeOtroAnio__NoEntranAlAnioFiscal()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio - 1, 12, 31), bruto: 5000m, css: 487.50m, isr: 300m);
        SembrarPlanilla(db, 2, new DateTime(Anio, 1, 15), bruto: 1000m, css: 97.50m, isr: 20m);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);

        acumulado.IsrRetenidoTotal.Should().Be(20m);
        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio)).Should().Be(2);
    }

    [Fact]
    public async Task PlanillasDeOtroEmpleado__NoAfectanElContadorDelEmpleado()
    {
        using var db = NuevoContexto();
        // La empresa lleva tres corridas del año, pero este empleado entró ahora.
        SembrarPlanilla(db, 1, new DateTime(Anio, 1, 15), 1000m, 97.50m, 20m, empleadoId: 99);
        SembrarPlanilla(db, 2, new DateTime(Anio, 1, 31), 1000m, 97.50m, 20m, empleadoId: 99);
        SembrarPlanilla(db, 3, new DateTime(Anio, 2, 15), 1000m, 97.50m, 20m, empleadoId: 99);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);

        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio))
            .Should().Be(1, "el contador cuenta las corridas del empleado, no las de la empresa");
        (await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio)).IngresoGravableTotal.Should().Be(0m);
    }

    [Fact]
    public async Task SaldosDeMigracion__SeSumanALoProcesadoPorElSistema()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio, 7, 15), bruto: 1000m, css: 97.50m, isr: 20m);
        db.AcumuladosFiscalesEmpleados.Add(new AcumuladoFiscalEmpleado
        {
            TenantId = TenantId,
            EmpleadoId = EmpleadoId,
            Anio = Anio,
            IngresoGravableInicial = 9_000m,
            DecimoInicial = 500m,
            IsrRetenidoInicial = 150m
        });
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);

        acumulado.IngresoGravableTotal.Should().Be(9_000m + 1_000m);
        acumulado.DecimoTotal.Should().Be(500m);
        acumulado.IsrRetenidoTotal.Should().Be(170m, "lo retenido antes de migrar no se le vuelve a cobrar");
    }

    [Fact]
    public async Task PartidasDeDecimo__SeAcumulanApartemDelSalario()
    {
        using var db = NuevoContexto();
        SembrarPlanilla(db, 1, new DateTime(Anio, 4, 15), bruto: 1000m, css: 97.50m, isr: 20m);

        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = 1,
            TenantId = TenantId,
            Numero = "D-1",
            PeriodoDesde = new DateTime(Anio, 1, 1),
            PeriodoHasta = new DateTime(Anio, 4, 15),
            FechaPago = new DateTime(Anio, 4, 15),
            Estado = EstadoDecimo.Pagada
        });
        db.DetallesDecimo.Add(new DetalleDecimo
        {
            Id = 1,
            TenantId = TenantId,
            PlanillaDecimoId = 1,
            EmpleadoId = EmpleadoId,
            MontoDecimo = 333.33m,
            CssEmpleado = 24.17m,
            ISR = 7.69m
        });
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);

        acumulado.IngresoGravableTotal.Should().Be(1_000m, "el décimo no se mezcla con el salario");
        acumulado.DecimoTotal.Should().Be(333.33m, "bruto, como en la columna XIII MEX de la hoja");
        acumulado.PartidasDecimoTotal.Should().Be(1);
        acumulado.IsrRetenidoTotal.Should().Be(27.69m, "el ISR del décimo también cuenta como retenido");
        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio))
            .Should().Be(2, "el décimo no suma una corrida de salario al contador");
    }


    // ====================================================================
    // La ficha es la hoja del contador, celda por celda
    // ====================================================================

    private static void SembrarEmpleadoQuincenal(ApplicationDbContext db, decimal salarioBase = 713.44m)
    {
        db.Empleados.Add(new Empleado
        {
            Id = EmpleadoId,
            TenantId = TenantId,
            Nombre = "Orlando",
            Apellido = "Barroso",
            NumeroIdentificacion = "8-000-0000",
            SalarioBase = salarioBase,
            PayPeriodType = PayPeriodType.Quincenal
        });
    }

    /// <summary>Fin de período de la quincena n: 15 y último de cada mes.</summary>
    private static DateTime FechaQuincena(int n)
    {
        var mes = (n + 1) / 2;
        return n % 2 == 1
            ? new DateTime(Anio, mes, 15)
            : new DateTime(Anio, mes, DateTime.DaysInMonth(Anio, mes));
    }

    private static void SembrarDecimo(ApplicationDbContext db, int id, int mes, decimal monto)
    {
        db.PlanillasDecimo.Add(new PlanillaDecimo
        {
            Id = id,
            TenantId = TenantId,
            Numero = $"D-{id}",
            PeriodoDesde = new DateTime(Anio, 1, 1),
            PeriodoHasta = new DateTime(Anio, mes, 15),
            FechaPago = new DateTime(Anio, mes, 15),
            Estado = EstadoDecimo.Pagada
        });
        db.DetallesDecimo.Add(new DetalleDecimo
        {
            Id = id,
            TenantId = TenantId,
            PlanillaDecimoId = id,
            EmpleadoId = EmpleadoId,
            MontoDecimo = monto,
            CssEmpleado = Math.Round(monto * 0.0725m, 2),
            ISR = 0m
        });
    }

    [Fact]
    public async Task Ficha__TieneLasMismasColumnasYCeldasQueLibro1()
    {
        // Libro1.xlsx, hoja Orlando_Barroso: cuatro quincenas cargadas y el resto vacío.
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db);
        SembrarPlanilla(db, 1, FechaQuincena(1), bruto: 356.72m, css: 34.78m, isr: 0m);
        SembrarPlanilla(db, 2, FechaQuincena(2), bruto: 445.90m, css: 43.48m, isr: 0m);
        SembrarPlanilla(db, 3, FechaQuincena(3), bruto: 466.48m, css: 45.48m, isr: 0m);
        SembrarPlanilla(db, 4, FechaQuincena(4), bruto: 466.48m, css: 45.48m, isr: 6.49m);
        await db.SaveChangesAsync();

        var ficha = await new AcumuladoFiscalService(db).ObtenerFichaAnualAsync(EmpleadoId, Anio);

        ficha.Should().NotBeNull();

        // Cabecera de la hoja
        ficha!.Empleado.Should().Be("Orlando Barroso");
        ficha.SalarioBase.Should().Be(713.44m);
        ficha.ConyugeDependiente.Should().Be("NO");
        ficha.PeriodosDePago.Should().Be(26m);
        ficha.NombrePeriodo.Should().Be("Quincenas");

        // 24 filas fijas, con MESES solo en la primera de cada mes
        ficha.Filas.Should().HaveCount(24);
        ficha.Filas[0].Mes.Should().Be("Enero");
        ficha.Filas[1].Mes.Should().BeEmpty();
        ficha.Filas[22].Mes.Should().Be("Diciembre");

        // Fila 1: 356.72 / 1 x 26 = 9,274.72 (restando el Seguro Social daba 8,370.44)
        var q1 = ficha.Filas[0];
        q1.Salarios.Should().Be(356.72m);
        q1.Acumulado.Should().Be(356.72m);
        q1.Periodos.Should().Be(1m);
        q1.IngresoGravable.Should().Be(9_274.72m);
        q1.RentaAnual.Should().Be(0m);

        ficha.Filas[1].IngresoGravable.Should().Be(10_434.06m);
        ficha.Filas[2].IngresoGravable.Should().Be(10_998.87m);

        // Fila 4: acumulado 1,735.58 -> 11,281.27 -> renta 42.19 -> 1.62 por período -> causado 6.49
        var q4 = ficha.Filas[3];
        q4.Acumulado.Should().Be(1_735.58m);
        q4.IngresoGravable.Should().Be(11_281.27m);
        q4.RentaAnual.Should().Be(42.19m);
        q4.RentaPorPeriodo.Should().Be(1.62m);
        q4.ImpuestoCausado.Should().Be(6.49m);
        q4.ImpuestoAPagar.Should().Be(6.49m);
        q4.RentaAcumulada.Should().Be(6.49m);

        // Las quincenas vacías siguen ahí: el acumulado se arrastra y la proyección baja.
        var q5 = ficha.Filas[4];
        q5.TieneDatos.Should().BeFalse();
        q5.Acumulado.Should().Be(1_735.58m);
        q5.IngresoGravable.Should().Be(9_025.02m);
        q5.RentaAnual.Should().Be(0m);

        // PERIODOS sin décimo: entero hasta el final.
        ficha.Filas[23].Periodos.Should().Be(24m);

        // Totales
        ficha.TotalSalarios.Should().Be(1_735.58m);
        ficha.TotalXiiiMes.Should().Be(0m);
    }

    [Fact]
    public async Task Ficha__ElDecimoVaEnLaColumnaXiiiMesYSaltaLosPeriodos()
    {
        // CALCULO RENTA.xlsx, hoja Marianela_Barroso: 2,000 por quincena y décimo
        // de 1,333.33 en abril, agosto y diciembre. Cierra en 6,350.00.
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db, 4_000m);
        for (var q = 1; q <= 24; q++)
            SembrarPlanilla(db, q, FechaQuincena(q), bruto: 2_000m, css: 195m, isr: 0m);
        SembrarDecimo(db, 1, mes: 4, 1_333.33m);
        SembrarDecimo(db, 2, mes: 8, 1_333.33m);
        SembrarDecimo(db, 3, mes: 12, 1_333.33m);
        await db.SaveChangesAsync();

        var ficha = await new AcumuladoFiscalService(db).ObtenerFichaAnualAsync(EmpleadoId, Anio);

        ficha.Should().NotBeNull();
        ficha!.Filas.Should().HaveCount(24, "el décimo no agrega filas: va en la columna XIII MEX");

        // Columna XIII MEX en la primera quincena de abril, agosto y diciembre
        ficha.Filas[6].XiiiMes.Should().Be(1_333.33m);
        ficha.Filas[14].XiiiMes.Should().Be(1_333.33m);
        ficha.Filas[22].XiiiMes.Should().Be(1_333.33m);
        ficha.Filas[7].XiiiMes.Should().Be(0m);

        // Columna PERIODOS: 7.667 / 16.333 / 25.000 / 26.000
        ficha.Filas[6].Periodos.Should().Be(7.667m);
        ficha.Filas[7].Periodos.Should().Be(8.667m);
        ficha.Filas[14].Periodos.Should().Be(16.333m);
        ficha.Filas[22].Periodos.Should().Be(25.000m);
        ficha.Filas[23].Periodos.Should().Be(26.000m);

        // Los meses con décimo se resaltan enteros
        ficha.Filas[6].EsMesDecimo.Should().BeTrue();
        ficha.Filas[7].EsMesDecimo.Should().BeTrue();
        ficha.Filas[8].EsMesDecimo.Should().BeFalse();

        // Celdas de la hoja
        ficha.Filas[6].Acumulado.Should().Be(15_333.33m);
        ficha.Filas[6].ImpuestoCausado.Should().Be(1_872.35m);
        ficha.Filas[23].Acumulado.Should().Be(51_999.99m);
        ficha.Filas[23].RentaAnual.Should().Be(6_350.00m);
        ficha.Filas[23].ImpuestoCausado.Should().Be(6_350.00m);

        ficha.TotalSalarios.Should().Be(48_000m);
        ficha.TotalXiiiMes.Should().Be(3_999.99m);
    }

    [Fact]
    public async Task Ficha__SeparaVacacionesExtrasYComisionDelSalario()
    {
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db, 1_000m);

        db.PayrollHeaders.Add(new PayrollHeader
        {
            Id = 1, TenantId = TenantId, PayrollNumber = "P-1",
            PeriodStartDate = new DateTime(Anio, 1, 1), PeriodEndDate = new DateTime(Anio, 1, 15),
            PayDate = new DateTime(Anio, 1, 15), PayPeriodType = PayPeriodType.Quincenal,
            Status = PayrollStatus.Approved
        });
        db.PayrollDetails.Add(new PayrollDetail
        {
            Id = 100, TenantId = TenantId, PayrollHeaderId = 1, EmpleadoId = EmpleadoId,
            GrossPay = 1_000m,                 // 500 salario + 100 vacaciones + 150 extras + 250 comisión
            MontoVacaciones = 100m,
            OvertimePay = 120m,
            MontoHorasExtraExceso = 30m,
            Commissions = 250m,
            CssEmployee = 97.50m
        });
        await db.SaveChangesAsync();

        var fila = (await new AcumuladoFiscalService(db).ObtenerFichaAnualAsync(EmpleadoId, Anio))!.Filas[0];

        fila.Salarios.Should().Be(500m, "lo que queda del bruto tras separar los demás conceptos");
        fila.Vacaciones.Should().Be(100m);
        fila.Extras.Should().Be(150m);
        fila.Comision.Should().Be(250m);
        fila.Acumulado.Should().Be(1_000m, "la suma de la fila es siempre el bruto real");
        fila.IngresoGravable.Should().Be(26_000m);
    }

    [Fact]
    public async Task Ficha__ElGastoDeRepresentacionNoEntraALaHoja()
    {
        // Tributa aparte con su propia tarifa; la hoja del contador no lo tiene.
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db, 2_000m);
        SembrarPlanilla(db, 1, FechaQuincena(1), bruto: 1_500m, css: 146.25m, isr: 120m,
            gastoRepresentacion: 500m, isrGastoRepresentacion: 50m);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var fila = (await servicio.ObtenerFichaAnualAsync(EmpleadoId, Anio))!.Filas[0];
        fila.Salarios.Should().Be(1_000m);
        fila.Acumulado.Should().Be(1_000m);

        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);
        acumulado.IngresoGravableTotal.Should().Be(1_000m, "bruto sin el gasto de representación");
        acumulado.GastoRepresentacionTotal.Should().Be(500m);
        acumulado.IsrRetenidoTotal.Should().Be(70m);
        acumulado.IsrGastoRepresentacionTotal.Should().Be(50m);
    }


    // ====================================================================
    // Meses importados del año: entran a la ficha y al motor como períodos
    // ====================================================================

    private static void SembrarMesImportado(ApplicationDbContext db, int mes, decimal salario)
    {
        db.DevengadosMensuales.Add(new DevengadoMensual
        {
            TenantId = TenantId, EmpleadoId = EmpleadoId, Anio = Anio, Mes = mes,
            Salario = salario, Origen = OrigenDevengado.Importado
        });
    }

    [Fact]
    public async Task Ficha__LosMesesImportadosLlenanSusQuincenasYLaProyeccionEsReal()
    {
        // Empresa que migra en septiembre con enero–agosto importados (713.44 al mes)
        // y la primera planilla de Pagly en la 1.ª quincena de septiembre.
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db);
        for (var m = 1; m <= 8; m++) SembrarMesImportado(db, m, 713.44m);
        SembrarPlanilla(db, 1, FechaQuincena(17), bruto: 356.72m, css: 34.78m, isr: 0m);
        await db.SaveChangesAsync();

        var ficha = await new AcumuladoFiscalService(db).ObtenerFichaAnualAsync(EmpleadoId, Anio);

        // Cada mes importado se reparte en sus dos quincenas y se marca como importado.
        ficha!.Filas[0].Salarios.Should().Be(356.72m);
        ficha.Filas[0].EsImportado.Should().BeTrue();
        ficha.Filas[15].Salarios.Should().Be(356.72m);
        ficha.Filas[15].EsImportado.Should().BeTrue();

        // La planilla de septiembre es la corrida 17 y NO es importada.
        ficha.Filas[16].Salarios.Should().Be(356.72m);
        ficha.Filas[16].EsImportado.Should().BeFalse();
        ficha.Filas[16].Periodos.Should().Be(17m);

        // Acumulado 17 x 356.72 = 6,064.24 → proyecta 9,274.72, como con salario fijo.
        ficha.Filas[16].Acumulado.Should().Be(6_064.24m);
        ficha.Filas[16].IngresoGravable.Should().Be(9_274.72m);

        // Sin este arreglo la fila 1 acumulaba ocho meses con PERIODOS 1 y proyectaba ~150,000.
        ficha.Filas[0].IngresoGravable.Should().Be(9_274.72m);
    }

    [Fact]
    public async Task Acumulado__LosMesesImportadosCuentanComoIngresoYComoPeriodos()
    {
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db);
        for (var m = 1; m <= 8; m++) SembrarMesImportado(db, m, 713.44m);
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        var acumulado = await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio);
        var periodo = await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio);

        acumulado.IngresoGravableTotal.Should().Be(8 * 713.44m);
        periodo.Should().Be(17, "ocho meses quincenales son 16 períodos; la planilla nueva es la 17");
    }

    [Fact]
    public async Task Acumulado__UnMesConPlanillaIgnoraLoImportadoDeEseMes()
    {
        using var db = NuevoContexto();
        SembrarEmpleadoQuincenal(db);
        SembrarMesImportado(db, 1, 999m);                                    // importado…
        SembrarPlanilla(db, 1, FechaQuincena(1), bruto: 356.72m, css: 0m, isr: 0m); // …pero enero tiene planilla
        await db.SaveChangesAsync();

        var servicio = new AcumuladoFiscalService(db);
        (await servicio.ObtenerAcumuladoAsync(EmpleadoId, Anio)).IngresoGravableTotal.Should().Be(356.72m);
        (await servicio.ObtenerNumeroPeriodoAsync(EmpleadoId, Anio)).Should().Be(2);
    }

    private class BypassTenantContext : ITenantContext
    {
        public int TenantId => 0;
        public TenantRole TenantRole => TenantRole.User;
        public string? UserId => null;
        public bool IsSystemAdmin => false;
        public bool HasTenant => false;
        public Task SetTenantAsync(int tenantId) => Task.CompletedTask;
        public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult<Tenant?>(null);
        public bool HasRole(TenantRole role) => false;
        public bool IsAdminOrOwner() => false;
        public void Clear() { }
    }
}
