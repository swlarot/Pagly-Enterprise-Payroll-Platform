# Especificaciones Técnicas: Implementación de Eliminación de Usuarios y Empleados
## Sistema Planilla SaaS - Especificaciones de Desarrollo

**Fecha**: 2026-02-01
**Versión**: 1.0
**Documento Base**: DELETION-ANALYSIS-COMPLIANCE.md

---

## 1. FASE 1: MEJORAS A ELIMINACIÓN DE USUARIOS

### 1.1 Validación de Último Owner

**Archivo:** `src/UI/Planilla.Web/Controllers/AdminController.cs`

**Modificar método:** `DeleteUser(string userId)` (línea 1063)

**Insertar después de línea 1091:**

```csharp
// VALIDACIÓN: No eliminar si es el último Owner de algún tenant
var ownerships = await _context.TenantUsers
    .Include(tu => tu.Tenant)
    .Where(tu => tu.UserId == userId && tu.Role == TenantRole.Owner && tu.IsActive)
    .ToListAsync();

foreach (var ownership in ownerships)
{
    var ownersCount = await _context.TenantUsers
        .Where(tu => tu.TenantId == ownership.TenantId
                  && tu.Role == TenantRole.Owner
                  && tu.IsActive
                  && tu.UserId != userId)
        .CountAsync();

    if (ownersCount == 0)
    {
        return BadRequest(new
        {
            error = $"No se puede eliminar este usuario porque es el único Owner del tenant '{ownership.Tenant.Name}'. " +
                    $"Asigne otro Owner antes de eliminar este usuario.",
            tenantId = ownership.TenantId,
            tenantName = ownership.Tenant.Name
        });
    }
}
```

**Testing:**
- Caso 1: Usuario es único Owner de 1 tenant → DEBE BLOQUEAR
- Caso 2: Usuario es Owner de 2 tenants, pero ambos tienen otro Owner → DEBE PERMITIR
- Caso 3: Usuario es Owner de 2 tenants, uno sin otro Owner → DEBE BLOQUEAR
- Caso 4: Usuario no es Owner de ningún tenant → DEBE PERMITIR

---

### 1.2 Endpoint de Desvinculación Usuario ↔ Empleado

**Archivo:** `src/UI/Planilla.Web/Controllers/EmpleadosController.cs`

**Agregar método:**

```csharp
/// <summary>
/// POST /api/empleados/{id}/unlink-user - Desvincula un usuario de un empleado
/// Requiere: Owner, Admin, Manager
/// </summary>
[HttpPost("{id}/unlink-user")]
[Authorize]
public async Task<IActionResult> UnlinkUserFromEmpleado(int id)
{
    try
    {
        var tenantId = this.GetCurrentTenantId();

        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (empleado == null)
        {
            return NotFound(new { error = "Empleado no encontrado" });
        }

        if (string.IsNullOrEmpty(empleado.UserId))
        {
            return BadRequest(new { error = "El empleado no está vinculado a ningún usuario" });
        }

        var previousUserId = empleado.UserId;
        empleado.UserId = null;

        _context.Empleados.Update(empleado);
        await _context.SaveChangesAsync();

        // Audit log
        var currentUserId = User.FindFirst("sub")?.Value;
        var auditLog = new AuditLogEntry
        {
            TenantId = tenantId,
            ActorUserId = currentUserId ?? "system",
            ActorEmail = User.FindFirst("email")?.Value ?? "system",
            Action = "EmpleadoUserUnlinked",
            EntityType = "Empleado",
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers["User-Agent"],
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                EmpleadoId = id,
                PreviousUserId = previousUserId,
                UnlinkedBy = currentUserId,
                UnlinkedAt = DateTime.UtcNow
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogEntries.Add(auditLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} unlinked user {PreviousUserId} from empleado {EmpleadoId} in tenant {TenantId}",
            currentUserId, previousUserId, id, tenantId);

        return Ok(new { success = true, message = "Usuario desvinculado exitosamente" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error unlinking user from empleado {EmpleadoId}", id);
        return StatusCode(500, new { error = "Error al desvincular usuario" });
    }
}
```

---

## 2. FASE 2: ELIMINACIÓN DE EMPLEADOS

### 2.1 Migración de Base de Datos

**Crear migración:**

```bash
dotnet ef migrations add AddEmpleadoDeletionFields --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
```

**Archivo:** `src/Core/Planilla.Domain/Entities/Empleado.cs`

**Agregar propiedades después de línea 37 (después de `EstaActivo`):**

```csharp
/// <summary>
/// Indica si el empleado ha sido eliminado del sistema (soft delete)
/// Los empleados eliminados no aparecen en listas normales pero se preservan para historial
/// </summary>
public bool IsDeleted { get; set; } = false;

/// <summary>
/// Fecha en la que el empleado fue eliminado
/// </summary>
public DateTime? DeletedAt { get; set; }

/// <summary>
/// ID del usuario que eliminó este empleado
/// </summary>
[StringLength(450)]
public string? DeletedBy { get; set; }

/// <summary>
/// Razón de la eliminación: Renuncia, Despido, FinDeContrato, Jubilacion, Otro
/// </summary>
[StringLength(50)]
public string? DeletionReason { get; set; }

/// <summary>
/// Observaciones adicionales sobre la eliminación
/// </summary>
[StringLength(500)]
public string? DeletionNotes { get; set; }
```

