namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para asignar un rol a un usuario del tenant.
/// El UserId se toma del path de la petición (PUT /api/tenant/users/{userId}/role) y se asigna en el controlador.
/// </summary>
public class AssignRoleToUserDto
{
    /// <summary>
    /// ID del usuario a quien se asignará el rol (asignado por el controlador desde el path).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// ID del rol personalizado a asignar (null para usar rol del sistema)
    /// </summary>
    public int? CustomRoleId { get; set; }

    /// <summary>
    /// Rol del sistema a asignar si CustomRoleId es null
    /// </summary>
    public int? SystemRole { get; set; }
}
