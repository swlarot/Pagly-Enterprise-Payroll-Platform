using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para visualizar los datos de una posición.
/// </summary>
public record PosicionVerDto(
    int Id,
    string Nombre,
    string Codigo,
    string? Descripcion,
    bool EstaActivo,
    int? DepartamentoId,
    string DepartamentoNombre,
    decimal SalarioMinimo,
    decimal SalarioMaximo,
    NivelRiesgo NivelRiesgo,
    int CantidadEmpleados
);

/// <summary>
/// DTO utilizado para crear una nueva posición.
/// </summary>
public record PosicionCrearDto(
    [Required]
    [StringLength(100)]
    string Nombre,

    [Required]
    [StringLength(20)]
    string Codigo,

    [StringLength(500)]
    string? Descripcion,

    [Required]
    int DepartamentoId,

    [Range(0, double.MaxValue)]
    decimal SalarioMinimo,

    [Range(0, double.MaxValue)]
    decimal SalarioMaximo,

    NivelRiesgo NivelRiesgo = NivelRiesgo.Bajo
);

/// <summary>
/// DTO utilizado para actualizar una posición existente.
/// </summary>
public record PosicionActualizarDto(
    [Required]
    [StringLength(100)]
    string Nombre,

    [Required]
    [StringLength(20)]
    string Codigo,

    [StringLength(500)]
    string? Descripcion,

    [Required]
    int DepartamentoId,

    [Range(0, double.MaxValue)]
    decimal SalarioMinimo,

    [Range(0, double.MaxValue)]
    decimal SalarioMaximo,

    NivelRiesgo NivelRiesgo,

    bool EstaActivo
);
