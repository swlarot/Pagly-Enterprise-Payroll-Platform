using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Application.Interfaces;

public interface IAsistenciaCalculationService
{
    decimal CalcularSalarioHora(decimal salarioMensual, int horasSemanales = 48);
    decimal CalcularSalarioDiario(decimal salarioMensual);

    Task<List<HoraExtra>> GetHorasExtraAprobadas(int empleadoId, DateTime periodoInicio, DateTime periodoFin);
    Task<(decimal montoTotal, decimal horasDiurnas, decimal horasNocturnas, decimal horasDomingoFeriado)>
        CalcularMontoHorasExtra(int empleadoId, decimal salarioHora, DateTime periodoInicio, DateTime periodoFin);

    Task<List<Ausencia>> GetAusenciasDelPeriodo(int empleadoId, DateTime periodoInicio, DateTime periodoFin);
    Task<(decimal descuento, decimal diasAusencia)>
        CalcularDescuentoAusencias(int empleadoId, decimal salarioDiario, DateTime periodoInicio, DateTime periodoFin);

    Task<List<SolicitudVacaciones>> GetVacacionesDelPeriodo(int empleadoId, DateTime periodoInicio, DateTime periodoFin);
    Task<(decimal montoVacaciones, decimal diasVacaciones)>
        CalcularVacaciones(int empleadoId, decimal salarioDiario, DateTime periodoInicio, DateTime periodoFin);

    Task MarcarHorasExtraPagadas(List<HoraExtra> horasExtra, int planillaDetailId);
    Task MarcarAusenciasProcesadas(List<Ausencia> ausencias, int planillaDetailId);
    Task MarcarVacacionesPagadas(List<SolicitudVacaciones> vacaciones, int planillaDetailId);
}
