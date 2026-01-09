using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO para el registro de un nuevo usuario y tenant
/// </summary>
public class RegisterDto
{
    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de la empresa es requerido")]
    [StringLength(200, ErrorMessage = "El nombre de la empresa no puede exceder 200 caracteres")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El RUC no puede exceder 20 caracteres")]
    public string? RUC { get; set; }

    [StringLength(5, ErrorMessage = "El DV no puede exceder 5 caracteres")]
    public string? DV { get; set; }
}
