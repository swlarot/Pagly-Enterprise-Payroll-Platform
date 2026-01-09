using Vorluno.Planilla.Application.Common;
using Vorluno.Planilla.Application.DTOs.Tenant;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para registrar eventos de auditoría en el sistema
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Registra una acción en el audit log del tenant actual
    /// </summary>
    /// <param name="action">Acción realizada (ej: "EmployeeCreated", "InviteCreated")</param>
    /// <param name="entityType">Tipo de entidad afectada (ej: "Employee", "TenantInvitation")</param>
    /// <param name="entityId">ID de la entidad afectada</param>
    /// <param name="metadata">Metadatos adicionales en formato clave-valor (sin PII sensible)</param>
    Task LogAsync(string action, string entityType, string? entityId = null, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Obtiene el audit log del tenant actual con filtros y paginación
    /// </summary>
    Task<Result<PagedResultDto<AuditLogDto>>> GetAuditLogAsync(AuditLogFilterDto filter);

    /// <summary>
    /// Obtiene el audit log de una entidad específica
    /// </summary>
    Task<Result<List<AuditLogDto>>> GetEntityAuditLogAsync(string entityType, string entityId);
}
