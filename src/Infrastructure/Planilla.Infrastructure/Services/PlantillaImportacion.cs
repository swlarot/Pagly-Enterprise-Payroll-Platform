// ====================================================================
// Planilla - Plantilla de importación de empleados
// Genera el Excel que la empresa llena para migrar a Pagly, y lo lee de
// vuelta. Tres hojas de datos:
//
//   Empleados      una fila por empleado, con sus datos
//   Salarios       una fila por cédula y una columna por mes (los últimos 60)
//   Saldos renta   lo ya retenido en el año en curso, para la ficha de ISR
//
// Los encabezados se buscan por nombre (sin acentos ni mayúsculas), así que
// el archivo sigue siendo válido aunque muevan columnas de sitio.
// ====================================================================

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Vorluno.Planilla.Application.DTOs.Importacion;
using Vorluno.Planilla.Application.Services;

namespace Vorluno.Planilla.Infrastructure.Services;

public static class PlantillaImportacion
{
    public const string HojaEmpleados = "Empleados";
    public const string HojaSalarios = "Salarios";
    public const string HojaSaldos = "Saldos renta";
    public const string HojaInstrucciones = "Instrucciones";

    /// <summary>Cuántos meses hacia atrás trae la hoja Salarios.</summary>
    public const int MesesHistoricos = 60;

    // Encabezados de la hoja Empleados, en orden. La clave es el nombre normalizado.
    private static readonly (string Titulo, string Clave, bool Obligatorio)[] ColumnasEmpleados =
    {
        ("Cédula", "cedula", true),
        ("Nombre", "nombre", true),
        ("Apellido", "apellido", true),
        ("Correo", "correo", false),
        ("Salario base mensual", "salariobasemensual", true),
        ("Fecha de contratación", "fechadecontratacion", true),
        ("Tipo de período", "tipodeperiodo", true),
        ("Tipo de contrato", "tipodecontrato", false),
        ("Departamento", "departamento", false),
        ("Posición", "posicion", false),
        ("Dependientes", "dependientes", false),
        ("Riesgo profesional", "riesgoprofesional", false),
        ("Gasto de representación mensual", "gastoderepresentacionmensual", false),
        ("Cotiza CSS", "cotizacss", false),
        ("Cotiza SE", "cotizase", false),
        ("Retiene ISR", "retieneisr", false),
    };

    // ====================================================================
    // GENERAR
    // ====================================================================

