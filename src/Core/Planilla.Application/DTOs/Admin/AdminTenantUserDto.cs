using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO de usuario de tenant para el Admin Panel
/// </summary>
public class AdminTenantUserDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public TenantRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsPendingInvitation { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }
}
