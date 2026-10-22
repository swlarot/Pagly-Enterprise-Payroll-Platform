using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Una marcación del reloj de asistencia: quién (su número en el reloj) y a
/// qué hora. Las huellas y los rostros se quedan en el reloj; aquí solo llega
/// esto. Se guarda una sola vez aunque el archivo se importe de nuevo.
/// </summary>
public class Marcacion : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Número de la persona en el reloj (no es la cédula).</summary>
    public string CodigoReloj { get; set; } = string.Empty;

    /// <summary>A quién corresponde, cuando su número ya está asociado a su ficha.</summary>
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    /// <summary>
    /// Fecha y hora tal como las registró el reloj (hora de Panamá). Se guarda
    /// marcada como UTC solo por requisito de la base de datos: no se convierte.
    /// </summary>
    public DateTime FechaHora { get; set; }

    /// <summary>De dónde llegó: "Archivo" (importado) o "Reloj" (enviada por el reloj).</summary>
    public string Origen { get; set; } = "Archivo";

    /// <summary>Nombre del archivo o número de serie del reloj.</summary>
    public string? Fuente { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
