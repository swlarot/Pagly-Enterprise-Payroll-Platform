using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO para aceptar una invitación (puede requerir crear cuenta o solo asociar)
/// </summary>
public class AcceptInvitationDto
{
    [Required(ErrorMessage = "El token de invitación es obligatorio")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del usuario (requerido si la cuenta no existe)
    /// </summary>
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? FullName { get; set; }

    /// <summary>
    /// Contraseña (requerida si la cuenta no existe)
    /// </summary>
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres")]
    public string? Password { get; set; }

    /// <summary>
    /// Confirmación de contraseña
    /// </summary>
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
    public string? ConfirmPassword { get; set; }
}