---

### 2.2 Servicio de Validación

**Archivo:** `src/Core/Planilla.Application/Services/EmpleadoValidationService.cs` (CREAR)

```csharp
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Application.Services;

public class DeletionValidationResult
{
    public bool CanDelete { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public interface IEmpleadoValidationService
{
    Task<DeletionValidationResult> ValidateForDeletionAsync(int empleadoId, int tenantId);
}

public class EmpleadoValidationService : IEmpleadoValidationService
{
    private readonly ApplicationDbContext _context;

    public EmpleadoValidationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeletionValidationResult> ValidateForDeletionAsync(int empleadoId, int tenantId)
    {
        var result = new DeletionValidationResult { CanDelete = true };

        // 1. BLOQUEADOR: Préstamos activos
        var prestamosActivos = await _context.Prestamos
            .Where(p => p.EmpleadoId == empleadoId
                     && p.TenantId == tenantId
                     && p.Estado != EstadoPrestamo.Pagado
                     && p.Estado != EstadoPrestamo.Cancelado)
            .ToListAsync();

        if (prestamosActivos.Any())
        {
            result.CanDelete = false;
            foreach (var prestamo in prestamosActivos)
            {
                result.Blockers.Add(
                    $"Préstamo activo con saldo de B/. {prestamo.MontoPendiente:N2} " +
                    $"({prestamo.CuotasPagadas}/{prestamo.NumeroCuotas} cuotas pagadas)");
            }
        }

        // 2. BLOQUEADOR: Deducciones judiciales activas
        var deduccionesJudiciales = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId
                     && d.TenantId == tenantId
                     && d.EstaActivo
                     && (d.TipoDeduccion == TipoDeduccion.PensionAlimenticia
                      || d.TipoDeduccion == TipoDeduccion.EmbargoJudicial))
            .ToListAsync();

        if (deduccionesJudiciales.Any())
        {
            result.CanDelete = false;
            foreach (var deduccion in deduccionesJudiciales)
            {
                result.Blockers.Add(
                    $"Deducción judicial activa: {deduccion.Descripcion} " +
                    $"(Referencia: {deduccion.Referencia ?? "N/A"})");
            }
        }

        // 3. BLOQUEADOR: Anticipos aprobados no descontados
        var anticiposPendientes = await _context.Anticipos
            .Where(a => a.EmpleadoId == empleadoId
                     && a.TenantId == tenantId
                     && a.Estado == EstadoAnticipo.Aprobado
                     && a.PlanillaId == null)
            .ToListAsync();

        if (anticiposPendientes.Any())
        {
            result.CanDelete = false;
            foreach (var anticipo in anticiposPendientes)
            {
                result.Blockers.Add(
                    $"Anticipo aprobado pendiente de descuento: B/. {anticipo.Monto:N2} " +
                    $"(Aprobado: {anticipo.FechaAprobacion:dd/MM/yyyy})");
            }
        }

        // 4. WARNING: Horas extra aprobadas no pagadas
        var horasExtraPendientes = await _context.HorasExtras
            .Where(h => h.EmpleadoId == empleadoId
                     && h.TenantId == tenantId
                     && h.EstaAprobada
                     && h.PlanillaDetailId == null)
            .ToListAsync();

        if (horasExtraPendientes.Any())
        {
            var totalHoras = horasExtraPendientes.Sum(h => h.CantidadHoras);
            var totalMonto = horasExtraPendientes.Sum(h => h.MontoCalculado ?? 0);
            result.Warnings.Add(
                $"Tiene {horasExtraPendientes.Count} registros de horas extra aprobadas pendientes de pago " +
                $"({totalHoras:N2} horas, B/. {totalMonto:N2})");
        }

        // 5. WARNING: Ausencias no procesadas
        var ausenciasPendientes = await _context.Ausencias
            .Where(a => a.EmpleadoId == empleadoId
                     && a.TenantId == tenantId
                     && a.PlanillaDetailId == null
                     && a.AfectaSalario)
            .ToListAsync();

        if (ausenciasPendientes.Any())
        {
            var totalDias = ausenciasPendientes.Sum(a => a.DiasAusencia);
            result.Warnings.Add(
                $"Tiene {ausenciasPendientes.Count} ausencias no procesadas " +
                $"({totalDias:N2} días)");
        }

        // 6. WARNING: Aparece en planillas DRAFT
        var planillasDraft = await _context.PayrollDetails
            .Include(pd => pd.PayrollHeader)
            .Where(pd => pd.EmpleadoId == empleadoId
                      && pd.TenantId == tenantId
                      && pd.PayrollHeader.Status == PayrollStatus.Draft)
            .Select(pd => pd.PayrollHeader)
            .Distinct()
            .ToListAsync();

        if (planillasDraft.Any())
        {
            foreach (var planilla in planillasDraft)
            {
                result.Warnings.Add(
                    $"Aparece en planilla DRAFT '{planilla.PayrollNumber}' " +
                    $"del período {planilla.PeriodStartDate:dd/MM/yyyy} al {planilla.PeriodEndDate:dd/MM/yyyy}");
            }
        }

        return result;
    }
}
```

