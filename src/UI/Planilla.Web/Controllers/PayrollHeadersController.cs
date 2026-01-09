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

    public PayrollHeadersController(
        ApplicationDbContext context,
        PayrollStateMachine stateMachine,
        PayrollCalculationOrchestratorPortable orchestrator,
        ITenantContext tenantContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    /// <summary>
    /// Lista todas las planillas del tenant actual con filtros opcionales.
    /// GET /api/payrollheaders?status=Calculated
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<IEnumerable<PayrollHeader>>> GetPayrollHeaders(
        [FromQuery] PayrollStatus? status)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.PayrollHeaders
            .Where(p => p.TenantId == tenantId) // ✅ SEGURIDAD: Filtrado por tenant obligatorio
            .Include(p => p.Details)
            .AsNoTracking()
            .AsQueryable();

        // Filtrar por Status si se especifica
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var payrollHeaders = await query
            .OrderByDescending(p => p.PeriodStartDate)
            .ToListAsync();

        return Ok(payrollHeaders);
    }

    /// <summary>
    /// Obtiene una planilla específica por ID del tenant actual.
    /// GET /api/payrollheaders/{id}
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<PayrollHeader>> GetPayrollHeader(int id)
    {
        var tenantId = _tenantContext.TenantId;
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
            Status = PayrollStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _context.PayrollHeaders.Add(payrollHeader);

        try
        {
            await _context.SaveChangesAsync();
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

            foreach (var employee in activeEmployees)
            {
                // Calcular usando el orquestador
                var calculationResult = await _orchestrator.CalculateEmployeePayrollAsync(
                    companyId: tenantId,
                    grossPay: employee.SalarioBase,
                    payFrequency: employee.PayFrequency,
                    yearsCotized: employee.YearsCotized,
                    averageSalaryLast10Years: employee.AverageSalaryLast10Years,
                    cssRiskPercentage: employee.CssRiskPercentage,
                    dependents: employee.Dependents,
                    isSubjectToCss: employee.IsSubjectToCss,
                    isSubjectToEducationalInsurance: employee.IsSubjectToEducationalInsurance,
                    isSubjectToIncomeTax: employee.IsSubjectToIncomeTax,
                    calculationDate: DateTime.UtcNow
                );

                // Crear PayrollDetail con los resultados
                var detail = new PayrollDetail
                {
                    PayrollHeaderId = payrollHeader.Id,
                    EmpleadoId = employee.Id,
                    GrossPay = calculationResult.GrossPay,
                    BaseSalary = employee.SalarioBase,
                    OvertimePay = 0, // TODO: Implementar overtime en futuras fases
                    Bonuses = 0,
                    Commissions = 0,
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
                    TenantId = tenantId // ✅ SEGURIDAD: TenantId del tenant
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
}

/// <summary>
/// DTO para crear una nueva planilla.
/// </summary>
public record CreatePayrollHeaderRequest(
    string PayrollNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime PayDate
);
