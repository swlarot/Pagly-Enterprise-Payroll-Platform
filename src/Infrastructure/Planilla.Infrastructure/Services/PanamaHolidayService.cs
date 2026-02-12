// ====================================================================
// Planilla - PanamaHolidayService
// Creado: 2026-02-10
// Descripción: Servicio para determinar días festivos nacionales de Panamá
// según Código de Trabajo Art. 49
// ====================================================================

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para determinar si una fecha es día festivo nacional en Panamá
/// y obtener información sobre festivos fijos y móviles.
/// </summary>
public class PanamaHolidayService
{
    private static readonly Dictionary<int, List<(int Month, int Day, string Name)>> FixedHolidays = new()
    {
        { 1, new List<(int, int, string)> { (1, 1, "Año Nuevo"), (1, 9, "Día de los Mártires") } },
        { 5, new List<(int, int, string)> { (5, 1, "Día del Trabajador") } },
        { 11, new List<(int, int, string)> { (11, 3, "Separación de Panamá de Colombia"), (11, 4, "Día de la Bandera"), (11, 10, "Primer Grito de Independencia"), (11, 28, "Independencia de Panamá") } },
        { 12, new List<(int, int, string)> { (12, 8, "Día de la Madre"), (12, 25, "Navidad"), (12, 31, "Fin de Año") } }
    };

    /// <summary>
    /// Determina si una fecha es día festivo nacional en Panamá
    /// </summary>
    public bool IsNationalHoliday(DateTime fecha)
    {
        // Verificar festivos fijos
        if (FixedHolidays.TryGetValue(fecha.Month, out var holidays))
        {
            if (holidays.Any(h => h.Day == fecha.Day))
                return true;
        }

        // Verificar festivos móviles (Carnaval y Semana Santa)
        if (IsCarnival(fecha) || IsHolyWeek(fecha))
            return true;

        return false;
    }

    /// <summary>
    /// Obtiene el nombre del día festivo si la fecha es festivo nacional
    /// </summary>
    public string? GetHolidayName(DateTime fecha)
    {
        // Verificar festivos fijos
        if (FixedHolidays.TryGetValue(fecha.Month, out var holidays))
        {
            var holiday = holidays.FirstOrDefault(h => h.Day == fecha.Day);
            if (holiday.Name != null)
                return holiday.Name;
        }

        // Verificar festivos móviles
        if (IsCarnival(fecha))
        {
            var dayOfWeek = fecha.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Monday)
                return "Carnaval (Lunes)";
            if (dayOfWeek == DayOfWeek.Tuesday)
                return "Carnaval (Martes)";
        }

        if (IsHolyWeek(fecha))
        {
            var dayOfWeek = fecha.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Thursday)
                return "Jueves Santo";
            if (dayOfWeek == DayOfWeek.Friday)
                return "Viernes Santo";
        }

        return null;
    }

    /// <summary>
    /// Determina si una fecha es día de Carnaval (Lunes y Martes antes del Miércoles de Ceniza)
    /// </summary>
    private bool IsCarnival(DateTime fecha)
    {
        var easter = CalculateEaster(fecha.Year);
        var ashWednesday = easter.AddDays(-46); // 46 días antes de Pascua
        var carnivalMonday = ashWednesday.AddDays(-2);
        var carnivalTuesday = ashWednesday.AddDays(-1);

        return fecha.Date == carnivalMonday.Date || fecha.Date == carnivalTuesday.Date;
    }

    /// <summary>
    /// Determina si una fecha es día de Semana Santa (Jueves Santo o Viernes Santo)
    /// </summary>
    private bool IsHolyWeek(DateTime fecha)
    {
        var easter = CalculateEaster(fecha.Year);
        var holyThursday = easter.AddDays(-3);
        var goodFriday = easter.AddDays(-2);

        return fecha.Date == holyThursday.Date || fecha.Date == goodFriday.Date;
    }

    /// <summary>
    /// Calcula la fecha de Pascua (Domingo de Resurrección) usando el algoritmo de Meeus/Jones/Butcher
    /// </summary>
    private DateTime CalculateEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateTime(year, month, day);
    }

    /// <summary>
    /// Obtiene todos los días festivos nacionales para un año específico
    /// </summary>
    public List<(DateTime Fecha, string Nombre)> GetHolidaysForYear(int year)
    {
        var holidays = new List<(DateTime, string)>();

        // Agregar festivos fijos
        foreach (var monthHolidays in FixedHolidays)
        {
            foreach (var holiday in monthHolidays.Value)
            {
                holidays.Add((new DateTime(year, monthHolidays.Key, holiday.Day), holiday.Name));
            }
        }

        // Agregar Carnaval
        var easter = CalculateEaster(year);
        var ashWednesday = easter.AddDays(-46);
        var carnivalMonday = ashWednesday.AddDays(-2);
        var carnivalTuesday = ashWednesday.AddDays(-1);
        holidays.Add((carnivalMonday, "Carnaval (Lunes)"));
        holidays.Add((carnivalTuesday, "Carnaval (Martes)"));

        // Agregar Semana Santa
        var holyThursday = easter.AddDays(-3);
        var goodFriday = easter.AddDays(-2);
        holidays.Add((holyThursday, "Jueves Santo"));
        holidays.Add((goodFriday, "Viernes Santo"));

        return holidays.OrderBy(h => h.Item1).ToList();
    }
}