**Registrar servicio en `Program.cs`:**

```csharp
// Agregar después de línea donde se registran servicios de aplicación
builder.Services.AddScoped<IEmpleadoValidationService, EmpleadoValidationService>();
```

---

### 2.3 DTOs

**Archivo:** `src/Core/Planilla.Application/DTOs/EmpleadoDtos.cs`

**Agregar al final del archivo:**

```csharp
/// <summary>
/// DTO para solicitud de eliminación de empleado
/// </summary>
public class DeleteEmpleadoDto
{
    [Required(ErrorMessage = "La razón de eliminación es obligatoria")]
    public string Reason { get; set; } = string.Empty;

    public DateTime? EffectiveDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Solo disponible para Owners - permite forzar eliminación ignorando warnings
    /// NO ignora blockers (préstamos activos, deducciones judiciales)
    /// </summary>
    public bool ForceDelete { get; set; } = false;
}

/// <summary>
/// DTO para resultado de validación de eliminación
/// </summary>
public class DeletionValidationResultDto
{
    public bool CanDelete { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

---

### 2.4 Endpoints en EmpleadosController

**Archivo:** `src/UI/Planilla.Web/Controllers/EmpleadosController.cs`

**Agregar endpoints:**

```csharp
/// <summary>
/// GET /api/empleados/{id}/deletion-validation - Valida si un empleado puede ser eliminado
/// Requiere: Owner, Admin, Manager
/// </summary>
[HttpGet("{id}/deletion-validation")]
[Authorize]
public async Task<IActionResult> ValidateEmpleadoDeletion(int id)
{
    try
    {
        var tenantId = this.GetCurrentTenantId();

        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (empleado == null)
        {
            return NotFound(new { error = "Empleado no encontrado" });
        }

        if (empleado.IsDeleted)
        {
            return BadRequest(new { error = "El empleado ya está eliminado" });
        }

        var validationService = HttpContext.RequestServices
            .GetRequiredService<IEmpleadoValidationService>();

        var result = await validationService.ValidateForDeletionAsync(id, tenantId);

        var dto = new DeletionValidationResultDto
        {
            CanDelete = result.CanDelete,
            Blockers = result.Blockers,
            Warnings = result.Warnings
        };

        return Ok(dto);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error validating empleado deletion for {EmpleadoId}", id);
        return StatusCode(500, new { error = "Error al validar eliminación" });
    }
}

