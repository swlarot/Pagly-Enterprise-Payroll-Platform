namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO de respuesta después de autenticación exitosa
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfoDto User { get; set; } = null!;
    public TenantInfoDto Tenant { get; set; } = null!;
    public SubscriptionInfoDto Subscription { get; set; } = null!;
}
