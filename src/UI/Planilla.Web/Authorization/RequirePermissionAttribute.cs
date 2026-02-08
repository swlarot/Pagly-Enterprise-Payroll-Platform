using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Authorization;

/// <summary>
/// Atributo de autorización basado en permisos granulares.
/// Verifica que el usuario actual tenga AL MENOS UNO de los permisos especificados.
/// Útil para Employee Self-Service: el usuario puede tener "employees.read" (ver todos) O "employee.view_self" (ver solo el suyo).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissions;

    /// <summary>
    /// Crea una nueva instancia del atributo RequirePermission
    /// </summary>
    /// <param name="permissions">Permisos requeridos (lógica OR: el usuario necesita AL MENOS UNO)</param>
    public RequirePermissionAttribute(params string[] permissions)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        if (_permissions.Length == 0)
        {
            throw new ArgumentException("Se debe especificar al menos un permiso", nameof(permissions));
        }
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

        // Verificar si el usuario tiene AL MENOS UNO de los permisos (lógica OR)
        foreach (var permission in _permissions)
        {
            var hasPermission = await roleService.HasPermissionAsync(permission);

            if (hasPermission.Success && hasPermission.Value == true)
            {
                // Tiene este permiso, permitir acceso
                return;
            }
        }

        // No tiene ninguno de los permisos requeridos
        context.Result = new ForbidResult();
    }
}
