namespace Vorluno.Planilla.Application.DTOs;

/// <summary>Un mes de la tabla «Salarios acumulados» de la hoja del contador.</summary>
public sealed record MesLiquidacion(int Anio, int Mes, decimal Monto, string Origen);

/// <summary>
/// Lo que la hoja del contador usa para liquidar: el devengado real del
/// empleado, no su salario base. Lo arma quien tiene acceso a datos
/// (IDevengadoMensualService, vacaciones y partidas de décimo) y se pasa al
/// cálculo, que sigue siendo puro.
///
/// Si no viene (empleado sin historial), el cálculo cae al método anterior
/// basado en el salario base y lo dice.
/// </summary>
public sealed record BasesDevengadasLiquidacion
{
    /// <summary>Los últimos 60 meses (Art. 224: 260 semanas), del más viejo al más nuevo.</summary>
    public required IReadOnlyList<MesLiquidacion> Meses60 { get; init; }

    /// <summary>Suma del devengado de esos 60 meses.</summary>
    public decimal Devengado60Meses => Meses60.Sum(m => m.Monto);

    /// <summary>Cuántos de esos meses traen datos (para avisar si falta historial).</summary>
    public int MesesConDatos => Meses60.Count(m => m.Monto > 0m);

    /// <summary>Devengado de los últimos 6 meses (Art. 149, promedio favorable).</summary>
    public required decimal Devengado6Meses { get; init; }

    /// <summary>Meses con datos dentro de esos 6 (para promediar solo lo que hay).</summary>
    public required int Meses6ConDatos { get; init; }

    /// <summary>Devengado del último mes trabajado (el otro término del «más favorable»).</summary>
    public required decimal UltimoMesDevengado { get; init; }

    /// <summary>
    /// Devengado desde la última vacación tomada (o desde la contratación):
    /// es la base de las vacaciones proporcionales (Art. 54: ÷ 11).
    /// </summary>
    public required decimal DevengadoDesdeUltimaVacacion { get; init; }

    /// <summary>Desde cuándo se acumula ese devengado, para enseñarlo en pantalla.</summary>
    public DateTime? VacacionesDesde { get; init; }

    /// <summary>
    /// Devengado desde la última partida de décimo pagada: base del décimo
    /// proporcional (÷ 12, junto con las vacaciones proporcionales).
    /// </summary>
    public required decimal DevengadoDesdeUltimaPartidaDecimo { get; init; }

    /// <summary>Desde cuándo se acumula ese devengado.</summary>
    public DateTime? DecimoDesde { get; init; }
}
