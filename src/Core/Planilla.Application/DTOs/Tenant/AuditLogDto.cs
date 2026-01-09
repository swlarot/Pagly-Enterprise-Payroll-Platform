namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO con información de una entrada del audit log
/// </summary>
public class AuditLogDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? MetadataJson { get; set; }
}
