using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO unificado que representa una deduccion pendiente de aplicar.
/// Acepta las 3 fuentes: DeduccionFija, Prestamo y Anticipo.
/// </summary>
public record DeduccionPendiente
{
    /// <summary>FK a DeduccionFija si viene de esa fuente</summary>
    public int? OrigenDeduccionFijaId { get; init; }

    /// <summary>FK a Prestamo si viene de esa fuente</summary>
    public int? OrigenPrestamoId { get; init; }

    /// <summary>FK a Anticipo si viene de esa fuente</summary>
    public int? OrigenAnticipoId { get; init; }

    /// <summary>Tipo de deduccion</summary>
    public TipoDeduccion TipoDeduccion { get; init; }

    /// <summary>Categoria de prelacion</summary>
    public CategoriaDeduccion Categoria { get; init; }

    /// <summary>Descripcion legible</summary>
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Monto fijo si no es porcentaje</summary>
    public decimal MontoFijo { get; init; }

    /// <summary>Porcentaje si es porcentual</summary>
    public decimal? Porcentaje { get; init; }

    /// <summary>Indica si es calculo por porcentaje</summary>
    public bool EsPorcentaje { get; init; }

    /// <summary>Base de calculo (bruto o neto)</summary>
    public BaseCalculoDeduccion BaseCalculo { get; init; }

    /// <summary>Prioridad dentro de su categoria (menor = mayor prioridad)</summary>
    public int Prioridad { get; init; }

    /// <summary>Nombre del acreedor para auditoria</summary>
    public string? NombreAcreedor { get; init; }

    /// <summary>Monto total a cobrar (para embargos con limite)</summary>
    public decimal? MontoTotalACobrar { get; init; }

    /// <summary>Monto ya cobrado acumulado</summary>
    public decimal MontoCobradoAcumulado { get; init; }
}
