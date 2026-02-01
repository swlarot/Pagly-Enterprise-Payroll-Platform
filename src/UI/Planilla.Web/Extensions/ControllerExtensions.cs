using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Web.Extensions;

/// <summary>
/// Extensiones para Controllers que facilitan la verificación de permisos y roles
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    /// Obtiene el TenantId del usuario actual desde los claims del JWT
    /// </summary>
    public static int GetCurrentTenantId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst("tenant_id");
        if (claim == null || !int.TryParse(claim.Value, out var tenantId))
        {
            throw new UnauthorizedAccessException("No se encontró el TenantId en el token");
        }
        return tenantId;
    }

    /// <summary>
    /// Obtiene el UserId del usuario actual desde los claims del JWT
    /// </summary>
    public static string GetCurrentUserId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? controller.User.FindFirst("sub");
        if (claim == null)
        {
            throw new UnauthorizedAccessException("No se encontró el UserId en el token");
        }
        return claim.Value;
    }

    /// <summary>
    /// Obtiene el rol del usuario actual en el tenant
    /// </summary>
    public static TenantRole GetCurrentTenantRole(this ControllerBase controller)
    {
        var roleClaim = controller.User.FindFirst("tenant_role");
        if (roleClaim == null || !Enum.TryParse<TenantRole>(roleClaim.Value, out var role))
        {
            throw new UnauthorizedAccessException("No se encontró el rol del tenant en el token");
        }
        return role;
    }

    /// <summary>
    /// Verifica si el usuario actual tiene al menos uno de los roles especificados
    /// </summary>
    public static bool HasAnyRole(this ControllerBase controller, params TenantRole[] roles)
    {
        try
        {
            var userRole = controller.GetCurrentTenantRole();
            return roles.Contains(userRole);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica si el usuario es SystemAdmin
    /// </summary>
    public static bool IsSystemAdmin(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst("is_system_admin");
        return claim?.Value == "true" || claim?.Value == "True";
    }

    /// <summary>
    /// Verifica si el usuario puede escribir (no es Employee ni Accountant en solo lectura)
    /// </summary>
    public static bool CanWrite(this ControllerBase controller)
    {
        try
        {
            var role = controller.GetCurrentTenantRole();
            return role == TenantRole.Owner
                || role == TenantRole.Admin
                || role == TenantRole.Manager;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica si el usuario puede eliminar recursos
    /// </summary>
    public static bool CanDelete(this ControllerBase controller)
    {
        try
        {
            var role = controller.GetCurrentTenantRole();
            return role == TenantRole.Owner || role == TenantRole.Admin;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica si es el propio dato del usuario (para Employee que solo ve su info)
    /// </summary>
    public static bool IsOwnData(this ControllerBase controller, string targetUserId)
    {
        try
        {
            var currentUserId = controller.GetCurrentUserId();
            return currentUserId == targetUserId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Crea una respuesta 403 Forbidden con mensaje personalizado
    /// </summary>
    public static ObjectResult Forbidden(this ControllerBase controller, string message = "No tienes permisos para realizar esta acción")
    {
        return new ObjectResult(new { error = message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
