using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Auth;
using Vorluno.Planilla.Application.DTOs.Tenant;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para gestión del tenant actual (actualización, usuarios, etc.)
/// CRÍTICO: Todas las operaciones filtradas por TenantId para aislamiento multi-tenant
/// </summary>
public class TenantManagementService : ITenantManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogService _auditLogService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IAuditLogService auditLogService,
        UserManager<AppUser> userManager,
        ILogger<TenantManagementService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _auditLogService = auditLogService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene información completa del tenant actual incluyendo suscripción y métricas de uso
    /// </summary>
    public async Task<Result<TenantDto>> GetCurrentTenantAsync()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<TenantDto>.Fail("Tenant context no encontrado");
            }

            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return Result<TenantDto>.Fail("Tenant no encontrado");
            }

            // Obtener métricas de uso
            var usersCount = await _context.TenantUsers
                .CountAsync(tu => tu.TenantId == tenantId && tu.IsActive);

            var employeesCount = await _context.Empleados
                .CountAsync(e => e.TenantId == tenantId);

            var companiesCount = 1; // Por ahora asumimos 1 compañía por tenant

            var pendingInvitationsCount = await _context.TenantInvitations
                .CountAsync(i => i.TenantId == tenantId && i.IsActive && !i.AcceptedAt.HasValue && !i.IsRevoked && i.ExpiresAt > DateTime.UtcNow);

            // Obtener límites del plan
            var limits = PlanFeatures.GetLimits(tenant.Subscription?.Plan ?? SubscriptionPlan.Free);

            var tenantDto = new TenantDto
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
                Subscription = tenant.Subscription != null ? new SubscriptionInfoDto
                {
                    Plan = tenant.Subscription.Plan,
                    PlanName = tenant.Subscription.Plan.ToString(),
                    Status = tenant.Subscription.Status,
                    StatusName = tenant.Subscription.Status.ToString(),
                    TrialEndsAt = tenant.Subscription.TrialEndsAt,
                    MaxEmployees = limits.MaxEmployees,
                    MaxUsers = limits.MaxUsers,
                    MaxCompanies = limits.MaxCompanies,
                    CanExportExcel = limits.CanExportExcel,
                    CanExportPdf = limits.CanExportPdf,
                    CanUseApi = limits.CanUseApi,
                    MonthlyPrice = limits.PricePerMonth
                } : null,
                Usage = new TenantUsageDto
                {
                    UsersCount = usersCount,
                    EmployeesCount = employeesCount,
                    CompaniesCount = companiesCount,
                    PendingInvitationsCount = pendingInvitationsCount,
                    MaxUsers = limits.MaxUsers,
                    MaxEmployees = limits.MaxEmployees,
                    MaxCompanies = limits.MaxCompanies
                }
            };

            return Result<TenantDto>.Ok(tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current tenant");
            return Result<TenantDto>.Fail("Error al obtener información del tenant");
        }
    }

    /// <summary>
    /// Actualiza información del tenant actual
    /// SOLO Owner/Admin pueden actualizar
    /// </summary>
    public async Task<Result<TenantDto>> UpdateTenantAsync(UpdateTenantDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<TenantDto>.Fail("Tenant context no encontrado");
            }

            // Verificar permisos
            if (!_tenantContext.IsAdminOrOwner())
            {
                return Result<TenantDto>.Fail("No tiene permisos para actualizar el tenant");
            }

            var tenant = await _context.Tenants
                .Include(t => t.Subscription)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return Result<TenantDto>.Fail("Tenant no encontrado");
            }

            // Actualizar propiedades permitidas
            var hasChanges = false;
            var changes = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(dto.Name) && tenant.Name != dto.Name)
            {
                changes["OldName"] = tenant.Name;
                changes["NewName"] = dto.Name;
                tenant.Name = dto.Name;
                hasChanges = true;
            }

            if (!string.IsNullOrEmpty(dto.Subdomain) && tenant.Subdomain != dto.Subdomain)
            {
                // Verificar que el nuevo subdomain no exista
                var subdomainExists = await _context.Tenants
                    .AnyAsync(t => t.Subdomain == dto.Subdomain && t.Id != tenantId);

                if (subdomainExists)
                {
                    return Result<TenantDto>.Fail("El subdominio ya está en uso");
                }

                changes["OldSubdomain"] = tenant.Subdomain;
                changes["NewSubdomain"] = dto.Subdomain;
                tenant.Subdomain = dto.Subdomain;
                hasChanges = true;
            }

            if (dto.RUC != null && tenant.RUC != dto.RUC)
            {
                changes["OldRUC"] = tenant.RUC ?? "null";
                changes["NewRUC"] = dto.RUC;
                tenant.RUC = dto.RUC;
                hasChanges = true;
            }

            if (dto.DV != null && tenant.DV != dto.DV)
            {
                tenant.DV = dto.DV;
                hasChanges = true;
            }

            if (dto.Address != null && tenant.Address != dto.Address)
            {
                tenant.Address = dto.Address;
                hasChanges = true;
            }

            if (dto.Phone != null && tenant.Phone != dto.Phone)
            {
                tenant.Phone = dto.Phone;
                hasChanges = true;
            }

            if (dto.Email != null && tenant.Email != dto.Email)
            {
                tenant.Email = dto.Email;
                hasChanges = true;
            }

            if (hasChanges)
            {
                tenant.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Audit log
                await _auditLogService.LogAsync("TenantUpdated", "Tenant", tenantId.ToString(), changes);

                _logger.LogInformation("Tenant {TenantId} updated by user {UserId}", tenantId, _tenantContext.UserId);
            }

            // Devolver tenant actualizado
            return await GetCurrentTenantAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant");
            return Result<TenantDto>.Fail("Error al actualizar el tenant");
        }
    }

    /// <summary>
    /// Obtiene la lista de usuarios del tenant actual
    /// </summary>
    public async Task<Result<List<TenantUserDto>>> GetTenantUsersAsync()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<List<TenantUserDto>>.Fail("Tenant context no encontrado");
            }

            var tenantUsers = await _context.TenantUsers
                .Include(tu => tu.User)
                .Where(tu => tu.TenantId == tenantId)
                .AsNoTracking()
                .Select(tu => new TenantUserDto
                {
                    Id = tu.Id,
                    TenantId = tu.TenantId,
                    UserId = tu.UserId,
                    Email = tu.User != null ? tu.User.Email! : tu.InvitedEmail ?? "unknown",
                    FullName = tu.User != null ? tu.User.NombreCompleto : null,
                    Role = tu.Role,
                    RoleName = tu.Role.ToString(),
                    IsActive = tu.IsActive,
                    JoinedAt = tu.JoinedAt,
                    LastLoginAt = tu.LastLoginAt,
                    IsPendingInvitation = tu.IsPendingInvitation
                })
                .OrderBy(tu => tu.Role)
                .ThenBy(tu => tu.Email)
                .ToListAsync();

            return Result<List<TenantUserDto>>.Ok(tenantUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant users");
            return Result<List<TenantUserDto>>.Fail("Error al obtener los usuarios del tenant");
        }
    }

    /// <summary>
    /// Actualiza un usuario del tenant (cambiar rol o activar/desactivar)
    /// VALIDACIÓN: Owner no puede cambiar su propio rol si es el único Owner
    /// </summary>
    public async Task<Result<TenantUserDto>> UpdateTenantUserAsync(int tenantUserId, UpdateTenantUserDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<TenantUserDto>.Fail("Tenant context no encontrado");
            }

            // Verificar permisos
            if (!_tenantContext.IsAdminOrOwner())
            {
                return Result<TenantUserDto>.Fail("No tiene permisos para actualizar usuarios");
            }

            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.User)
                .FirstOrDefaultAsync(tu => tu.Id == tenantUserId && tu.TenantId == tenantId);

            if (tenantUser == null)
            {
                return Result<TenantUserDto>.Fail("Usuario no encontrado en este tenant");
            }

            var changes = new Dictionary<string, string>();

            // Validar cambio de rol
            if (dto.Role.HasValue && tenantUser.Role != dto.Role.Value)
            {
                // No permitir que Owner cambie su propio rol si es el único Owner
                if (tenantUser.UserId == _tenantContext.UserId && tenantUser.Role == TenantRole.Owner)
                {
                    var ownerCount = await _context.TenantUsers
                        .CountAsync(tu => tu.TenantId == tenantId && tu.Role == TenantRole.Owner && tu.IsActive);

                    if (ownerCount <= 1)
                    {
                        return Result<TenantUserDto>.Fail("No puedes cambiar tu rol de Owner si eres el único Owner del tenant");
                    }
                }

                changes["OldRole"] = tenantUser.Role.ToString();
                changes["NewRole"] = dto.Role.Value.ToString();
                tenantUser.Role = dto.Role.Value;
            }

            // Validar cambio de estado
            if (dto.IsActive.HasValue && tenantUser.IsActive != dto.IsActive.Value)
            {
                // No permitir que Owner se desactive a sí mismo si es el único Owner
                if (tenantUser.UserId == _tenantContext.UserId && tenantUser.Role == TenantRole.Owner && !dto.IsActive.Value)
                {
                    var activeOwnerCount = await _context.TenantUsers
                        .CountAsync(tu => tu.TenantId == tenantId && tu.Role == TenantRole.Owner && tu.IsActive);

                    if (activeOwnerCount <= 1)
                    {
                        return Result<TenantUserDto>.Fail("No puedes desactivarte si eres el único Owner activo del tenant");
                    }
                }

                changes["OldIsActive"] = tenantUser.IsActive.ToString();
                changes["NewIsActive"] = dto.IsActive.Value.ToString();
                tenantUser.IsActive = dto.IsActive.Value;
            }

            if (changes.Any())
            {
                tenantUser.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Audit log
                var action = dto.Role.HasValue ? "UserRoleChanged" : "UserStatusChanged";
                await _auditLogService.LogAsync(action, "TenantUser", tenantUserId.ToString(), changes);

                _logger.LogInformation("TenantUser {TenantUserId} updated by user {UserId}", tenantUserId, _tenantContext.UserId);
            }

            // Devolver usuario actualizado
            var result = await _context.TenantUsers
                .Include(tu => tu.User)
                .Where(tu => tu.Id == tenantUserId)
                .Select(tu => new TenantUserDto
                {
                    Id = tu.Id,
                    TenantId = tu.TenantId,
                    UserId = tu.UserId,
                    Email = tu.User != null ? tu.User.Email! : tu.InvitedEmail ?? "unknown",
                    FullName = tu.User != null ? tu.User.NombreCompleto : null,
                    Role = tu.Role,
                    RoleName = tu.Role.ToString(),
                    IsActive = tu.IsActive,
                    JoinedAt = tu.JoinedAt,
                    LastLoginAt = tu.LastLoginAt,
                    IsPendingInvitation = tu.IsPendingInvitation
                })
                .FirstOrDefaultAsync();

            return Result<TenantUserDto>.Ok(result!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant user");
            return Result<TenantUserDto>.Fail("Error al actualizar el usuario");
        }
    }

    /// <summary>
    /// Elimina (soft delete) un usuario del tenant
    /// VALIDACIÓN: Owner no puede eliminarse a sí mismo si es el único Owner
    /// </summary>
    public async Task<Result<bool>> RemoveTenantUserAsync(int tenantUserId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId == 0)
            {
                return Result<bool>.Fail("Tenant context no encontrado");
            }

            // Verificar permisos
            if (!_tenantContext.IsAdminOrOwner())
            {
                return Result<bool>.Fail("No tiene permisos para eliminar usuarios");
            }

            var tenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.Id == tenantUserId && tu.TenantId == tenantId);

            if (tenantUser == null)
            {
                return Result<bool>.Fail("Usuario no encontrado en este tenant");
            }

            // No permitir que Owner se elimine a sí mismo si es el único Owner
            if (tenantUser.UserId == _tenantContext.UserId && tenantUser.Role == TenantRole.Owner)
            {
                var activeOwnerCount = await _context.TenantUsers
                    .CountAsync(tu => tu.TenantId == tenantId && tu.Role == TenantRole.Owner && tu.IsActive);

                if (activeOwnerCount <= 1)
                {
                    return Result<bool>.Fail("No puedes eliminarte si eres el único Owner del tenant");
                }
            }

            // Soft delete
            tenantUser.IsActive = false;
            tenantUser.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Audit log
            await _auditLogService.LogAsync("UserRemovedFromTenant", "TenantUser", tenantUserId.ToString(), new Dictionary<string, string>
            {
                ["RemovedUserEmail"] = tenantUser.User?.Email ?? tenantUser.InvitedEmail ?? "unknown",
                ["RemovedUserRole"] = tenantUser.Role.ToString()
            });

            _logger.LogInformation("TenantUser {TenantUserId} removed from tenant {TenantId} by user {UserId}",
                tenantUserId, tenantId, _tenantContext.UserId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing tenant user");
            return Result<bool>.Fail("Error al eliminar el usuario");
        }
    }
}
