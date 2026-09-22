using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>Un mes del cuatrimestre de un empleado, con su origen.</summary>
public sealed record MesDecimo(
    int Anio,
    int Mes,
    decimal Monto,
    OrigenDevengado Origen,
    /// <summary>Fracción del mes que cae dentro del período (1 = mes completo; 0.5 = media quincena importada prorrateada).</summary>
    decimal Fraccion,
    /// <summary>Números de las planillas de Pagly que aportan a este mes (vacío si no es Planilla).</summary>
    IReadOnlyList<string> Planillas);

/// <summary>Lo que se le pagaría a un empleado en esta partida, antes de guardar nada.</summary>
public sealed record EmpleadoDecimoPreview(
    int EmpleadoId,
    string NombreCompleto,
    string NumeroIdentificacion,
    IReadOnlyList<MesDecimo> Meses,
    decimal TotalDevengado,
    decimal MontoDecimo,
    decimal CssEmpleado,
    decimal CssPatrono,
    decimal SeEmpleado,
    decimal SePatrono,
    decimal Isr,
    decimal TotalDeducciones,
    decimal NetoPago)
{
    public bool TieneMesesSinDatos => Meses.Any(m => m.Origen == OrigenDevengado.SinDatos);
}

/// <summary>Un mes escrito a mano en pantalla para un empleado (solo meses sin planilla de Pagly).</summary>
public sealed record AjusteMesDecimo(int EmpleadoId, int Anio, int Mes, decimal Monto);

public sealed record DecimoPreview(IReadOnlyList<EmpleadoDecimoPreview> Empleados)
{
    public decimal TotalDevengado => Empleados.Sum(e => e.TotalDevengado);
    public decimal TotalDecimo => Empleados.Sum(e => e.MontoDecimo);
    public decimal TotalNeto => Empleados.Sum(e => e.NetoPago);
    public int EmpleadosConMesesSinDatos => Empleados.Count(e => e.TieneMesesSinDatos);
}

public interface IDecimoCalculationService
{
    /// <summary>
    /// Calcula, sin guardar, el décimo de cada empleado activo para el período,
    /// aplicando los meses escritos a mano. Es lo que la pantalla enseña antes
    /// de crear la partida.
    /// </summary>
    Task<DecimoPreview> PrevisualizarAsync(
        DateTime periodoDesde, DateTime periodoHasta, DateTime fechaPago, int tenantId,
        IReadOnlyList<AjusteMesDecimo>? ajustes = null, CancellationToken ct = default);

    /// <summary>
    /// Calcula y persiste los detalles de una partida existente (Borrador o
    /// Calculada). Los meses escritos a mano se guardan además como devengado
    /// manual del empleado, para que la liquidación y la ficha los vean.
    /// </summary>
    Task<DecimoCalculationSummary> CalcularAsync(
        int planillaDecimoId, int tenantId,
        IReadOnlyList<AjusteMesDecimo>? ajustes = null, CancellationToken ct = default);
}

public record DecimoCalculationSummary(int EmpleadosProcesados, decimal TotalDecimo);
