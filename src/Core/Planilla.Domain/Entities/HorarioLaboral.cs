using Vorluno.Planilla.Domain.Interfaces;

namespace Vorluno.Planilla.Domain.Entities;

/// <summary>
/// Horario contra el que se comparan las marcaciones del reloj: a qué hora se
/// entra y se sale, qué días, cuánto se tolera de llegada tarde y a partir de
/// cuántos minutos después de la salida se propone hora extra al director.
/// </summary>
public class HorarioLaboral : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Nombre del horario («Docentes», «Administrativos»).</summary>
    public string Nombre { get; set; } = string.Empty;

    public TimeSpan HoraEntrada { get; set; }
    public TimeSpan HoraSalida { get; set; }

    /// <summary>
    /// Días laborables como banderas: lunes = 1, martes = 2, miércoles = 4,
    /// jueves = 8, viernes = 16, sábado = 32, domingo = 64. Lunes a viernes = 31.
    /// </summary>
    public int DiasLaborables { get; set; } = 31;

    /// <summary>Minutos de gracia antes de contar una llegada tarde.</summary>
    public int ToleranciaMinutos { get; set; } = 10;

    /// <summary>Minutos después de la salida a partir de los cuales se propone hora extra.</summary>
    public int MinutosParaHoraExtra { get; set; } = 30;

    /// <summary>El que se usa para quien no tiene un horario asignado.</summary>
    public bool EsPredeterminado { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Tenant? Tenant { get; set; }

    /// <summary>¿Se trabaja ese día de la semana según este horario?</summary>
    public bool EsLaborable(DayOfWeek dia)
    {
        // DayOfWeek: domingo = 0 … sábado = 6. Bandera: lunes = 1 … domingo = 64.
        var bit = dia == DayOfWeek.Sunday ? 64 : 1 << ((int)dia - 1);
        return (DiasLaborables & bit) != 0;
    }
}
