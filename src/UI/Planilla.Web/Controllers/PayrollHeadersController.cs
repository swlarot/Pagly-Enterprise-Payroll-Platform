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
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Results;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Application.Helpers;
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
    private readonly IAsistenciaCalculationService _asistenciaService;
    private readonly PayrollProcessingService _processingService;

    private readonly IAcumuladoFiscalService _acumuladoFiscalService;
    private readonly IHorasPlanillaService _horasService;

    public PayrollHeadersController(
        ApplicationDbContext context,
        PayrollStateMachine stateMachine,
        PayrollCalculationOrchestratorPortable orchestrator,
        ITenantContext tenantContext,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        IAsistenciaCalculationService asistenciaService,
        PayrollProcessingService processingService,
        IAcumuladoFiscalService acumuladoFiscalService,
        IHorasPlanillaService horasService)
    {
        _acumuladoFiscalService = acumuladoFiscalService ?? throw new ArgumentNullException(nameof(acumuladoFiscalService));
        _horasService = horasService ?? throw new ArgumentNullException(nameof(horasService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _asistenciaService = asistenciaService ?? throw new ArgumentNullException(nameof(asistenciaService));
        _processingService = processingService ?? throw new ArgumentNullException(nameof(processingService));
    }

    /// <summary>
    /// Lista todas las planillas del tenant actual con filtros opcionales.
    /// 🔐 EMPLOYEE SELF-SERVICE: Si el usuario está vinculado a un empleado, solo ve las planillas donde aparece.
    /// GET /api/payrollheaders?status=Calculated
    /// </summary>
    [HttpGet]
    [RequirePermission(SystemPermission.PayrollView, SystemPermission.PayrollViewSelf)]
    public async Task<ActionResult<IEnumerable<PayrollHeader>>> GetPayrollHeaders(
        [FromQuery] PayrollStatus? status,
        [FromQuery] int? empleadoId = null,
        [FromQuery] int? anio = null,
        [FromQuery] int? mes = null)
    {
        if (mes is < 1 or > 12) return BadRequest(new { message = "El mes debe estar entre 1 y 12." });
        if (anio is < 2000 or > 2100) return BadRequest(new { message = "Año fuera de rango." });

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
        else if (empleadoId.HasValue)
        {
            // Owner/Admin puede filtrar por empleado específico (para ver perfil de empleado)
            query = query.Where(p => p.Details.Any(d => d.EmpleadoId == empleadoId.Value));
        }

        // Filtrar por Status si se especifica
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        // Vista por mes: una planilla pertenece al mes de su PERÍODO TRABAJADO
        // (PeriodStartDate), nunca al de su fecha de pago. Es la misma regla del
        // reporte mensual, del SIPE y del año fiscal de la ficha de renta.
        if (anio.HasValue)
        {
            query = query.Where(p => p.PeriodStartDate.Year == anio.Value);
            if (mes.HasValue) query = query.Where(p => p.PeriodStartDate.Month == mes.Value);
        }

        // Vista por mes: proyección ligera. Con miles de empleados, mandar los
        // Details de cada planilla en la lista es inviable; la planilla desplegada
        // pide sus empleados paginados a GET /{id}/details.
        if (anio.HasValue)
        {
            var resumen = await query
                .OrderBy(p => p.PeriodStartDate).ThenBy(p => p.Id)
                .Select(p => new PayrollHeaderResumenDto(
                    p.Id, p.PayrollNumber, p.PeriodStartDate, p.PeriodEndDate, p.PayDate,
                    p.PayPeriodType, p.TipoPlanilla, p.Status,
                    p.TotalGrossPay, p.TotalDeductions, p.TotalNetPay, p.TotalEmployerCost,
                    p.Details.Count(d => linkedEmployeeId == null || d.EmpleadoId == linkedEmployeeId.Value),
                    p.ProcessedDate, p.ApprovedDate, p.PaidDate, p.CreatedAt))
                .ToListAsync();
            return Ok(resumen);
        }

        var payrollHeaders = await query.OrderByDescending(p => p.PeriodStartDate).ToListAsync();

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
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult<PayrollHeader>> CreatePayrollHeader([FromBody] CreatePayrollHeaderRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Validación de fechas en el servidor (antes solo la hacía la pantalla).
        if (request.PeriodEndDate.Date <= request.PeriodStartDate.Date)
            return BadRequest(new { message = "La fecha de fin del período debe ser posterior a la de inicio." });
        if (request.PayDate.HasValue && request.PayDate.Value.Date < request.PeriodStartDate.Date)
            return BadRequest(new { message = "La fecha de pago no puede ser anterior al inicio del período." });
        if ((request.PeriodEndDate.Date - request.PeriodStartDate.Date).TotalDays > 62)
            return BadRequest(new { message = "Un período no puede durar más de dos meses." });

        var payDate = request.PayDate ?? request.PeriodEndDate;

        // ====================================================================
        // Auto-generar PayrollNumber si no se proporciona o si ya existe
        // ====================================================================
        string payrollNumber = request.PayrollNumber ?? string.Empty;

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
            PayDate = DateTime.SpecifyKind(payDate, DateTimeKind.Utc),
            PayPeriodType = request.PayPeriodType,
            TipoPlanilla = request.TipoPlanilla,
            Status = PayrollStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _context.PayrollHeaders.Add(payrollHeader);

        int horasGeneradas = 0;
        ResumenNovedades? novedades = null;
        try
        {
            // La planilla nace con sus horas: una fila por empleado activo con
            // sus regulares, más las horas extra y ausencias aprobadas del
            // período. Todo en una transacción: o queda completa o no queda.
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();
            horasGeneradas = await _horasService.GenerarHorasPorDefectoAsync(payrollHeader);
            novedades = await _horasService.ImportarNovedadesAsync(payrollHeader, ModoNovedades.Sobrescribir);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

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
                        ["Status"] = payrollHeader.Status.ToString(),
                        ["HorasGeneradas"] = horasGeneradas.ToString(),
                        ["EmpleadosConNovedades"] = (novedades?.EmpleadosConNovedades ?? 0).ToString()
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

        return CreatedAtAction(nameof(GetPayrollHeader), new { id = payrollHeader.Id }, new
        {
            payrollHeader.Id,
            payrollHeader.PayrollNumber,
            payrollHeader.PeriodStartDate,
            payrollHeader.PeriodEndDate,
            payrollHeader.PayDate,
            payrollHeader.PayPeriodType,
            payrollHeader.TipoPlanilla,
            payrollHeader.Status,
            horasGeneradas,
            novedades = new
            {
                empleados = novedades?.EmpleadosConNovedades ?? 0,
                horasExtra = novedades?.HorasExtra ?? 0m,
                horasAusencia = novedades?.HorasAusencia ?? 0m,
            }
        });
    }

    /// <summary>
    /// Calcula una planilla (Draft → Calculated o Calculated → Calculated).
    /// POST /api/payrollheaders/{id}/calculate
    /// </summary>
    [HttpPost("{id}/calculate")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
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
            decimal totalEmployerCss = 0;
            decimal totalEmployerSe = 0;
            decimal totalRiskInsurance = 0;

            // Obtener horas registradas para esta planilla (si existen)
            var employeeHoursMap = await _context.PayrollEmployeeHours
                .Where(h => h.PayrollHeaderId == payrollHeader.Id && h.TenantId == tenantId)
                .ToDictionaryAsync(h => h.EmpleadoId);

            var detailDeduccionPairs = new List<(PayrollDetail detail, DeduccionesResult dedResult)>();

            // DEV-31: Pre-cargar datos compartidos UNA sola vez antes del loop (evita N+1)
            var employeeIds = activeEmployees.Select(e => e.Id).ToList();

            var taxConfigPreload = await _context.PayrollTaxConfigurations
                .Where(c => c.IsActive && c.TenantId == tenantId &&
                            c.EffectiveStartDate <= DateTime.UtcNow &&
                            (c.EffectiveEndDate == null || c.EffectiveEndDate >= DateTime.UtcNow))
                .OrderByDescending(c => c.EffectiveStartDate)
                .FirstOrDefaultAsync();
            decimal salarioMinimoMensual = taxConfigPreload?.SalarioMinimoLegal ?? 700.00m;
            decimal salarioMinimoPeriodo = DeduccionPrioridadEngine.ProrratearSalarioMinimo(
                salarioMinimoMensual, payrollHeader.PayPeriodType);

            var deduccionesMap = (await _context.DeduccionesFijas
                .Where(d => employeeIds.Contains(d.EmpleadoId) && d.EstaActivo &&
                            d.FechaInicio <= payrollHeader.PeriodStartDate &&
                            (d.FechaFin == null || d.FechaFin >= payrollHeader.PeriodStartDate) &&
                            (d.EstadoOrdenJudicial == null || d.EstadoOrdenJudicial != EstadoOrdenJudicial.Levantada))
                .OrderBy(d => d.Prioridad)
                .ToListAsync())
                .GroupBy(d => d.EmpleadoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var prestamosMap = (await _context.Prestamos
                .Where(p => employeeIds.Contains(p.EmpleadoId) && p.Estado == EstadoPrestamo.Activo && p.CuotasPagadas < p.NumeroCuotas)
                .ToListAsync())
                .GroupBy(p => p.EmpleadoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var anticiposMap = (await _context.Anticipos
                .Where(a => employeeIds.Contains(a.EmpleadoId) && a.Estado == EstadoAnticipo.Aprobado &&
                            a.FechaDescuento.Date >= payrollHeader.PeriodStartDate.Date &&
                            a.FechaDescuento.Date <= payrollHeader.PeriodEndDate.Date)
                .ToListAsync())
                .GroupBy(a => a.EmpleadoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // DEV-35: Delegar cálculo por empleado al servicio (elimina duplicación con PayrollProcessingService)
            foreach (var employee in activeEmployees)
            {
                var hoursForEmployee = employeeHoursMap.TryGetValue(employee.Id, out var h) ? h : null;
                var dfForEmployee = deduccionesMap.TryGetValue(employee.Id, out var dfList) ? dfList : new List<DeduccionFija>();
                var prForEmployee = prestamosMap.TryGetValue(employee.Id, out var prList) ? prList : new List<Prestamo>();
                var antForEmployee = anticiposMap.TryGetValue(employee.Id, out var antList) ? antList : new List<Anticipo>();

                var (detail, deduccionesResult) = await _processingService.CalculateFromPreloadedDataAsync(
                    tenantId, employee, payrollHeader,
                    hoursForEmployee, dfForEmployee, prForEmployee, antForEmployee,
                    salarioMinimoPeriodo);

                _context.PayrollDetails.Add(detail);
                detailDeduccionPairs.Add((detail, deduccionesResult));

                // Acumular totales
                totalGrossPay += detail.GrossPay;
                totalDeductions += detail.TotalDeductions;
                totalNetPay += detail.NetPay;
                totalEmployerCost += detail.EmployerCost;
                totalEmployerCss += detail.CssEmployer;
                totalEmployerSe += detail.EducationalInsuranceEmployer;
                totalRiskInsurance += detail.RiskContribution;
            }

            // ====================================================================
            // 4. Actualizar totales en PayrollHeader
            // ====================================================================
            payrollHeader.TotalGrossPay = totalGrossPay;
            payrollHeader.TotalDeductions = totalDeductions;
            payrollHeader.TotalNetPay = totalNetPay;
            payrollHeader.TotalEmployerCost = totalEmployerCost;
            payrollHeader.TotalEmployerCss = totalEmployerCss;
            payrollHeader.TotalEmployerSe = totalEmployerSe;
            payrollHeader.TotalRiskInsurance = totalRiskInsurance;
            payrollHeader.Status = PayrollStatus.Calculated;
            payrollHeader.ProcessedDate = DateTime.UtcNow;
            payrollHeader.ProcessedBy = _tenantContext.UserId ?? "system";
            payrollHeader.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Persistir auditoría de deducciones aplicadas (para reportes de acreedor y prelación)
            foreach (var (det, dedRes) in detailDeduccionPairs)
            {
                await _processingService.CreateDeduccionesAplicadasAsync(det, dedRes);
            }

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
            // DEV-33: Solo exponer detalles técnicos en Development, nunca en Production
            var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            return StatusCode(500, env.IsDevelopment()
                ? (object)new { message = "Error al calcular la planilla", detail = ex.Message, innerError = ex.InnerException?.Message }
                : new { message = "Error al calcular la planilla. Consulte los logs del servidor." });
        }
    }

    /// <summary>
    /// Aprueba una planilla calculada (Calculated → Approved).
    /// POST /api/payrollheaders/{id}/approve
    /// </summary>
    [HttpPost("{id}/approve")]
    [RequirePermission(SystemPermission.PayrollApprove)]
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
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<ActionResult> PayPayroll(int id, [FromServices] ILogger<PayrollHeadersController> logger)
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

        // DEV-25: Marcar planilla como Paid y actualizar estados de préstamos/anticipos
        await using var payTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Obtener todos los detalles de la planilla
            var details = await _context.PayrollDetails
                .Where(d => d.PayrollHeaderId == payrollHeader.Id && d.TenantId == tenantId)
                .ToListAsync();

            // 2. Para cada detalle, procesar préstamos y anticipos descontados
            foreach (var detail in details)
            {
                // Obtener deducciones aplicadas de esta línea
                var deducciones = await _context.DeduccionesAplicadas
                    .Where(d => d.PayrollDetailId == detail.Id)
                    .ToListAsync();

                var prestamoIds = deducciones
                    .Where(d => d.PrestamoId.HasValue)
                    .Select(d => d.PrestamoId!.Value)
                    .Distinct()
                    .ToList();

                var montoAplicadoPorPrestamo = deducciones
                    .Where(d => d.PrestamoId.HasValue && d.MontoAplicado > 0m)
                    .GroupBy(d => d.PrestamoId!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.MontoAplicado));

                // Solo procesar anticipos que realmente tuvieron monto descontado
                var anticipoIds = deducciones
                    .Where(d => d.AnticipoId.HasValue && d.MontoAplicado > 0m)
                    .Select(d => d.AnticipoId!.Value)
                    .Distinct()
                    .ToList();

                if (prestamoIds.Count > 0)
                    await _processingService.ProcessPrestamosAsync(prestamoIds, detail.Id, payrollHeader.Id, montoAplicadoPorPrestamo);

                if (anticipoIds.Count > 0)
                    await _processingService.ProcessAnticiposAsync(anticipoIds, detail.Id, payrollHeader.Id);
            }

            // 3. Marcar planilla como Pagada
            payrollHeader.Status = PayrollStatus.Paid;
            payrollHeader.UpdatedAt = DateTime.UtcNow;
            _context.PayrollHeaders.Update(payrollHeader);
            await _context.SaveChangesAsync();

            await payTransaction.CommitAsync();

            return Ok(new
            {
                message = "Planilla marcada como pagada y préstamos/anticipos actualizados",
                payrollHeaderId = payrollHeader.Id,
                totalNetPay = payrollHeader.TotalNetPay
            });
        }
        catch (Exception ex)
        {
            await payTransaction.RollbackAsync();
            var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            logger.LogError(ex, "Error marking payroll {PayrollId} as paid", id);
            return StatusCode(500, env.IsDevelopment()
                ? (object)new { message = "Error al marcar la planilla como pagada", detail = ex.Message }
                : new { message = "Error al marcar la planilla como pagada. Consulte los logs del servidor." });
        }
    }

    /// <summary>
    /// Cancela una planilla que no ha sido pagada.
    /// POST /api/payrollheaders/{id}/cancel
    /// </summary>
    [HttpPost("{id}/cancel")]
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<ActionResult> CancelPayroll(int id, [FromBody] CancelPayrollRequest? request = null)
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
        var estadoAnterior = payrollHeader.Status;
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
                        ["PreviousStatus"] = estadoAnterior.ToString(),
                        ["CancelledDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                        ["Reason"] = request?.Reason ?? string.Empty
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
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult> GetPayrollHours(int id, [FromQuery] int? page = null, [FromQuery] int? size = null, [FromQuery] string? q = null)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        var consulta = _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == id && h.TenantId == tenantId)
            .AsNoTracking();

        // Búsqueda por nombre, apellido o cédula (para miles de empleados).
        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim().ToLower();
            consulta = consulta.Where(h =>
                (h.Empleado!.Nombre + " " + h.Empleado.Apellido).ToLower().Contains(texto)
                || h.Empleado.NumeroIdentificacion.ToLower().Contains(texto));
        }

        var proyeccion = consulta
            .OrderBy(h => h.Empleado!.Apellido).ThenBy(h => h.Empleado!.Nombre).ThenBy(h => h.EmpleadoId)
            .Select(h => new
            {
                h.Id, h.EmpleadoId,
                empleado = new { h.Empleado!.Id, h.Empleado.Nombre, h.Empleado.Apellido, h.Empleado.NumeroIdentificacion },
                h.RegularHours, h.SundayHours, h.HolidayHours,
                h.OvertimeDayHours, h.OvertimeNightHours, h.OvertimeHolidayHours, h.OvertimeMixedHours, h.OvertimeExcessHours,
                h.AbsenceHours, h.DisabilityHours, h.Commissions,
            });

        // Sin página: la lista completa, como siempre (compatibilidad).
        if (page is null) return Ok(await proyeccion.ToListAsync());

        var pagina = Math.Max(1, page.Value);
        var tamano = Math.Clamp(size ?? 50, 1, 500);
        var total = await consulta.CountAsync();
        var items = await proyeccion.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return Ok(new { items, total, page = pagina, size = tamano });
    }

    /// <summary>
    /// Empleados de una planilla, paginados y con búsqueda. Es lo que enseña
    /// la planilla desplegada; con miles de empleados no se mandan todos juntos.
    /// GET /api/payrollheaders/{id}/details?page=1&amp;size=50&amp;q=perez
    /// </summary>
    [HttpGet("{id}/details")]
    [RequirePermission(SystemPermission.PayrollView, SystemPermission.PayrollViewSelf)]
    public async Task<ActionResult> GetPayrollDetails(int id, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? q = null)
    {
        var tenantId = _tenantContext.TenantId;
        var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();

        var existe = await _context.PayrollHeaders.AnyAsync(p => p.Id == id && p.TenantId == tenantId);
        if (!existe) return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        var consulta = _context.PayrollDetails
            .Where(d => d.PayrollHeaderId == id && d.TenantId == tenantId)
            .AsNoTracking();

        if (linkedEmployeeId.HasValue)
            consulta = consulta.Where(d => d.EmpleadoId == linkedEmployeeId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim().ToLower();
            consulta = consulta.Where(d =>
                (d.Empleado!.Nombre + " " + d.Empleado.Apellido).ToLower().Contains(texto)
                || d.Empleado.NumeroIdentificacion.ToLower().Contains(texto));
        }

        var pagina = Math.Max(1, page);
        var tamano = Math.Clamp(size, 1, 500);
        var total = await consulta.CountAsync();

        // Totales y columnas opcionales de TODA la planilla (no solo de la página).
        var totales = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                bruto = g.Sum(d => d.GrossPay),
                css = g.Sum(d => d.CssEmployee),
                se = g.Sum(d => d.EducationalInsuranceEmployee),
                isr = g.Sum(d => d.IncomeTax),
                pension = g.Sum(d => d.PensionAlimenticia),
                embargos = g.Sum(d => d.Embargos),
                fijas = g.Sum(d => d.DeduccionesFijas),
                prestamos = g.Sum(d => d.Prestamos),
                anticipos = g.Sum(d => d.Anticipos),
                deducciones = g.Sum(d => d.TotalDeductions),
                neto = g.Sum(d => d.NetPay),
            })
            .FirstOrDefaultAsync();

        var items = await consulta
            .OrderBy(d => d.Empleado!.Apellido).ThenBy(d => d.Empleado!.Nombre).ThenBy(d => d.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(d => new
            {
                d.Id, d.EmpleadoId,
                empleado = new { d.Empleado!.Id, d.Empleado.Nombre, d.Empleado.Apellido, d.Empleado.NumeroIdentificacion },
                d.GrossPay, d.CssEmployee, d.EducationalInsuranceEmployee, d.IncomeTax,
                d.PensionAlimenticia, d.Embargos, d.DeduccionesFijas, d.Prestamos, d.Anticipos,
                d.TotalDeductions, d.NetPay, d.TuvoLimitacionSalarioMinimo,
            })
            .ToListAsync();

        return Ok(new { items, total, page = pagina, size = tamano, totales });
    }

    /// <summary>
    /// Registra/actualiza las horas de un empleado en una planilla específica.
    /// PUT /api/payrollheaders/{payrollId}/hours/{empleadoId}
    /// </summary>
    [HttpPut("{payrollId}/hours/{empleadoId}")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
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
            existing.Commissions = request.Commissions;
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
                DisabilityHours = request.DisabilityHours,
                Commissions = request.Commissions
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
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult> GenerateDefaultHours(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        if (payrollHeader.Status != PayrollStatus.Draft && payrollHeader.Status != PayrollStatus.Calculated)
            return BadRequest(new { message = "Solo se pueden generar horas en planillas con estado Draft o Calculated" });

        var generated = await _horasService.GenerarHorasPorDefectoAsync(payrollHeader);
        await _context.SaveChangesAsync();
        return Ok(new { message = $"Horas generadas para {generated} empleados", generated });
    }

    /// <summary>
    /// Importa horas extra y ausencias aprobadas del período a PayrollEmployeeHours.
    /// POST /api/payrollheaders/{id}/hours/import-novedades?mode=overwrite|sum
    /// </summary>
    [HttpPost("{id}/hours/import-novedades")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult> ImportNovedades(int id, [FromQuery] string mode = "overwrite")
    {
        var tenantId = _tenantContext.TenantId;
        var modo = (mode ?? "overwrite").ToLowerInvariant() switch
        {
            "sum" => ModoNovedades.Sumar,
            "ask" => ModoNovedades.Preguntar,
            _ => ModoNovedades.Sobrescribir,
        };

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = $"Planilla con ID {id} no encontrada" });

        if (payrollHeader.Status != PayrollStatus.Draft && payrollHeader.Status != PayrollStatus.Calculated)
            return BadRequest(new { message = "Solo se pueden importar novedades en planillas con estado Draft o Calculated" });

        var r = await _horasService.ImportarNovedadesAsync(payrollHeader, modo);
        if (r.RequiereConfirmacion)
        {
            return Ok(new
            {
                requiresConfirmation = true,
                employeesWithExistingValues = r.EmpleadosConValoresPrevios,
                message = $"Hay {r.EmpleadosConValoresPrevios} empleado(s) con horas extra/ausencias ya registradas. ¿Desea sobrescribir o sumar?"
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            requiresConfirmation = false,
            message = $"Importadas {r.HorasExtra:F1} horas extra y {r.HorasAusencia:F1} horas de ausencias de {r.EmpleadosConNovedades} empleado(s)",
            summary = new
            {
                employeesProcessed = r.EmpleadosConNovedades,
                overtimeDayHours = r.HorasExtraDiurnas,
                overtimeNightHours = r.HorasExtraNocturnas,
                absenceHours = r.HorasAusencia,
                totalOvertimeHours = r.HorasExtra
            }
        });
    }

    /// <summary>
    /// Obtiene el desglose detallado de deducciones aplicadas para un detalle de planilla.
    /// GET /api/payrollheaders/{id}/details/{detailId}/deducciones
    /// </summary>
    [HttpGet("{id}/details/{detailId}/deducciones")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult> GetDeduccionesDeDetalle(int id, int detailId)
    {
        var tenantId = _tenantContext.TenantId;

        var detail = await _context.PayrollDetails
            .Where(d => d.Id == detailId && d.PayrollHeaderId == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (detail == null)
            return NotFound(new { message = "Detalle de planilla no encontrado" });

        var deducciones = await _context.DeduccionesAplicadas
            .Where(da => da.PayrollDetailId == detailId && da.TenantId == tenantId)
            .OrderBy(da => da.OrdenAplicacion)
            .AsNoTracking()
            .ToListAsync();

        return Ok(new
        {
            payrollDetailId = detailId,
            salarioMinimoAplicado = detail.SalarioMinimoLegalAplicado,
            tuvoLimitacion = detail.TuvoLimitacionSalarioMinimo,
            montoLimitadoTotal = detail.MontoLimitadoPorSalarioMinimo,
            deducciones = deducciones.Select(da => new
            {
                da.Id,
                da.OrdenAplicacion,
                categoria = da.Categoria.ToString(),
                da.TipoDeduccion,
                da.Descripcion,
                da.NombreAcreedor,
                da.MontoSolicitado,
                da.MontoAplicado,
                da.MontoLimitado,
                da.RazonLimitacion,
                da.SaldoDisponibleAntes,
                da.SaldoDisponibleDespues,
                da.FechaTransferencia,
                da.ReferenciaTransferencia
            })
        });
    }

    /// <summary>
    /// Marca la transferencia de una deducción aplicada a su acreedor.
    /// PUT /api/payrollheaders/deducciones-aplicadas/{id}/transferencia
    /// </summary>
    [HttpPut("deducciones-aplicadas/{deduccionAplicadaId}/transferencia")]
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<ActionResult> MarcarTransferencia(int deduccionAplicadaId, [FromBody] MarcarTransferenciaRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var deduccionAplicada = await _context.DeduccionesAplicadas
            .FirstOrDefaultAsync(da => da.Id == deduccionAplicadaId && da.TenantId == tenantId);

        if (deduccionAplicada == null)
            return NotFound(new { message = "Deduccion aplicada no encontrada" });

        deduccionAplicada.FechaTransferencia = DateTime.SpecifyKind(request.FechaTransferencia, DateTimeKind.Utc);
        deduccionAplicada.ReferenciaTransferencia = request.ReferenciaTransferencia;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Transferencia registrada exitosamente" });
    }

    /// <summary>
    /// Asegura que el tenant tenga configuración de impuestos (CSS, SE, ISR). Si no existe, crea la configuración por defecto.
    /// Útil cuando "Calcular Planilla" falla por falta de configuración (p. ej. empresa creada antes de tener seed al arrancar).
    /// POST /api/payrollheaders/ensure-tax-config
    /// </summary>
    [HttpPost("ensure-tax-config")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
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

    /// <summary>
    /// Elimina una planilla completamente (cualquier estado).
    /// Para planillas con cálculos (Calculated/Approved/Paid): revierte el estado
    /// de préstamos y anticipos antes de borrar para mantener la integridad de datos.
    /// Las cascadas EF Core eliminan PayrollDetails, DeduccionesAplicadas y PayrollEmployeeHours.
    /// DELETE /api/payrollheaders/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(SystemPermission.PayrollDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var payrollHeader = await _context.PayrollHeaders
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (payrollHeader == null)
            return NotFound(new { message = "Planilla no encontrada." });

        // Solo revertir préstamos y anticipos si la planilla fue efectivamente pagada
        // (es cuando ProcessPrestamosAsync/ProcessAnticiposAsync realmente modificaron los saldos)
        if (payrollHeader.Status == PayrollStatus.Paid)
        {
            var payrollDetailIds = await _context.PayrollDetails
                .Where(pd => pd.PayrollHeaderId == id)
                .Select(pd => pd.Id)
                .ToListAsync();

            var deduccionesAplicadas = await _context.DeduccionesAplicadas
                .Where(d => d.TenantId == tenantId && payrollDetailIds.Contains(d.PayrollDetailId))
                .ToListAsync();

            // Revertir préstamos
            var prestamoIds = deduccionesAplicadas
                .Where(d => d.PrestamoId != null && d.MontoAplicado > 0m)
                .Select(d => d.PrestamoId!.Value)
                .Distinct()
                .ToList();

            if (prestamoIds.Any())
            {
                // Eliminar PagoPrestamo vinculados a esta planilla antes de revertir saldos
                var pagosARevertir = await _context.PagosPrestamos
                    .Where(p => p.PlanillaDetailId.HasValue && payrollDetailIds.Contains(p.PlanillaDetailId.Value))
                    .ToListAsync();
                _context.PagosPrestamos.RemoveRange(pagosARevertir);

                var prestamos = await _context.Prestamos
                    .Where(p => prestamoIds.Contains(p.Id) && p.TenantId == tenantId)
                    .ToListAsync();

                foreach (var prestamo in prestamos)
                {
                    var montoRevertir = deduccionesAplicadas
                        .Where(d => d.PrestamoId == prestamo.Id && d.MontoAplicado > 0m)
                        .Sum(d => d.MontoAplicado);
                    prestamo.MontoPendiente += montoRevertir;
                    if (prestamo.CuotasPagadas > 0)
                        prestamo.CuotasPagadas -= 1;
                    // Si estaba marcado como Pagado, reactivar
                    if (prestamo.Estado == EstadoPrestamo.Pagado)
                        prestamo.Estado = EstadoPrestamo.Activo;
                }
            }

            // Revertir anticipos a Aprobado (estaban aprobados antes de ser descontados)
            var anticipoIds = deduccionesAplicadas
                .Where(d => d.AnticipoId != null && d.MontoAplicado > 0m)
                .Select(d => d.AnticipoId!.Value)
                .Distinct()
                .ToList();

            if (anticipoIds.Any())
            {
                var anticipos = await _context.Anticipos
                    .Where(a => anticipoIds.Contains(a.Id) && a.TenantId == tenantId)
                    .ToListAsync();

                foreach (var anticipo in anticipos)
                {
                    anticipo.Estado = EstadoAnticipo.Aprobado;
                }
            }
        }

        _context.PayrollHeaders.Remove(payrollHeader);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Retorna el desglose detallado del cálculo de planilla para un empleado específico.
    /// GET /api/payrollheaders/{id}/details/{detailId}/breakdown
    /// </summary>
    [HttpGet("{id}/details/{detailId}/breakdown")]
    [RequirePermission(SystemPermission.PayrollView, SystemPermission.PayrollViewSelf)]
    public async Task<ActionResult<PayrollBreakdownDto>> GetDetailBreakdown(int id, int detailId)
    {
        var tenantId = _tenantContext.TenantId;

        var detail = await _context.PayrollDetails
            .Include(d => d.DeduccionesAplicadas)
            .Include(d => d.PayrollHeader)
            .FirstOrDefaultAsync(d => d.Id == detailId && d.PayrollHeaderId == id && d.TenantId == tenantId);

        if (detail == null)
            return NotFound(new { message = "Detalle no encontrado" });

        // Cargar horas del empleado para este período
        var horasEmpleado = await _context.PayrollEmployeeHours
            .FirstOrDefaultAsync(h => h.PayrollHeaderId == id && h.EmpleadoId == detail.EmpleadoId && h.TenantId == tenantId);

        // --- Ingresos ---
        var ingresos = new IngresosBreakdown(
            SalarioBase: detail.BaseSalary,
            HorasRegulares: horasEmpleado?.RegularPay ?? detail.BaseSalary,
            HorasDomingo: horasEmpleado?.SundayPay ?? 0,
            HorasFeriado: horasEmpleado?.HolidayPay ?? 0,
            HorasExtraDiurnas: horasEmpleado?.OvertimeDayPay ?? 0,
            HorasExtraNocturnas: horasEmpleado?.OvertimeNightPay ?? 0,
            HorasExtraFestivos: horasEmpleado?.OvertimeHolidayPay ?? 0,
            HorasExtraMixtas: horasEmpleado?.OvertimeMixedPay ?? 0,
            HorasExtraExceso: horasEmpleado?.OvertimeExcessPay ?? 0,
            Comisiones: detail.Commissions,
            Bonos: detail.Bonuses,
            GrossPay: detail.GrossPay
        );

        // --- CSS ---
        // Derivar base usada: si se aplicó tope, cssBase < grossPay
        decimal cssBase = detail.CssEmployee > 0 ? Math.Round(detail.CssEmployee / PayrollConstants.CssTasaEmpleado, 2) : detail.GrossPay;
        bool seAplicoTope = cssBase < detail.GrossPay - 0.01m;

        var css = new CssBreakdownDto(
            BaseUsada: cssBase,
            TopeSalarial: seAplicoTope ? cssBase : 0,
            SeAplicoTope: seAplicoTope,
            Tasa: PayrollConstants.CssTasaEmpleado * 100,
            Monto: detail.CssEmployee
        );

        // --- Seguro Educativo ---
        var se = new SeBreakdownDto(
            Base: detail.GrossPay,
            Tasa: 1.25m,
            Monto: detail.EducationalInsuranceEmployee
        );

        // --- ISR: el mismo camino del motor acumulativo, para que el número
        //     coincida con la fila de la ficha anual del empleado ---
        var payPeriodType = detail.PayrollHeader?.PayPeriodType ?? Vorluno.Planilla.Domain.Enums.PayPeriodType.Quincenal;
        var anioFiscal = (detail.PayrollHeader?.PeriodEndDate ?? DateTime.UtcNow).Year;
        var acumuladoAnterior = await _acumuladoFiscalService.ObtenerAcumuladoAsync(
            detail.EmpleadoId, anioFiscal, excluirPayrollHeaderId: id);
        var numeroPeriodo = await _acumuladoFiscalService.ObtenerNumeroPeriodoAsync(
            detail.EmpleadoId, anioFiscal, excluirPayrollHeaderId: id);

        var gastoRepresentacion = Math.Min(detail.GastoRepresentacion, detail.GrossPay);
        var gravablePeriodo = detail.GrossPay - gastoRepresentacion;
        var movimientos = new List<Vorluno.Planilla.Application.Services.MovimientoIsr>
        {
            new(Vorluno.Planilla.Domain.Enums.TratamientoIsr.GravableAcumulable, gravablePeriodo)
        };
        if (gastoRepresentacion > 0m)
            movimientos.Add(new(Vorluno.Planilla.Domain.Enums.TratamientoIsr.GastoRepresentacion, gastoRepresentacion));

        var motor = Vorluno.Planilla.Application.Services.MotorIsrPanama.Calcular(
            new Vorluno.Planilla.Application.Services.CorridaIsr
            {
                Frecuencia = payPeriodType,
                NumeroPeriodoEmpleado = numeroPeriodo,
                AcumuladoAnterior = acumuladoAnterior,
                Movimientos = movimientos
            });

        var periodosDePago = Vorluno.Planilla.Application.Services.MotorIsrPanama.ObtenerPeriodosEquivalentesAnuales(payPeriodType);
        var tieneSaldo = acumuladoAnterior.IsrRetenidoInicial > 0m || acumuladoAnterior.DecimoInicial > 0m || acumuladoAnterior.IngresoGravableInicial > 0m;
        var tieneImportados = await _context.DevengadosMensuales.AnyAsync(d => d.EmpleadoId == detail.EmpleadoId && d.Anio == anioFiscal);

        var isr = new IsrBreakdownDto(
            IngresoGravablePeriodo: gravablePeriodo,
            NumeroPeriodo: numeroPeriodo,
            PeriodoEquivalente: motor.PeriodoEquivalente,
            PeriodosDePago: Math.Round(periodosDePago, 2),
            Acumulado: motor.IngresoGravableAcumulado + motor.DecimoAcumulado,
            IngresoAnualProyectado: motor.IngresoAnualProyectado,
            RentaAnual: motor.IsrAnualProyectado,
            RentaPorPeriodo: Math.Round(motor.IsrAnualProyectado / periodosDePago, 2, MidpointRounding.AwayFromZero),
            ImpuestoCausado: motor.IsrDebidoAcumulado,
            RetenidoAntes: motor.IsrRetenidoTotalAnterior,
            ADescontar: motor.IsrDescontarPeriodo,
            GastoRepresentacion: gastoRepresentacion,
            IsrGastoRepresentacion: motor.IsrGastoRepresentacionPeriodo,
            IsrPeriodo: detail.IncomeTax,
            TieneSaldoInicial: tieneSaldo,
            TieneMesesImportados: tieneImportados
        );

        // --- Acreedores ---
        var acreedores = detail.DeduccionesAplicadas
            .OrderBy(d => d.OrdenAplicacion)
            .Select(d => new AcreedorItemDto(
                Descripcion: d.Descripcion,
                Categoria: d.Categoria.ToString(),
                MontoSolicitado: d.MontoSolicitado,
                MontoAplicado: d.MontoAplicado,
                FueLimitado: d.MontoLimitado > 0,
                RazonLimitacion: d.RazonLimitacion
            ))
            .ToList();

        var breakdown = new PayrollBreakdownDto(
            Ingresos: ingresos,
            Css: css,
            Se: se,
            Isr: isr,
            Acreedores: acreedores,
            TotalDeducciones: detail.TotalDeductions,
            NetPay: detail.NetPay
        );

        return Ok(breakdown);
    }
}

/// <summary>
/// DTO para crear una nueva planilla.
/// </summary>
/// <summary>Motivo de anulación, para auditoría.</summary>
public record CancelPayrollRequest(string? Reason);

public record CreatePayrollHeaderRequest(
    string? PayrollNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    /// <summary>
    /// Opcional: cada empresa paga cuando quiere y la fecha de pago no define
    /// nada en el cálculo. Si no viene, se usa el fin del período.
    /// </summary>
    DateTime? PayDate = null,
    PayPeriodType PayPeriodType = PayPeriodType.Quincenal,
    TipoPlanilla TipoPlanilla = TipoPlanilla.Regular
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
    decimal DisabilityHours = 0,
    decimal Commissions = 0
);

public record MarcarTransferenciaRequest(
    DateTime FechaTransferencia,
    string? ReferenciaTransferencia
);
