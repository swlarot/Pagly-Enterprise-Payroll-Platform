using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Authorization;

/// <summary>
/// Atributo de autorización basado en permisos granulares.
/// Verifica que el usuario actual tenga el permiso especificado en su rol.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    /// <summary>
    /// Crea una nueva instancia del atributo RequirePermission
    /// </summary>
    /// <param name="permission">Permiso requerido (ej: "employees.create")</param>
    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Verificar que el usuario esté autenticado
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Obtener el servicio de roles
        var roleService = context.HttpContext.RequestServices
            .GetService<ICustomTenantRoleService>();

        if (roleService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Verificar si el usuario tiene el permiso
        var hasPermission = await roleService.HasPermissionAsync(_permission);

        if (!hasPermission.Success || hasPermission.Value != true)
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
