namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// Respuesta al crear una invitación, contiene el token para compartir
/// </summary>
public class InvitationResponseDto
{
    public int InvitationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string InviteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Días restantes antes de que expire la invitación
    /// </summary>
    public int DaysUntilExpiration => Math.Max(0, (ExpiresAt - DateTime.UtcNow).Days);
}
