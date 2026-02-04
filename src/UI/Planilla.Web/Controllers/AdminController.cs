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
using Vorluno.Planilla.Infrastructure.Services;

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
    private readonly IBrevoEmailService _brevoEmailService;

    public AdminController(
        ApplicationDbContext context,
        UserManager<AppUser> userManager,
        ILogger<AdminController> logger,
        IBrevoEmailService brevoEmailService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _brevoEmailService = brevoEmailService;
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
                        TotalEmployees = t.Empleados.Count(e => !e.IsDeleted),
                        ActiveEmployees = t.Empleados.Count(e => e.EstaActivo && !e.IsDeleted),
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
                    TotalEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && !e.IsDeleted),
                    ActiveEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && e.EstaActivo && !e.IsDeleted),
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
    /// POST /api/admin/users - Crea un nuevo usuario en el sistema (sin asignar a tenant)
    /// Requiere: IsSystemAdmin = true
    /// La contraseña predefinida "Planilla2024!Temp" se asigna automáticamente
    /// </summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Correo);
        if (existingUser != null)
        {
            return BadRequest(new { message = "Ya existe un usuario con ese correo" });
        }

        var user = new AppUser
        {
            UserName = dto.Correo,
            Email = dto.Correo,
            NombreCompleto = $"{dto.Nombre} {dto.Apellido}",
            Telefono = dto.Telefono,
            EmailConfirmed = true,  // Auto-confirmar
            IsSystemAdmin = false
        };

        // Contraseña predefinida universal
        var result = await _userManager.CreateAsync(user, "Planilla2024!Temp");

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = "Error creando usuario", details = errors });
        }

        // Enviar email de bienvenida con Brevo
        try
        {
            var loginLink = $"{Request.Scheme}://{Request.Host}/login";
            await _brevoEmailService.SendInvitationEmailAsync(
                dto.Correo,
                $"{dto.Nombre} {dto.Apellido}",
                loginLink,
                "Planilla"
            );
            _logger.LogInformation("Welcome email sent to {Email}", dto.Correo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", dto.Correo);
            // No fallar la creación si el email falla
        }

        _logger.LogInformation("SystemAdmin created user {UserId} ({Email})", user.Id, user.Email);

        return Ok(new { userId = user.Id, email = user.Email, message = "Usuario creado exitosamente" });
    }

    /// <summary>
    /// POST /api/admin/tenants - Crea un nuevo tenant (SIN owner - se asigna después)
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Generar subdomain único
            var subdomain = GenerateUniqueSubdomain(dto.Nombre);

            // 2. Crear Tenant (SIN owner)
            var tenant = new Tenant
            {
                Name = dto.Nombre,
                Subdomain = subdomain,
                RUC = dto.RUC,
                DV = dto.DV,
                TipoContribuyente = dto.TipoContribuyente,
                Phone = dto.Telefono,
                Email = dto.Correo,
                Pais = dto.Pais ?? "Panamá",
                Address = dto.Direccion,
                SitioWeb = dto.SitioWeb,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            // 3. Crear Subscription Free por defecto
            var limits = PlanFeatures.GetLimits(SubscriptionPlan.Free);

            var subscription = new Subscription
            {
                TenantId = tenant.Id,
                Plan = SubscriptionPlan.Free,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                MonthlyPrice = limits.PricePerMonth,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Asociar subscription al tenant
            tenant.SubscriptionId = subscription.Id;
            _context.Tenants.Update(tenant);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("SystemAdmin {AdminId} created tenant {TenantId} ({TenantName})",
                User.FindFirst("sub")?.Value, tenant.Id, tenant.Name);

            // Construir respuesta
            var response = new
            {
                tenantId = tenant.Id,
                name = tenant.Name,
                subdomain = tenant.Subdomain,
                ruc = tenant.RUC,
                dv = tenant.DV,
                tipoContribuyente = tenant.TipoContribuyente,
                message = "Tenant creado exitosamente. Ahora asigna usuarios desde el módulo de Usuarios."
            };

            return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating tenant: {TenantName}", dto.Nombre);
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
            if (!string.IsNullOrEmpty(dto.Nombre))
                tenant.Name = dto.Nombre;

            if (dto.TipoContribuyente != null)
                tenant.TipoContribuyente = dto.TipoContribuyente;

            if (dto.Telefono != null)
                tenant.Phone = dto.Telefono;

            if (dto.Correo != null)
                tenant.Email = dto.Correo;

            if (dto.Pais != null)
                tenant.Pais = dto.Pais;

            if (dto.Direccion != null)
                tenant.Address = dto.Direccion;

            if (dto.SitioWeb != null)
                tenant.SitioWeb = dto.SitioWeb;

            if (dto.LogoUrl != null)
                tenant.LogoUrl = dto.LogoUrl;

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
                    TotalEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && !e.IsDeleted),
                    ActiveEmployees = await _context.Empleados.CountAsync(e => e.TenantId == id && e.EstaActivo && !e.IsDeleted),
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
            var totalEmployees = await _context.Empleados.CountAsync(e => !e.IsDeleted);

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
    /// POST /api/admin/tenants/{id}/users - Asigna un usuario existente a un tenant
    /// Requiere: IsSystemAdmin = true
    /// </summary>
    [HttpPost("tenants/{id}/users")]
    public async Task<IActionResult> InviteUserToTenant(int id, [FromBody] AssignUserToTenantDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
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

            // 3. Buscar usuario existente
            var user = await _userManager.FindByEmailAsync(request.UserEmail);

            if (user == null)
            {
                return NotFound(new { error = $"No existe un usuario con el email {request.UserEmail}. Debe crear el usuario primero desde /system-admin/users" });
            }

            // Verificar que el usuario no esté eliminado
            if (user.IsDeleted)
            {
                return BadRequest(new { error = "El usuario está eliminado. Debe reactivarlo primero." });
            }

            // Verificar que no sea SystemAdmin (los SystemAdmin no se asignan a tenants)
            if (user.IsSystemAdmin)
            {
                return BadRequest(new { error = "No se puede asignar un usuario SystemAdmin a un tenant." });
            }

            // 4. Verificar si ya existe TenantUser
            var existingTenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.TenantId == id && tu.UserId == user.Id);

            if (existingTenantUser != null)
            {
                return Conflict(new { error = "El usuario ya está asignado a este tenant" });
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
                Action = "AssignUserToTenant",
                EntityType = "TenantUser",
                EntityId = tenantUser.Id.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    UserEmail = user.Email,
                    Role = request.Role.ToString(),
                    AssignedBy = adminUserId
                }),
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogEntries.Add(auditLog);
            await _context.SaveChangesAsync();

            // 7. Enviar email de invitación con Brevo
            try
            {
                var loginLink = $"{Request.Scheme}://{Request.Host}/login";
                await _brevoEmailService.SendInvitationEmailAsync(
                    user.Email!,
                    user.NombreCompleto ?? user.Email ?? "Usuario",
                    loginLink,
                    tenant.Name
                );
                _logger.LogInformation("Invitation email sent to {Email} for tenant {TenantName}", user.Email, tenant.Name);
            }
            catch (Exception emailEx)
            {
                _logger.LogWarning(emailEx, "Failed to send invitation email to {Email}, but user was assigned successfully", user.Email);
                // No fallar la transacción si el email falla
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "SystemAdmin {AdminId} assigned user {Email} to tenant {TenantId} with role {Role}",
                adminUserId, user.Email, id, request.Role);

            // 8. Retornar DTO
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
            _logger.LogError(ex, "Error assigning user to tenant {TenantId}", id);
            return StatusCode(500, new { error = "Error al asignar usuario al tenant" });
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
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "El ID del usuario es requerido" });
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

            // 3b. VALIDACIÓN CRÍTICA: No eliminar el último Owner de ningún tenant
            var ownerMemberships = await _context.TenantUsers
                .Where(tu => tu.UserId == userId && tu.Role == TenantRole.Owner && tu.IsActive)
                .Select(tu => new { tu.TenantId, tu.Tenant.Name })
                .ToListAsync();

            foreach (var ownership in ownerMemberships)
            {
                var ownersCount = await _context.TenantUsers
                    .Where(tu => tu.TenantId == ownership.TenantId
                              && tu.Role == TenantRole.Owner
                              && tu.IsActive)
                    .CountAsync();

                if (ownersCount <= 1)
                {
                    return BadRequest(new
                    {
                        error = $"No se puede eliminar el último Owner del tenant '{ownership.Name}'. " +
                                $"Debes asignar otro Owner antes de eliminar este usuario.",
                        tenantId = ownership.TenantId,
                        tenantName = ownership.Name
                    });
                }
            }

            // 3c. VERIFICAR: Empleado vinculado (warning, no blocker)
            var linkedEmployee = await _context.Empleados
                .FirstOrDefaultAsync(e => e.UserId == userId && e.EstaActivo);

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

            // 6b. Desvincular empleado si existe
            if (linkedEmployee != null)
            {
                linkedEmployee.UserId = null;
                _context.Empleados.Update(linkedEmployee);
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
                        DeletedAt = user.DeletedAt,
                        HadLinkedEmployee = linkedEmployee != null,
                        LinkedEmployeeName = linkedEmployee != null ? $"{linkedEmployee.Nombre} {linkedEmployee.Apellido}" : null
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
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "El ID del usuario es requerido" });
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
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "El ID del usuario es requerido" });
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
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "El ID del usuario es requerido" });
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

    /// <summary>
    /// GET /api/admin/cleanup/duplicate-users - Lista usuarios duplicados por email
    /// Requiere: IsSystemAdmin = true
    /// TEMPORAL: Para investigación y limpieza de duplicados
    /// </summary>
    [HttpGet("cleanup/duplicate-users")]
    public async Task<IActionResult> GetDuplicateUsers()
    {
        try
        {
            // Encontrar emails duplicados
            var duplicates = await _context.Users
                .Where(u => !u.IsDeleted)
                .GroupBy(u => u.Email!.ToLower())
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    Email = g.Key,
                    Count = g.Count(),
                    Users = g.Select(u => new
                    {
                        u.Id,
                        u.Email,
                        u.UserName,
                        u.NombreCompleto,
                        u.EmailConfirmed,
                        u.IsSystemAdmin,
                        CreatedAt = u.LockoutEnd // Placeholder si no hay CreatedAt
                    }).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("SystemAdmin {AdminId} retrieved duplicate users. Found {Count} duplicate email groups",
                User.FindFirst("sub")?.Value, duplicates.Count);

            return Ok(new { duplicates, total = duplicates.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting duplicate users");
            return StatusCode(500, new { error = "Error al obtener usuarios duplicados" });
        }
    }

    /// <summary>
    /// DELETE /api/admin/cleanup/user/{userId} - Elimina físicamente un usuario duplicado
    /// Requiere: IsSystemAdmin = true
    /// TEMPORAL: Para limpieza de usuarios duplicados
    /// ADVERTENCIA: Esto hace un hard delete, usar con precaución
    /// </summary>
    [HttpDelete("cleanup/user/{userId}")]
    public async Task<IActionResult> HardDeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "El ID del usuario es requerido" });
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "Usuario no encontrado" });
            }

            // Verificar que no tenga empleados vinculados
            var linkedEmpleados = await _context.Empleados
                .Where(e => e.UserId == userId)
                .ToListAsync();

            if (linkedEmpleados.Any())
            {
                return BadRequest(new
                {
                    error = "No se puede eliminar este usuario porque tiene empleados vinculados",
                    empleados = linkedEmpleados.Select(e => new { e.Id, e.Nombre, e.Email }).ToList()
                });
            }

            // Verificar que no sea SystemAdmin
            if (user.IsSystemAdmin)
            {
                return BadRequest(new { error = "No se puede eliminar un SystemAdmin" });
            }

            // Verificar que no tenga membresías activas
            var activeMemberships = await _context.TenantUsers
                .Where(tu => tu.UserId == userId && tu.IsActive)
                .ToListAsync();

            if (activeMemberships.Any())
            {
                return BadRequest(new
                {
                    error = "No se puede eliminar este usuario porque tiene membresías activas en tenants",
                    tenants = activeMemberships.Select(tu => new { tu.TenantId, tu.Role }).ToList()
                });
            }

            // HARD DELETE: Eliminar membresías inactivas primero
            var inactiveMemberships = await _context.TenantUsers
                .Where(tu => tu.UserId == userId)
                .ToListAsync();

            _context.TenantUsers.RemoveRange(inactiveMemberships);
            await _context.SaveChangesAsync();

            // HARD DELETE: Eliminar usuario
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { error = "Error al eliminar usuario", details = errors });
            }

            _logger.LogWarning("SystemAdmin {AdminId} performed HARD DELETE on user {UserId} ({Email})",
                User.FindFirst("sub")?.Value, userId, user.Email);

            return Ok(new { success = true, message = $"Usuario {user.Email} eliminado permanentemente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hard deleting user {UserId}", userId);
            return StatusCode(500, new { error = "Error al eliminar el usuario" });
        }
    }
}
