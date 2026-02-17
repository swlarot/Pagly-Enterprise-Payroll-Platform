using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para creación y actualización de deducciones fijas.
/// </summary>
public record CreateDeduccionRequest(
    int EmpleadoId,
    TipoDeduccion TipoDeduccion,
    string Descripcion,
    decimal Monto,
    decimal? Porcentaje,
    bool EsPorcentaje,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string? Referencia,
    int Prioridad,
    string? Observaciones,
    // Acreedor
    string? NombreAcreedor = null,
    string? IdentificacionAcreedor = null,
    string? CuentaBancariaAcreedor = null,
    string? BancoAcreedor = null,
    // Orden Judicial
    string? NumeroExpediente = null,
    string? Juzgado = null,
    DateTime? FechaOrdenJudicial = null,
    string? NombreJuez = null,
    EstadoOrdenJudicial? EstadoOrdenJudicial = null,
    // Control de calculo
    BaseCalculoDeduccion BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
    CategoriaDeduccion? Categoria = null,
    decimal? MontoTotalACobrar = null,
    // Autorizacion del trabajador
    bool TieneAutorizacionEscrita = false,
    DateTime? FechaAutorizacion = null,
    string? DocumentoAutorizacionRef = null,
    // Aplicacion especial
    bool AplicaADecimoTercerMes = false,
    bool AplicaAPrestaciones = false,
    // Referencia al catalogo de acreedores
    int? AcreedorId = null
);
