using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Roles;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de gestión de roles personalizados
/// </summary>
public class CustomTenantRoleService : ICustomTenantRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CustomTenantRoleService> _logger;

    public CustomTenantRoleService(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ILogger<CustomTenantRoleService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<List<CustomTenantRoleDto>>> GetAllRolesAsync()
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var customRoles = await _context.Set<CustomTenantRole>()
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .Include(r => r.Permissions)
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Name)
                .Select(r => new CustomTenantRoleDto
                {
                    Id = r.Id,
                    TenantId = r.TenantId,
                    Name = r.Name,
                    Description = r.Description,
                    IsSystem = r.IsSystem,
                    Color = r.Color,
                    DisplayOrder = r.DisplayOrder,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    IsActive = r.IsActive,
                    Permissions = r.Permissions.Select(p => p.Permission).ToList(),
                    UserCount = r.Users.Count(u => u.IsActive)
                })
                .ToListAsync();

            return Result<List<CustomTenantRoleDto>>.Ok(customRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener roles del tenant {TenantId}", _tenantContext.TenantId);
            return Result<List<CustomTenantRoleDto>>.Fail("Error al obtener los roles");
        }
    }

    public async Task<Result<CustomTenantRoleDto>> GetRoleByIdAsync(int roleId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var role = await _context.Set<CustomTenantRole>()
                .Where(r => r.Id == roleId && r.TenantId == tenantId)
                .Include(r => r.Permissions)
                .Select(r => new CustomTenantRoleDto
                {
                    Id = r.Id,
                    TenantId = r.TenantId,
                    Name = r.Name,
                    Description = r.Description,
                    IsSystem = r.IsSystem,
                    Color = r.Color,
                    DisplayOrder = r.DisplayOrder,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    IsActive = r.IsActive,
                    Permissions = r.Permissions.Select(p => p.Permission).ToList(),
                    UserCount = r.Users.Count(u => u.IsActive)
                })
                .FirstOrDefaultAsync();

            if (role == null)
                return Result<CustomTenantRoleDto>.Fail("Rol no encontrado");

            return Result<CustomTenantRoleDto>.Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener rol {RoleId}", roleId);
            return Result<CustomTenantRoleDto>.Fail("Error al obtener el rol");
        }
    }

    public async Task<Result<CustomTenantRoleDto>> CreateRoleAsync(CreateCustomTenantRoleDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verificar que el usuario actual es Owner
            var currentUserRole = await GetCurrentUserRoleAsync();
            if (currentUserRole != TenantRole.Owner)
            {
                return Result<CustomTenantRoleDto>.Fail(
                    "Solo el propietario del tenant puede crear roles personalizados");
            }

            // Verificar que no exista un rol con el mismo nombre
            var existingRole = await _context.Set<CustomTenantRole>()
                .AnyAsync(r => r.TenantId == tenantId && r.Name == dto.Name && r.IsActive);

            if (existingRole)
            {
                return Result<CustomTenantRoleDto>.Fail(
                    $"Ya existe un rol con el nombre '{dto.Name}'");
            }

            // Validar permisos
            var validPermissions = SystemPermission.GetAllPermissions()
                .Select(p => p.Key)
                .ToHashSet();

            var invalidPermissions = dto.Permissions
                .Where(p => !validPermissions.Contains(p))
                .ToList();

            if (invalidPermissions.Any())
            {
                return Result<CustomTenantRoleDto>.Fail(
                    $"Permisos inválidos: {string.Join(", ", invalidPermissions)}");
            }

            // Crear el rol
            var role = new CustomTenantRole
            {
                TenantId = tenantId,
                Name = dto.Name,
                Description = dto.Description,
                IsSystem = false,
                Color = dto.Color ?? "#6B7280",
                DisplayOrder = dto.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Set<CustomTenantRole>().Add(role);
            await _context.SaveChangesAsync();

            // Asignar permisos
            foreach (var permission in dto.Permissions)
            {
                var rolePermission = new RolePermission
                {
                    TenantId = tenantId,
                    CustomTenantRoleId = role.Id,
                    Permission = permission,
                    GrantedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Set<RolePermission>().Add(rolePermission);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Rol personalizado '{RoleName}' creado en tenant {TenantId} por usuario {UserId}",
                role.Name, tenantId, _currentUserService.UserId);

            // Retornar el rol creado
            return await GetRoleByIdAsync(role.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear rol personalizado");
            return Result<CustomTenantRoleDto>.Fail("Error al crear el rol");
        }
    }

    public async Task<Result<CustomTenantRoleDto>> UpdateRoleAsync(int roleId, UpdateCustomTenantRoleDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verificar que el usuario actual es Owner
            var currentUserRole = await GetCurrentUserRoleAsync();
            if (currentUserRole != TenantRole.Owner)
            {
                return Result<CustomTenantRoleDto>.Fail(
                    "Solo el propietario del tenant puede modificar roles");
            }

            var role = await _context.Set<CustomTenantRole>()
                .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId);

            if (role == null)
                return Result<CustomTenantRoleDto>.Fail("Rol no encontrado");

            // No permitir editar roles del sistema
            if (role.IsSystem)
            {
                return Result<CustomTenantRoleDto>.Fail(
                    "No se pueden modificar los roles del sistema");
            }

            // Verificar nombre duplicado
            var duplicateName = await _context.Set<CustomTenantRole>()
                .AnyAsync(r => r.TenantId == tenantId &&
                              r.Name == dto.Name &&
                              r.Id != roleId &&
                              r.IsActive);

            if (duplicateName)
            {
                return Result<CustomTenantRoleDto>.Fail(
                    $"Ya existe otro rol con el nombre '{dto.Name}'");
            }

            // Actualizar el rol
            role.Name = dto.Name;
            role.Description = dto.Description;
            role.Color = dto.Color ?? role.Color;
            role.DisplayOrder = dto.DisplayOrder;
            role.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Rol personalizado '{RoleName}' actualizado en tenant {TenantId}",
                role.Name, tenantId);

            return await GetRoleByIdAsync(role.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar rol {RoleId}", roleId);
            return Result<CustomTenantRoleDto>.Fail("Error al actualizar el rol");
        }
    }

    public async Task<Result<bool>> DeleteRoleAsync(int roleId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verificar que el usuario actual es Owner
            var currentUserRole = await GetCurrentUserRoleAsync();
            if (currentUserRole != TenantRole.Owner)
            {
                return Result<bool>.Fail(
                    "Solo el propietario del tenant puede eliminar roles");
            }

            var role = await _context.Set<CustomTenantRole>()
                .Include(r => r.Users)
                .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId);

            if (role == null)
                return Result<bool>.Fail("Rol no encontrado");

            // No permitir eliminar roles del sistema
            if (role.IsSystem)
            {
                return Result<bool>.Fail(
                    "No se pueden eliminar los roles del sistema");
            }

            // No permitir eliminar si hay usuarios con este rol
            var activeUsers = role.Users.Count(u => u.IsActive);
            if (activeUsers > 0)
            {
                return Result<bool>.Fail(
                    $"No se puede eliminar el rol porque hay {activeUsers} usuario(s) asignado(s). " +
                    "Reasigne los usuarios a otro rol antes de eliminar.");
            }

            // Soft delete del rol y sus permisos
            role.IsActive = false;
            role.UpdatedAt = DateTime.UtcNow;

            var permissions = await _context.Set<RolePermission>()
                .Where(p => p.CustomTenantRoleId == roleId)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                permission.IsActive = false;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Rol personalizado '{RoleName}' eliminado en tenant {TenantId}",
                role.Name, tenantId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar rol {RoleId}", roleId);
            return Result<bool>.Fail("Error al eliminar el rol");
        }
    }

    public async Task<Result<List<string>>> GetRolePermissionsAsync(int roleId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var permissions = await _context.Set<RolePermission>()
                .Where(p => p.CustomTenantRoleId == roleId &&
                           p.TenantId == tenantId &&
                           p.IsActive)
                .Select(p => p.Permission)
                .ToListAsync();

            return Result<List<string>>.Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener permisos del rol {RoleId}", roleId);
            return Result<List<string>>.Fail("Error al obtener permisos");
        }
    }

    public async Task<Result<bool>> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verificar que el usuario actual es Owner
            var currentUserRole = await GetCurrentUserRoleAsync();
            if (currentUserRole != TenantRole.Owner)
            {
                return Result<bool>.Fail(
                    "Solo el propietario del tenant puede modificar permisos");
            }

            var role = await _context.Set<CustomTenantRole>()
                .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId);

            if (role == null)
                return Result<bool>.Fail("Rol no encontrado");

            // No permitir editar permisos de roles del sistema
            if (role.IsSystem)
            {
                return Result<bool>.Fail(
                    "No se pueden modificar permisos de roles del sistema");
            }

            // Validar permisos
            var validPermissions = SystemPermission.GetAllPermissions()
                .Select(p => p.Key)
                .ToHashSet();

            var invalidPermissions = dto.Permissions
                .Where(p => !validPermissions.Contains(p))
                .ToList();

            if (invalidPermissions.Any())
            {
                return Result<bool>.Fail(
                    $"Permisos inválidos: {string.Join(", ", invalidPermissions)}");
            }

            // Eliminar permisos actuales (soft delete)
            var currentPermissions = await _context.Set<RolePermission>()
                .Where(p => p.CustomTenantRoleId == roleId && p.IsActive)
                .ToListAsync();

            foreach (var permission in currentPermissions)
            {
                permission.IsActive = false;
            }

            // Agregar nuevos permisos
            foreach (var permissionKey in dto.Permissions.Distinct())
            {
                var rolePermission = new RolePermission
                {
                    TenantId = tenantId,
                    CustomTenantRoleId = roleId,
                    Permission = permissionKey,
                    GrantedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Set<RolePermission>().Add(rolePermission);
            }

            role.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Permisos del rol {RoleId} actualizados en tenant {TenantId}",
                roleId, tenantId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar permisos del rol {RoleId}", roleId);
            return Result<bool>.Fail("Error al actualizar permisos");
        }
    }

    public async Task<Result<bool>> AssignRoleToUserAsync(AssignRoleToUserDto dto)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verificar que el usuario actual es Owner
            var currentUserRole = await GetCurrentUserRoleAsync();
            if (currentUserRole != TenantRole.Owner)
            {
                return Result<bool>.Fail(
                    "Solo el Owner puede asignar roles a usuarios");
            }

            var tenantUser = await _context.TenantUsers
                .FirstOrDefaultAsync(tu => tu.UserId == dto.UserId &&
                                          tu.TenantId == tenantId &&
                                          tu.IsActive);

            if (tenantUser == null)
                return Result<bool>.Fail("Usuario no encontrado en este tenant");

            // Si se asigna rol personalizado, validarlo
            if (dto.CustomRoleId.HasValue)
            {
                var customRole = await _context.Set<CustomTenantRole>()
                    .FirstOrDefaultAsync(r => r.Id == dto.CustomRoleId.Value &&
                                             r.TenantId == tenantId &&
                                             r.IsActive);

                if (customRole == null)
                    return Result<bool>.Fail("Rol personalizado no encontrado");

                tenantUser.CustomTenantRoleId = dto.CustomRoleId.Value;
                tenantUser.Role = TenantRole.User; // Valor por defecto cuando usa rol personalizado
            }
            else if (dto.SystemRole.HasValue)
            {
                // Solo Owner puede asignar rol de Owner
                if (dto.SystemRole == (int)TenantRole.Owner && currentUserRole != TenantRole.Owner)
                {
                    return Result<bool>.Fail(
                        "Solo el Owner actual puede asignar el rol de Owner a otro usuario");
                }

                tenantUser.Role = (TenantRole)dto.SystemRole.Value;
                tenantUser.CustomTenantRoleId = null;
            }
            else
            {
                return Result<bool>.Fail(
                    "Debe especificar un rol personalizado o un rol del sistema");
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Rol asignado al usuario {UserId} en tenant {TenantId}",
                dto.UserId, tenantId);

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al asignar rol al usuario {UserId}", dto.UserId);
            return Result<bool>.Fail("Error al asignar rol al usuario");
        }
    }

    public async Task<Result<List<PermissionDto>>> GetAllPermissionsAsync()
    {
        try
        {
            var permissions = SystemPermission.GetAllPermissions()
                .Select(p => new PermissionDto
                {
                    Key = p.Key,
                    Name = p.Name,
                    Description = p.Description,
                    Category = p.Category
                })
                .ToList();

            return Result<List<PermissionDto>>.Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener permisos del sistema");
            return Result<List<PermissionDto>>.Fail("Error al obtener permisos");
        }
    }

    public async Task<Result<bool>> HasPermissionAsync(string permission)
    {
        try
        {
            var permissions = await GetCurrentUserPermissionsAsync();

            if (!permissions.Success)
                return Result<bool>.Fail(permissions.ErrorMessage);

            var hasPermission = permissions.Value.Contains(permission);
            return Result<bool>.Ok(hasPermission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar permiso {Permission}", permission);
            return Result<bool>.Fail("Error al verificar permiso");
        }
    }

    public async Task<Result<List<string>>> GetCurrentUserPermissionsAsync()
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _tenantContext.TenantId;

            if (string.IsNullOrEmpty(userId))
                return Result<List<string>>.Fail("Usuario no autenticado");

            var tenantUser = await _context.TenantUsers
                .Include(tu => tu.CustomRole)
                    .ThenInclude(r => r!.Permissions)
                .FirstOrDefaultAsync(tu => tu.UserId == userId &&
                                          tu.TenantId == tenantId &&
                                          tu.IsActive);

            if (tenantUser == null)
                return Result<List<string>>.Fail("Usuario no pertenece al tenant");

            List<string> permissions;

            if (tenantUser.CustomTenantRoleId.HasValue && tenantUser.CustomRole != null)
            {
                // Usar permisos del rol personalizado
                permissions = tenantUser.CustomRole.Permissions
                    .Where(p => p.IsActive)
                    .Select(p => p.Permission)
                    .ToList();
            }
            else
            {
                // Usar permisos del rol del sistema
                permissions = SystemPermission.GetDefaultPermissionsForSystemRole(tenantUser.Role);
            }

            return Result<List<string>>.Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener permisos del usuario actual");
            return Result<List<string>>.Fail("Error al obtener permisos");
        }
    }

    /// <summary>
    /// Helper: Obtiene el rol del usuario actual
    /// </summary>
    private async Task<TenantRole> GetCurrentUserRoleAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _tenantContext.TenantId;

        var tenantUser = await _context.TenantUsers
            .FirstOrDefaultAsync(tu => tu.UserId == userId &&
                                      tu.TenantId == tenantId &&
                                      tu.IsActive);

        return tenantUser?.Role ?? TenantRole.User;
    }
}
