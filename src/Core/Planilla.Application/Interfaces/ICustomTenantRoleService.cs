using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Roles;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para gestión de roles personalizados del tenant
/// </summary>
public interface ICustomTenantRoleService
{
    /// <summary>
    /// Obtiene todos los roles del tenant actual (sistema + personalizados)
    /// </summary>
    Task<Result<List<CustomTenantRoleDto>>> GetAllRolesAsync();

    /// <summary>
    /// Obtiene un rol por su ID
    /// </summary>
    Task<Result<CustomTenantRoleDto>> GetRoleByIdAsync(int roleId);

    /// <summary>
    /// Crea un nuevo rol personalizado
    /// </summary>
    Task<Result<CustomTenantRoleDto>> CreateRoleAsync(CreateCustomTenantRoleDto dto);

    /// <summary>
    /// Actualiza un rol personalizado existente
    /// </summary>
    Task<Result<CustomTenantRoleDto>> UpdateRoleAsync(int roleId, UpdateCustomTenantRoleDto dto);

    /// <summary>
    /// Elimina un rol personalizado (solo si no es sistema y no tiene usuarios asignados)
    /// </summary>
    Task<Result<bool>> DeleteRoleAsync(int roleId);

    /// <summary>
    /// Obtiene los permisos asignados a un rol
    /// </summary>
    Task<Result<List<string>>> GetRolePermissionsAsync(int roleId);

    /// <summary>
    /// Actualiza los permisos de un rol
    /// </summary>
    Task<Result<bool>> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsDto dto);

    /// <summary>
    /// Asigna un rol a un usuario del tenant
    /// </summary>
    Task<Result<bool>> AssignRoleToUserAsync(AssignRoleToUserDto dto);

    /// <summary>
    /// Obtiene todos los permisos disponibles en el sistema
    /// </summary>
    Task<Result<List<PermissionDto>>> GetAllPermissionsAsync();

    /// <summary>
    /// Verifica si el usuario actual tiene un permiso específico
    /// </summary>
    Task<Result<bool>> HasPermissionAsync(string permission);

    /// <summary>
    /// Obtiene todos los permisos del usuario actual
    /// </summary>
    Task<Result<List<string>>> GetCurrentUserPermissionsAsync();
}
