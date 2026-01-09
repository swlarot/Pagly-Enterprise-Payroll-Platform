using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Proporciona acceso al contexto del tenant actual en la solicitud HTTP
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// ID del tenant actual (0 si no hay tenant en el contexto)
    /// </summary>
    int TenantId { get; }

    /// <summary>
    /// Rol del usuario actual dentro del tenant (Employee por defecto)
    /// </summary>
    TenantRole TenantRole { get; }

    /// <summary>
    /// ID del usuario actual
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Indica si hay un tenant en el contexto actual
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Establece el tenant actual
    /// </summary>
    Task SetTenantAsync(int tenantId);

    /// <summary>
    /// Obtiene información completa del tenant actual
    /// </summary>
    Task<Tenant?> GetCurrentTenantAsync();

    /// <summary>
    /// Verifica si el usuario tiene un rol específico o superior
    /// </summary>
    bool HasRole(TenantRole role);

    /// <summary>
    /// Verifica si el usuario es Owner o Admin
    /// </summary>
    bool IsAdminOrOwner();

    /// <summary>
    /// Limpia el contexto del tenant
    /// </summary>
    void Clear();
}
