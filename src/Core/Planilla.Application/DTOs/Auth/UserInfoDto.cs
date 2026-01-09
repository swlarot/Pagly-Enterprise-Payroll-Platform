using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO con información del usuario autenticado
/// </summary>
public class UserInfoDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TenantRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
