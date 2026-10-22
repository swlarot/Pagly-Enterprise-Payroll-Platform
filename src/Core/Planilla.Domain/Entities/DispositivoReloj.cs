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

    /// <summary>
    /// Hasta cuándo se espera la primera conexión después de registrarlo.
    /// Fuera de esa ventana, un reloj sin vincular no puede vincularse.
    /// </summary>
    public DateTime? EsperandoVinculoHasta { get; set; }

    /// <summary>
    /// IP desde la que alguien de la empresa registró el reloj (o reabrió la
    /// espera). Si el reloj se conecta desde esa misma, se vincula solo: está
    /// en la misma red que quien lo registró.
    /// </summary>
    public string? IpEsperada { get; set; }

    /// <summary>IP desde la que se conectó por primera vez, a la espera de que alguien la confirme.</summary>
    public string? IpPendiente { get; set; }

    /// <summary>IP confirmada: solo se aceptan envíos desde aquí.</summary>
    public string? IpVinculada { get; set; }
    public DateTime? VinculadoEn { get; set; }

    /// <summary>Última IP rechazada (por ejemplo, si el colegio cambió de internet).</summary>
    public string? IpRechazada { get; set; }
    public DateTime? IpRechazadaEn { get; set; }

    public DateTime? UltimaConexion { get; set; }
    public DateTime? UltimaMarcacion { get; set; }
    public int MarcasRecibidas { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
