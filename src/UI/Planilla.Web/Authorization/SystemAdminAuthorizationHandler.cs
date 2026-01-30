using Microsoft.AspNetCore.Authorization;

namespace Vorluno.Planilla.Web.Authorization;

/// <summary>
/// Handler para verificar si un usuario tiene el claim is_system_admin = true
/// </summary>
public class SystemAdminAuthorizationHandler : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemAdminRequirement requirement)
    {
        // Buscar el claim is_system_admin
        var isSystemAdminClaim = context.User.FindFirst("is_system_admin");

        if (isSystemAdminClaim != null && isSystemAdminClaim.Value == "true")
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