/// <summary>
/// DELETE /api/empleados/{id} - Elimina un empleado (soft delete)
/// Requiere: Owner (force), Admin, Manager
/// </summary>
[HttpDelete("{id}")]
[Authorize]
public async Task<IActionResult> DeleteEmpleado(int id, [FromBody] DeleteEmpleadoDto dto)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        var tenantId = this.GetCurrentTenantId();
        var currentUserId = User.FindFirst("sub")?.Value;
        var currentUserRole = this.GetCurrentTenantRole();

        // 1. Verificar que el empleado existe
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (empleado == null)
        {
            return NotFound(new { error = "Empleado no encontrado" });
        }

        if (empleado.IsDeleted)
        {
            return BadRequest(new { error = "El empleado ya está eliminado" });
        }

        // 2. Validar permisos
        if (currentUserRole != TenantRole.Owner && dto.ForceDelete)
        {
            return Forbid(); // Solo Owners pueden forzar
        }

        // 3. Ejecutar validaciones
        var validationService = HttpContext.RequestServices
            .GetRequiredService<IEmpleadoValidationService>();

        var validation = await validationService.ValidateForDeletionAsync(id, tenantId);

        // 4. BLOQUEADORES: Siempre impiden eliminación (incluso con ForceDelete)
        if (validation.Blockers.Any())
        {
            return BadRequest(new
            {
                error = "No se puede eliminar el empleado. Resuelva los siguientes bloqueadores:",
                blockers = validation.Blockers,
                canForce = false
            });
        }

        // 5. WARNINGS: Impiden eliminación a menos que ForceDelete = true
        if (validation.Warnings.Any() && !dto.ForceDelete)
        {
            return BadRequest(new
            {
                error = "El empleado tiene advertencias. Use ForceDelete=true para continuar.",
                warnings = validation.Warnings,
                canForce = true,
                requiresOwner = true
            });
        }

        // 6. Marcar como eliminado
        empleado.IsDeleted = true;
        empleado.DeletedAt = dto.EffectiveDate ?? DateTime.UtcNow;
        empleado.DeletedBy = currentUserId;
        empleado.DeletionReason = dto.Reason;
        empleado.DeletionNotes = dto.Notes;

        // También marcar como inactivo
        empleado.EstaActivo = false;

        // Desvincular usuario si existe
        var previousUserId = empleado.UserId;
        empleado.UserId = null;

        _context.Empleados.Update(empleado);
        await _context.SaveChangesAsync();

        // 7. Si hay horas extra pendientes, marcarlas como no aprobadas
        var horasExtraPendientes = await _context.HorasExtras
            .Where(h => h.EmpleadoId == id
                     && h.TenantId == tenantId
                     && h.EstaAprobada
                     && h.PlanillaDetailId == null)
            .ToListAsync();

        foreach (var hora in horasExtraPendientes)
        {
            hora.EstaAprobada = false;
            hora.Observaciones = $"Empleado eliminado - Horas extra canceladas automáticamente";
        }

        if (horasExtraPendientes.Any())
        {
            _context.HorasExtras.UpdateRange(horasExtraPendientes);
            await _context.SaveChangesAsync();
        }

        // 8. Eliminar de planillas DRAFT
        var payrollDetailsDraft = await _context.PayrollDetails
            .Include(pd => pd.PayrollHeader)
            .Where(pd => pd.EmpleadoId == id
                      && pd.TenantId == tenantId
                      && pd.PayrollHeader.Status == PayrollStatus.Draft)
            .ToListAsync();

        if (payrollDetailsDraft.Any())
        {
            _context.PayrollDetails.RemoveRange(payrollDetailsDraft);
            await _context.SaveChangesAsync();

            // Recalcular totales de planillas afectadas
            var affectedPayrolls = payrollDetailsDraft
                .Select(pd => pd.PayrollHeaderId)
                .Distinct()
                .ToList();

            foreach (var payrollId in affectedPayrolls)
            {
                var payroll = await _context.PayrollHeaders
                    .Include(ph => ph.Details)
                    .FirstOrDefaultAsync(ph => ph.Id == payrollId);

                if (payroll != null)
                {
                    payroll.TotalGrossPay = payroll.Details.Sum(d => d.GrossPay);
                    payroll.TotalDeductions = payroll.Details.Sum(d => d.TotalDeductions);
                    payroll.TotalNetPay = payroll.Details.Sum(d => d.NetPay);
                    payroll.TotalEmployerCost = payroll.Details.Sum(d => d.EmployerCost);
                    payroll.UpdatedAt = DateTime.UtcNow;

                    _context.PayrollHeaders.Update(payroll);
                }
            }

            await _context.SaveChangesAsync();
        }

        // 9. Audit log
        var auditLog = new AuditLogEntry
        {
            TenantId = tenantId,
            ActorUserId = currentUserId ?? "system",
            ActorEmail = User.FindFirst("email")?.Value ?? "system",
            Action = "EmpleadoDeleted",
            EntityType = "Empleado",
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers["User-Agent"],
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                EmpleadoId = id,
                EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellido}",
                EmpleadoCedula = empleado.NumeroIdentificacion,
                Reason = dto.Reason,
                Notes = dto.Notes,
                EffectiveDate = empleado.DeletedAt,
                DeletedBy = currentUserId,
                ForceDelete = dto.ForceDelete,
                Warnings = validation.Warnings,
                HorasExtraCanceladas = horasExtraPendientes.Count,
                PlanillasDraftAfectadas = payrollDetailsDraft.Count,
                PreviousUserId = previousUserId
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogEntries.Add(auditLog);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        _logger.LogInformation(
            "User {UserId} deleted empleado {EmpleadoId} ({EmpleadoNombre}) in tenant {TenantId}. Reason: {Reason}",
            currentUserId, id, $"{empleado.Nombre} {empleado.Apellido}", tenantId, dto.Reason);

        // TODO: Enviar email a Owners si hay warnings

        return Ok(new
        {
            success = true,
            message = $"Empleado {empleado.Nombre} {empleado.Apellido} eliminado exitosamente",
            warnings = validation.Warnings,
            horasExtraCanceladas = horasExtraPendientes.Count,
            planillasAfectadas = payrollDetailsDraft.Select(pd => pd.PayrollHeaderId).Distinct().Count()
        });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error deleting empleado {EmpleadoId}", id);
        return StatusCode(500, new { error = "Error al eliminar empleado" });
    }
}

