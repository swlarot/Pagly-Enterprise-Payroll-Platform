using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Results;

/// <summary>
/// Resultado del motor de prelacion con desglose completo de deducciones aplicadas.
/// </summary>
public class DeduccionesResult
{
    /// <summary>Total de pensiones alimenticias aplicadas</summary>
    public decimal TotalPensionAlimenticia { get; set; }

    /// <summary>Total de embargos judiciales aplicados</summary>
    public decimal TotalEmbargos { get; set; }

    /// <summary>Total de deducciones voluntarias aplicadas (incluye prestamos y anticipos)</summary>
    public decimal TotalVoluntarias { get; set; }

    /// <summary>Total de prestamos aplicados (subset de voluntarias)</summary>
    public decimal TotalPrestamos { get; set; }

    /// <summary>Total de anticipos aplicados (subset de voluntarias)</summary>
    public decimal TotalAnticipos { get; set; }

    /// <summary>Total general de todas las deducciones adicionales</summary>
    public decimal TotalDeduccionesAdicionales =>
        TotalPensionAlimenticia + TotalEmbargos + TotalVoluntarias;

    /// <summary>Total de montos limitados por proteccion de salario minimo</summary>
    public decimal MontoLimitadoPorSalarioMinimo { get; set; }

    /// <summary>Indica si hubo alguna limitacion por salario minimo</summary>
    public bool TuvoLimitacion { get; set; }

    /// <summary>Salario minimo del periodo aplicado</summary>
    public decimal SalarioMinimoAplicado { get; set; }

    /// <summary>Desglose itemizado de cada deduccion con auditoria</summary>
    public List<DeduccionAplicadaItem> Detalle { get; set; } = new();

    /// <summary>IDs de prestamos procesados (para actualizar cuotas)</summary>
    public List<int> PrestamoIds { get; set; } = new();

    /// <summary>IDs de anticipos procesados (para marcar como descontados)</summary>
    public List<int> AnticipoIds { get; set; } = new();
}

/// <summary>
/// Item individual de deduccion aplicada con auditoria completa.
/// </summary>
public class DeduccionAplicadaItem
{
    public int? OrigenDeduccionFijaId { get; set; }
    public int? OrigenPrestamoId { get; set; }
    public int? OrigenAnticipoId { get; set; }
    public TipoDeduccion TipoDeduccion { get; set; }
    public CategoriaDeduccion Categoria { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal MontoSolicitado { get; set; }
    public decimal MontoAplicado { get; set; }
    public decimal MontoLimitado { get; set; }
    public string? RazonLimitacion { get; set; }
    public decimal SaldoDisponibleAntes { get; set; }
    public decimal SaldoDisponibleDespues { get; set; }
    public int OrdenAplicacion { get; set; }
    public string? NombreAcreedor { get; set; }
}
