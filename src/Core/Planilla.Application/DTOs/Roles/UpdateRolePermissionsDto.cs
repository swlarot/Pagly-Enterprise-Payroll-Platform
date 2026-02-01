using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para actualizar los permisos de un rol
/// </summary>
public class UpdateRolePermissionsDto
{
    /// <summary>
    /// Lista de permisos a asignar al rol (reemplaza los existentes)
    /// </summary>
    [Required(ErrorMessage = "La lista de permisos es requerida")]
    public List<string> Permissions { get; set; } = new();
}
