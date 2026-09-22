using Vorluno.Planilla.Application.DTOs;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Arma el devengado real que necesita una liquidación: los 60 meses del
/// Art. 224, los 6 del Art. 149, y lo devengado desde la última vacación y
/// desde la última partida de décimo. Devuelve null si el empleado no tiene
/// ni un mes con datos: ahí la liquidación cae al salario base y lo avisa.
/// </summary>
public interface IBasesLiquidacionProvider
{
    Task<BasesDevengadasLiquidacion?> ObtenerAsync(
        int empleadoId, DateTime fechaContratacion, DateTime fechaTerminacion, CancellationToken ct = default);
}
