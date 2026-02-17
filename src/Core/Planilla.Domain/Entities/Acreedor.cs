using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Catalogo maestro de acreedores por tenant.
/// Centraliza datos de beneficiarios de deducciones y prestamos.
/// </summary>
public class Acreedor : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    // Informacion general
    public string Nombre { get; set; } = string.Empty;
    public TipoAcreedor TipoAcreedor { get; set; }
    public string? Identificacion { get; set; }
    public string? TipoIdentificacion { get; set; }

    // Datos bancarios
    public string? Banco { get; set; }
    public string? TipoCuenta { get; set; }
    public string? NumeroCuenta { get; set; }
    public string? IBAN { get; set; }

    // Contacto
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? ContactoNombre { get; set; }

    // Estado
    public bool EstaActivo { get; set; } = true;
    public string? Observaciones { get; set; }

    // Auditoria
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navegacion
    public ICollection<DeduccionFija> Deducciones { get; set; } = new List<DeduccionFija>();
    public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
    public virtual Tenant? Tenant { get; set; }
}
