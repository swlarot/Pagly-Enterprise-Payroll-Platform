namespace Vorluno.Planilla.Application.DTOs.Roles;

/// <summary>
/// DTO para representar un permiso del sistema
/// </summary>
public class PermissionDto
{
    /// <summary>
    /// Clave única del permiso (ej: "employees.read")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del permiso
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada de lo que permite este permiso
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Categoría del permiso (employees, payroll, reports, settings, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;
}
