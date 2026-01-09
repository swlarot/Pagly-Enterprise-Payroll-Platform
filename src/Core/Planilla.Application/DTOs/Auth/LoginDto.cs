using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO para el inicio de sesión
/// </summary>
public class LoginDto
{
    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}
