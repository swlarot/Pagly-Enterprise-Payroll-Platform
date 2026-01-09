namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// Filtros para consultar el audit log
/// </summary>
public class AuditLogFilterDto
{
    /// <summary>
    /// Fecha desde (opcional)
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Fecha hasta (opcional)
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Filtrar por tipo de acción (opcional)
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filtrar por tipo de entidad (opcional)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Filtrar por email del usuario (opcional)
    /// </summary>
    public string? ActorEmail { get; set; }

    /// <summary>
    /// Número de página (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Tamaño de página (default: 50, max: 100)
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Valida y ajusta el PageSize si excede el límite
    /// </summary>
    public void ValidatePageSize()
    {
        if (PageSize > 100) PageSize = 100;
        if (PageSize < 1) PageSize = 50;
    }

    /// <summary>
    /// Valida y ajusta la página si es inválida
    /// </summary>
    public void ValidatePage()
    {
        if (Page < 1) Page = 1;
    }
}
