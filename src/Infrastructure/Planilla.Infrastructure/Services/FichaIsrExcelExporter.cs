// ====================================================================
// Planilla - FichaIsrExcelExporter
// Genera la ficha anual de ISR como el libro del contador: misma disposición
// de celdas, mismos encabezados y —lo importante— las MISMAS FÓRMULAS en las
// columnas de cálculo, no valores pegados. Así el contador puede abrirla junto
// a la suya y auditar celda por celda.
//
//   A1 Empleado   H1 Salario Base / I1   J1 Conyuge es dependiente / L1
//   H3 Periodos de Pagos / I3 26 / J3 Quincenas
//   Fila 5 encabezados, filas 6..29 quincenas, fila 31 totales.
// ====================================================================

using ClosedXML.Excel;
using Vorluno.Planilla.Application.DTOs;

namespace Vorluno.Planilla.Infrastructure.Services;

public static class FichaIsrExcelExporter
{
    private const int FilaEncabezado = 5;
    private const int PrimeraFila = 6;

    public static byte[] Exportar(FichaIsrAnualDto ficha)
    {
        using var workbook = new XLWorkbook();
        var nombreHoja = ficha.Empleado.Replace(' ', '_');
        if (nombreHoja.Length > 31) nombreHoja = nombreHoja[..31];
        var ws = workbook.Worksheets.Add(nombreHoja);

        // ── Cabecera, en las mismas celdas que la hoja del contador ──
        ws.Cell("A1").Value = "Empleado";
        ws.Cell("B1").Value = ficha.Empleado;
        ws.Cell("H1").Value = "Salario Base";
        ws.Cell("I1").Value = ficha.SalarioBase;
        ws.Cell("J1").Value = "Conyuge es dependiente";
        ws.Cell("L1").Value = ficha.ConyugeDependiente;
        ws.Cell("H3").Value = "Periodos de Pagos";
        ws.Cell("I3").Value = ficha.PeriodosDePago;
        ws.Cell("J3").Value = ficha.NombrePeriodo;

        foreach (var c in new[] { "A1", "H1", "J1", "H3" })
        {
            ws.Cell(c).Style.Font.Bold = true;
            ws.Cell(c).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6E0B4");
        }
        ws.Cell("I1").Style.NumberFormat.Format = "#,##0.00";

        // ── Encabezados de columna ──
        var encabezados = new[]
        {
            "MESES", "QUINCENAS", "PERIODOS", "SALARIOS", "VACACIONES", "EXTRAS", "COMISION",
            "XIII MEX", "ACUMULADO", "INGRESO GRAVABLE", "RENTA ANUAL", "RENTA POR PERIODO",
            "IMPUESTO CAUSADO", "IMPUESTO A PAGAR", "RENTA ACUMULADA"
        };
        if (ficha.NombrePeriodo != "Quincenas") encabezados[1] = ficha.NombrePeriodo.ToUpperInvariant();

        for (var i = 0; i < encabezados.Length; i++)
        {
            var cell = ws.Cell(FilaEncabezado, i + 1);
            cell.Value = encabezados[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6E0B4");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        ws.Row(FilaEncabezado).Height = 30;

        // ── Filas ──
        var fila = PrimeraFila;
        foreach (var f in ficha.Filas)
        {
            // A..H: datos
            ws.Cell(fila, 1).Value = f.Mes;
            ws.Cell(fila, 2).Value = f.Quincena;
            ws.Cell(fila, 3).Value = f.Periodos;
            ws.Cell(fila, 4).Value = f.Salarios;
            ws.Cell(fila, 5).Value = f.Vacaciones;
            ws.Cell(fila, 6).Value = f.Extras;
            ws.Cell(fila, 7).Value = f.Comision;
            ws.Cell(fila, 8).Value = f.XiiiMes;

            // I..O: las fórmulas literales del libro
            var acumuladoAnterior = fila == PrimeraFila ? string.Empty : $"+I{fila - 1}";
            ws.Cell(fila, 9).FormulaA1 = $"=SUM(D{fila}:H{fila}){acumuladoAnterior}";
            ws.Cell(fila, 10).FormulaA1 = $"=I{fila}/C{fila}*I$3";
            ws.Cell(fila, 11).FormulaA1 =
                $"=IF(J{fila}>50000,((J{fila}-50000)*25%)+5850,IF(J{fila}<11000,0,(J{fila}-11000)*15%))";
            ws.Cell(fila, 12).FormulaA1 = $"=K{fila}/I$3";
            ws.Cell(fila, 13).FormulaA1 = $"=L{fila}*C{fila}";
            ws.Cell(fila, 14).FormulaA1 = $"=M{fila}";
            ws.Cell(fila, 15).FormulaA1 = $"=N{fila}";

            ws.Cell(fila, 3).Style.NumberFormat.Format = "0.000";
            ws.Range(fila, 4, fila, 15).Style.NumberFormat.Format = "#,##0.00";

            if (f.EsMesDecimo)
                ws.Range(fila, 1, fila, 15).Style.Fill.BackgroundColor = XLColor.Yellow;

            fila++;
        }

        var ultima = fila - 1;

        // Bordes del cuerpo
        ws.Range(FilaEncabezado, 1, ultima, 15).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(FilaEncabezado, 1, ultima, 15).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

        // ── Fila de totales, con fórmulas, como en la hoja ──
        var totales = ultima + 2;
        foreach (var col in new[] { 4, 5, 6, 7, 8 })
        {
            var letra = (char)('A' + col - 1);
            ws.Cell(totales, col).FormulaA1 = $"=SUM({letra}{PrimeraFila}:{letra}{ultima})";
            ws.Cell(totales, col).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(totales, col).Style.Font.Bold = true;
        }
        // Chequeo del libro: suma de conceptos menos acumulado final debe dar 0.
        ws.Cell(totales, 9).FormulaA1 = $"=SUM(D{totales}:H{totales})-I{ultima}";
        ws.Cell(totales, 9).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns(1, 15).AdjustToContents();
        ws.Column(1).Width = 12;
        ws.SheetView.FreezeRows(FilaEncabezado);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