/// <summary>
/// POST /api/empleados/{id}/reactivate - Reactiva un empleado eliminado
/// Requiere: Owner, Admin
/// </summary>
[HttpPost("{id}/reactivate")]
[Authorize]
public async Task<IActionResult> ReactivateEmpleado(int id)
{
    try
    {
        var tenantId = this.GetCurrentTenantId();
        var currentUserId = User.FindFirst("sub")?.Value;
        var currentUserRole = this.GetCurrentTenantRole();

        // Solo Owner y Admin pueden reactivar
        if (currentUserRole != TenantRole.Owner && currentUserRole != TenantRole.Admin)
        {
            return Forbid();
        }

        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (empleado == null)
        {
            return NotFound(new { error = "Empleado no encontrado" });
        }

        if (!empleado.IsDeleted)
        {
            return BadRequest(new { error = "El empleado no está eliminado" });
        }

        // Verificar límites del plan
        var tenant = await _context.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        var currentEmpleadosCount = await _context.Empleados
            .CountAsync(e => e.TenantId == tenantId && !e.IsDeleted);

        var maxEmpleados = tenant?.Subscription?.GetEffectiveMaxEmployees() ?? 0;

        if (currentEmpleadosCount >= maxEmpleados)
        {
            return BadRequest(new
            {
                error = $"El tenant ha alcanzado el límite de {maxEmpleados} empleados activos para el plan {tenant?.Subscription?.Plan}. " +
                        $"Actualmente tiene {currentEmpleadosCount} empleados activos."
            });
        }

        // Reactivar
        empleado.IsDeleted = false;
        empleado.DeletedAt = null;
        empleado.DeletedBy = null;
        empleado.DeletionReason = null;
        empleado.DeletionNotes = null;
        empleado.EstaActivo = true;

        _context.Empleados.Update(empleado);
        await _context.SaveChangesAsync();

        // Audit log
        var auditLog = new AuditLogEntry
        {
            TenantId = tenantId,
            ActorUserId = currentUserId ?? "system",
            ActorEmail = User.FindFirst("email")?.Value ?? "system",
            Action = "EmpleadoReactivated",
            EntityType = "Empleado",
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers["User-Agent"],
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                EmpleadoId = id,
                EmpleadoNombre = $"{empleado.Nombre} {empleado.Apellido}",
                ReactivatedBy = currentUserId,
                ReactivatedAt = DateTime.UtcNow
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogEntries.Add(auditLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} reactivated empleado {EmpleadoId} in tenant {TenantId}",
            currentUserId, id, tenantId);

        return Ok(new
        {
            success = true,
            message = $"Empleado {empleado.Nombre} {empleado.Apellido} reactivado exitosamente"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error reactivating empleado {EmpleadoId}", id);
        return StatusCode(500, new { error = "Error al reactivar empleado" });
    }
}
```

---

### 2.5 Filtrado en Queries Existentes

**IMPORTANTE:** Agregar filtro `!e.IsDeleted` en todas las queries de empleados.

**Archivo:** `src/UI/Planilla.Web/Controllers/EmpleadosController.cs`

**Modificar método `GetEmpleados()` existente:**

```csharp
// Línea ~50: Modificar query
var empleados = await _context.Empleados
    .Where(e => e.TenantId == tenantId && !e.IsDeleted)  // AGREGAR !e.IsDeleted
    .Include(e => e.Departamento)
    .Include(e => e.Posicion)
    .OrderBy(e => e.Apellido)
    .ThenBy(e => e.Nombre)
    .ToListAsync();
```

**Agregar endpoint para incluir eliminados (solo para reportes):**

```csharp
/// <summary>
/// GET /api/empleados/all-including-deleted - Obtiene TODOS los empleados (incluyendo eliminados)
/// Requiere: Owner, Admin
/// </summary>
[HttpGet("all-including-deleted")]
[Authorize]
public async Task<IActionResult> GetAllEmpleadosIncludingDeleted()
{
    try
    {
        var tenantId = this.GetCurrentTenantId();
        var currentUserRole = this.GetCurrentTenantRole();

        // Solo Owner y Admin pueden ver eliminados
        if (currentUserRole != TenantRole.Owner && currentUserRole != TenantRole.Admin)
        {
            return Forbid();
        }

        var empleados = await _context.Empleados
            .Where(e => e.TenantId == tenantId)  // SIN filtrar IsDeleted
            .Include(e => e.Departamento)
            .Include(e => e.Posicion)
            .OrderByDescending(e => e.EstaActivo)
            .ThenByDescending(e => e.IsDeleted)
            .ThenBy(e => e.Apellido)
            .ThenBy(e => e.Nombre)
            .Select(e => new EmpleadoDto
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Apellido = e.Apellido,
                NumeroIdentificacion = e.NumeroIdentificacion,
                Email = e.Email,
                SalarioBase = e.SalarioBase,
                FechaContratacion = e.FechaContratacion,
                EstaActivo = e.EstaActivo,
                IsDeleted = e.IsDeleted,
                DeletedAt = e.DeletedAt,
                DeletionReason = e.DeletionReason,
                DepartamentoId = e.DepartamentoId,
                DepartamentoNombre = e.Departamento != null ? e.Departamento.Nombre : null,
                PosicionId = e.PosicionId,
                PosicionNombre = e.Posicion != null ? e.Posicion.Nombre : null
            })
            .ToListAsync();

        return Ok(empleados);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting all empleados including deleted");
        return StatusCode(500, new { error = "Error al obtener empleados" });
    }
}
```

---

## 3. FASE 3: FRONTEND - COMPONENTES REACT

### 3.1 Modal de Confirmación de Eliminación

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/components/empleados/DeleteEmpleadoModal.tsx` (CREAR)

```tsx
import React, { useState, useEffect } from 'react';
import { X, AlertTriangle, XCircle, Info } from 'lucide-react';
import { Button } from '../ui/Button';
import { Modal } from '../ui/Modal';
import toast from 'react-hot-toast';
import { empleadosService } from '../../services/empleadosService';

interface Empleado {
  id: number;
  nombre: string;
  apellido: string;
  numeroIdentificacion: string;
  departamentoNombre?: string;
}

interface DeletionValidation {
  canDelete: boolean;
  blockers: string[];
  warnings: string[];
}

interface DeleteEmpleadoModalProps {
  empleado: Empleado;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const DeleteEmpleadoModal: React.FC<DeleteEmpleadoModalProps> = ({
  empleado,
  isOpen,
  onClose,
  onSuccess
}) => {
  const [validation, setValidation] = useState<DeletionValidation | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const [reason, setReason] = useState('');
  const [notes, setNotes] = useState('');
  const [forceDelete, setForceDelete] = useState(false);
  const [understood, setUnderstood] = useState(false);

  useEffect(() => {
    if (isOpen) {
      loadValidation();
    } else {
      // Reset form
      setValidation(null);
      setReason('');
      setNotes('');
      setForceDelete(false);
      setUnderstood(false);
    }
  }, [isOpen, empleado.id]);

  const loadValidation = async () => {
    try {
      setIsLoading(true);
      const result = await empleadosService.validateDeletion(empleado.id);
      setValidation(result);
    } catch (error: any) {
      toast.error(error.message || 'Error al validar eliminación');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!reason) {
      toast.error('Debe seleccionar una razón de eliminación');
      return;
    }

    if (validation && validation.warnings.length > 0 && !understood) {
      toast.error('Debe confirmar que entiende las implicaciones');
      return;
    }

    try {
      setIsDeleting(true);

      await empleadosService.delete(empleado.id, {
        reason,
        notes,
        forceDelete
      });

      toast.success(`Empleado ${empleado.nombre} ${empleado.apellido} eliminado exitosamente`);
      onSuccess();
      onClose();
    } catch (error: any) {
      if (error.canForce && !forceDelete) {
        toast.error('Debe activar "Forzar eliminación" para continuar (solo Owners)');
      } else {
        toast.error(error.message || 'Error al eliminar empleado');
      }
    } finally {
      setIsDeleting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Confirmar Eliminación de Empleado">
      <div className="space-y-6">
        {/* Información del empleado */}
        <div className="bg-gray-50 p-4 rounded-lg">
          <h3 className="font-semibold text-gray-900">
            {empleado.nombre} {empleado.apellido}
          </h3>
          <p className="text-sm text-gray-600">Cédula: {empleado.numeroIdentificacion}</p>
          {empleado.departamentoNombre && (
            <p className="text-sm text-gray-600">Departamento: {empleado.departamentoNombre}</p>
          )}
        </div>

        {/* Loading validation */}
        {isLoading && (
          <div className="text-center py-4">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
            <p className="text-sm text-gray-600 mt-2">Validando eliminación...</p>
          </div>
        )}

        {/* Validation results */}
        {!isLoading && validation && (
          <>
            {/* Blockers */}
            {validation.blockers.length > 0 && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                <div className="flex items-start">
                  <XCircle className="w-5 h-5 text-red-600 mt-0.5 mr-2 flex-shrink-0" />
                  <div className="flex-1">
                    <h4 className="font-semibold text-red-900 mb-2">
                      BLOQUEADORES (No se puede eliminar):
                    </h4>
                    <ul className="space-y-1">
                      {validation.blockers.map((blocker, idx) => (
                        <li key={idx} className="text-sm text-red-800">• {blocker}</li>
                      ))}
                    </ul>
                    <p className="text-sm text-red-700 mt-3 font-medium">
                      Para eliminar este empleado, debe resolver los bloqueadores listados arriba.
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Warnings */}
            {validation.warnings.length > 0 && validation.canDelete && (
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                <div className="flex items-start">
                  <AlertTriangle className="w-5 h-5 text-yellow-600 mt-0.5 mr-2 flex-shrink-0" />
                  <div className="flex-1">
                    <h4 className="font-semibold text-yellow-900 mb-2">ADVERTENCIAS:</h4>
                    <ul className="space-y-1">
                      {validation.warnings.map((warning, idx) => (
                        <li key={idx} className="text-sm text-yellow-800">• {warning}</li>
                      ))}
                    </ul>
                  </div>
                </div>
              </div>
            )}

            {/* Info sobre lo que sucederá */}
            {validation.canDelete && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <div className="flex items-start">
                  <Info className="w-5 h-5 text-blue-600 mt-0.5 mr-2 flex-shrink-0" />
                  <div className="flex-1">
                    <h4 className="font-semibold text-blue-900 mb-2">Al eliminar este empleado:</h4>
                    <ul className="space-y-1 text-sm text-blue-800">
                      <li>✓ Se marcará como inactivo en el sistema</li>
                      <li>✓ Se preservará su historial de planillas</li>
                      <li>✓ Las horas extra pendientes NO se pagarán</li>
                      <li>✓ Se excluirá de planillas en estado DRAFT</li>
                      <li>✓ El usuario vinculado (si existe) se desvinculará</li>
                    </ul>
                  </div>
                </div>
              </div>
            )}

            {/* Form - Solo si puede eliminar */}
            {validation.canDelete && (
              <div className="space-y-4">
                {/* Razón */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Razón de eliminación *
                  </label>
                  <select
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                    required
                  >
                    <option value="">Seleccionar...</option>
                    <option value="Renuncia">Renuncia voluntaria</option>
                    <option value="Despido">Despido justificado</option>
                    <option value="DespidoSinCausa">Despido sin justa causa</option>
                    <option value="FinContrato">Fin de contrato temporal</option>
                    <option value="Jubilacion">Jubilación</option>
                    <option value="Otro">Otro</option>
                  </select>
                </div>

                {/* Observaciones */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Observaciones (opcional)
                  </label>
                  <textarea
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                    rows={3}
                    maxLength={500}
                    placeholder="Información adicional sobre la eliminación..."
                  />
                  <p className="text-xs text-gray-500 mt-1">{notes.length}/500 caracteres</p>
                </div>

                {/* Checkbox de confirmación */}
                {validation.warnings.length > 0 && (
                  <div className="flex items-start">
                    <input
                      type="checkbox"
                      id="understood"
                      checked={understood}
                      onChange={(e) => setUnderstood(e.target.checked)}
                      className="mt-1 mr-2"
                    />
                    <label htmlFor="understood" className="text-sm text-gray-700">
                      Entiendo las advertencias y deseo proceder con la eliminación
                    </label>
                  </div>
                )}
              </div>
            )}
          </>
        )}

        {/* Botones */}
        <div className="flex justify-end space-x-3 pt-4 border-t">
          <Button variant="secondary" onClick={onClose} disabled={isDeleting}>
            Cancelar
          </Button>

          {validation && validation.canDelete && (
            <Button
              variant="danger"
              onClick={handleDelete}
              disabled={isDeleting || !reason || (validation.warnings.length > 0 && !understood)}
            >
              {isDeleting ? 'Eliminando...' : 'Eliminar Empleado'}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  );
};
```

---

### 3.2 Servicio de Empleados (Frontend)

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/services/empleadosService.ts`

**Agregar métodos:**

```typescript
// Agregar al final del archivo existente

export const empleadosService = {
  // ... métodos existentes ...

  /**
   * Valida si un empleado puede ser eliminado
   */
  validateDeletion: async (empleadoId: number): Promise<DeletionValidation> => {
    const response = await fetch(`/api/empleados/${empleadoId}/deletion-validation`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('token')}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Error al validar eliminación');
    }

    return response.json();
  },

  /**
   * Elimina un empleado (soft delete)
   */
  delete: async (empleadoId: number, data: DeleteEmpleadoDto): Promise<void> => {
    const response = await fetch(`/api/empleados/${empleadoId}`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('token')}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(data)
    });

    if (!response.ok) {
      const error = await response.json();
      throw error;
    }

    return response.json();
  },

  /**
   * Reactiva un empleado eliminado
   */
  reactivate: async (empleadoId: number): Promise<void> => {
    const response = await fetch(`/api/empleados/${empleadoId}/reactivate`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('token')}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Error al reactivar empleado');
    }

    return response.json();
  }
};

