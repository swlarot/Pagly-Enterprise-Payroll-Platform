namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Roles de usuario dentro de un tenant
/// </summary>
public enum TenantRole
{
    /// <summary>
    /// Propietario del tenant - acceso total, puede eliminar tenant
    /// </summary>
    Owner = 0,

    /// <summary>
    /// Administrador - gestión completa excepto billing y eliminación de tenant
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Gerente - puede gestionar planillas, empleados y reportes
    /// </summary>
    Manager = 2,

    /// <summary>
    /// Contador - acceso de solo lectura a reportes y consultas
    /// </summary>
    Accountant = 3,

    /// <summary>
    /// Empleado - solo puede ver su propia información
    /// </summary>
    Employee = 4
}
