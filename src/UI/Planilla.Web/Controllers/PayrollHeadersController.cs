// ====================================================================
// Planilla - PayrollHeadersController
// Source: Core360 Stage 3
// Creado: 2025-12-26
// Descripción: Controller de workflow de planilla con multi-tenancy seguro
// Endpoints: CRUD + calculate, approve, pay, cancel
// ====================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para gestionar el workflow de planillas con seguridad multi-tenant.
/// Implementa CRUD básico y transiciones de estado (calculate, approve, pay, cancel).
/// </summary>
[Authorize] // ✅ SEGURIDAD: Todos los endpoints requieren autenticación
[ApiController]
[Route("api/[controller]")]
public class PayrollHeadersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly PayrollStateMachine _stateMachine;
    private readonly PayrollCalculationOrchestratorPortable _orchestrator;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly AsistenciaCalculationService _asistenciaService;

    public PayrollHeadersController(
        ApplicationDbContext context,
        PayrollStateMachine stateMachine,
        PayrollCalculationOrchestratorPortable orchestrator,
        ITenantContext tenantContext,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        AsistenciaCalculationService asistenciaService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _asistenciaService = asistenciaService ?? throw new ArgumentNullException(nameof(asistenciaService));
    }

    /// <summary>
    /// Lista todas las planillas del tenant actual con filtros opcionales.
    /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo ve las planillas donde aparece.
    /// GET /api/payrollheaders?status=Calculated
    /// </summary>
    [HttpGet]
    [RequirePermission(SystemPermission.PayrollView, SystemPermission.PayrollViewSelf)]
    public async Task<ActionResult<IEnumerable<PayrollHeader>>> GetPayrollHeaders(
        [FromQuery] PayrollStatus? status)
    {
        var tenantId = _tenantContext.TenantId;
        var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();

        var query = _context.PayrollHeaders
            .Where(p => p.TenantId == tenantId) // ✅ SEGURIDAD: Filtrado por tenant obligatorio
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado) // ✅ CRÍTICO: Incluir empleado para cálculos en frontend
            .AsNoTracking()
            .AsQueryable();

        // 🎯 EMPLOYEE SELF-SERVICE: Filtrar planillas donde el empleado aparece
        if (linkedEmployeeId.HasValue)
        {
            query = query.Where(p => p.Details.Any(d => d.EmpleadoId == linkedEmployeeId.Value));
        }

        // Filtrar por Status si se especifica
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var payrollHeaders = await query
            .OrderByDescending(p => p.PeriodStartDate)
            .ToListAsync();

        // 🎯 Si es empleado vinculado, filtrar detalles para mostrar solo SU línea
        if (linkedEmployeeId.HasValue)
        {
            foreach (var header in payrollHeaders)
            {
                header.Details = header.Details
                    .Where(d => d.EmpleadoId == linkedEmployeeId.Value)
                    .ToList();
            }
        }

        return Ok(payrollHeaders);
    }

    /// <summary>
    /// Obtiene una planilla específica por ID del tenant actual.
    /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo ve su detalle de planilla.
    /// GET /api/payrollheaders/{id}
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(SystemPermission.PayrollView, SystemPermission.PayrollViewSelf)]
    public async Task<ActionResult<PayrollHeader>> GetPayrollHeader(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();

        var payrollHeader = await _context.PayrollHeaders
            .Where(p => p.Id == id && p.TenantId == tenantId) // ✅ SEGURIDAD: Verificar tenant
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (payrollHeader == null)
        {
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });
        }

        // 🎯 EMPLOYEE SELF-SERVICE: Verificar que el empleado está en esta planilla
        if (linkedEmployeeId.HasValue)
        {
            var hasEmployeeInPayroll = payrollHeader.Details.Any(d => d.EmpleadoId == linkedEmployeeId.Value);
            if (!hasEmployeeInPayroll)
            {
                return Forbid(); // 403 - El empleado no está en esta planilla
            }

            // Filtrar detalles para mostrar solo SU línea
            payrollHeader.Details = payrollHeader.Details
                .Where(d => d.EmpleadoId == linkedEmployeeId.Value)
                .ToList();
        }

        return Ok(payrollHeader);
    }

    /// <summary>
    /// Crea una nueva planilla en estado Draft para el tenant actual.
    /// POST /api/payrollheaders
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult<PayrollHeader>> CreatePayrollHeader([FromBody] CreatePayrollHeaderRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // ====================================================================
        // Auto-generar PayrollNumber si no se proporciona o si ya existe
        // ====================================================================
        string payrollNumber = request.PayrollNumber;

        // Verificar si el PayrollNumber ya existe para este tenant
        bool numberExists = await _context.PayrollHeaders
            .AnyAsync(p => p.TenantId == tenantId && p.PayrollNumber == payrollNumber);

        // Si no se proporciona o ya existe, auto-generar uno nuevo
        if (string.IsNullOrWhiteSpace(payrollNumber) || numberExists)
        {
            int year = request.PeriodStartDate.Year;

            // Obtener el último número de planilla del año para este tenant
            var lastPayroll = await _context.PayrollHeaders
                .Where(p => p.TenantId == tenantId
                    && p.PayrollNumber.StartsWith($"{year}-"))
                .OrderByDescending(p => p.PayrollNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastPayroll != null)
            {
                // Extraer el número secuencial del último PayrollNumber (formato: YYYY-NNN)
                var parts = lastPayroll.PayrollNumber.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out int currentNumber))
                {
                    nextNumber = currentNumber + 1;
                }
            }

            // Generar nuevo PayrollNumber con formato YYYY-NNN
            payrollNumber = $"{year}-{nextNumber:D3}";
        }

        var payrollHeader = new PayrollHeader
        {
            TenantId = tenantId, // ✅ SEGURIDAD: TenantId del token JWT
            PayrollNumber = payrollNumber,
            PeriodStartDate = DateTime.SpecifyKind(request.PeriodStartDate, DateTimeKind.Utc),
            PeriodEndDate = DateTime.SpecifyKind(request.PeriodEndDate, DateTimeKind.Utc),
            PayDate = DateTime.SpecifyKind(request.PayDate, DateTimeKind.Utc),
            PayPeriodType = request.PayPeriodType,
            Status = PayrollStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _context.PayrollHeaders.Add(payrollHeader);

        try
        {
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG: Registrar creación de planilla
            try
            {
                await _auditLogService.LogAsync(
                    "PayrollCreated",
                    "PayrollHeader",
                    payrollHeader.Id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["PayrollNumber"] = payrollHeader.PayrollNumber,
                        ["PeriodStart"] = payrollHeader.PeriodStartDate.ToString("yyyy-MM-dd"),
                        ["PeriodEnd"] = payrollHeader.PeriodEndDate.ToString("yyyy-MM-dd"),
                        ["PayDate"] = payrollHeader.PayDate.ToString("yyyy-MM-dd"),
                        ["Status"] = payrollHeader.Status.ToString()
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_PayrollHeader_TenantId_PayrollNumber") == true)
        {
            return Conflict(new
            {
                message = $"Ya existe una planilla con el número '{payrollNumber}' para tu empresa",
                detail = "Por favor, intente nuevamente con un número diferente"
            });
        }

        return CreatedAtAction(nameof(GetPayrollHeader), new { id = payrollHeader.Id }, payrollHeader);
    }

    /// <summary>
    /// Calcula una planilla (Draft → Calculated o Calculated → Calculated).
    /// POST /api/payrollheaders/{id}/calculate
    /// </summary>
    [HttpPost("{id}/calculate")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> CalculatePayroll(int id, [FromServices] ILogger<PayrollHeadersController> logger)
    {
        var tenantId = _tenantContext.TenantId;
        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
        {
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });
        }

        // Validar transición de estado
        try
        {
            _stateMachine.ValidateTransition(payrollHeader.Status, PayrollStatus.Calculated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Phase E: Usar transacción para operación atómica
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // ====================================================================
            // 1. Obtener empleados activos del tenant
            // ====================================================================
            var activeEmployees = await _context.Empleados
                .Where(e => e.TenantId == tenantId && e.EstaActivo) // ✅ SEGURIDAD: Filtrado por tenant
                .ToListAsync();

            if (activeEmployees.Count == 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "No hay empleados activos para calcular en esta planilla" });
            }

            // ====================================================================
            // 2. Limpiar detalles existentes si es re-cálculo
            // ====================================================================
            var existingDetails = await _context.PayrollDetails
                .Where(d => d.PayrollHeaderId == payrollHeader.Id)
                .ToListAsync();

            if (existingDetails.Any())
            {
                _context.PayrollDetails.RemoveRange(existingDetails);
            }

            // ====================================================================
            // 3. Calcular planilla para cada empleado
            // ====================================================================
            decimal totalGrossPay = 0;
            decimal totalDeductions = 0;
            decimal totalNetPay = 0;
            decimal totalEmployerCost = 0;

            // Obtener horas registradas para esta planilla (si existen)
            var employeeHoursMap = await _context.PayrollEmployeeHours
                .Where(h => h.PayrollHeaderId == payrollHeader.Id && h.TenantId == tenantId)
                .ToDictionaryAsync(h => h.EmpleadoId);

            foreach (var employee in activeEmployees)
            {
                // Determinar grossPay: si hay horas registradas, calcular basado en horas
                decimal grossPay = employee.SalarioBase;

                if (employeeHoursMap.TryGetValue(employee.Id, out var hours))
                {
                    var hourlyRate = employee.HourlyRate > 0
                        ? employee.HourlyRate
                        : (employee.HoursPerPeriod > 0 ? employee.SalarioBase / employee.HoursPerPeriod : employee.SalarioBase);

                    hours.RegularPay = hours.RegularHours * hourlyRate;
                    hours.SundayPay = hours.SundayHours * hourlyRate * 1.50m;
                    hours.HolidayPay = hours.HolidayHours * hourlyRate * 1.50m;
                    hours.OvertimeDayPay = hours.OvertimeDayHours * hourlyRate * 1.25m;
                    hours.OvertimeNightPay = hours.OvertimeNightHours * hourlyRate * 1.50m;
                    
                    // Calcular pagos de horas extra complejas
                    // Festivos: promedio entre diurna (3.125x) y nocturna (3.75x) = 3.4375x
                    // En la práctica, se debería separar por tipo, pero para compatibilidad usamos promedio
                    hours.OvertimeHolidayPay = hours.OvertimeHolidayHours * hourlyRate * 3.4375m;
                    
                    // Mixtas: promedio entre diurna-nocturna (1.50x) y nocturna-diurna (1.75x) = 1.625x
                    hours.OvertimeMixedPay = hours.OvertimeMixedHours * hourlyRate * 1.625m;
                    
                    // Exceso: factor adicional 1.75x sobre el factor base promedio (1.375x) = 2.40625x
                    // Nota: En la práctica, el exceso se aplica sobre el tipo específico, pero para simplificar usamos promedio
                    hours.OvertimeExcessPay = hours.OvertimeExcessHours * hourlyRate * 2.40625m;
                    
                    hours.AbsenceDeduction = hours.AbsenceHours * hourlyRate;
                    hours.TotalHoursPay = hours.RegularPay + hours.SundayPay + hours.HolidayPay
                        + hours.OvertimeDayPay + hours.OvertimeNightPay
                        + hours.OvertimeHolidayPay + hours.OvertimeMixedPay + hours.OvertimeExcessPay
                        - hours.AbsenceDeduction;
                    hours.UpdatedAt = DateTime.UtcNow;

                    grossPay = hours.TotalHoursPay;
                }

                // Calcular usando el orquestador — ISR se anualiza según PayPeriodType de la PLANILLA
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

                // Calcular horas extra aprobadas del período (incluye todos los tipos: festivos, mixtas, exceso)
                decimal overtimePay = 0m;
                decimal horasExtraDiurnas = 0m;
                decimal horasExtraNocturnas = 0m;
                decimal horasExtraDomingoFeriado = 0m;

                var horasExtraAprobadas = await _asistenciaService.GetHorasExtraAprobadas(
                    employee.Id, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

                if (horasExtraAprobadas.Any())
                {
                    // Calcular salario hora para horas extra
                    var salarioHora = employee.HourlyRate > 0
                        ? employee.HourlyRate
                        : (employee.HoursPerPeriod > 0 ? employee.SalarioBase / employee.HoursPerPeriod : employee.SalarioBase / 208m);

                    var (montoHorasExtra, horasDiurnas, horasNocturnas, horasDomingoFeriado) =
                        await _asistenciaService.CalcularMontoHorasExtra(
                            employee.Id, salarioHora, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

                    overtimePay = montoHorasExtra;
                    horasExtraDiurnas = horasDiurnas;
                    horasExtraNocturnas = horasNocturnas;
                    horasExtraDomingoFeriado = horasDomingoFeriado;
                }
                else if (employeeHoursMap.ContainsKey(employee.Id))
                {
                    // Fallback: usar horas manuales si no hay horas extra aprobadas
                    overtimePay = employeeHoursMap[employee.Id].OvertimeDayPay + employeeHoursMap[employee.Id].OvertimeNightPay;
                    horasExtraDiurnas = employeeHoursMap[employee.Id].OvertimeDayHours;
                    horasExtraNocturnas = employeeHoursMap[employee.Id].OvertimeNightHours;
                }

                var detail = new PayrollDetail
                {
                    PayrollHeaderId = payrollHeader.Id,
                    EmpleadoId = employee.Id,
                    GrossPay = calculationResult.GrossPay,
                    BaseSalary = employee.SalarioBase,
                    OvertimePay = overtimePay,
                    Bonuses = 0,
                    Commissions = 0,
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
                    TotalDeductions = calculationResult.TotalDeductions,
                    NetPay = calculationResult.NetPay,
                    EmployerCost = calculationResult.TotalEmployerCost,
                    TenantId = tenantId
                };

                _context.PayrollDetails.Add(detail);

                // Acumular totales
                totalGrossPay += calculationResult.GrossPay;
                totalDeductions += calculationResult.TotalDeductions;
                totalNetPay += calculationResult.NetPay;
                totalEmployerCost += calculationResult.TotalEmployerCost;
            }

            // ====================================================================
            // 4. Actualizar totales en PayrollHeader
            // ====================================================================
            payrollHeader.TotalGrossPay = totalGrossPay;
            payrollHeader.TotalDeductions = totalDeductions;
            payrollHeader.TotalNetPay = totalNetPay;
            payrollHeader.TotalEmployerCost = totalEmployerCost;
            payrollHeader.Status = PayrollStatus.Calculated;
            payrollHeader.ProcessedDate = DateTime.UtcNow;
            payrollHeader.ProcessedBy = _tenantContext.UserId ?? "system";
            payrollHeader.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // ✅ AUDIT LOG: Registrar cálculo de planilla
            try
            {
                await _auditLogService.LogAsync(
                    "PayrollCalculated",
                    "PayrollHeader",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["PayrollNumber"] = payrollHeader.PayrollNumber,
                        ["PeriodStart"] = payrollHeader.PeriodStartDate.ToString("yyyy-MM-dd"),
                        ["PeriodEnd"] = payrollHeader.PeriodEndDate.ToString("yyyy-MM-dd"),
                        ["TotalEmployees"] = activeEmployees.Count.ToString(),
                        ["TotalGrossPay"] = totalGrossPay.ToString("N2"),
                        ["TotalDeductions"] = totalDeductions.ToString("N2"),
                        ["TotalNetPay"] = totalNetPay.ToString("N2"),
                        ["TotalEmployerCost"] = totalEmployerCost.ToString("N2")
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }

            return Ok(new
            {
                message = "Planilla calculada exitosamente",
                payrollHeaderId = payrollHeader.Id,
                status = payrollHeader.Status.ToString(),
                employeesProcessed = activeEmployees.Count,
                totalGrossPay = totalGrossPay,
                totalDeductions = totalDeductions,
                totalNetPay = totalNetPay,
                totalEmployerCost = totalEmployerCost
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            logger.LogWarning("Conflict detected while calculating payroll {PayrollId}", id);
            return Conflict(new { message = "La planilla fue modificada por otro usuario. Por favor, recargue e intente nuevamente." });
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Invalid operation while calculating payroll {PayrollId}", id);
            return BadRequest(new {
                message = "Error de validación al calcular la planilla",
                detail = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Unexpected error calculating payroll {PayrollId}: {Message}", id, ex.Message);
            return StatusCode(500, new {
                message = "Error al calcular la planilla",
                detail = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Aprueba una planilla calculada (Calculated → Approved).
    /// POST /api/payrollheaders/{id}/approve
    /// </summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> ApprovePayroll(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
        {
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });
        }

        // Validar transición de estado
        try
        {
            _stateMachine.ValidateTransition(payrollHeader.Status, PayrollStatus.Approved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Marcar como aprobada
        payrollHeader.Status = PayrollStatus.Approved;
        payrollHeader.IsApproved = true;
        payrollHeader.ApprovedDate = DateTime.UtcNow;
        payrollHeader.ApprovedBy = _tenantContext.UserId ?? "system";
        payrollHeader.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG: Registrar aprobación de planilla
            try
            {
                await _auditLogService.LogAsync(
                    "PayrollApproved",
                    "PayrollHeader",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["PayrollNumber"] = payrollHeader.PayrollNumber,
                        ["TotalNetPay"] = payrollHeader.TotalNetPay.ToString("N2"),
                        ["ApprovedBy"] = payrollHeader.ApprovedBy ?? "Unknown",
                        ["ApprovedDate"] = payrollHeader.ApprovedDate?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }

            return Ok(new
            {
                message = "Planilla aprobada exitosamente",
                payrollHeaderId = payrollHeader.Id,
                status = payrollHeader.Status.ToString(),
                approvedBy = payrollHeader.ApprovedBy,
                approvedDate = payrollHeader.ApprovedDate
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "La planilla fue modificada por otro usuario. Por favor, recargue e intente nuevamente." });
        }
    }

    /// <summary>
    /// Paga una planilla aprobada (Approved → Paid).
    /// POST /api/payrollheaders/{id}/pay
    /// NOTA: Stub - integración bancaria pendiente
    /// </summary>
    [HttpPost("{id}/pay")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> PayPayroll(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
        {
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });
        }

        // Validar transición de estado
        try
        {
            _stateMachine.ValidateTransition(payrollHeader.Status, PayrollStatus.Paid);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // TODO: Integración bancaria pendiente
        return StatusCode(501, new
        {
            message = "Integración bancaria pendiente",
            detail = "El endpoint de pago requiere integración con el sistema bancario. " +
                     "Funcionalidad disponible en próxima fase (Phase E).",
            payrollHeaderId = payrollHeader.Id,
            totalNetPay = payrollHeader.TotalNetPay
        });
    }

    /// <summary>
    /// Cancela una planilla que no ha sido pagada.
    /// POST /api/payrollheaders/{id}/cancel
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> CancelPayroll(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
        {
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });
        }

        // Validar transición de estado
        try
        {
            _stateMachine.ValidateTransition(payrollHeader.Status, PayrollStatus.Cancelled);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Marcar como cancelada
        payrollHeader.Status = PayrollStatus.Cancelled;
        payrollHeader.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();

            // ✅ AUDIT LOG: Registrar cancelación de planilla
            try
            {
                await _auditLogService.LogAsync(
                    "PayrollCancelled",
                    "PayrollHeader",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["PayrollNumber"] = payrollHeader.PayrollNumber,
                        ["PreviousStatus"] = PayrollStatus.Approved.ToString(), // Asumimos que venía de Approved
                        ["CancelledDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")
                    });
            }
            catch (Exception)
            {
                // No bloqueamos la operación si falla el audit log
            }

            return Ok(new
            {
                message = "Planilla cancelada exitosamente",
                payrollHeaderId = payrollHeader.Id,
                status = payrollHeader.Status.ToString()
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "La planilla fue modificada por otro usuario. Por favor, recargue e intente nuevamente." });
        }
    }

    // ====================================================================
    // ENDPOINTS DE HORAS TRABAJADAS
    // ====================================================================

    /// <summary>
    /// Obtiene las horas registradas para todos los empleados de una planilla.
    /// GET /api/payrollheaders/{id}/hours
    /// </summary>
    [HttpGet("{id}/hours")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> GetPayrollHours(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        var hours = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == id && h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .ToListAsync();

        return Ok(hours);
    }

    /// <summary>
    /// Registra/actualiza las horas de un empleado en una planilla específica.
    /// PUT /api/payrollheaders/{payrollId}/hours/{empleadoId}
    /// </summary>
    [HttpPut("{payrollId}/hours/{empleadoId}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> UpsertEmployeeHours(
        int payrollId, int empleadoId, [FromBody] UpsertEmployeeHoursRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == payrollId && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {payrollId} no encontrada" });

        if (payrollHeader.Status != PayrollStatus.Draft && payrollHeader.Status != PayrollStatus.Calculated)
            return BadRequest(new { message = "Solo se pueden modificar horas en planillas con estado Draft o Calculated" });

        var employee = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

        if (employee == null)
            return NotFound(new { message = $"Empleado con ID {empleadoId} no encontrado" });

        var existing = await _context.PayrollEmployeeHours
            .FirstOrDefaultAsync(h => h.PayrollHeaderId == payrollId && h.EmpleadoId == empleadoId && h.TenantId == tenantId);

        if (existing != null)
        {
            existing.RegularHours = request.RegularHours;
            existing.SundayHours = request.SundayHours;
            existing.HolidayHours = request.HolidayHours;
            existing.OvertimeDayHours = request.OvertimeDayHours;
            existing.OvertimeNightHours = request.OvertimeNightHours;
            existing.OvertimeHolidayHours = request.OvertimeHolidayHours;
            existing.OvertimeMixedHours = request.OvertimeMixedHours;
            existing.OvertimeExcessHours = request.OvertimeExcessHours;
            existing.AbsenceHours = request.AbsenceHours;
            existing.DisabilityHours = request.DisabilityHours;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newHours = new PayrollEmployeeHours
            {
                PayrollHeaderId = payrollId,
                EmpleadoId = empleadoId,
                TenantId = tenantId,
                RegularHours = request.RegularHours,
                SundayHours = request.SundayHours,
                HolidayHours = request.HolidayHours,
                OvertimeDayHours = request.OvertimeDayHours,
                OvertimeNightHours = request.OvertimeNightHours,
                OvertimeHolidayHours = request.OvertimeHolidayHours,
                OvertimeMixedHours = request.OvertimeMixedHours,
                OvertimeExcessHours = request.OvertimeExcessHours,
                AbsenceHours = request.AbsenceHours,
                DisabilityHours = request.DisabilityHours
            };
            _context.PayrollEmployeeHours.Add(newHours);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Horas registradas exitosamente" });
    }

    /// <summary>
    /// Auto-genera horas regulares default para todos los empleados activos.
    /// POST /api/payrollheaders/{id}/hours/generate-defaults
    /// </summary>
    [HttpPost("{id}/hours/generate-defaults")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> GenerateDefaultHours(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        if (payrollHeader.Status != PayrollStatus.Draft && payrollHeader.Status != PayrollStatus.Calculated)
            return BadRequest(new { message = "Solo se pueden generar horas en planillas con estado Draft o Calculated" });

        var activeEmployees = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo && !e.IsDeleted)
            .ToListAsync();

        var existingHoursIds = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == id && h.TenantId == tenantId)
            .Select(h => h.EmpleadoId)
            .ToListAsync();

        int generated = 0;
        foreach (var emp in activeEmployees)
        {
            if (existingHoursIds.Contains(emp.Id)) continue;

            _context.PayrollEmployeeHours.Add(new PayrollEmployeeHours
            {
                PayrollHeaderId = id,
                EmpleadoId = emp.Id,
                TenantId = tenantId,
                RegularHours = emp.HoursPerPeriod
            });
            generated++;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"Horas generadas para {generated} empleados", generated });
    }

    /// <summary>
    /// Importa horas extra y ausencias aprobadas del período a PayrollEmployeeHours.
    /// POST /api/payrollheaders/{id}/hours/import-novedades?mode=overwrite|sum
    /// </summary>
    [HttpPost("{id}/hours/import-novedades")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> ImportNovedades(int id, [FromQuery] string mode = "overwrite")
    {
        var tenantId = _tenantContext.TenantId;
        var modeLower = mode?.ToLower() ?? "overwrite";
        var overwrite = modeLower == "overwrite";
        var isSumMode = modeLower == "sum";

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        if (payrollHeader.Status != PayrollStatus.Draft && payrollHeader.Status != PayrollStatus.Calculated)
            return BadRequest(new { message = "Solo se pueden importar novedades en planillas con estado Draft o Calculated" });

        var activeEmployees = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo && !e.IsDeleted)
            .ToListAsync();

        var existingHours = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == id && h.TenantId == tenantId)
            .ToDictionaryAsync(h => h.EmpleadoId);

        int employeesWithData = 0;
        decimal totalOvertimeDay = 0;
        decimal totalOvertimeNight = 0;
        decimal totalAbsenceHours = 0;
        var employeesWithExistingValues = new List<int>();

        foreach (var employee in activeEmployees)
        {
            // Consultar horas extra aprobadas del período
            var horasExtra = await _asistenciaService.GetHorasExtraAprobadas(
                employee.Id, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

            // Consultar ausencias del período
            var ausencias = await _asistenciaService.GetAusenciasDelPeriodo(
                employee.Id, payrollHeader.PeriodStartDate, payrollHeader.PeriodEndDate);

            if (horasExtra.Count == 0 && ausencias.Count == 0) continue;

            // Sumar horas extra por tipo
            // Nota: Mantenemos compatibilidad con campos antiguos (OvertimeDayHours/OvertimeNightHours)
            // pero también distribuimos en nuevos campos específicos para tipos complejos
            decimal overtimeDay = 0;
            decimal overtimeNight = 0;
            decimal overtimeHoliday = 0;
            decimal overtimeMixed = 0;
            decimal overtimeExcess = 0;
            
            foreach (var he in horasExtra)
            {
                // Clasificar para campos antiguos (compatibilidad)
                switch (he.TipoHoraExtra)
                {
                    case TipoHoraExtra.Diurna:
                    case TipoHoraExtra.DomingoFeriado:
                        overtimeDay += he.CantidadHoras;
                        break;
                    case TipoHoraExtra.Nocturna:
                    case TipoHoraExtra.NocturnaDomingoFeriado:
                        overtimeNight += he.CantidadHoras;
                        break;
                    case TipoHoraExtra.FiestaNacionalDiurna:
                        overtimeDay += he.CantidadHoras; // Para compatibilidad
                        overtimeHoliday += he.CantidadHoras; // Nuevo campo específico
                        break;
                    case TipoHoraExtra.FiestaNacionalNocturna:
                        overtimeNight += he.CantidadHoras; // Para compatibilidad
                        overtimeHoliday += he.CantidadHoras; // Nuevo campo específico
                        break;
                    case TipoHoraExtra.MixtaDiurnaNocturna:
                        overtimeDay += he.CantidadHoras; // Para compatibilidad
                        overtimeMixed += he.CantidadHoras; // Nuevo campo específico
                        break;
                    case TipoHoraExtra.MixtaNocturnaDiurna:
                        overtimeNight += he.CantidadHoras; // Para compatibilidad
                        overtimeMixed += he.CantidadHoras; // Nuevo campo específico
                        break;
                    default:
                        overtimeDay += he.CantidadHoras;
                        break;
                }
                
                // Contar horas con exceso
                if (he.EsExceso)
                {
                    overtimeExcess += he.CantidadHoras;
                }
            }

            // Convertir días de ausencia a horas (asumiendo 8 horas por día laboral)
            decimal absenceHours = 0;
            foreach (var ausencia in ausencias)
            {
                var inicio = ausencia.FechaInicio < payrollHeader.PeriodStartDate 
                    ? payrollHeader.PeriodStartDate 
                    : ausencia.FechaInicio;
                var fin = ausencia.FechaFin > payrollHeader.PeriodEndDate 
                    ? payrollHeader.PeriodEndDate 
                    : ausencia.FechaFin;
                var dias = (decimal)(fin - inicio).TotalDays + 1;
                absenceHours += dias * 8m; // 8 horas por día
            }

            if (overtimeDay == 0 && overtimeNight == 0 && overtimeHoliday == 0 
                && overtimeMixed == 0 && overtimeExcess == 0 && absenceHours == 0) continue;

            employeesWithData++;

            // Verificar si ya tiene valores en PayrollEmployeeHours
            if (existingHours.TryGetValue(employee.Id, out var existing))
            {
                bool hasExistingValues = existing.OvertimeDayHours > 0 
                    || existing.OvertimeNightHours > 0 
                    || existing.AbsenceHours > 0;

                // Solo pedir confirmación en la primera llamada (overwrite por defecto) si hay valores
                // Si es modo "sum", siempre sumar sin preguntar
                if (hasExistingValues && overwrite && !isSumMode)
                {
                    employeesWithExistingValues.Add(employee.Id);
                    continue; // Skip para pedir confirmación
                }

                // Actualizar existente
                if (overwrite && !isSumMode)
                {
                    // Sobrescribir valores existentes
                    existing.OvertimeDayHours = overtimeDay;
                    existing.OvertimeNightHours = overtimeNight;
                    existing.OvertimeHolidayHours = overtimeHoliday;
                    existing.OvertimeMixedHours = overtimeMixed;
                    existing.OvertimeExcessHours = overtimeExcess;
                    existing.AbsenceHours = absenceHours;
                }
                else
                {
                    // Sumar a valores existentes (modo "sum" o cuando no hay valores previos)
                    existing.OvertimeDayHours += overtimeDay;
                    existing.OvertimeNightHours += overtimeNight;
                    existing.OvertimeHolidayHours += overtimeHoliday;
                    existing.OvertimeMixedHours += overtimeMixed;
                    existing.OvertimeExcessHours += overtimeExcess;
                    existing.AbsenceHours += absenceHours;
                }
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Crear nuevo registro (solo con novedades, RegularHours se mantiene en 0 o se llena con auto-llenar)
                var newHours = new PayrollEmployeeHours
                {
                    PayrollHeaderId = id,
                    EmpleadoId = employee.Id,
                    TenantId = tenantId,
                    RegularHours = 0, // No se tocan las regulares
                    OvertimeDayHours = overtimeDay,
                    OvertimeNightHours = overtimeNight,
                    OvertimeHolidayHours = overtimeHoliday,
                    OvertimeMixedHours = overtimeMixed,
                    OvertimeExcessHours = overtimeExcess,
                    AbsenceHours = absenceHours,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PayrollEmployeeHours.Add(newHours);
                existingHours[employee.Id] = newHours;
            }

            totalOvertimeDay += overtimeDay;
            totalOvertimeNight += overtimeNight;
            totalAbsenceHours += absenceHours;
        }

        // Si hay empleados con valores existentes y no se eligió overwrite, retornar info para confirmación
        if (employeesWithExistingValues.Count > 0 && !overwrite)
        {
            return Ok(new
            {
                requiresConfirmation = true,
                employeesWithExistingValues = employeesWithExistingValues.Count,
                message = $"Hay {employeesWithExistingValues.Count} empleado(s) con horas extra/ausencias ya registradas. ¿Desea sobrescribir o sumar?"
            });
        }

        await _context.SaveChangesAsync();

        var totalOvertimeHours = totalOvertimeDay + totalOvertimeNight;
        return Ok(new
        {
            requiresConfirmation = false,
            message = $"Importadas {totalOvertimeHours:F1} horas extra y {totalAbsenceHours:F1} horas de ausencias de {employeesWithData} empleado(s)",
            summary = new
            {
                employeesProcessed = employeesWithData,
                overtimeDayHours = totalOvertimeDay,
                overtimeNightHours = totalOvertimeNight,
                absenceHours = totalAbsenceHours,
                totalOvertimeHours = totalOvertimeHours
            }
        });
    }

    /// <summary>
    /// Asegura que el tenant tenga configuración de impuestos (CSS, SE, ISR). Si no existe, crea la configuración por defecto.
    /// Útil cuando "Calcular Planilla" falla por falta de configuración (p. ej. empresa creada antes de tener seed al arrancar).
    /// POST /api/payrollheaders/ensure-tax-config
    /// </summary>
    [HttpPost("ensure-tax-config")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> EnsureTaxConfig([FromServices] ILogger<PayrollHeadersController> logger)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId <= 0)
            return BadRequest(new { message = "No se pudo determinar el tenant" });
        try
        {
            await PayrollConfigSeeder.SeedForNewTenantAsync(_context, tenantId, logger);
            return Ok(new { message = "Configuración de planilla verificada o creada correctamente" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureTaxConfig failed for tenant {TenantId}", tenantId);
            return StatusCode(500, new { message = "No se pudo crear la configuración. Contacte al administrador." });
        }
    }
}

/// <summary>
/// DTO para crear una nueva planilla.
/// </summary>
public record CreatePayrollHeaderRequest(
    string PayrollNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime PayDate,
    PayPeriodType PayPeriodType = PayPeriodType.Quincenal
);

public record UpsertEmployeeHoursRequest(
    decimal RegularHours,
    decimal SundayHours = 0,
    decimal HolidayHours = 0,
    decimal OvertimeDayHours = 0,
    decimal OvertimeNightHours = 0,
    decimal OvertimeHolidayHours = 0,
    decimal OvertimeMixedHours = 0,
    decimal OvertimeExcessHours = 0,
    decimal AbsenceHours = 0,
    decimal DisabilityHours = 0
);