    public static byte[] Generar(DateTime hoy)
    {
        using var wb = new XLWorkbook();
        var verde = XLColor.FromHtml("#C6E0B4");
        var gris = XLColor.FromHtml("#EDEDED");

        // ── Instrucciones ──
        var wi = wb.Worksheets.Add(HojaInstrucciones);
        var instrucciones = new[]
        {
            "Cómo llenar esta plantilla",
            "",
            "1. Hoja Empleados: una fila por empleado. Las columnas con * son obligatorias.",
            "   La cédula es la llave: si ya existe en Pagly, se actualizan sus datos; si no, se crea.",
            "   Tipo de período: Semanal, Bisemanal, Quincenal o Mensual.",
            "   Riesgo profesional: la clase que la Caja de Seguro Social asignó a la empresa (0.56, 0.98, 2.10, 3.64 o 5.67).",
            "",
            $"2. Hoja Salarios: una fila por cédula y una columna por mes, los últimos {MesesHistoricos} meses.",
            "   Escribe lo que el empleado DEVENGÓ ese mes (salario más extras, comisiones y vacaciones pagadas).",
            "   Deja la celda vacía si no tienes el dato. No pongas cero salvo que de verdad no cobró.",
            "   Con estos meses Pagly calcula la prima de antigüedad, la indemnización, las vacaciones y el décimo.",
            "",
            $"3. Hoja {HojaSaldos}: lo que ya se le retuvo al empleado en {hoy.Year} antes de entrar a Pagly.",
            "   ISR retenido, décimo pagado y cuántas partidas de décimo (0 a 3). Solo para empresas que migran a mitad de año.",
            "",
            "Al subir el archivo, Pagly revisa cada fila y te deja corregir lo que falte antes de guardar nada.",
        };
        for (var i = 0; i < instrucciones.Length; i++) wi.Cell(i + 1, 1).Value = instrucciones[i];
        wi.Cell(1, 1).Style.Font.Bold = true;
        wi.Cell(1, 1).Style.Font.FontSize = 14;
        wi.Column(1).Width = 110;

        // ── Empleados ──
        var we = wb.Worksheets.Add(HojaEmpleados);
        for (var c = 0; c < ColumnasEmpleados.Length; c++)
        {
            var (titulo, _, obligatorio) = ColumnasEmpleados[c];
            var cell = we.Cell(1, c + 1);
            cell.Value = obligatorio ? $"{titulo} *" : titulo;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = obligatorio ? verde : gris;
            cell.Style.Alignment.WrapText = true;
        }
        we.Row(1).Height = 32;
        we.SheetView.FreezeRows(1);

        // Fila de ejemplo (la importación la ignora si la cédula es la de ejemplo)
        var ejemplo = new object[]
        {
            "8-123-4567", "Ana", "Pérez", "ana@empresa.com", 900.00, new DateTime(2022, 3, 1),
            "Quincenal", "Indefinido", "Administración", "Asistente", 0, 0.56, 0, "SI", "SI", "SI"
        };
        for (var c = 0; c < ejemplo.Length; c++)
        {
            var cell = we.Cell(2, c + 1);
            switch (ejemplo[c])
            {
                case double d: cell.Value = d; break;
                case int n: cell.Value = n; break;
                case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                default: cell.Value = ejemplo[c].ToString(); break;
            }
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }

        // Validación de datos en las columnas de lista (filas 2 a 2000)
        const int filas = 2000;
        ListaDesplegable(we, 7, filas, ImportacionEmpleadosValidator.TiposPeriodo);
        ListaDesplegable(we, 8, filas, ImportacionEmpleadosValidator.TiposContrato);
        ListaDesplegable(we, 12, filas, ImportacionEmpleadosValidator.ClasesRiesgo.Select(r => r.ToString("0.00", CultureInfo.InvariantCulture)).ToArray());
        ListaDesplegable(we, 14, filas, new[] { "SI", "NO" });
        ListaDesplegable(we, 15, filas, new[] { "SI", "NO" });
        ListaDesplegable(we, 16, filas, new[] { "SI", "NO" });
        we.Range(2, 6, filas, 6).Style.DateFormat.Format = "dd/MM/yyyy";
        we.Range(2, 5, filas, 5).Style.NumberFormat.Format = "#,##0.00";
        we.Range(2, 13, filas, 13).Style.NumberFormat.Format = "#,##0.00";
        we.Columns(1, ColumnasEmpleados.Length).Width = 18;

        // ── Salarios ──
        var ws = wb.Worksheets.Add(HojaSalarios);
        ws.Cell(1, 1).Value = "Cédula *";
        ws.Cell(1, 2).Value = "Nombre (referencia)";
        var meses = MesesDeLaPlantilla(hoy);
        for (var i = 0; i < meses.Count; i++)
        {
            var cell = ws.Cell(1, 3 + i);
            cell.Value = meses[i].ToString("yyyy-MM", CultureInfo.InvariantCulture);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Range(1, 1, 1, 2 + meses.Count).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = verde;
        ws.Range(1, 3, 1, 2 + meses.Count).Style.Fill.BackgroundColor = gris;
        ws.Range(2, 3, filas, 2 + meses.Count).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(2, 1).Value = "8-123-4567";
        ws.Cell(2, 2).Value = "Ana Pérez";
        ws.Range(2, 1, 2, 2).Style.Font.Italic = true;
        ws.Range(2, 1, 2, 2).Style.Font.FontColor = XLColor.Gray;
        ws.SheetView.Freeze(1, 2);
        ws.Column(1).Width = 14;
        ws.Column(2).Width = 22;
        ws.Columns(3, 2 + meses.Count).Width = 10;

        // ── Saldos renta ──
        var wr = wb.Worksheets.Add(HojaSaldos);
        var colsSaldos = new[]
        {
            "Cédula *", "Nombre (referencia)", $"ISR retenido en {hoy.Year}", $"Décimo pagado en {hoy.Year}",
            "Partidas de décimo pagadas (0-3)", "Gastos de representación pagados", "ISR retenido sobre gastos de representación"
        };
        for (var c = 0; c < colsSaldos.Length; c++)
        {
            var cell = wr.Cell(1, c + 1);
            cell.Value = colsSaldos[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = c < 2 ? verde : gris;
            cell.Style.Alignment.WrapText = true;
        }
        wr.Row(1).Height = 40;
        wr.Cell(2, 1).Value = "8-123-4567";
        wr.Cell(2, 2).Value = "Ana Pérez";
        wr.Range(2, 1, 2, 2).Style.Font.Italic = true;
        wr.Range(2, 1, 2, 2).Style.Font.FontColor = XLColor.Gray;
        wr.Range(2, 3, filas, 7).Style.NumberFormat.Format = "#,##0.00";
        ListaDesplegable(wr, 5, filas, new[] { "0", "1", "2", "3" });
        wr.SheetView.FreezeRows(1);
        wr.Columns(1, colsSaldos.Length).Width = 20;

        wb.Worksheet(HojaInstrucciones).SetTabActive();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Los últimos N meses, del más antiguo al mes anterior al actual… incluido el actual.</summary>
    public static List<DateTime> MesesDeLaPlantilla(DateTime hoy)
    {
        var fin = new DateTime(hoy.Year, hoy.Month, 1);
        return Enumerable.Range(0, MesesHistoricos)
            .Select(i => fin.AddMonths(-(MesesHistoricos - 1 - i)))
            .ToList();
    }

    private static void ListaDesplegable(IXLWorksheet ws, int columna, int filas, string[] valores)
    {
        var dv = ws.Range(2, columna, filas, columna).CreateDataValidation();
        dv.List($"\"{string.Join(",", valores)}\"", true);
        dv.IgnoreBlanks = true;
        dv.ErrorTitle = "Valor no permitido";
        dv.ErrorMessage = $"Elige uno de: {string.Join(", ", valores)}";
    }

    // ====================================================================
    // LEER
    // ====================================================================

    public sealed class Lectura
    {
        public List<FilaImportacionDto> Filas { get; } = new();
        public List<string> ProblemasDelArchivo { get; } = new();
        public List<string> MesesDelArchivo { get; } = new();
    }

    public static Lectura Leer(Stream archivo, int anioSaldos)
    {
        var lectura = new Lectura();
        XLWorkbook wb;
        try { wb = new XLWorkbook(archivo); }
        catch (Exception)
        {
            lectura.ProblemasDelArchivo.Add("El archivo no es un Excel válido (.xlsx). Descarga la plantilla y llénala.");
            return lectura;
        }

        using (wb)
        {
            var hojaEmp = wb.Worksheets.FirstOrDefault(w => Normalizar(w.Name) == Normalizar(HojaEmpleados));
            if (hojaEmp is null)
            {
                lectura.ProblemasDelArchivo.Add($"El archivo no tiene la hoja «{HojaEmpleados}». Usa la plantilla de Pagly.");
                return lectura;
            }

            // Columnas por nombre
            var columnas = MapearEncabezados(hojaEmp);
            var faltan = ColumnasEmpleados.Where(c => c.Obligatorio && !columnas.ContainsKey(c.Clave)).Select(c => c.Titulo).ToList();
            if (faltan.Count > 0)
            {
                lectura.ProblemasDelArchivo.Add($"En la hoja «{HojaEmpleados}» faltan las columnas: {string.Join(", ", faltan)}.");
                return lectura;
            }

            var ultimaFila = hojaEmp.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= ultimaFila; r++)
            {
                string Texto(string clave) => columnas.TryGetValue(clave, out var c) ? hojaEmp.Cell(r, c).GetString().Trim() : string.Empty;
                decimal? Numero(string clave) => columnas.TryGetValue(clave, out var c) ? LeerDecimal(hojaEmp.Cell(r, c)) : null;

                var cedula = ImportacionEmpleadosValidator.NormalizarCedula(Texto("cedula"));
                var nombre = Texto("nombre");
                if (cedula.Length == 0 && nombre.Length == 0 && Texto("apellido").Length == 0) continue; // fila vacía
                if (cedula == "8-123-4567" && nombre == "Ana") continue; // fila de ejemplo

                var fila = new FilaImportacionDto
                {
                    Fila = r,
                    Cedula = cedula,
                    Nombre = nombre,
                    Apellido = Texto("apellido"),
                    Email = Texto("correo").Length > 0 ? Texto("correo") : null,
                    SalarioBase = Numero("salariobasemensual"),
                    FechaContratacion = columnas.TryGetValue("fechadecontratacion", out var cf) ? LeerFecha(hojaEmp.Cell(r, cf)) : null,
                    TipoPeriodo = Texto("tipodeperiodo").Length > 0 ? Texto("tipodeperiodo") : null,
                    TipoContrato = Texto("tipodecontrato").Length > 0 ? Texto("tipodecontrato") : null,
                    Departamento = Texto("departamento").Length > 0 ? Texto("departamento") : null,
                    Posicion = Texto("posicion").Length > 0 ? Texto("posicion") : null,
                    Dependientes = (int?)Numero("dependientes"),
                    RiesgoProfesional = Numero("riesgoprofesional"),
                    GastoRepresentacionMensual = Numero("gastoderepresentacionmensual"),
                    SujetoCss = LeerSiNo(Texto("cotizacss")),
                    SujetoSe = LeerSiNo(Texto("cotizase")),
                    SujetoIsr = LeerSiNo(Texto("retieneisr")),
                };
                lectura.Filas.Add(fila);
            }

            var porCedula = lectura.Filas
                .Where(f => f.Cedula.Length > 0)
                .GroupBy(f => f.Cedula)
                .ToDictionary(g => g.Key, g => g.First());

            // ── Salarios ──
            var hojaSal = wb.Worksheets.FirstOrDefault(w => Normalizar(w.Name) == Normalizar(HojaSalarios));
            if (hojaSal is not null)
            {
                var ultimaCol = hojaSal.LastColumnUsed()?.ColumnNumber() ?? 2;
                var mesesPorColumna = new Dictionary<int, (int Anio, int Mes)>();
                for (var c = 1; c <= ultimaCol; c++)
                {
                    var enc = hojaSal.Cell(1, c).GetString().Trim();
                    if (DateTime.TryParseExact(enc, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                        || DateTime.TryParseExact(enc, "MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                        || (hojaSal.Cell(1, c).DataType == XLDataType.DateTime && hojaSal.Cell(1, c).TryGetValue(out d)))
                    {
                        mesesPorColumna[c] = (d.Year, d.Month);
                        lectura.MesesDelArchivo.Add($"{d.Year}-{d.Month:D2}");
                    }
                }

                if (mesesPorColumna.Count == 0)
                    lectura.ProblemasDelArchivo.Add($"La hoja «{HojaSalarios}» no tiene columnas de mes (formato 2026-01).");

                var ultimaFilaSal = hojaSal.LastRowUsed()?.RowNumber() ?? 1;
                var sinEmpleado = new List<string>();
                for (var r = 2; r <= ultimaFilaSal; r++)
                {
                    var cedula = ImportacionEmpleadosValidator.NormalizarCedula(hojaSal.Cell(r, 1).GetString());
                    if (cedula.Length == 0) continue;
                    if (!porCedula.TryGetValue(cedula, out var fila))
                    {
                        if (cedula != "8-123-4567") sinEmpleado.Add(cedula);
                        continue;
                    }
                    foreach (var (col, (anio, mes)) in mesesPorColumna)
                    {
                        var monto = LeerDecimal(hojaSal.Cell(r, col));
                        fila.Meses.Add(new MesImportadoDto { Anio = anio, Mes = mes, Monto = monto });
                    }
                }
                if (sinEmpleado.Count > 0)
                    lectura.ProblemasDelArchivo.Add(
                        $"En «{HojaSalarios}» hay cédulas que no están en «{HojaEmpleados}» y se ignoran: {string.Join(", ", sinEmpleado.Take(5))}{(sinEmpleado.Count > 5 ? "…" : "")}.");
            }

            // ── Saldos renta ──
            var hojaSaldos = wb.Worksheets.FirstOrDefault(w => Normalizar(w.Name).StartsWith(Normalizar(HojaSaldos)));
            if (hojaSaldos is not null)
            {
                var ultimaFilaS = hojaSaldos.LastRowUsed()?.RowNumber() ?? 1;
                for (var r = 2; r <= ultimaFilaS; r++)
                {
                    var cedula = ImportacionEmpleadosValidator.NormalizarCedula(hojaSaldos.Cell(r, 1).GetString());
                    if (cedula.Length == 0 || !porCedula.TryGetValue(cedula, out var fila)) continue;

                    var isr = LeerDecimal(hojaSaldos.Cell(r, 3));
                    var decimo = LeerDecimal(hojaSaldos.Cell(r, 4));
                    var partidas = LeerDecimal(hojaSaldos.Cell(r, 5));
                    var gr = LeerDecimal(hojaSaldos.Cell(r, 6));
                    var isrGr = LeerDecimal(hojaSaldos.Cell(r, 7));
                    if (isr is null && decimo is null && partidas is null && gr is null && isrGr is null) continue;

                    fila.Saldos = new SaldosRentaImportadosDto
                    {
                        Anio = anioSaldos,
                        IsrRetenido = isr,
                        DecimoPagado = decimo,
                        PartidasDecimo = partidas is null ? null : (int)partidas.Value,
                        GastoRepresentacionPagado = gr,
                        IsrGastoRepresentacion = isrGr
                    };
                }
            }
        }

        return lectura;
    }

    private static Dictionary<string, int> MapearEncabezados(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>();
        var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var c = 1; c <= ultimaCol; c++)
        {
            var enc = Normalizar(ws.Cell(1, c).GetString());
            var col = ColumnasEmpleados.FirstOrDefault(x => x.Clave == enc);
            if (col.Clave is not null && !map.ContainsKey(col.Clave)) map[col.Clave] = c;
        }
        return map;
    }

    /// <summary>Minúsculas, sin acentos, sin espacios ni asteriscos: "Fecha de contratación *" → "fechadecontratacion".</summary>
    public static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static decimal? LeerDecimal(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var d)) return (decimal)d;
        var texto = cell.GetString().Trim().Replace("B/.", "").Replace("$", "").Replace(",", "").Trim();
        if (texto.Length == 0) return null;
        return decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateTime? LeerFecha(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt)) return dt.Date;
        var texto = cell.GetString().Trim();
        foreach (var f in new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy" })
            if (DateTime.TryParseExact(texto, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
        return null;
    }

    private static bool? LeerSiNo(string texto)
    {
        var t = Normalizar(texto);
        return t switch { "si" or "s" or "yes" or "true" or "1" => true, "no" or "n" or "false" or "0" => false, _ => null };
    }
}
