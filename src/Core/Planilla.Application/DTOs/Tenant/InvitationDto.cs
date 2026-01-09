using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO con información de una invitación
/// </summary>
public class InvitationDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public TenantRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public DateTime? AcceptedAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsValid { get; set; }
    public bool IsExpired { get; set; }
}
