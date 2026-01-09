using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO para actualizar información del tenant
/// </summary>
public class UpdateTenantDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El subdominio no puede exceder 100 caracteres")]
    public string? Subdomain { get; set; }

    [StringLength(20, ErrorMessage = "El RUC no puede exceder 20 caracteres")]
    public string? RUC { get; set; }

    [StringLength(10, ErrorMessage = "El DV no puede exceder 10 caracteres")]
    public string? DV { get; set; }

    [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
    [Phone(ErrorMessage = "Formato de teléfono inválido")]
    public string? Phone { get; set; }

    [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
    [EmailAddress(ErrorMessage = "Formato de email inválido")]
    public string? Email { get; set; }
}
