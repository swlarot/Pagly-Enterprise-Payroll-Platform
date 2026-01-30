using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para actualizar un tenant desde el Admin Panel
/// </summary>
public class UpdateAdminTenantDto
{
    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? Name { get; set; }

    /// <summary>
    /// Registro Único de Contribuyente (RUC) de Panamá
    /// </summary>
    [StringLength(20)]
    public string? RUC { get; set; }

    /// <summary>
    /// Dígito Verificador del RUC
    /// </summary>
    [StringLength(10)]
    public string? DV { get; set; }

    /// <summary>
    /// Dirección física de la empresa
    /// </summary>
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// Teléfono de contacto
    /// </summary>
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// Email de contacto de la empresa
    /// </summary>
    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// Activar o desactivar el tenant
    /// </summary>
    public bool? IsActive { get; set; }
}