// Types
interface DeletionValidation {
  canDelete: boolean;
  blockers: string[];
  warnings: string[];
}

interface DeleteEmpleadoDto {
  reason: string;
  effectiveDate?: string;
  notes?: string;
  forceDelete?: boolean;
}
```

---

### 3.3 Integración en EmpleadosPage

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/pages/EmpleadosPage.jsx`

**Modificar para agregar botón de eliminación y modal:**

```jsx
// Agregar import
import { DeleteEmpleadoModal } from '../components/empleados/DeleteEmpleadoModal';

// Agregar state
const [deleteModalOpen, setDeleteModalOpen] = useState(false);
const [empleadoToDelete, setEmpleadoToDelete] = useState(null);

// Agregar función de eliminación
const handleDeleteClick = (empleado) => {
  setEmpleadoToDelete(empleado);
  setDeleteModalOpen(true);
};

const handleDeleteSuccess = () => {
  loadEmpleados(); // Recargar lista
};

// En el renderizado de la tabla, agregar botón:
<button
  onClick={() => handleDeleteClick(empleado)}
  className="text-red-600 hover:text-red-800"
  title="Eliminar empleado"
>
  <Trash2 className="w-4 h-4" />
</button>

// Agregar modal al final del componente (antes del cierre del return):
{empleadoToDelete && (
  <DeleteEmpleadoModal
    empleado={empleadoToDelete}
    isOpen={deleteModalOpen}
    onClose={() => setDeleteModalOpen(false)}
    onSuccess={handleDeleteSuccess}
  />
)}
```

