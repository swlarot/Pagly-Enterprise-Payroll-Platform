namespace Vorluno.Planilla.Domain.Interfaces;

/// <summary>
/// Marca una entidad como perteneciente a un tenant específico.
/// Las entidades que implementan esta interfaz serán automáticamente filtradas
/// por TenantId mediante Global Query Filters en EF Core.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// ID del tenant al que pertenece esta entidad
    /// </summary>
    int TenantId { get; set; }
}
