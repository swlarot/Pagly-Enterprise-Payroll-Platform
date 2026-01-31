namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// Resultado paginado de audit logs
/// </summary>
public class AuditLogPagedResultDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AuditLogDto> Data { get; set; } = new();
}
