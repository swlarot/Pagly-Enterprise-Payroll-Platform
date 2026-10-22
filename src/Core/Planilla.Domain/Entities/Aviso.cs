using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Aviso para una persona dentro de Pagly (la campana). Se marca como leído;
/// no se borra.
/// </summary>
public class Aviso : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Usuario que lo recibe.</summary>
    public string UsuarioId { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Ruta de la app a la que lleva el aviso.</summary>
    public string? Enlace { get; set; }

    public int? SolicitudRevisionId { get; set; }

    public bool Leido { get; set; }
    public DateTime? LeidoEn { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
