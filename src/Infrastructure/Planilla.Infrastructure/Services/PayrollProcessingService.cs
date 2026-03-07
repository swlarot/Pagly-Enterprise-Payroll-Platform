// ====================================================================
// Planilla - PayrollProcessingService
// Creado: 2025-12-27
// Actualizado: 2026-02-16 - Motor de prelacion con proteccion salario minimo
// Descripción: Servicio de procesamiento de planilla con deducciones adicionales
// Integra préstamos, deducciones fijas, anticipos, horas extra, ausencias y vacaciones
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
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

        var horasRegistradas = await _context.PayrollEmployeeHours
            .FirstOrDefaultAsync(h => h.PayrollHeaderId == payrollHeaderId && h.EmpleadoId == empleado.Id);
        decimal comisiones = horasRegistradas?.Commissions ?? 0m;

        decimal salarioPeriodo = empleado.GetSalarioPeriodo();
        decimal grossPayAjustado = salarioPeriodo + montoHorasExtra - descuentoAusencias + comisiones;

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
            Commissions = comisiones,

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
    /// DEV-35: Calcula el detalle de planilla para un empleado usando datos pre-cargados en memoria.
    /// Elimina N+1 queries del loop de cálculo. El controller pre-carga los datos antes del loop
    /// y llama a este método por empleado.
    /// </summary>
    public async Task<(PayrollDetail detail, DeduccionesResult deduccionesResult)> CalculateFromPreloadedDataAsync(
        int tenantId,
        Empleado employee,
        PayrollHeader payrollHeader,
        PayrollEmployeeHours? hours,
        List<DeduccionFija> deduccionesFijas,
        List<Prestamo> prestamosActivos,
        List<Anticipo> anticiposAprobados,
        decimal salarioMinimoPeriodo)
    {
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payrollHeader.PayPeriodType);

        // ====================================================================
        // PASO 1: Calcular GrossPay
        // ====================================================================
        decimal grossPay = employee.GetSalarioPeriodo();

        if (hours != null)
        {
            // DEV-87: SundayHours y HolidayHours deben ser subconjuntos de RegularHours.
            // Si superan el total, el cálculo de GrossPay sería incorrecto silenciosamente.
            if (hours.SundayHours + hours.HolidayHours > hours.RegularHours)
            {
                throw new InvalidOperationException(
                    $"Las horas de domingo ({hours.SundayHours}) + feriado ({hours.HolidayHours}) " +
                    $"no pueden exceder las horas regulares totales ({hours.RegularHours}) " +
                    $"para el empleado {employee.Id}.");
            }

            var hourlyRate = employee.HourlyRate > 0
                ? employee.HourlyRate
                : Empleado.ComputeHourlyRateFromMonthly(employee.SalarioBase, employee.HoursPerWeek);

            // DEV-88: RegularPay excluye horas de domingo y feriado (son subconjuntos).
            // SundayPay y HolidayPay usan tasa completa (base + recargo), por eso se excluyen de RegularPay.
            hours.RegularPay = (hours.RegularHours - hours.SundayHours - hours.HolidayHours) * hourlyRate;
            // Snap a salario exacto solo cuando no hay horas especiales (evita truncar la diferencia real)
            if (hours.SundayHours == 0 && hours.HolidayHours == 0)
            {
                var salarioPeriodoExacto = employee.GetSalarioPeriodo();
                if (Math.Abs(hours.RegularPay - salarioPeriodoExacto) < 0.05m)
                    hours.RegularPay = salarioPeriodoExacto;
            }

            hours.SundayPay = hours.SundayHours * hourlyRate * 1.50m;   // Art. 26: tasa completa 1.50x (base + recargo 50%)
            hours.HolidayPay = hours.HolidayHours * hourlyRate * 2.50m; // Art. 49: tasa completa 2.50x (base + recargo 150%)
            hours.OvertimeDayPay = hours.OvertimeDayHours * hourlyRate * 1.25m;
            hours.OvertimeNightPay = hours.OvertimeNightHours * hourlyRate * 1.50m;
            hours.OvertimeHolidayPay = hours.OvertimeHolidayHours * hourlyRate * 3.125m;
            // DEV-27: Mixta 1.50x (D→N). Para 1.75x (N→D) haría falta campo/convención en PayrollEmployeeHours.
            hours.OvertimeMixedPay = hours.OvertimeMixedHours * hourlyRate * 1.50m;
            hours.OvertimeExcessPay = hours.OvertimeExcessHours * hourlyRate * 2.1875m;
            hours.AbsenceDeduction = hours.AbsenceHours * hourlyRate;
            hours.TotalHoursPay = hours.RegularPay + hours.SundayPay + hours.HolidayPay
                + hours.OvertimeDayPay + hours.OvertimeNightPay
                + hours.OvertimeHolidayPay + hours.OvertimeMixedPay + hours.OvertimeExcessPay
                - hours.AbsenceDeduction;
            hours.UpdatedAt = DateTime.UtcNow;

            grossPay = hours.TotalHoursPay + hours.Commissions;
        }

        // ====================================================================
        // PASO 2: Deducciones legales CSS + SE + ISR
        // DEV-24 FIX: usar PayPeriodType de la planilla, no PayFrequency del empleado
        // ====================================================================
        var calculationResult = await _orchestrator.CalculateEmployeePayrollAsync(
            companyId: tenantId,
            grossPay: grossPay,
            payFrequency: payrollHeader.PayPeriodType.ToString(),
            yearsCotized: employee.YearsCotized,
            averageSalaryLast10Years: employee.AverageSalaryLast10Years,
            cssRiskPercentage: employee.CssRiskPercentage,
            dependents: employee.Dependents,
            isSubjectToCss: employee.IsSubjectToCss,
            isSubjectToEducationalInsurance: employee.IsSubjectToEducationalInsurance,
            isSubjectToIncomeTax: employee.IsSubjectToIncomeTax,
            calculationDate: DateTime.UtcNow
        );

        // ====================================================================
        // PASO 3: Deducciones adicionales con motor de prelación (datos pre-cargados)
        // ====================================================================
        decimal netoPostLegal = grossPay
            - calculationResult.CssEmployee
            - calculationResult.EducationalInsuranceEmployee
            - calculationResult.IncomeTax;

        var deduccionesPendientes = new List<DeduccionPendiente>();

        foreach (var df in deduccionesFijas)
        {
            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenDeduccionFijaId = df.Id,
                TipoDeduccion = df.TipoDeduccion,
                Categoria = InferirCategoria(df),
                Descripcion = df.Descripcion,
                MontoFijo = RoundingPolicy.Round(df.Monto * 12m / periodsPerYear),
                Porcentaje = df.Porcentaje,
                EsPorcentaje = df.EsPorcentaje,
                BaseCalculo = df.BaseCalculo,
                Prioridad = df.Prioridad,
                NombreAcreedor = df.NombreAcreedor,
                MontoTotalACobrar = df.MontoTotalACobrar,
                MontoCobradoAcumulado = df.MontoCobradoAcumulado
            });
        }

        foreach (var prestamo in prestamosActivos)
        {
            var montoCuotaPeriodo = Math.Round(prestamo.CuotaMensual * 12m / periodsPerYear, 2);
            montoCuotaPeriodo = Math.Min(montoCuotaPeriodo, prestamo.MontoPendiente);

            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenPrestamoId = prestamo.Id,
                TipoDeduccion = TipoDeduccion.PrestamoInterno,
                Categoria = CategoriaDeduccion.Voluntaria,
                Descripcion = $"Prestamo #{prestamo.Id} - Cuota {prestamo.CuotasPagadas + 1}/{prestamo.NumeroCuotas}",
                MontoFijo = montoCuotaPeriodo,
                EsPorcentaje = false,
                BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
                Prioridad = 100
            });
        }

        foreach (var anticipo in anticiposAprobados)
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

        var deduccionesResult = _deduccionEngine.AplicarDeduccionesConPrelacion(
            grossPay, netoPostLegal, salarioMinimoPeriodo, deduccionesPendientes);

        // ====================================================================
        // PASO 4: Horas extra aprobadas del período (desde asistenciaService)
        // ====================================================================
        decimal overtimePay = 0m;
        decimal horasExtraDiurnas = 0m;
        decimal horasExtraNocturnas = 0m;
        decimal horasExtraDomingoFeriado = 0m;

        var horasExtraAprobadas = await _asistenciaService.GetHorasExtraAprobadas(
            employee.Id, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

        if (horasExtraAprobadas.Any())
        {
            var salarioHora = employee.HourlyRate > 0
                ? employee.HourlyRate
                : Empleado.ComputeHourlyRateFromMonthly(employee.SalarioBase, employee.HoursPerWeek);

            var (montoHorasExtra, horasDiurnas, horasNocturnas, horasDomFeriado) =
                await _asistenciaService.CalcularMontoHorasExtra(
                    employee.Id, salarioHora, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

            overtimePay = montoHorasExtra;
            horasExtraDiurnas = horasDiurnas;
            horasExtraNocturnas = horasNocturnas;
            horasExtraDomingoFeriado = horasDomFeriado;
        }
        else if (hours != null)
        {
            overtimePay = hours.OvertimeDayPay + hours.OvertimeNightPay;
            horasExtraDiurnas = hours.OvertimeDayHours;
            horasExtraNocturnas = hours.OvertimeNightHours;
        }

        // ====================================================================
        // PASO 5: Construir PayrollDetail
        // ====================================================================
        decimal totalDedsForEmployee = calculationResult.TotalDeductions + deduccionesResult.TotalDeduccionesAdicionales;
        decimal netPayForEmployee = calculationResult.NetPay - deduccionesResult.TotalDeduccionesAdicionales;

        var detail = new PayrollDetail
        {
            PayrollHeaderId = payrollHeader.Id,
            EmpleadoId = employee.Id,
            GrossPay = calculationResult.GrossPay,
            BaseSalary = employee.SalarioBase,
            OvertimePay = overtimePay,
            Bonuses = 0,
            Commissions = hours?.Commissions ?? 0,
            HorasExtraDiurnas = horasExtraDiurnas,
            HorasExtraNocturnas = horasExtraNocturnas,
            HorasExtraDomingoFeriado = horasExtraDomingoFeriado,
            MontoHorasExtra = overtimePay,
            CssEmployee = calculationResult.CssEmployee,
            CssEmployer = calculationResult.CssEmployer,
            RiskContribution = calculationResult.RiskContribution,
            EducationalInsuranceEmployee = calculationResult.EducationalInsuranceEmployee,
            EducationalInsuranceEmployer = calculationResult.EducationalInsuranceEmployer,
            IncomeTax = calculationResult.IncomeTax,
            OtherDeductions = 0,
            DeduccionesFijas = deduccionesResult.TotalDeduccionesAdicionales - deduccionesResult.TotalPrestamos - deduccionesResult.TotalAnticipos,
            Prestamos = deduccionesResult.TotalPrestamos,
            Anticipos = deduccionesResult.TotalAnticipos,
            PensionAlimenticia = deduccionesResult.TotalPensionAlimenticia,
            Embargos = deduccionesResult.TotalEmbargos,
            DeduccionesVoluntarias = deduccionesResult.TotalVoluntarias,
            SalarioMinimoLegalAplicado = deduccionesResult.SalarioMinimoAplicado,
            MontoLimitadoPorSalarioMinimo = deduccionesResult.MontoLimitadoPorSalarioMinimo,
            TuvoLimitacionSalarioMinimo = deduccionesResult.TuvoLimitacion,
            TotalDeductions = totalDedsForEmployee,
            NetPay = netPayForEmployee,
            EmployerCost = calculationResult.TotalEmployerCost,
            TenantId = tenantId
        };

        return (detail, deduccionesResult);
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
        var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payPeriodType);

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
                MontoFijo = RoundingPolicy.Round(df.Monto * 12m / periodsPerYear),
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
            var montoCuotaPeriodo = Math.Round(prestamo.CuotaMensual * 12m / periodsPerYear, 2);
            montoCuotaPeriodo = Math.Min(montoCuotaPeriodo, prestamo.MontoPendiente);

            deduccionesPendientes.Add(new DeduccionPendiente
            {
                OrigenPrestamoId = prestamo.Id,
                TipoDeduccion = TipoDeduccion.PrestamoInterno,
                Categoria = CategoriaDeduccion.Voluntaria,
                Descripcion = $"Prestamo #{prestamo.Id} - Cuota {prestamo.CuotasPagadas + 1}/{prestamo.NumeroCuotas}",
                MontoFijo = montoCuotaPeriodo,
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
    /// Procesa los pagos de préstamos asociados a un detalle de planilla.
    /// Usa el monto realmente aplicado (prorrateado por frecuencia de pago) del resultado de deducciones.
    /// </summary>
    public async Task ProcessPrestamosAsync(List<int> prestamoIds, int payrollDetailId, int payrollHeaderId, DeduccionesResult deduccionesResult)
    {
        var montoPorPrestamo = deduccionesResult.Detalle
            .Where(d => d.OrigenPrestamoId.HasValue && d.MontoAplicado > 0m)
            .ToDictionary(d => d.OrigenPrestamoId!.Value, d => d.MontoAplicado);
        await ProcessPrestamosAsync(prestamoIds, payrollDetailId, payrollHeaderId, montoPorPrestamo);
    }

    /// <summary>
    /// Procesa los pagos de préstamos con montos aplicados por préstamo (para flujo Marcar como Pagada desde BD).
    /// </summary>
    public async Task ProcessPrestamosAsync(List<int> prestamoIds, int payrollDetailId, int payrollHeaderId, IReadOnlyDictionary<int, decimal> montoAplicadoPorPrestamoId)
    {
        foreach (var prestamoId in prestamoIds)
        {
            var prestamo = await _context.Prestamos.FindAsync(prestamoId);
            if (prestamo == null) continue;

            if (!montoAplicadoPorPrestamoId.TryGetValue(prestamoId, out var montoAplicado) || montoAplicado <= 0m) continue;

            var saldoAnterior = prestamo.MontoPendiente;
            var saldoNuevo = Math.Max(0m, saldoAnterior - montoAplicado);

            var pago = new PagoPrestamo
            {
                PrestamoId = prestamoId,
                PlanillaDetailId = payrollDetailId,
                FechaPago = DateTime.UtcNow,
                MontoPagado = montoAplicado,
                SaldoAnterior = saldoAnterior,
                SaldoNuevo = saldoNuevo,
                NumeroCuota = prestamo.CuotasPagadas + 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.PagosPrestamos.Add(pago);

            prestamo.MontoPendiente = saldoNuevo;
            prestamo.CuotasPagadas++;
            prestamo.UpdatedAt = DateTime.UtcNow;

            if (prestamo.CuotasPagadas >= prestamo.NumeroCuotas || prestamo.MontoPendiente <= 0m)
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

        // Procesar préstamos (usar monto realmente aplicado/prorrateado) y anticipos
        await ProcessPrestamosAsync(prestamoIds, detail.Id, payrollHeaderId, deduccionesResult);
        await ProcessAnticiposAsync(anticipoIds, detail.Id, payrollHeaderId);

        // Procesar conceptos de asistencia
        await _asistenciaService.MarcarHorasExtraPagadas(horasExtra, detail.Id);
        await _asistenciaService.MarcarAusenciasProcesadas(ausencias, detail.Id);
        await _asistenciaService.MarcarVacacionesPagadas(vacaciones, detail.Id);

        await _context.SaveChangesAsync();

        return detail;
    }
}