---

## 4. TESTING

### 4.1 Testing Backend - Unit Tests

**Archivo:** `tests/Planilla.Tests/Services/EmpleadoValidationServiceTests.cs` (CREAR)

```csharp
using Xunit;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Domain.Entities;

namespace Planilla.Tests.Services;

public class EmpleadoValidationServiceTests
{
    private ApplicationDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ValidateForDeletion_WithActiveLoan_ReturnsBlocker()
    {
        // Arrange
        var context = GetInMemoryContext();
        var service = new EmpleadoValidationService(context);

        var empleado = new Empleado
        {
            Id = 1,
            TenantId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            NumeroIdentificacion = "8-123-4567",
            SalarioBase = 1000
        };

        var prestamo = new Prestamo
        {
            Id = 1,
            EmpleadoId = 1,
            TenantId = 1,
            MontoOriginal = 1000,
            MontoPendiente = 500,
            Estado = EstadoPrestamo.Activo
        };

        context.Empleados.Add(empleado);
        context.Prestamos.Add(prestamo);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ValidateForDeletionAsync(1, 1);

        // Assert
        Assert.False(result.CanDelete);
        Assert.NotEmpty(result.Blockers);
        Assert.Contains("Préstamo activo", result.Blockers[0]);
    }

    [Fact]
    public async Task ValidateForDeletion_WithJudicialDeduction_ReturnsBlocker()
    {
        // Arrange
        var context = GetInMemoryContext();
        var service = new EmpleadoValidationService(context);

        var empleado = new Empleado
        {
            Id = 1,
            TenantId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            NumeroIdentificacion = "8-123-4567",
            SalarioBase = 1000
        };

        var deduccion = new DeduccionFija
        {
            Id = 1,
            EmpleadoId = 1,
            TenantId = 1,
            TipoDeduccion = TipoDeduccion.PensionAlimenticia,
            Descripcion = "Pensión alimenticia - Exp. 123-2024",
            Monto = 200,
            EstaActivo = true,
            Referencia = "Exp. 123-2024"
        };

        context.Empleados.Add(empleado);
        context.DeduccionesFijas.Add(deduccion);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ValidateForDeletionAsync(1, 1);

        // Assert
        Assert.False(result.CanDelete);
        Assert.NotEmpty(result.Blockers);
        Assert.Contains("Deducción judicial", result.Blockers[0]);
    }

    [Fact]
    public async Task ValidateForDeletion_WithNoBlockers_AllowsDeletion()
    {
        // Arrange
        var context = GetInMemoryContext();
        var service = new EmpleadoValidationService(context);

        var empleado = new Empleado
        {
            Id = 1,
            TenantId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            NumeroIdentificacion = "8-123-4567",
            SalarioBase = 1000
        };

        context.Empleados.Add(empleado);
        await context.SaveChangesAsync();

        // Act
        var result = await service.ValidateForDeletionAsync(1, 1);

        // Assert
        Assert.True(result.CanDelete);
        Assert.Empty(result.Blockers);
    }
}
```

