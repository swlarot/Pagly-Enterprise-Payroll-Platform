using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para crear un nuevo tenant desde el Admin Panel
/// </summary>
public class CreateTenantDto
{
    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    [Required(ErrorMessage = "El nombre de la empresa es requerido")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email del propietario/owner del tenant
    /// </summary>
    [Required(ErrorMessage = "El email del propietario es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string OwnerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña para el usuario owner
    /// </summary>
    [Required(ErrorMessage = "La contraseña es requerida")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
    public string OwnerPassword { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del propietario
    /// </summary>
    [StringLength(200)]
    public string? OwnerFullName { get; set; }

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
    public string? CompanyEmail { get; set; }
}
