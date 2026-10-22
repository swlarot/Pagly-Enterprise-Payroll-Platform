using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Reloj de asistencia que envía sus marcaciones solo, por internet (opción
/// «servidor en la nube» / ADMS de ZKTeco). Se identifica por su número de
/// serie: solo se aceptan marcas de relojes registrados y activos. No se
/// borra: se desactiva.
/// </summary>
public class DispositivoReloj : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Número de serie del reloj (menú «Información del sistema» o etiqueta trasera).</summary>
    public string NumeroSerie { get; set; } = string.Empty;

    /// <summary>Cómo lo llama la empresa («Entrada principal»).</summary>
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime? UltimaConexion { get; set; }
    public DateTime? UltimaMarcacion { get; set; }
    public int MarcasRecibidas { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