---

### 4.2 Testing de Integración

**Archivo:** `tests/Planilla.IntegrationTests/EmpleadosControllerTests.cs`

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace Planilla.IntegrationTests;

public class EmpleadosControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EmpleadosControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteEmpleado_WithActiveLoan_ReturnsBadRequest()
    {
        // Arrange
        // (setup test data con préstamo activo)

        var deleteDto = new
        {
            reason = "Renuncia",
            notes = "Test",
            forceDelete = false
        };

        // Act
        var response = await _client.DeleteAsJsonAsync($"/api/empleados/1", deleteDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("préstamo activo", error.Error, StringComparison.OrdinalIgnoreCase);
    }

    // Más tests...
}
```

---

## 5. PLAN DE ROLLOUT

### Día 1-2: Backend - Usuarios
- Implementar validación de "último Owner"
- Agregar endpoint de desvinculación
- Testing unitario

### Día 3-5: Backend - Empleados
- Migración de BD
- Servicio de validación
- Endpoints CRUD
- Testing unitario

### Día 6-8: Frontend
- Componente DeleteEmpleadoModal
- Integración en EmpleadosPage
- Testing manual

### Día 9-10: Testing de Integración
- Tests end-to-end
- Corrección de bugs

### Día 11: Documentación
- Swagger/OpenAPI
- Guía de usuario
- Release notes

### Día 12: Deploy a Staging
- Deploy
- Testing en staging con datos reales (anonimizados)

### Día 13-14: Deploy a Producción
- Deploy gradual (feature flag)
- Monitoreo de logs y errores
- Comunicación a usuarios

---

## 6. FEATURE FLAG (OPCIONAL)

Para rollout gradual, agregar feature flag:

```csharp
// appsettings.json
{
  "FeatureFlags": {
    "EmpleadoDeletionEnabled": false  // true para habilitar
  }
}

// En controller
if (!_configuration.GetValue<bool>("FeatureFlags:EmpleadoDeletionEnabled"))
{
    return StatusCode(501, new { error = "Esta funcionalidad aún no está disponible" });
}
```

---

## 7. MONITOREO Y ALERTAS

**Métricas a monitorear:**
- Número de empleados eliminados por día/semana
- Razones de eliminación más comunes
- Cantidad de eliminaciones bloqueadas (y por qué blocker)
- Tiempo promedio de validación

**Alertas:**
- Más de 10 empleados eliminados en un día por un mismo tenant
- Más de 50% de intentos de eliminación bloqueados
- Errores en validación de eliminación (500 errors)

---

**FIN DE ESPECIFICACIONES TÉCNICAS**

**Próximo paso:** Aprobar y comenzar implementación en sprint planning.
