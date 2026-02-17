using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para creacion y actualizacion de acreedores.
/// </summary>
public record CreateAcreedorRequest(
    string Nombre,
    TipoAcreedor TipoAcreedor,
    string? Identificacion = null,
    string? TipoIdentificacion = null,
    string? Banco = null,
    string? TipoCuenta = null,
    string? NumeroCuenta = null,
    string? IBAN = null,
    string? Telefono = null,
    string? Email = null,
    string? Direccion = null,
    string? ContactoNombre = null,
    string? Observaciones = null
);
