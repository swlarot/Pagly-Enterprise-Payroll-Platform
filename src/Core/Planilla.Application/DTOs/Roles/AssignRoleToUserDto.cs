using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para asignar un rol a un usuario del tenant
/// </summary>
public class AssignRoleToUserDto
{
    /// <summary>
    /// ID del usuario a quien se asignará el rol
    /// </summary>
    [Required(ErrorMessage = "El ID del usuario es requerido")]
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
