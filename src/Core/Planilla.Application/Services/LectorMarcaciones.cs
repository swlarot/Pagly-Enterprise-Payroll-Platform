// ====================================================================
// Lee las marcaciones que exporta un reloj ZKTeco, sin saber de antemano
// el formato exacto:
//   · el archivo del USB del reloj (1_attlog.dat): "  12  2026-10-05 07:02:11 ...";
//   · un CSV o una hoja de Excel del programa del reloj, con encabezados
//     como "AC-No.", "Nombre", "Fecha/Hora" (o "Fecha" y "Hora" separadas).
// Las columnas se reconocen por su nombre; las fechas, en los formatos
// habituales (día antes que mes, como en Panamá). Lo que no se entiende
// se informa por línea; nada se inventa.
// ====================================================================

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vorluno.Planilla.Application.Services;

public sealed record MarcacionLeida(string Codigo, string? Nombre, DateTime FechaHora, int Linea);

public sealed record ProblemaLectura(int Linea, string Mensaje);

public sealed record ResultadoLectura(
    List<MarcacionLeida> Marcas,
    List<ProblemaLectura> Problemas,
    string Formato,
    string? Columnas);

public static class LectorMarcaciones
{
    private static readonly Regex LineaAttlog = new(
        @"^\s*(?<cod>[^\s,;]+)\s+(?<fecha>\d{4}-\d{1,2}-\d{1,2})\s+(?<hora>\d{1,2}:\d{2}(:\d{2})?)",
        RegexOptions.Compiled);

    private static readonly string[] AliasCodigo =
    {
        "ac-no", "ac no", "acno", "no", "id", "user id", "userid", "pin", "enroll number", "enrollnumber", "enroll no",
        "emp no", "numero", "num", "no de usuario", "id de usuario", "id usuario", "codigo", "person id", "personnel id",
        "employee id", "id empleado", "no empleado", "numero de empleado", "no.", "n",
    };
    private static readonly string[] AliasNombre = { "name", "nombre", "nombres", "full name", "nombre completo", "first name", "empleado" };
    private static readonly string[] AliasFechaHora =
    {
        "time", "datetime", "date time", "date/time", "fecha/hora", "fecha y hora", "fecha hora", "checktime", "check time",
        "punch time", "hora de marcacion", "marcacion", "record time", "attendance time", "fecha-hora",
    };
    private static readonly string[] AliasFecha = { "date", "fecha", "dia" };
    private static readonly string[] AliasHora = { "hora", "time", "hour" };

