// ====================================================================
// Planilla - PayrollProcessingService
// Creado: 2025-12-27
// Actualizado: 2025-12-28 - Integración de asistencia
// Descripción: Servicio de procesamiento de planilla con deducciones adicionales
// Integra préstamos, deducciones fijas, anticipos, horas extra, ausencias y vacaciones
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio que procesa planillas completas incluyendo deducciones adicionales
/// y conceptos de asistencia (horas extra, ausencias, vacaciones).
/// </summary>
public class PayrollProcessingService
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollCalculationOrchestratorPortable _orchestrator;
    private readonly AsistenciaCalculationService _asistenciaService;

    public PayrollProcessingService(
        ApplicationDbContext context,
        PayrollCalculationOrchestratorPortable orchestrator,
        AsistenciaCalculationService asistenciaService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _asistenciaService = asistenciaService ?? throw new ArgumentNullException(nameof(asistenciaService));
    }

    /// <summary>
    /// Calcula la planilla para un empleado específico incluyendo deducciones adicionales
    /// y conceptos de asistencia (horas extra, ausencias, vacaciones).
    /// </summary>
    public async Task<(PayrollDetail detail, List<int> prestamoIds, List<int> anticipoIds, List<HoraExtra> horasExtra, List<Ausencia> ausencias, List<SolicitudVacaciones> vacaciones)> CalculateForEmployeeAsync(
        int companyId,
        Empleado empleado,
        DateTime payrollPeriodStart,
        DateTime payrollPeriodEnd,
        int payrollHeaderId)
    {
        // ====================================================================
        // PASO 1: Calcular conceptos de asistencia
        // ====================================================================

        // Calcular salario hora y diario para conceptos de asistencia
        // SalarioBase ya es mensual
        decimal salarioMensual = empleado.SalarioBase;
        decimal salarioHora = _asistenciaService.CalcularSalarioHora(salarioMensual, empleado.HoursPerWeek);
        decimal salarioDiario = _asistenciaService.CalcularSalarioDiario(salarioMensual);

        // Horas extra aprobadas del período
        var horasExtra = await _asistenciaService.GetHorasExtraAprobadas(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (montoHorasExtra, horasDiurnas, horasNocturnas, horasDomingoFeriado) =
            await _asistenciaService.CalcularMontoHorasExtra(empleado.Id, salarioHora, payrollPeriodStart, payrollPeriodEnd);

        // Ausencias del período que afectan salario
        var ausencias = await _asistenciaService.GetAusenciasDelPeriodo(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (descuentoAusencias, diasAusencia) =
            await _asistenciaService.CalcularDescuentoAusencias(empleado.Id, salarioDiario, payrollPeriodStart, payrollPeriodEnd);

        // Vacaciones del período
        var vacaciones = await _asistenciaService.GetVacacionesDelPeriodo(empleado.Id, payrollPeriodStart, payrollPeriodEnd);
        var (montoVacaciones, diasVacaciones) =
            await _asistenciaService.CalcularVacaciones(empleado.Id, salarioDiario, payrollPeriodStart, payrollPeriodEnd);

        // ====================================================================
        // PASO 2: Calcular GrossPay ajustado con asistencia
        // ====================================================================

        // GrossPay ajustado = salario del período + horasExtra - ausencias
        // SalarioBase es mensual, necesitamos el salario del período para esta planilla
        // (Las vacaciones ya están incluidas en el salario base en Panamá)
        decimal salarioPeriodo = empleado.GetSalarioPeriodo();
        decimal grossPayAjustado = salarioPeriodo + montoHorasExtra - descuentoAusencias;

        // ====================================================================
        // PASO 3: Calcular deducciones básicas (CSS, SE, ISR)
        // ====================================================================

        var payrollResult = await _orchestrator.CalculateEmployeePayrollAsync(
            companyId,
            grossPayAjustado,  // Usar salario ajustado con asistencia
            "Quincenal", // TODO: Obtener de configuración del empleado
            0, // TODO: Obtener años cotizados del empleado
            grossPayAjustado, // TODO: Obtener promedio últimos 10 años
            0.56m, // TODO: Obtener nivel de riesgo del empleado
            0, // TODO: Obtener dependientes del empleado
            true, // isSubjectToCss
            true, // isSubjectToEducationalInsurance
            true, // isSubjectToIncomeTax
            payrollPeriodStart
        );

        // ====================================================================
        // PASO 4: Calcular deducciones adicionales (préstamos, deducciones fijas, anticipos)
        // ====================================================================

        var (deduccionesFijas, prestamos, anticipos, prestamoIds, anticipoIds) =
            await GetDeduccionesAdicionalesAsync(empleado.Id, payrollPeriodStart);

        // ====================================================================
        // PASO 5: Calcular totales
        // ====================================================================

        decimal totalDeductions = payrollResult.CssEmployee +
                                  payrollResult.EducationalInsuranceEmployee +
                                  payrollResult.IncomeTax +
                                  deduccionesFijas +
                                  prestamos +
                                  anticipos +
                                  descuentoAusencias; // Las ausencias también son deducción

        decimal netPay = grossPayAjustado - totalDeductions + descuentoAusencias; // Compensar ausencias ya restadas del bruto

        // ====================================================================
        // PASO 6: Crear el detalle de planilla con todos los conceptos
        // ====================================================================

        var detail = new PayrollDetail
        {
            PayrollHeaderId = payrollHeaderId,
            EmpleadoId = empleado.Id,

            // Salario bruto (ya incluye ajustes de asistencia)
            GrossPay = grossPayAjustado,
            BaseSalary = empleado.SalarioBase,
            OvertimePay = montoHorasExtra,
            Bonuses = 0, // TODO: Calcular bonificaciones
            Commissions = 0, // TODO: Calcular comisiones

            // Deducciones básicas
            CssEmployee = payrollResult.CssEmployee,
            CssEmployer = payrollResult.CssEmployer,
            RiskContribution = payrollResult.RiskContribution,
            EducationalInsuranceEmployee = payrollResult.EducationalInsuranceEmployee,
            EducationalInsuranceEmployer = payrollResult.EducationalInsuranceEmployer,
            IncomeTax = payrollResult.IncomeTax,

            // Deducciones adicionales
            OtherDeductions = 0,
            DeduccionesFijas = deduccionesFijas,
            Prestamos = prestamos,
            Anticipos = anticipos,

            // Asistencia: Horas Extra
            HorasExtraDiurnas = horasDiurnas,
            HorasExtraNocturnas = horasNocturnas,
            HorasExtraDomingoFeriado = horasDomingoFeriado,
            MontoHorasExtra = montoHorasExtra,

            // Asistencia: Ausencias
            DiasAusenciaInjustificada = diasAusencia,
            MontoDescuentoAusencias = descuentoAusencias,

            // Asistencia: Vacaciones
            DiasVacaciones = diasVacaciones,
            MontoVacaciones = montoVacaciones,

            // Totales
            TotalDeductions = totalDeductions,
            NetPay = netPay,
            EmployerCost = payrollResult.TotalEmployerCost,

            CreatedAt = DateTime.UtcNow
        };

        return (detail, prestamoIds, anticipoIds, horasExtra, ausencias, vacaciones);
    }

    /// <summary>
    /// Obtiene y calcula todas las deducciones adicionales aplicables a un empleado en una fecha específica
    /// </summary>
    private async Task<(decimal deduccionesFijas, decimal prestamos, decimal anticipos, List<int> prestamoIds, List<int> anticipoIds)>
        GetDeduccionesAdicionalesAsync(int empleadoId, DateTime fechaPlanilla)
    {
        decimal totalDeduccionesFijas = 0;
        decimal totalPrestamos = 0;
        decimal totalAnticipos = 0;
        var prestamoIds = new List<int>();
        var anticipoIds = new List<int>();

        // ====================================================================
        // 1. Obtener deducciones fijas activas
        // ====================================================================
        var deducciones = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId &&
                        d.EstaActivo &&
                        d.FechaInicio <= fechaPlanilla &&
                        (d.FechaFin == null || d.FechaFin >= fechaPlanilla))
            .OrderBy(d => d.Prioridad)
            .ToListAsync();

        foreach (var deduccion in deducciones)
        {
            if (deduccion.EsPorcentaje && deduccion.Porcentaje.HasValue)
            {
                // Calcular porcentaje sobre salario bruto
                var empleado = await _context.Empleados.FindAsync(empleadoId);
                if (empleado != null)
                {
                    totalDeduccionesFijas += empleado.SalarioBase * (deduccion.Porcentaje.Value / 100);
                }
            }
            else
            {
                totalDeduccionesFijas += deduccion.Monto;
            }
        }

        // ====================================================================
        // 2. Obtener préstamos activos con cuotas pendientes
        // ====================================================================
        var prestamos = await _context.Prestamos
            .Where(p => p.EmpleadoId == empleadoId &&
                        p.Estado == EstadoPrestamo.Activo &&
                        p.CuotasPagadas < p.NumeroCuotas)
            .ToListAsync();

        foreach (var prestamo in prestamos)
        {
            totalPrestamos += prestamo.CuotaMensual;
            prestamoIds.Add(prestamo.Id);
        }

        // ====================================================================
        // 3. Obtener anticipos aprobados para esta fecha
        // ====================================================================
        var anticipos = await _context.Anticipos
            .Where(a => a.EmpleadoId == empleadoId &&
                        a.Estado == EstadoAnticipo.Aprobado &&
                        a.FechaDescuento.Date == fechaPlanilla.Date)
            .ToListAsync();

        foreach (var anticipo in anticipos)
        {
            totalAnticipos += anticipo.Monto;
            anticipoIds.Add(anticipo.Id);
        }

        return (totalDeduccionesFijas, totalPrestamos, totalAnticipos, prestamoIds, anticipoIds);
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

            // Crear registro de pago
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

            // Actualizar préstamo
            prestamo.MontoPendiente -= prestamo.CuotaMensual;
            prestamo.CuotasPagadas++;
            prestamo.UpdatedAt = DateTime.UtcNow;

            // Si ya pagó todas las cuotas, marcar como pagado
            if (prestamo.CuotasPagadas >= prestamo.NumeroCuotas)
            {
                prestamo.Estado = EstadoPrestamo.Pagado;
                prestamo.MontoPendiente = 0; // Asegurar que quede en 0
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

            // Marcar anticipo como descontado
            anticipo.Estado = EstadoAnticipo.Descontado;
            anticipo.PlanillaId = payrollHeaderId;
            anticipo.UpdatedAt = DateTime.UtcNow;

            _context.Anticipos.Update(anticipo);
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
        // Calcular planilla con deducciones adicionales y conceptos de asistencia
        var (detail, prestamoIds, anticipoIds, horasExtra, ausencias, vacaciones) = await CalculateForEmployeeAsync(
            companyId,
            empleado,
            payrollPeriodStart,
            payrollPeriodEnd,
            payrollHeaderId
        );

        // Guardar el detalle de planilla
        _context.PayrollDetails.Add(detail);
        await _context.SaveChangesAsync();

        // Procesar préstamos y anticipos
        await ProcessPrestamosAsync(prestamoIds, detail.Id, payrollHeaderId);
        await ProcessAnticiposAsync(anticipoIds, detail.Id, payrollHeaderId);

        // Procesar conceptos de asistencia (marcar como pagados/procesados)
        await _asistenciaService.MarcarHorasExtraPagadas(horasExtra, detail.Id);
        await _asistenciaService.MarcarAusenciasProcesadas(ausencias, detail.Id);
        await _asistenciaService.MarcarVacacionesPagadas(vacaciones, detail.Id);

        // Guardar cambios de asistencia
        await _context.SaveChangesAsync();

        return detail;
    }
}
