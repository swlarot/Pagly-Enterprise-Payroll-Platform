namespace Vorluno.Planilla.Application.Interfaces;

public interface IDecimoCalculationService
{
    /// <summary>
    /// Calcula el décimo tercer mes para todos los empleados activos del tenant,
    /// persiste los DetalleDecimo y actualiza los totales de la PlanillaDecimo.
    /// </summary>
    Task<DecimoCalculationSummary> CalcularAsync(int planillaDecimoId, int tenantId);
}

public record DecimoCalculationSummary(int EmpleadosProcesados, decimal TotalDecimo);
