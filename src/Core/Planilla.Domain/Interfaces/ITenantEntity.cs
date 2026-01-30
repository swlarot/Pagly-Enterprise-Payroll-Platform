namespace Vorluno.Planilla.Domain.Interfaces;

/// <summary>
/// Interfaz que marca entidades que pertenecen a un tenant específico.
/// Todas las entidades con TenantId deben implementar esta interfaz para
/// garantizar el filtrado automático por tenant en todas las queries.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// ID del tenant al que pertenece esta entidad.
    /// Se usa para filtrado automático en ApplicationDbContext mediante Global Query Filters.
    /// </summary>
    int TenantId { get; set; }
}
