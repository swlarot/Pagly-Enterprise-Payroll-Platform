using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Un cambio sensible (de salario o una baja) que espera que otra persona lo
/// confirme. Solo existe si la empresa activó «exigir confirmación de otra
/// persona». Queda como historial: quién pidió, quién confirmó o rechazó,
/// cuándo y por qué. No se borra.
/// </summary>
public class CambioPendiente : ITenantEntity
{
    public const string TipoSalario = "Salario";
    public const string TipoBaja = "Baja";

    public int Id { get; set; }
    public int TenantId { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    /// <summary>"Salario" o "Baja".</summary>
    public string Tipo { get; set; } = TipoSalario;

    public decimal? SalarioAnterior { get; set; }
    public decimal? SalarioNuevo { get; set; }

    public string SolicitadoPor { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>"Pendiente", "Confirmado", "Rechazado" o "Retirado".</summary>
    public string Estado { get; set; } = "Pendiente";

    public string? ResueltoPor { get; set; }
    public DateTime? ResueltoEn { get; set; }

    /// <summary>Por qué se rechazó (obligatorio al rechazar).</summary>
    public string? Comentario { get; set; }

    public virtual Tenant? Tenant { get; set; }
}
