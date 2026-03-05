// ====================================================================
// Planilla - PayrollProcessingService
// Creado: 2025-12-27
// Actualizado: 2026-02-16 - Motor de prelacion con proteccion salario minimo
// Descripción: Servicio de procesamiento de planilla con deducciones adicionales
// Integra préstamos, deducciones fijas, anticipos, horas extra, ausencias y vacaciones
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Results;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio que procesa planillas completas incluyendo deducciones adicionales
/// con motor de prelacion legal y proteccion de salario minimo.
/// </summary>
public class PayrollProcessingService
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollCalculationOrchestratorPortable _orchestrator;
    private readonly IAsistenciaCalculationService _asistenciaService;
    private readonly IDeduccionPrioridadEngine _deduccionEngine;

    public PayrollProcessingService(
        ApplicationDbContext context,
        PayrollCalculationOrchestratorPortable orchestrator,
        IAsistenciaCalculationService asistenciaService,
        IDeduccionPrioridadEngine deduccionEngine)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _asistenciaService = asistenciaService ?? throw new ArgumentNullException(nameof(asistenciaService));
        _deduccionEngine = deduccionEngine ?? throw new ArgumentNullException(nameof(deduccionEngine));
    }

    /// <summary>
    /// Calcula la planilla para un empleado específico incluyendo deducciones adicionales
    /// con prelacion legal, proteccion de salario minimo, y conceptos de asistencia.
    /// </summary>
    public async Task<(PayrollDetail detail, List<int> prestamoIds, List<int> anticipoIds, List<HoraExtra> horasExtra, List<Ausencia> ausencias, List<SolicitudVacaciones> vacaciones, DeduccionesResult deduccionesResult)> CalculateForEmployeeAsync(
        int companyId,
        Empleado empleado,
        DateTime payrollPeriodStart,
        DateTime payrollPeriodEnd,
        int payrollHeaderId)
    {
        // ====================================================================
        // PASO 1: Calcular conceptos de asistencia
        // ====================================================================

        decimal salarioMensual = empleado.SalarioBase;
        decimal salarioHora = _asistenciaService.CalcularSalarioHora(salarioMensual, empleado.HoursPerWeek);
        decimal salarioDiario = _asistenciaService.CalcularSalarioDiario(salarioMensual);

        var horasExtra = await _asistenciaService.GetHorasExtraAprobadas(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (montoHorasExtra, horasDiurnas, horasNocturnas, horasDomingoFeriado) =
            await _asistenciaService.CalcularMontoHorasExtra(empleado.Id, salarioHora, payrollPeriodStart, payrollPeriodEnd);

        var ausencias = await _asistenciaService.GetAusenciasDelPeriodo(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (descuentoAusencias, diasAusencia) =
            await _asistenciaService.CalcularDescuentoAusencias(empleado.Id, salarioDiario, payrollPeriodStart, payrollPeriodEnd);

        var vacaciones = await _asistenciaService.GetVacacionesDelPeriodo(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (montoVacaciones, diasVacaciones) =
            await _asistenciaService.CalcularVacaciones(empleado.Id, salarioDiario, payrollPeriodStart, payrollPeriodEnd);

        // ====================================================================
        // PASO 2: Calcular GrossPay ajustado con asistencia
        // ====================================================================

        decimal salarioPeriodo = empleado.GetSalarioPeriodo();
        decimal grossPayAjustado = salarioPeriodo + montoHorasExtra - descuentoAusencias;

        // ====================================================================
        // PASO 3: Calcular deducciones legales (CSS, SE, ISR)
        // ====================================================================

        var payrollResult = await _orchestrator.CalculateEmployeePayrollAsync(
            companyId,
            grossPayAjustado,
            empleado.PayFrequency,
            empleado.YearsCotized,
            empleado.AverageSalaryLast10Years > 0 ? empleado.AverageSalaryLast10Years : grossPayAjustado,
            empleado.CssRiskPercentage,
            empleado.Dependents,
            empleado.IsSubjectToCss,
            empleado.IsSubjectToEducationalInsurance,
            empleado.IsSubjectToIncomeTax,
            payrollPeriodStart
        );

        // ====================================================================
        // PASO 4: Calcular deducciones adicionales con motor de prelacion
        // ====================================================================

        decimal netoPostLegal = grossPayAjustado
            - payrollResult.CssEmployee
            - payrollResult.EducationalInsuranceEmployee
            - payrollResult.IncomeTax;

        var deduccionesResult = await GetDeduccionesAdicionalesConPrelacionAsync(
            empleado.Id, grossPayAjustado, netoPostLegal,
            empleado.PayPeriodType, payrollPeriodStart);

        // ====================================================================
        // PASO 5: Calcular totales
        // ====================================================================

        decimal totalDeductions = payrollResult.CssEmployee +
                                  payrollResult.EducationalInsuranceEmployee +
                                  payrollResult.IncomeTax +
                                  deduccionesResult.TotalDeduccionesAdicionales;

        decimal netPay = grossPayAjustado - totalDeductions;

        // ====================================================================
        // PASO 6: Crear el detalle de planilla con todos los conceptos
        // ====================================================================

        var detail = new PayrollDetail
        {
            PayrollHeaderId = payrollHeaderId,
            EmpleadoId = empleado.Id,

            // Salario bruto
            GrossPay = grossPayAjustado,
            BaseSalary = empleado.SalarioBase,
            OvertimePay = montoHorasExtra,
            Bonuses = 0,
            Commissions = 0,

            // Deducciones legales
            CssEmployee = payrollResult.CssEmployee,
            CssEmployer = payrollResult.CssEmployer,
            RiskContribution = payrollResult.RiskContribution,
            EducationalInsuranceEmployee = payrollResult.EducationalInsuranceEmployee,
            EducationalInsuranceEmployer = payrollResult.EducationalInsuranceEmployer,
            IncomeTax = payrollResult.IncomeTax,

            // Deducciones adicionales - totales legacy
            OtherDeductions = 0,
            DeduccionesFijas = deduccionesResult.TotalDeduccionesAdicionales - deduccionesResult.TotalPrestamos - deduccionesResult.TotalAnticipos,
            Prestamos = deduccionesResult.TotalPrestamos,
            Anticipos = deduccionesResult.TotalAnticipos,

            // Deducciones adicionales - desglose por categoria
            PensionAlimenticia = deduccionesResult.TotalPensionAlimenticia,
            Embargos = deduccionesResult.TotalEmbargos,
            DeduccionesVoluntarias = deduccionesResult.TotalVoluntarias,
            SalarioMinimoLegalAplicado = deduccionesResult.SalarioMinimoAplicado,
            MontoLimitadoPorSalarioMinimo = deduccionesResult.MontoLimitadoPorSalarioMinimo,
            TuvoLimitacionSalarioMinimo = deduccionesResult.TuvoLimitacion,

            // Asistencia
            HorasExtraDiurnas = horasDiurnas,
            HorasExtraNocturnas = horasNocturnas,
            HorasExtraDomingoFeriado = horasDomingoFeriado,
            MontoHorasExtra = montoHorasExtra,
            DiasAusenciaInjustificada = diasAusencia,
            MontoDescuentoAusencias = descuentoAusencias,
            DiasVacaciones = diasVacaciones,
            MontoVacaciones = montoVacaciones,

            // Totales
            TotalDeductions = totalDeductions,
            NetPay = netPay,
            EmployerCost = payrollResult.TotalEmployerCost,

            CreatedAt = DateTime.UtcNow
        };

        return (detail, deduccionesResult.PrestamoIds, deduccionesResult.AnticipoIds, horasExtra, ausencias, vacaciones, deduccionesResult);
    }

    /// <summary>
    /// Obtiene y calcula todas las deducciones adicionales con motor de prelacion
    /// y proteccion de salario minimo.
    /// </summary>
    private async Task<DeduccionesResult> GetDeduccionesAdicionalesConPrelacionAsync(
        int empleadoId, decimal grossPay, decimal netoPostLegal,
        PayPeriodType payPeriodType, DateTime fechaPlanilla)
    {
        // Cargar salario minimo legal de la configuracion vigente
        var taxConfig = await _context.PayrollTaxConfigurations
            .Where(c => c.IsActive &&
                        c.EffectiveStartDate <= fechaPlanilla &&
                        (c.EffectiveEndDate == null || c.EffectiveEndDate >= fechaPlanilla))
            .OrderByDescending(c => c.EffectiveStartDate)
            .FirstOrDefaultAsync();

        decimal salarioMinimoMensual = taxConfig?.SalarioMinimoLegal ?? 700.00m;
        decimal salarioMinimoPeriodo = DeduccionPrioridadEngine.ProrratearSalarioMinimo(salarioMinimoMensual, payPeriodType);

        // Convertir las 3 fuentes a DeduccionPendiente unificado
        var deduccionesPendientes = new List<DeduccionPendiente>();

        // 1. Deducciones fijas activas (excluir ordenes levantadas)
        var deduccionesFijas = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId &&
                        d.EstaActivo &&
                        d.FechaInicio <= fechaPlanilla &&
                        (d.FechaFin == null || d.FechaFin >= fechaPlanilla) &&
                        (d.EstadoOrdenJudicial == null || d.EstadoOrdenJudicial != EstadoOrdenJudicial.Levantada))
            .OrderBy(d => d.Prioridad)
            .ToListAsync();

        foreach (var df in deduccionesFijas)
        {
            // Determinar categoria segun tipo de deduccion
            var categoria = InferirCategoria(df);

            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenDeduccionFijaId = df.Id,
                TipoDeduccion = df.TipoDeduccion,
                Categoria = categoria,
                Descripcion = df.Descripcion,
                MontoFijo = df.Monto,
                Porcentaje = df.Porcentaje,
                EsPorcentaje = df.EsPorcentaje,
                BaseCalculo = df.BaseCalculo,
                Prioridad = df.Prioridad,
                NombreAcreedor = df.NombreAcreedor,
                MontoTotalACobrar = df.MontoTotalACobrar,
                MontoCobradoAcumulado = df.MontoCobradoAcumulado
            });
        }

        // 2. Prestamos activos con cuotas pendientes
        var prestamos = await _context.Prestamos
            .Where(p => p.EmpleadoId == empleadoId &&
                        p.Estado == EstadoPrestamo.Activo &&
                        p.CuotasPagadas < p.NumeroCuotas)
            .ToListAsync();

        foreach (var prestamo in prestamos)
        {
            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenPrestamoId = prestamo.Id,
                TipoDeduccion = TipoDeduccion.PrestamoInterno,
                Categoria = CategoriaDeduccion.Voluntaria,
                Descripcion = $"Prestamo #{prestamo.Id} - Cuota {prestamo.CuotasPagadas + 1}/{prestamo.NumeroCuotas}",
                MontoFijo = prestamo.CuotaMensual,
                EsPorcentaje = false,
                BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
                Prioridad = 100,
                MontoTotalACobrar = prestamo.MontoOriginal,
                MontoCobradoAcumulado = prestamo.MontoOriginal - prestamo.MontoPendiente
            });
        }

        // 3. Anticipos aprobados para esta fecha
        var anticipos = await _context.Anticipos
            .Where(a => a.EmpleadoId == empleadoId &&
                        a.Estado == EstadoAnticipo.Aprobado &&
                        a.FechaDescuento.Date == fechaPlanilla.Date)
            .ToListAsync();

        foreach (var anticipo in anticipos)
        {
            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenAnticipoId = anticipo.Id,
                TipoDeduccion = TipoDeduccion.Otro,
                Categoria = CategoriaDeduccion.Voluntaria,
                Descripcion = $"Anticipo #{anticipo.Id} - {anticipo.Motivo}",
                MontoFijo = anticipo.Monto,
                EsPorcentaje = false,
                BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
                Prioridad = 200
            });
        }

        // Aplicar motor de prelacion
        return _deduccionEngine.AplicarDeduccionesConPrelacion(
            grossPay, netoPostLegal, salarioMinimoPeriodo, deduccionesPendientes);
    }

    /// <summary>
    /// Infiere la categoria de prelacion segun el tipo de deduccion.
    /// </summary>
    private static CategoriaDeduccion InferirCategoria(DeduccionFija df)
    {
        // Si la categoria fue establecida explicitamente, usarla
        if (df.Categoria != CategoriaDeduccion.Voluntaria || df.TipoDeduccion == TipoDeduccion.PensionAlimenticia || df.TipoDeduccion == TipoDeduccion.Embargo)
        {
            return df.TipoDeduccion switch
            {
                TipoDeduccion.PensionAlimenticia => CategoriaDeduccion.PensionAlimenticia,
                TipoDeduccion.Embargo => CategoriaDeduccion.EmbargoJudicial,
                // PrestamoBancario con orden judicial es embargo
                TipoDeduccion.PrestamoBancario when df.NumeroExpediente != null => CategoriaDeduccion.EmbargoJudicial,
                _ => df.Categoria
            };
        }

        return df.Categoria;
    }

    /// <summary>
    /// Procesa los pagos de préstamos asociados a un detalle de planilla
    /// </summary>
    public async Task ProcessPrestamosAsync(List<int> prestamoIds, int payrollDetailId, int payrollHeaderId)
    {
        foreach (var prestamoId in prestamoIds)
        {
            var prestamo = await _context.Prestamos.FindAsync(prestamoId);
            if (prestamo == null) continue;

            var pago = new PagoPrestamo
            {
                PrestamoId = prestamoId,
                PlanillaDetailId = payrollDetailId,
                FechaPago = DateTime.UtcNow,
                MontoPagado = prestamo.CuotaMensual,
                SaldoAnterior = prestamo.MontoPendiente,
                SaldoNuevo = prestamo.MontoPendiente - prestamo.CuotaMensual,
                NumeroCuota = prestamo.CuotasPagadas + 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.PagosPrestamos.Add(pago);

            prestamo.MontoPendiente -= prestamo.CuotaMensual;
            prestamo.CuotasPagadas++;
            prestamo.UpdatedAt = DateTime.UtcNow;

            if (prestamo.CuotasPagadas >= prestamo.NumeroCuotas)
            {
                prestamo.Estado = EstadoPrestamo.Pagado;
                prestamo.MontoPendiente = 0;
            }

            _context.Prestamos.Update(prestamo);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Procesa los anticipos asociados a un detalle de planilla
    /// </summary>
    public async Task ProcessAnticiposAsync(List<int> anticipoIds, int payrollDetailId, int payrollHeaderId)
    {
        foreach (var anticipoId in anticipoIds)
        {
            var anticipo = await _context.Anticipos.FindAsync(anticipoId);
            if (anticipo == null) continue;

            anticipo.Estado = EstadoAnticipo.Descontado;
            anticipo.PlanillaId = payrollHeaderId;
            anticipo.UpdatedAt = DateTime.UtcNow;

            _context.Anticipos.Update(anticipo);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Crea registros DeduccionAplicada para auditoria y actualiza MontoCobradoAcumulado.
    /// </summary>
    public async Task CreateDeduccionesAplicadasAsync(PayrollDetail detail, DeduccionesResult deduccionesResult)
    {
        foreach (var item in deduccionesResult.Detalle)
        {
            var deduccionAplicada = new DeduccionAplicada
            {
                TenantId = detail.TenantId,
                PayrollDetailId = detail.Id,
                DeduccionFijaId = item.OrigenDeduccionFijaId,
                PrestamoId = item.OrigenPrestamoId,
                AnticipoId = item.OrigenAnticipoId,
                TipoDeduccion = item.TipoDeduccion,
                Categoria = item.Categoria,
                Descripcion = item.Descripcion,
                MontoSolicitado = item.MontoSolicitado,
                MontoAplicado = item.MontoAplicado,
                MontoLimitado = item.MontoLimitado,
                RazonLimitacion = item.RazonLimitacion,
                SaldoDisponibleAntes = item.SaldoDisponibleAntes,
                SaldoDisponibleDespues = item.SaldoDisponibleDespues,
                OrdenAplicacion = item.OrdenAplicacion,
                NombreAcreedor = item.NombreAcreedor,
                CreatedAt = DateTime.UtcNow
            };

            _context.DeduccionesAplicadas.Add(deduccionAplicada);

            // Actualizar MontoCobradoAcumulado en DeduccionesFijas con MontoTotalACobrar
            if (item.OrigenDeduccionFijaId.HasValue && item.MontoAplicado > 0)
            {
                var df = await _context.DeduccionesFijas.FindAsync(item.OrigenDeduccionFijaId.Value);
                if (df?.MontoTotalACobrar.HasValue == true)
                {
                    df.MontoCobradoAcumulado += item.MontoAplicado;
                    // Auto-desactivar si ya se cobro el total
                    if (df.MontoCobradoAcumulado >= df.MontoTotalACobrar.Value)
                    {
                        df.EstaActivo = false;
                        df.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Procesa la planilla completa para un empleado, calculando deducciones, conceptos de asistencia
    /// y actualizando estados de todas las entidades relacionadas.
    /// </summary>
    public async Task<PayrollDetail> ProcessEmployeePayrollAsync(
        int companyId,
        Empleado empleado,
        DateTime payrollPeriodStart,
        DateTime payrollPeriodEnd,
        int payrollHeaderId)
    {
        var (detail, prestamoIds, anticipoIds, horasExtra, ausencias, vacaciones, deduccionesResult) = await CalculateForEmployeeAsync(
            companyId,
            empleado,
            payrollPeriodStart,
            payrollPeriodEnd,
            payrollHeaderId
        );

        // Guardar el detalle de planilla
        _context.PayrollDetails.Add(detail);
        await _context.SaveChangesAsync();

        // Persistir auditoría de deducciones aplicadas (para reportes)
        await CreateDeduccionesAplicadasAsync(detail, deduccionesResult);

        // Procesar préstamos y anticipos
        await ProcessPrestamosAsync(prestamoIds, detail.Id, payrollHeaderId);
        await ProcessAnticiposAsync(anticipoIds, detail.Id, payrollHeaderId);

        // Procesar conceptos de asistencia
        await _asistenciaService.MarcarHorasExtraPagadas(horasExtra, detail.Id);
        await _asistenciaService.MarcarAusenciasProcesadas(ausencias, detail.Id);
        await _asistenciaService.MarcarVacacionesPagadas(vacaciones, detail.Id);

        await _context.SaveChangesAsync();

        return detail;
    }
}
