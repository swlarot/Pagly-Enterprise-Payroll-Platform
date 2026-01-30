using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO para solicitar renovación o revocación de refresh token
/// </summary>
public class RefreshTokenRequestDto
{
    /// <summary>
    /// Refresh token que se desea renovar o revocar
    /// </summary>
    [Required(ErrorMessage = "El refresh token es requerido")]
    public string RefreshToken { get; set; } = string.Empty;
}
