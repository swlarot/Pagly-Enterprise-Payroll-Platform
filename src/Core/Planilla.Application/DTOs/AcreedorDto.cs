using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para visualizacion de acreedores con counts de deducciones/prestamos.
/// </summary>
public record AcreedorDto(
    int Id,
    string Nombre,
    TipoAcreedor TipoAcreedor,
    string TipoAcreedorNombre,
    string? Identificacion,
    string? TipoIdentificacion,
    string? Banco,
    string? TipoCuenta,
    string? NumeroCuenta,
    string? IBAN,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? ContactoNombre,
    string? Observaciones,
    bool EstaActivo,
    int DeduccionesActivas,
    int PrestamosActivos,
    DateTime CreatedAt
);
