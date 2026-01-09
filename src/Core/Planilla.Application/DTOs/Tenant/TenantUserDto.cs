using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO con información de un usuario del tenant
/// </summary>
public class TenantUserDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public TenantRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsPendingInvitation { get; set; }
}
