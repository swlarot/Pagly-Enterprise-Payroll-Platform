using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO para visualización de deducciones fijas.
/// </summary>
public record DeduccionFijaDto(
    int Id,
    int EmpleadoId,
    string EmpleadoNombre,
    TipoDeduccion TipoDeduccion,
    string TipoDeduccionNombre,
    string Descripcion,
    decimal Monto,
    decimal? Porcentaje,
    bool EsPorcentaje,
    DateTime FechaInicio,
    DateTime? FechaFin,
    bool EstaActivo,
    string? Referencia,
    int Prioridad,
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
    DateTime? FechaLevantamiento = null,
    string? MotivoLevantamiento = null,
    // Control de calculo
    BaseCalculoDeduccion BaseCalculo = BaseCalculoDeduccion.SalarioBruto,
    CategoriaDeduccion Categoria = CategoriaDeduccion.Voluntaria,
    decimal? MontoTotalACobrar = null,
    decimal MontoCobradoAcumulado = 0,
    // Autorizacion
    bool TieneAutorizacionEscrita = false,
    DateTime? FechaAutorizacion = null,
    string? DocumentoAutorizacionRef = null,
    // Aplicacion especial
    bool AplicaADecimoTercerMes = false,
    bool AplicaAPrestaciones = false,
    // Referencia al catalogo de acreedores
    int? AcreedorId = null,
    string? AcreedorNombre = null
);
