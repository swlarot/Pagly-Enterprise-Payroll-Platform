using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Admin;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para el Panel de Administración del Sistema (SystemAdmin)
/// SEGURIDAD: Todos los endpoints requieren que el usuario tenga IsSystemAdmin = true
/// IMPORTANTE: Los SystemAdmins NO están limitados por TenantContext - pueden ver todos los tenants
/// </summary>
[Authorize(Policy = "RequireSystemAdmin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        UserManager<AppUser> userManager,
        ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/tenants - Lista todos los tenants del sistema con información de suscripción
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        try
        {
            var tenants = await _context.Tenants
                .Include(t => t.Subscription)
                .Include(t => t.Users)
                    .ThenInclude(tu => tu.User)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new AdminTenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Subdomain = t.Subdomain,
                    RUC = t.RUC,
                    DV = t.DV,
                    Address = t.Address,
                    Phone = t.Phone,
                    Email = t.Email,
                    CreatedAt = t.CreatedAt,
                    IsActive = t.IsActive,
                    Subscription = t.Subscription == null ? null : new SubscriptionInfoDto
                    {
                        Plan = t.Subscription.Plan,
                        PlanName = t.Subscription.Plan.ToString(),
                        Status = t.Subscription.Status,
                        StatusName = t.Subscription.Status.ToString(),
                        TrialEndsAt = t.Subscription.TrialEndsAt,
                        MaxEmployees = t.Subscription.GetEffectiveMaxEmployees(),
                        MaxUsers = t.Subscription.GetEffectiveMaxUsers(),
                        MaxCompanies = PlanFeatures.GetLimits(t.Subscription.Plan).MaxCompanies,
                        CanExportExcel = PlanFeatures.GetLimits(t.Subscription.Plan).CanExportExcel,
                        CanExportPdf = PlanFeatures.GetLimits(t.Subscription.Plan).CanExportPdf,
                        CanUseApi = PlanFeatures.GetLimits(t.Subscription.Plan).CanUseApi,
                        MonthlyPrice = t.Subscription.MonthlyPrice
                    },
                    Owner = t.Users
                        .Where(u => u.Role == TenantRole.Owner)
                        .Select(u => new OwnerInfoDto
                        {
                            UserId = u.UserId,
                            Email = u.User != null ? u.User.Email ?? string.Empty : string.Empty,
                            FullName = u.User != null ? u.User.NombreCompleto : null,
                            JoinedAt = u.JoinedAt,
                            LastLoginAt = u.LastLoginAt
                        })
                        .FirstOrDefault(),
                    Usage = new AdminTenantUsageDto
                    {
                        TotalUsers = t.Users.Count,
                        ActiveUsers = t.Users.Count(u => u.IsActive),
                        TotalEmployees = t.Empleados.Count,
                        ActiveEmployees = t.Empleados.Count(e => e.EstaActivo),
                        TotalPayrolls = t.PayrollHeaders.Count,
                        PendingInvitations = t.Users.Count(u => u.IsPendingInvitation),
                        MaxUsers = t.Subscription != null ? t.Subscription.GetEffectiveMaxUsers() : 0,
                        MaxEmployees = t.Subscription != null ? t.Subscription.GetEffectiveMaxEmployees() : 0
                    }
                })
                .ToListAsync();

            _logger.LogInformation("SystemAdmin {UserId} listed all tenants ({Count})",
                User.FindFirst("sub")?.Value, tenants.Count);

            return Ok(tenants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all tenants");
            return StatusCode(500, new { error = "Error al obtener los tenants" });
        }
    }

    /// <summary>
    /// GET /api/admin/tenants/{id} - Obtiene detalles de un tenant específico
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpGet("tenants/{id}")]
    public async Task<IActionResult> GetTenantById(int id)
    {
        try
        {
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .Include(t => t.Users)
                    .ThenInclude(tu => tu.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // Obtener el propietario (Owner)
            var owner = tenant.Users.FirstOrDefault(u => u.Role == TenantRole.Owner);

            var tenantDto = new AdminTenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Subdomain = tenant.Subdomain,
                RUC = tenant.RUC,
                DV = tenant.DV,
                Address = tenant.Address,
                Phone = tenant.Phone,
                Email = tenant.Email,
                CreatedAt = tenant.CreatedAt,
                IsActive = tenant.IsActive,
                Subscription = tenant.Subscription == null ? null : new SubscriptionInfoDto
                {
                    Plan = tenant.Subscription.Plan,
                    PlanName = tenant.Subscription.Plan.ToString(),
                    Status = tenant.Subscription.Status,
                    StatusName = tenant.Subscription.Status.ToString(),
                    TrialEndsAt = tenant.Subscription.TrialEndsAt,
                    MaxEmployees = tenant.Subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = tenant.Subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = PlanFeatures.GetLimits(tenant.Subscription.Plan).MaxCompanies,
                    CanExportExcel = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanExportExcel,
                    CanExportPdf = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanExportPdf,
                    CanUseApi = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanUseApi,
                    MonthlyPrice = tenant.Subscription.MonthlyPrice
                },
                Owner = owner?.User == null ? null : new OwnerInfoDto
                {
                    UserId = owner.UserId,
                    Email = owner.User.Email ?? string.Empty,
                    FullName = owner.User.NombreCompleto,
                    JoinedAt = owner.JoinedAt,
                    LastLoginAt = owner.LastLoginAt
                },
                Usage = new AdminTenantUsageDto
                {
                    TotalUsers = tenant.Users.Count,
                    ActiveUsers = tenant.Users.Count(u => u.IsActive),
                    TotalEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id),
                    ActiveEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && e.EstaActivo),
                    TotalPayrolls = await _context.PayrollHeaders.CountAsync(p => p.TenantId == id),
                    PendingInvitations = tenant.Users.Count(u => u.IsPendingInvitation),
                    MaxUsers = tenant.Subscription?.GetEffectiveMaxUsers() ?? 0,
                    MaxEmployees = tenant.Subscription?.GetEffectiveMaxEmployees() ?? 0
                }
            };

            _logger.LogInformation("SystemAdmin {UserId} viewed tenant {TenantId}",
                User.FindFirst("sub")?.Value, id);

            return Ok(tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al obtener el tenant" });
        }
    }

    /// <summary>
    /// POST /api/admin/tenants - Crea un nuevo tenant con usuario owner
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Verificar que el email no exista
        var existingUser = await _userManager.FindByEmailAsync(dto.OwnerEmail);
        if (existingUser != null)
        {
            return BadRequest(new { error = "El email del propietario ya está registrado en el sistema" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Crear usuario owner en Identity
            var user = new AppUser
            {
                UserName = dto.OwnerEmail,
                Email = dto.OwnerEmail,
                EmailConfirmed = true,
                NombreCompleto = dto.OwnerFullName ?? dto.Name,
                IsSystemAdmin = false
            };

            var createResult = await _userManager.CreateAsync(user, dto.OwnerPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return BadRequest(new { error = "Error al crear usuario", details = errors });
            }

            // 2. Generar subdomain único
            var subdomain = GenerateUniqueSubdomain(dto.Name);

            // 3. Crear Tenant
            var tenant = new Tenant
            {
                Name = dto.Name,
                Subdomain = subdomain,
                RUC = dto.RUC,
                DV = dto.DV,
                Address = dto.Address,
                Phone = dto.Phone,
                Email = dto.CompanyEmail,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            // 4. Crear Subscription (Professional con 14 días de prueba por defecto)
            var trialEndsAt = DateTime.UtcNow.AddDays(14);
            var limits = PlanFeatures.GetLimits(SubscriptionPlan.Professional);

            var subscription = new Subscription
            {
                TenantId = tenant.Id,
                Plan = SubscriptionPlan.Professional,
                Status = SubscriptionStatus.Trialing,
                StartDate = DateTime.UtcNow,
                TrialEndsAt = trialEndsAt,
                MonthlyPrice = limits.PricePerMonth,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Asociar subscription al tenant
            tenant.SubscriptionId = subscription.Id;
            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            // 5. Crear TenantUser con rol Owner
            var tenantUser = new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = TenantRole.Owner,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.TenantUsers.Add(tenantUser);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("SystemAdmin {AdminId} created tenant {TenantId} ({TenantName}) with owner {OwnerId}",
                User.FindFirst("sub")?.Value, tenant.Id, tenant.Name, user.Id);

            // Construir respuesta
            var response = new AdminTenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Subdomain = tenant.Subdomain,
                RUC = tenant.RUC,
                DV = tenant.DV,
                Address = tenant.Address,
                Phone = tenant.Phone,
                Email = tenant.Email,
                CreatedAt = tenant.CreatedAt,
                IsActive = tenant.IsActive,
                Subscription = new SubscriptionInfoDto
                {
                    Plan = subscription.Plan,
                    PlanName = subscription.Plan.ToString(),
                    Status = subscription.Status,
                    StatusName = subscription.Status.ToString(),
                    TrialEndsAt = subscription.TrialEndsAt,
                    MaxEmployees = subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = subscription.MonthlyPrice
                },
                Owner = new OwnerInfoDto
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    FullName = user.NombreCompleto,
                    JoinedAt = tenantUser.JoinedAt,
                    LastLoginAt = null
                },
                Usage = new AdminTenantUsageDto
                {
                    TotalUsers = 1,
                    ActiveUsers = 1,
                    TotalEmployees = 0,
                    ActiveEmployees = 0,
                    TotalPayrolls = 0,
                    PendingInvitations = 0,
                    MaxUsers = subscription.GetEffectiveMaxUsers(),
                    MaxEmployees = subscription.GetEffectiveMaxEmployees()
                }
            };

            return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating tenant: {TenantName}", dto.Name);
            return StatusCode(500, new { error = "Error al crear el tenant. Por favor, intente nuevamente." });
        }
    }

    /// <summary>
    /// PUT /api/admin/tenants/{id} - Actualiza información de un tenant
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPut("tenants/{id}")]
    public async Task<IActionResult> UpdateTenant(int id, [FromBody] UpdateAdminTenantDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // Actualizar solo los campos proporcionados
            if (!string.IsNullOrEmpty(dto.Name))
                tenant.Name = dto.Name;

            if (dto.RUC != null)
                tenant.RUC = dto.RUC;

            if (dto.DV != null)
                tenant.DV = dto.DV;

            if (dto.Address != null)
                tenant.Address = dto.Address;

            if (dto.Phone != null)
                tenant.Phone = dto.Phone;

            if (dto.Email != null)
                tenant.Email = dto.Email;

            if (dto.IsActive.HasValue)
                tenant.IsActive = dto.IsActive.Value;

            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation("SystemAdmin {AdminId} updated tenant {TenantId}",
                User.FindFirst("sub")?.Value, id);

            // Retornar tenant actualizado
            var owner = await _context.TenantUsers
                .Include(tu => tu.User)
                .FirstOrDefaultAsync(tu => tu.TenantId == id && tu.Role == TenantRole.Owner);

            var response = new AdminTenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Subdomain = tenant.Subdomain,
                RUC = tenant.RUC,
                DV = tenant.DV,
                Address = tenant.Address,
                Phone = tenant.Phone,
                Email = tenant.Email,
                CreatedAt = tenant.CreatedAt,
                IsActive = tenant.IsActive,
                Subscription = tenant.Subscription == null ? null : new SubscriptionInfoDto
                {
                    Plan = tenant.Subscription.Plan,
                    PlanName = tenant.Subscription.Plan.ToString(),
                    Status = tenant.Subscription.Status,
                    StatusName = tenant.Subscription.Status.ToString(),
                    TrialEndsAt = tenant.Subscription.TrialEndsAt,
                    MaxEmployees = tenant.Subscription.GetEffectiveMaxEmployees(),
                    MaxUsers = tenant.Subscription.GetEffectiveMaxUsers(),
                    MaxCompanies = PlanFeatures.GetLimits(tenant.Subscription.Plan).MaxCompanies,
                    CanExportExcel = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanExportExcel,
                    CanExportPdf = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanExportPdf,
                    CanUseApi = PlanFeatures.GetLimits(tenant.Subscription.Plan).CanUseApi,
                    MonthlyPrice = tenant.Subscription.MonthlyPrice
                },
                Owner = owner?.User == null ? null : new OwnerInfoDto
                {
                    UserId = owner.UserId,
                    Email = owner.User.Email ?? string.Empty,
                    FullName = owner.User.NombreCompleto,
                    JoinedAt = owner.JoinedAt,
                    LastLoginAt = owner.LastLoginAt
                },
                Usage = new AdminTenantUsageDto
                {
                    TotalUsers = await _context.TenantUsers.CountAsync(u => u.TenantId == id),
                    ActiveUsers = await _context.TenantUsers.CountAsync(u => u.TenantId == id && u.IsActive),
                    TotalEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id),
                    ActiveEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && e.EstaActivo),
                    TotalPayrolls = await _context.PayrollHeaders.CountAsync(p => p.TenantId == id),
                    PendingInvitations = await _context.TenantUsers.CountAsync(u => u.TenantId == id && u.IsPendingInvitation),
                    MaxUsers = tenant.Subscription?.GetEffectiveMaxUsers() ?? 0,
                    MaxEmployees = tenant.Subscription?.GetEffectiveMaxEmployees() ?? 0
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al actualizar el tenant" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/tenants/{id} - Desactiva un tenant (soft delete)
    /// Requiere: IsSystemAdmin = true
    /// NOTA: No elimina físicamente el tenant, solo lo marca como inactivo
    /// </summary>
    [HttpDelete("tenants/{id}")]
    public async Task<IActionResult> DeactivateTenant(int id)
    {
        try
        {
            var tenant = await _context.Tenants.FindAsync(id);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            tenant.IsActive = false;
            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            _logger.LogWarning("SystemAdmin {AdminId} deactivated tenant {TenantId} ({TenantName})",
                User.FindFirst("sub")?.Value, id, tenant.Name);

            return Ok(new { success = true, message = "Tenant desactivado exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al desactivar el tenant" });
        }
    }

    /// <summary>
    /// GET /api/admin/metrics - Obtiene métricas generales del sistema
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetSystemMetrics()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            var sevenDaysAgo = now.AddDays(-7);

            var totalTenants = await _context.Tenants.CountAsync();
            var activeTenants = await _context.Tenants.CountAsync(t => t.IsActive);

            var totalUsers = await _context.TenantUsers.CountAsync();
            var totalEmployees = await _context.Empleados.CountAsync();

            // Distribución por plan
            var planCounts = await _context.Subscriptions
                .GroupBy(s => s.Plan)
                .Select(g => new { Plan = g.Key, Count = g.Count() })
                .ToListAsync();

            var planDistribution = new PlanDistributionDto
            {
                Free = planCounts.FirstOrDefault(p => p.Plan == SubscriptionPlan.Free)?.Count ?? 0,
                Starter = planCounts.FirstOrDefault(p => p.Plan == SubscriptionPlan.Starter)?.Count ?? 0,
                Professional = planCounts.FirstOrDefault(p => p.Plan == SubscriptionPlan.Professional)?.Count ?? 0,
                Enterprise = planCounts.FirstOrDefault(p => p.Plan == SubscriptionPlan.Enterprise)?.Count ?? 0
            };

            // Crecimiento reciente
            var tenantsLast30Days = await _context.Tenants
                .CountAsync(t => t.CreatedAt >= thirtyDaysAgo);

            var tenantsLast7Days = await _context.Tenants
                .CountAsync(t => t.CreatedAt >= sevenDaysAgo);

            var usersLast30Days = await _context.TenantUsers
                .CountAsync(tu => tu.JoinedAt >= thirtyDaysAgo);

            var usersLast7Days = await _context.TenantUsers
                .CountAsync(tu => tu.JoinedAt >= sevenDaysAgo);

            var metrics = new SystemMetricsDto
            {
                TotalTenants = totalTenants,
                ActiveTenants = activeTenants,
                TotalUsers = totalUsers,
                TotalEmployees = totalEmployees,
                PlanDistribution = planDistribution,
                RecentGrowth = new RecentGrowthDto
                {
                    Last7Days = new GrowthPeriodDto
                    {
                        NewTenants = tenantsLast7Days,
                        NewUsers = usersLast7Days
                    },
                    Last30Days = new GrowthPeriodDto
                    {
                        NewTenants = tenantsLast30Days,
                        NewUsers = usersLast30Days
                    }
                }
            };

            _logger.LogInformation("SystemAdmin {AdminId} retrieved system metrics",
                User.FindFirst("sub")?.Value);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system metrics");
            return StatusCode(500, new { error = "Error al obtener las métricas del sistema" });
        }
    }

    /// <summary>
    /// GET /api/admin/tenants/{id}/users - Lista usuarios de un tenant específico
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpGet("tenants/{id}/users")]
    public async Task<IActionResult> GetTenantUsers(int id)
    {
        try
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            var users = await _context.TenantUsers
                .Include(tu => tu.User)
                .Where(tu => tu.TenantId == id)
                .OrderBy(tu => tu.JoinedAt)
                .Select(tu => new AdminTenantUserDto
                {
                    Id = tu.Id,
                    UserId = tu.UserId,
                    Email = tu.User != null ? tu.User.Email ?? string.Empty : string.Empty,
                    FullName = tu.User != null ? tu.User.NombreCompleto : null,
                    Role = tu.Role,
                    RoleName = tu.Role.ToString(),
                    IsActive = tu.IsActive,
                    JoinedAt = tu.JoinedAt,
                    LastLoginAt = tu.LastLoginAt,
                    IsPendingInvitation = tu.IsPendingInvitation,
                    InvitationExpiresAt = tu.InvitationExpiresAt
                })
                .ToListAsync();

            _logger.LogInformation("SystemAdmin {AdminId} listed users for tenant {TenantId}",
                User.FindFirst("sub")?.Value, id);

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al obtener los usuarios del tenant" });
        }
    }

    /// <summary>
    /// PUT /api/admin/tenants/{id}/subscription - Actualiza la suscripción de un tenant
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPut("tenants/{id}/subscription")]
    public async Task<IActionResult> UpdateTenantSubscription(int id, [FromBody] UpdateTenantSubscriptionDto dto)
    {
        try
        {
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            if (tenant.Subscription == null)
            {
                return BadRequest(new { error = "El tenant no tiene una suscripción activa" });
            }

            // Actualizar plan
            var oldPlan = tenant.Subscription.Plan;
            tenant.Subscription.Plan = dto.Plan;

            // Obtener límites del nuevo plan
            var limits = PlanFeatures.GetLimits(dto.Plan);
            tenant.Subscription.CustomMaxEmployees = limits.MaxEmployees;
            tenant.Subscription.CustomMaxUsers = limits.MaxUsers;
            tenant.Subscription.MonthlyPrice = limits.PricePerMonth;

            // Extender trial si se especificó
            if (dto.ExtendTrialDays.HasValue && dto.ExtendTrialDays.Value > 0)
            {
                if (tenant.Subscription.TrialEndsAt.HasValue)
                {
                    tenant.Subscription.TrialEndsAt = tenant.Subscription.TrialEndsAt.Value.AddDays(dto.ExtendTrialDays.Value);
                }
                else
                {
                    tenant.Subscription.TrialEndsAt = DateTime.UtcNow.AddDays(dto.ExtendTrialDays.Value);
                    tenant.Subscription.Status = SubscriptionStatus.Trialing;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "SystemAdmin {AdminId} updated subscription for tenant {TenantId}: {OldPlan} -> {NewPlan}",
                User.FindFirst("sub")?.Value, id, oldPlan, dto.Plan);

            // Retornar el tenant actualizado
            return await GetTenantById(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription for tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al actualizar la suscripción del tenant" });
        }
    }

    /// <summary>
    /// GET /api/admin/tenants/{id}/audit - Obtiene logs de auditoría de un tenant específico
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpGet("tenants/{id}/audit")]
    public async Task<IActionResult> GetTenantAuditLog(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        [FromQuery] string? userId = null)
    {
        try
        {
            // Verificar que el tenant existe
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // Limitar pageSize
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 50;
            if (page < 1) page = 1;

            // Construir query
            var query = _context.AuditLogEntries
                .Where(a => a.TenantId == id);

            // Aplicar filtros
            if (from.HasValue)
                query = query.Where(a => a.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.CreatedAt <= to.Value);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.ActorUserId == userId);

            // Obtener total
            var total = await query.CountAsync();

            // Obtener datos paginados
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    ActorEmail = a.ActorEmail,
                    IpAddress = a.IpAddress,
                    UserAgent = a.UserAgent,
                    MetadataJson = a.MetadataJson,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            var result = new AuditLogPagedResultDto
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Data = logs
            };

            _logger.LogInformation(
                "SystemAdmin {AdminId} retrieved audit logs for tenant {TenantId} (Page {Page}, Filters: {Filters})",
                User.FindFirst("sub")?.Value, id, page, new { from, to, action, userId });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs for tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al obtener los logs de auditoría" });
        }
    }

    /// <summary>
    /// GET /api/admin/system/users - Lista TODOS los usuarios del sistema con sus membresías de tenants
    /// Requiere: IsSystemAdmin = true
    /// IMPORTANTE: No filtra por tenant - muestra todos los usuarios del sistema
    /// </summary>
    [HttpGet("system/users")]
    public async Task<IActionResult> GetAllSystemUsers(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Limitar pageSize
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 20;
            if (page < 1) page = 1;

            // Query base de usuarios - excluir eliminados
            var query = _context.Users
                .Where(u => !u.IsDeleted)
                .AsQueryable();

            // Filtro de búsqueda (case-insensitive)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(u =>
                    u.Email!.ToLower().Contains(searchLower) ||
                    (u.NombreCompleto != null && u.NombreCompleto.ToLower().Contains(searchLower)) ||
                    u.UserName!.ToLower().Contains(searchLower));
            }

            // Total count
            var total = await query.CountAsync();

            // Paginación
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.NombreCompleto,
                    u.IsSystemAdmin,
                    // ASP.NET Identity no tiene CreatedAt por defecto, usar DateTime.MinValue como placeholder
                    CreatedAt = DateTime.UtcNow // TODO: Agregar CreatedAt a AppUser si es necesario
                })
                .ToListAsync();

            // Obtener membresías de tenants para cada usuario
            var userIds = users.Select(u => u.Id).ToList();
            var tenantMemberships = await _context.TenantUsers
                .Where(tu => userIds.Contains(tu.UserId))
                .Include(tu => tu.Tenant)
                .Select(tu => new
                {
                    tu.UserId,
                    TenantMembership = new UserTenantMembershipDto
                    {
                        TenantId = tu.TenantId,
                        TenantName = tu.Tenant.Name,
                        Role = tu.Role.ToString(),
                        JoinedAt = tu.JoinedAt,
                        IsActive = tu.IsActive,
                        LastLoginAt = tu.LastLoginAt
                    }
                })
                .ToListAsync();

            // Agrupar por userId
            var membershipsByUser = tenantMemberships
                .GroupBy(tm => tm.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TenantMembership).ToList());

            // Mapear a DTOs
            var result = users.Select(u => new SystemUserDto
            {
                UserId = u.Id,
                Email = u.Email ?? string.Empty,
                FullName = u.NombreCompleto ?? u.Email ?? "Sin nombre",
                CreatedAt = u.CreatedAt,
                IsActive = true, // TODO: Agregar IsActive a AppUser si es necesario
                IsSystemAdmin = u.IsSystemAdmin,
                Tenants = membershipsByUser.ContainsKey(u.Id)
                    ? membershipsByUser[u.Id]
                    : new List<UserTenantMembershipDto>()
            }).ToList();

            var response = new SystemUserPagedResultDto
            {
                Data = result,
                Total = total,
                Page = page,
                PageSize = pageSize
            };

            _logger.LogInformation(
                "SystemAdmin {AdminId} listed all system users (Total: {Total}, Page: {Page}, Search: {Search})",
                User.FindFirst("sub")?.Value, total, page, search ?? "none");

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all system users");
            return StatusCode(500, new { error = "Error al obtener los usuarios del sistema" });
        }
    }

    /// <summary>
    /// POST /api/admin/tenants/{id}/users - Invita un usuario a un tenant
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("tenants/{id}/users")]
    public async Task<IActionResult> InviteUserToTenant(int id, [FromBody] InviteUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // No permitir crear Owner desde este endpoint
        if (request.Role == TenantRole.Owner)
        {
            return BadRequest(new { error = "No se puede crear usuarios con rol Owner desde este endpoint" });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Verificar que el tenant existe
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .Include(t => t.Users)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // 2. VALIDAR LÍMITES DEL PLAN
            var currentUserCount = tenant.Users.Count(u => u.IsActive);
            var maxUsers = GetMaxUsersForPlan(tenant.Subscription?.Plan ?? SubscriptionPlan.Free);

            if (currentUserCount >= maxUsers)
            {
                return BadRequest(new
                {
                    error = $"El tenant ha alcanzado el límite de {maxUsers} usuarios para el plan {tenant.Subscription?.Plan}. " +
                            $"Actualmente tiene {currentUserCount} usuarios activos."
                });
            }

            // 3. Buscar o crear usuario en Identity
            var user = await _userManager.FindByEmailAsync(request.Email);

            // Verificar que el usuario no esté eliminado
            if (user != null && user.IsDeleted)
            {
                return BadRequest(new { error = "El usuario con este email está eliminado. Debe reactivarlo primero." });
            }

            if (user == null)
            {
                // Crear nuevo usuario
                user = new AppUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = true,
                    NombreCompleto = request.FullName,
                    IsSystemAdmin = false
                };

                var createResult = await _userManager.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    return BadRequest(new { error = "Error al crear usuario", details = errors });
                }
            }

            // 4. Verificar si ya existe TenantUser
            var existingTenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == id && tu.UserId == user.Id);

            if (existingTenantUser != null)
            {
                return Conflict(new { error = "El usuario ya pertenece a este tenant" });
            }

            // 5. Crear TenantUser
            var tenantUser = new TenantUser
            {
                TenantId = id,
                UserId = user.Id,
                Role = request.Role,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsPendingInvitation = false
            };

            _context.TenantUsers.Add(tenantUser);
            await _context.SaveChangesAsync();

            // 6. AUDIT LOG
            var adminUserId = User.FindFirst("sub")?.Value ?? "system";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var auditLog = new AuditLogEntry
            {
                TenantId = id,
                ActorUserId = adminUserId,
                ActorEmail = User.FindFirst("email")?.Value ?? "system@admin.com",
                Action = "InviteUser",
                EntityType = "TenantUser",
                EntityId = tenantUser.Id.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserEmail = user.Email,
                    Role = request.Role.ToString(),
                    InvitedBy = adminUserId
                }),
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogEntries.Add(auditLog);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "SystemAdmin {AdminId} invited user {Email} to tenant {TenantId} with role {Role}",
                adminUserId, user.Email, id, request.Role);

            // 7. Retornar DTO
            var response = new AdminTenantUserDto
            {
                Id = tenantUser.Id,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.NombreCompleto,
                Role = tenantUser.Role,
                RoleName = tenantUser.Role.ToString(),
                IsActive = tenantUser.IsActive,
                JoinedAt = tenantUser.JoinedAt,
                LastLoginAt = tenantUser.LastLoginAt,
                IsPendingInvitation = tenantUser.IsPendingInvitation,
                InvitationExpiresAt = tenantUser.InvitationExpiresAt
            };

            return CreatedAtAction(nameof(GetTenantUsers), new { id }, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error inviting user to tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al invitar usuario al tenant" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/users/{userId} - Elimina completamente un usuario del sistema (soft delete)
    /// Requiere: IsSystemAdmin = true
    /// Validaciones:
    /// - No se puede eliminar el último SystemAdmin
    /// - Marca usuario como eliminado (IsDeleted = true)
    /// - Desactiva todas las asociaciones TenantUser del usuario
    /// </summary>
    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        try
        {
            // 1. Verificar que el usuario existe
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "Usuario no encontrado" });
            }

            // 2. Si ya está eliminado, retornar error
            if (user.IsDeleted)
            {
                return BadRequest(new { error = "El usuario ya está eliminado" });
            }

            // 3. VALIDACIÓN CRÍTICA: No eliminar el último SystemAdmin
            if (user.IsSystemAdmin)
            {
                var systemAdminsCount = await _userManager.Users
                    .Where(u => u.IsSystemAdmin && !u.IsDeleted)
                    .CountAsync();

                if (systemAdminsCount <= 1)
                {
                    return BadRequest(new { error = "No se puede eliminar el último SystemAdmin del sistema" });
                }
            }

            // 4. Obtener ID del admin actual
            var currentAdminId = User.FindFirst("sub")?.Value;

            // 5. Marcar usuario como eliminado (soft delete)
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = currentAdminId;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest(new { error = "Error al eliminar usuario", details = errors });
            }

            // 6. Desactivar todas las asociaciones TenantUser
            var tenantUsers = await _context.TenantUsers
                .Where(tu => tu.UserId == userId)
                .ToListAsync();

            foreach (var tenantUser in tenantUsers)
            {
                tenantUser.IsActive = false;
            }

            await _context.SaveChangesAsync();

            // 7. AUDIT LOG para cada tenant afectado
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            foreach (var tenantUser in tenantUsers)
            {
                var auditLog = new AuditLogEntry
                {
                    TenantId = tenantUser.TenantId,
                    ActorUserId = currentAdminId ?? "system",
                    ActorEmail = User.FindFirst("email")?.Value ?? "system@admin.com",
                    Action = "UserDeleted",
                    EntityType = "AppUser",
                    EntityId = userId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        UserEmail = user.Email,
                        DeletedBy = currentAdminId,
                        DeletedAt = user.DeletedAt
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogEntries.Add(auditLog);
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning("SystemAdmin {AdminId} deleted user {UserId} ({Email})",
                currentAdminId, userId, user.Email);

            return Ok(new { success = true, message = "Usuario eliminado exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return StatusCode(500, new { error = "Error al eliminar el usuario" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/tenants/{tenantId}/users/{userId} - Remueve un usuario de un tenant específico
    /// Requiere: IsSystemAdmin = true
    /// Validaciones:
    /// - No se puede remover al último Owner de un tenant
    /// - Marca TenantUser como inactivo (IsActive = false)
    /// </summary>
    [HttpDelete("tenants/{tenantId}/users/{userId}")]
    public async Task<IActionResult> RemoveUserFromTenant(int tenantId, string userId)
    {
        try
        {
            // 1. Verificar que el tenant existe
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // 2. Buscar la relación TenantUser
            var tenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

            if (tenantUser == null)
            {
                return NotFound(new { error = "El usuario no pertenece a este tenant" });
            }

            // 3. Si ya está inactivo, retornar error
            if (!tenantUser.IsActive)
            {
                return BadRequest(new { error = "El usuario ya está inactivo en este tenant" });
            }

            // 4. VALIDACIÓN CRÍTICA: No remover al último Owner del tenant
            if (tenantUser.Role == TenantRole.Owner)
            {
                var ownersCount = await _context.TenantUsers
                    .Where(tu => tu.TenantId == tenantId
                              && tu.Role == TenantRole.Owner
                              && tu.IsActive)
                    .CountAsync();

                if (ownersCount <= 1)
                {
                    return BadRequest(new { error = "No se puede remover el último Owner del tenant" });
                }
            }

            // 5. Marcar TenantUser como inactivo
            tenantUser.IsActive = false;
            _context.TenantUsers.Update(tenantUser);
            await _context.SaveChangesAsync();

            // 6. AUDIT LOG
            var currentAdminId = User.FindFirst("sub")?.Value;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var user = await _userManager.FindByIdAsync(userId);

            var auditLog = new AuditLogEntry
            {
                TenantId = tenantId,
                ActorUserId = currentAdminId ?? "system",
                ActorEmail = User.FindFirst("email")?.Value ?? "system@admin.com",
                Action = "UserRemovedFromTenant",
                EntityType = "TenantUser",
                EntityId = tenantUser.Id.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserEmail = user?.Email,
                    Role = tenantUser.Role.ToString(),
                    RemovedBy = currentAdminId
                }),
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogEntries.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "SystemAdmin {AdminId} removed user {UserId} from tenant {TenantId}",
                currentAdminId, userId, tenantId);

            return Ok(new { success = true, message = "Usuario removido del tenant exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from tenant {TenantId}", userId, tenantId);
            return StatusCode(500, new { error = "Error al remover usuario del tenant" });
        }
    }

    /// <summary>
    /// POST /api/admin/users/{userId}/reactivate - Reactiva un usuario previamente eliminado
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("users/{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUser(string userId)
    {
        try
        {
            // 1. Verificar que el usuario existe
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "Usuario no encontrado" });
            }

            // 2. Verificar que el usuario está eliminado
            if (!user.IsDeleted)
            {
                return BadRequest(new { error = "El usuario no está eliminado" });
            }

            // 3. Reactivar usuario
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletedBy = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest(new { error = "Error al reactivar usuario", details = errors });
            }

            // 4. AUDIT LOG - registrar en todos los tenants donde el usuario tiene membresía
            var currentAdminId = User.FindFirst("sub")?.Value;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var tenantUsers = await _context.TenantUsers
                .Where(tu => tu.UserId == userId)
                .ToListAsync();

            foreach (var tenantUser in tenantUsers)
            {
                var auditLog = new AuditLogEntry
                {
                    TenantId = tenantUser.TenantId,
                    ActorUserId = currentAdminId ?? "system",
                    ActorEmail = User.FindFirst("email")?.Value ?? "system@admin.com",
                    Action = "UserReactivated",
                    EntityType = "AppUser",
                    EntityId = userId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        UserEmail = user.Email,
                        ReactivatedBy = currentAdminId,
                        ReactivatedAt = DateTime.UtcNow
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogEntries.Add(auditLog);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("SystemAdmin {AdminId} reactivated user {UserId} ({Email})",
                currentAdminId, userId, user.Email);

            return Ok(new { success = true, message = "Usuario reactivado exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user {UserId}", userId);
            return StatusCode(500, new { error = "Error al reactivar el usuario" });
        }
    }

    /// <summary>
    /// POST /api/admin/tenants/{tenantId}/users/{userId}/reactivate - Reactiva un usuario en un tenant específico
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("tenants/{tenantId}/users/{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUserInTenant(int tenantId, string userId)
    {
        try
        {
            // 1. Verificar que el tenant existe
            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return NotFound(new { error = "Tenant no encontrado" });
            }

            // 2. Buscar la relación TenantUser
            var tenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

            if (tenantUser == null)
            {
                return NotFound(new { error = "El usuario no pertenece a este tenant" });
            }

            // 3. Verificar que el usuario está inactivo
            if (tenantUser.IsActive)
            {
                return BadRequest(new { error = "El usuario ya está activo en este tenant" });
            }

            // 4. VALIDAR LÍMITES DEL PLAN antes de reactivar
            var currentUserCount = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            var maxUsers = GetMaxUsersForPlan(tenant.Subscription?.Plan ?? SubscriptionPlan.Free);

            if (currentUserCount >= maxUsers)
            {
                return BadRequest(new
                {
                    error = $"El tenant ha alcanzado el límite de {maxUsers} usuarios activos para el plan {tenant.Subscription?.Plan}. " +
                            $"Actualmente tiene {currentUserCount} usuarios activos."
                });
            }

            // 5. Reactivar TenantUser
            tenantUser.IsActive = true;
            _context.TenantUsers.Update(tenantUser);
            await _context.SaveChangesAsync();

            // 6. AUDIT LOG
            var currentAdminId = User.FindFirst("sub")?.Value;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var user = await _userManager.FindByIdAsync(userId);

            var auditLog = new AuditLogEntry
            {
                TenantId = tenantId,
                ActorUserId = currentAdminId ?? "system",
                ActorEmail = User.FindFirst("email")?.Value ?? "system@admin.com",
                Action = "UserReactivatedInTenant",
                EntityType = "TenantUser",
                EntityId = tenantUser.Id.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserEmail = user?.Email,
                    Role = tenantUser.Role.ToString(),
                    ReactivatedBy = currentAdminId
                }),
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogEntries.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "SystemAdmin {AdminId} reactivated user {UserId} in tenant {TenantId}",
                currentAdminId, userId, tenantId);

            return Ok(new { success = true, message = "Usuario reactivado en el tenant exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user {UserId} in tenant {TenantId}", userId, tenantId);
            return StatusCode(500, new { error = "Error al reactivar usuario en el tenant" });
        }
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Verifica si el usuario actual es un SystemAdmin
    /// </summary>
    private async Task<bool> IsSystemAdminAsync()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        return user.IsSystemAdmin;
    }

    /// <summary>
    /// Genera un subdomain único basado en el nombre de la empresa
    /// </summary>
    private string GenerateUniqueSubdomain(string companyName)
    {
        // Generar subdomain base limpiando el nombre de la empresa
        var baseSubdomain = new string(companyName
            .ToLower()
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Take(20)
            .ToArray())
            .Replace(' ', '-');

        // Verificar si existe
        var subdomain = baseSubdomain;
        var counter = 1;

        while (_context.Tenants.Any(t => t.Subdomain == subdomain))
        {
            subdomain = $"{baseSubdomain}-{counter}";
            counter++;
        }

        return subdomain;
    }

    /// <summary>
    /// Obtiene el máximo de usuarios permitidos según el plan
    /// </summary>
    private int GetMaxUsersForPlan(SubscriptionPlan plan)
    {
        return plan switch
        {
            SubscriptionPlan.Free => 1,
            SubscriptionPlan.Starter => 3,
            SubscriptionPlan.Professional => 10,
            SubscriptionPlan.Enterprise => int.MaxValue,
            _ => 1
        };
    }
}
