namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para representar un rol personalizado del tenant
/// </summary>
public class CustomTenantRoleDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public string? Color { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Lista de permisos asignados a este rol (claves de permisos)
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Cantidad de usuarios con este rol asignado
    /// </summary>
    public int UserCount { get; set; }
}
