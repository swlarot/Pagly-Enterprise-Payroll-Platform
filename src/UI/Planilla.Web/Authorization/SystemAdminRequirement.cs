using Microsoft.AspNetCore.Authorization;

namespace Vorluno.Planilla.Web.Authorization;

/// <summary>
/// Authorization requirement para verificar que el usuario sea SystemAdmin
/// </summary>
public class SystemAdminRequirement : IAuthorizationRequirement
{
    // No requiere parámetros adicionales - solo verificar el claim is_system_admin
}
