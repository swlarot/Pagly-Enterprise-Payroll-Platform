namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Roles de usuario dentro de un tenant (simplificados)
/// Los permisos específicos se definen mediante CustomTenantRole
/// </summary>
public enum TenantRole
{
    /// <summary>
    /// Propietario del tenant - acceso total, puede eliminar tenant y gestionar suscripción
    /// </summary>
    Owner = 0,

    /// <summary>
    /// Usuario regular - permisos definidos por CustomTenantRole asignado
    /// </summary>
    User = 1
}
