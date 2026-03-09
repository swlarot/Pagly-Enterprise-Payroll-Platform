namespace Vorluno.Planilla.Application.Helpers;

public static class DateTimeHelper
{
    private static readonly TimeZoneInfo PanamaZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Panama");

    /// <summary>Retorna la hora actual en timezone de Panamá (UTC-5).</summary>
    public static DateTime NowPanama() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PanamaZone);
}
