namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO de respuesta después de autenticación exitosa
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// Access token JWT (válido por 24 horas)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token para renovar el access token (válido por 7 días)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Fecha de expiración del access token
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Información del usuario autenticado
    /// </summary>
    public UserInfoDto User { get; set; } = null!;

    /// <summary>
    /// Información del tenant (null para SystemAdmins)
    /// </summary>
    public TenantInfoDto? Tenant { get; set; }

    /// <summary>
    /// Información de la suscripción (null para SystemAdmins)
    /// </summary>
    public SubscriptionInfoDto? Subscription { get; set; }
}
