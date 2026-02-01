using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para actualizar un rol personalizado existente
/// </summary>
public class UpdateCustomTenantRoleDto
{
    /// <summary>
    /// Nombre del rol
    /// </summary>
    [Required(ErrorMessage = "El nombre del rol es requerido")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del rol
    /// </summary>
    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; set; }

    /// <summary>
    /// Color hexadecimal para el rol (#RRGGBB)
    /// </summary>
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "El color debe estar en formato hexadecimal #RRGGBB")]
    public string? Color { get; set; }

    /// <summary>
    /// Orden de visualización
    /// </summary>
    [Range(0, 1000, ErrorMessage = "El orden debe estar entre 0 y 1000")]
    public int DisplayOrder { get; set; }
}
