using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Tenant;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para gestión del tenant actual
/// </summary>
public interface ITenantManagementService
{
    /// <summary>
    /// Obtiene información completa del tenant actual incluyendo suscripción y métricas de uso
    /// </summary>
    Task<Result<TenantDto>> GetCurrentTenantAsync();

    /// <summary>
    /// Actualiza información del tenant actual
    /// </summary>
    Task<Result<TenantDto>> UpdateTenantAsync(UpdateTenantDto dto);

    /// <summary>
    /// Obtiene la lista de usuarios del tenant actual
    /// </summary>
    Task<Result<List<TenantUserDto>>> GetTenantUsersAsync();

    /// <summary>
    /// Actualiza un usuario del tenant (cambiar rol o activar/desactivar)
    /// </summary>
    Task<Result<TenantUserDto>> UpdateTenantUserAsync(int tenantUserId, UpdateTenantUserDto dto);

    /// <summary>
    /// Elimina (soft delete) un usuario del tenant
    /// </summary>
    Task<Result<bool>> RemoveTenantUserAsync(int tenantUserId);
}