    private static readonly string[] FormatosFechaHora =
    {
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm:ss", "yyyy-MM-dd H:mm", "yyyy-MM-ddTHH:mm:ss",
        "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/M/d H:mm:ss", "yyyy/M/d H:mm",
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm:ss", "d/M/yyyy H:mm",
        "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "d-M-yyyy H:mm:ss", "d-M-yyyy H:mm",
        "d/M/yyyy h:mm:ss tt", "d/M/yyyy h:mm tt", "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy hh:mm tt",
        "yyyy-MM-dd h:mm:ss tt", "yyyy-MM-dd h:mm tt",
    };
    private static readonly string[] FormatosFecha = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy/M/d", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" };
    private static readonly string[] FormatosHora = { "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm", "h:mm:ss tt", "h:mm tt", "hh:mm:ss tt", "hh:mm tt" };

    /// <summary>Archivo de texto: el .dat del USB o un CSV.</summary>
    public static ResultadoLectura LeerTexto(string contenido)
    {
        var lineas = contenido.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var noVacias = lineas.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (noVacias.Count == 0)
            return new ResultadoLectura(new(), new() { new(0, "El archivo está vacío.") }, "vacío", null);

        // ¿Es el archivo del USB? La mayoría de líneas: número, fecha y hora.
        var comoAttlog = noVacias.Count(l => LineaAttlog.IsMatch(l));
        if (comoAttlog >= Math.Max(1, noVacias.Count / 2))
            return LeerAttlog(lineas);

        var separador = DetectarSeparador(noVacias.Take(10).ToList());
        var filas = lineas
            .Select(l => (IReadOnlyList<object?>)PartirCsv(l, separador).Cast<object?>().ToList())
            .ToList();
        return LeerTabla(filas, separador == '\t' ? "texto separado por tabulaciones" : "CSV");
    }

    /// <summary>Tabla ya separada en celdas (CSV o la primera hoja de un Excel).</summary>
    public static ResultadoLectura LeerTabla(IReadOnlyList<IReadOnlyList<object?>> filas, string formato)
    {
        var problemas = new List<ProblemaLectura>();

        // El encabezado puede no estar en la primera fila (títulos, logotipo, filtros).
        int filaEncabezado = -1, colCodigo = -1, colNombre = -1, colFechaHora = -1, colFecha = -1, colHora = -1;
        for (var i = 0; i < Math.Min(filas.Count, 15) && filaEncabezado < 0; i++)
        {
            var nombres = filas[i].Select(c => Normalizar(Convert.ToString(c, CultureInfo.InvariantCulture))).ToList();
            var cod = Buscar(nombres, AliasCodigo);
            var fh = Buscar(nombres, AliasFechaHora);
            var f = Buscar(nombres, AliasFecha);
            if (cod < 0 || (fh < 0 && f < 0)) continue;

            filaEncabezado = i;
            colCodigo = cod;
            colNombre = Buscar(nombres, AliasNombre);
            colFecha = f;
            // Si hay columna de fecha aparte, "time"/"hora" es solo la hora.
            colHora = f >= 0 ? Buscar(nombres, AliasHora, excepto: f) : -1;
            colFechaHora = f >= 0 && colHora >= 0 ? -1 : fh;
        }

        if (filaEncabezado < 0)
            return new ResultadoLectura(new(), new()
            {
                new(0, "No se encontraron las columnas del número de la persona y de la fecha y hora de la marcación."),
            }, formato, null);

        var encabezado = filas[filaEncabezado];
        string Titulo(int c) => c >= 0 && c < encabezado.Count ? Convert.ToString(encabezado[c], CultureInfo.InvariantCulture)?.Trim() ?? "" : "";
        var columnas = string.Join(" · ", new[]
        {
            $"número: «{Titulo(colCodigo)}»",
            colNombre >= 0 ? $"nombre: «{Titulo(colNombre)}»" : null,
            colFechaHora >= 0 ? $"fecha y hora: «{Titulo(colFechaHora)}»" : $"fecha: «{Titulo(colFecha)}», hora: «{Titulo(colHora)}»",
        }.Where(x => x != null));

        var marcas = new List<MarcacionLeida>();
        for (var i = filaEncabezado + 1; i < filas.Count; i++)
        {
            var fila = filas[i];
            object? Celda(int c) => c >= 0 && c < fila.Count ? fila[c] : null;
            if (fila.All(c => string.IsNullOrWhiteSpace(Convert.ToString(c, CultureInfo.InvariantCulture)))) continue;

            var codigo = NormalizarCodigo(Convert.ToString(Celda(colCodigo), CultureInfo.InvariantCulture));
            if (codigo == null)
            {
                problemas.Add(new(i + 1, "Sin número de persona."));
                continue;
            }

            DateTime? cuando = colFechaHora >= 0
                ? AFechaHora(Celda(colFechaHora))
                : Combinar(AFecha(Celda(colFecha)), AHora(Celda(colHora)));
            if (cuando == null)
            {
                problemas.Add(new(i + 1, $"No se entendió la fecha y hora de la persona {codigo}."));
                continue;
            }

            var nombre = colNombre >= 0 ? Convert.ToString(Celda(colNombre), CultureInfo.InvariantCulture)?.Trim() : null;
            marcas.Add(new MarcacionLeida(codigo, string.IsNullOrWhiteSpace(nombre) ? null : nombre, cuando.Value, i + 1));
        }

        return new ResultadoLectura(SinRepetir(marcas), problemas, formato, columnas);
    }

    private static ResultadoLectura LeerAttlog(string[] lineas)
    {
        var marcas = new List<MarcacionLeida>();
        var problemas = new List<ProblemaLectura>();
        for (var i = 0; i < lineas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i])) continue;
            var m = LineaAttlog.Match(lineas[i]);
            var codigo = m.Success ? NormalizarCodigo(m.Groups["cod"].Value) : null;
            var cuando = m.Success ? AFechaHora($"{m.Groups["fecha"].Value} {m.Groups["hora"].Value}") : null;
            if (codigo == null || cuando == null)
            {
                problemas.Add(new(i + 1, "La línea no tiene número, fecha y hora."));
                continue;
            }
            marcas.Add(new MarcacionLeida(codigo, null, cuando.Value, i + 1));
        }
        return new ResultadoLectura(SinRepetir(marcas), problemas, "archivo del reloj (attlog)", "número · fecha · hora");
    }

    private static List<MarcacionLeida> SinRepetir(List<MarcacionLeida> marcas) =>
        marcas.GroupBy(m => (m.Codigo, m.FechaHora)).Select(g => g.First()).OrderBy(m => m.Codigo).ThenBy(m => m.FechaHora).ToList();

    /// <summary>"0012" y "12" son la misma persona en el reloj.</summary>
    public static string? NormalizarCodigo(string? crudo)
    {
        var t = crudo?.Trim().Trim('"', '\'');
        if (string.IsNullOrEmpty(t)) return null;
        if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n == Math.Floor(n) && n >= 0)
            return ((long)n).ToString(CultureInfo.InvariantCulture);
        return t.Length > 30 ? t[..30] : t;
    }

    private static DateTime? AFechaHora(object? celda)
    {
        switch (celda)
        {
            case DateTime d: return d;
            case double oa when oa > 20000 && oa < 80000: return DateTime.FromOADate(oa);
        }
        var t = LimpiarAmPm(Convert.ToString(celda, CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(t)) return null;
        return DateTime.TryParseExact(t, FormatosFechaHora, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var r) ? r : null;
    }

    private static DateTime? AFecha(object? celda)
    {
        switch (celda)
        {
            case DateTime d: return d.Date;
            case double oa when oa > 20000 && oa < 80000: return DateTime.FromOADate(oa).Date;
        }
        var t = Convert.ToString(celda, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        // Algunos programas ponen la fecha con hora 00:00:00 en la columna de fecha.
        var soloFecha = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return DateTime.TryParseExact(soloFecha, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var r) ? r : null;
    }

    private static TimeSpan? AHora(object? celda)
    {
        switch (celda)
        {
            case DateTime d: return d.TimeOfDay;
            case TimeSpan ts: return ts;
            case double frac when frac >= 0 && frac < 1: return TimeSpan.FromDays(frac);
        }
        var t = LimpiarAmPm(Convert.ToString(celda, CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(t)) return null;
        if (DateTime.TryParseExact(t, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var r)) return r.TimeOfDay;
        return AFechaHora(t)?.TimeOfDay;
    }

    private static DateTime? Combinar(DateTime? fecha, TimeSpan? hora) =>
        fecha.HasValue && hora.HasValue ? fecha.Value.Date + hora.Value : null;

    /// <summary>"7:02 a. m." → "7:02 AM" (así lo escriben los equipos en español).</summary>
    private static string? LimpiarAmPm(string? t)
    {
        if (t == null) return null;
        var s = t.Trim();
        s = Regex.Replace(s, @"\s*a\.?\s*m\.?$", " AM", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*p\.?\s*m\.?$", " PM", RegexOptions.IgnoreCase);
        return s;
    }

    private static int Buscar(List<string> nombres, string[] alias, int excepto = -1)
    {
        for (var i = 0; i < nombres.Count; i++)
            if (i != excepto && alias.Contains(nombres[i])) return i;
        return -1;
    }

    /// <summary>"Fecha/Hora " → "fecha/hora"; "Número" → "numero"; "AC-No." → "ac-no".</summary>
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sinTildes = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sinTildes.Append(c);
        var s = Regex.Replace(sinTildes.ToString(), @"\s+", " ").TrimEnd('.', ':', ' ');
        return s == "no" || s == "n" ? s : s.Replace(".", "");
    }

    private static char DetectarSeparador(List<string> muestra)
    {
        var candidatos = new[] { '\t', ';', ',' };
        return candidatos
            .Select(c => (c, n: muestra.Sum(l => l.Count(x => x == c))))
            .OrderByDescending(x => x.n)
            .First().c;
    }

    /// <summary>Parte una línea CSV respetando comillas.</summary>
    private static List<string> PartirCsv(string linea, char separador)
    {
        var celdas = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else entreComillas = !entreComillas;
            }
            else if (c == separador && !entreComillas)
            {
                celdas.Add(actual.ToString());
                actual.Clear();
            }
            else actual.Append(c);
        }
        celdas.Add(actual.ToString());
        return celdas;
    }
}
