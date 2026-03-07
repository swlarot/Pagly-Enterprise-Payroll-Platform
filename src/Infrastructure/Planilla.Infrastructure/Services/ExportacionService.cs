// ====================================================================
// Planilla - ExportacionService (Rediseño C-012)
// Actualizado: 2026-02-20
// Exporta los 5 reportes a Excel (ClosedXML) y PDF (QuestPDF)
// ====================================================================

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Vorluno.Planilla.Application.DTOs.Reportes;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para exportar los 5 reportes de planilla a Excel y PDF.
/// Usa ClosedXML para .xlsx y QuestPDF Community para .pdf.
/// </summary>
public class ExportacionService
{
    public ExportacionService()
    {
        // QuestPDF Community license — gratuita sin restricciones para proyectos open-source
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ====================================================================
    // Helpers privados
    // ====================================================================

    private static void AplicarEstiloEncabezado(IXLWorksheet ws, string titulo, string empresa, string ruc, string periodo, int columnas)
    {
        ws.Cell("A1").Value = empresa;
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, columnas).Merge();

        ws.Cell("A2").Value = $"RUC: {ruc}";
        ws.Cell("A3").Value = $"Período: {periodo}";
        ws.Cell("A4").Value = titulo;
        ws.Cell("A4").Style.Font.Bold = true;
        ws.Cell("A5").Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
    }

    private static void AplicarEstiloHeaderFila(IXLWorksheet ws, int fila, int columnas)
    {
        var rng = ws.Range(fila, 1, fila, columnas);
        rng.Style.Font.Bold = true;
        rng.Style.Fill.BackgroundColor = XLColor.LightGray;
        rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void AplicarEstiloTotales(IXLWorksheet ws, int fila, int columnas)
    {
        var rng = ws.Range(fila, 1, fila, columnas);
        rng.Style.Font.Bold = true;
        rng.Style.Fill.BackgroundColor = XLColor.LightYellow;
    }

    // ====================================================================
    // REPORTE 1: Planilla Regular — Excel
    // ====================================================================

    /// <summary>Exporta la Planilla Regular a Excel con horas y deducciones resumidas.</summary>
    public byte[] ExportarExcelPlanillaRegular(ReportePlanillaRegularDto reporte)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Planilla Regular");

        const int cols = 14;
        AplicarEstiloEncabezado(ws, $"PLANILLA REGULAR — {reporte.Estado}", reporte.NombreEmpresa, reporte.Ruc, reporte.Periodo, cols);
        ws.Cell("A5").Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}  |  N° Planilla: {reporte.NumeroPlanilla}  |  Fecha de Pago: {reporte.FechaPago:dd/MM/yyyy}";

        var hr = 7;
        ws.Cell(hr, 1).Value = "Cédula";
        ws.Cell(hr, 2).Value = "Nombre";
        ws.Cell(hr, 3).Value = "H. Regulares";
        ws.Cell(hr, 4).Value = "H. Domingo";
        ws.Cell(hr, 5).Value = "H. Feriado";
        ws.Cell(hr, 6).Value = "H. Extra";
        ws.Cell(hr, 7).Value = "Sal. Bruto";
        ws.Cell(hr, 8).Value = "CSS Emp.";
        ws.Cell(hr, 9).Value = "SE Emp.";
        ws.Cell(hr, 10).Value = "ISR";
        ws.Cell(hr, 11).Value = "Acreedores";
        ws.Cell(hr, 12).Value = "Sal. Neto";
        ws.Cell(hr, 13).Value = "Limitación";
        ws.Cell(hr, 14).Value = "Razón";
        AplicarEstiloHeaderFila(ws, hr, cols);

        var row = hr + 1;
        foreach (var emp in reporte.Empleados)
        {
            ws.Cell(row, 1).Value = emp.Cedula;
            ws.Cell(row, 2).Value = emp.NombreCompleto;
            ws.Cell(row, 3).Value = emp.HorasRegulares;
            ws.Cell(row, 4).Value = emp.HorasDomingo;
            ws.Cell(row, 5).Value = emp.HorasFeriado;
            ws.Cell(row, 6).Value = emp.HorasExtra;
            ws.Cell(row, 7).Value = emp.SalarioBruto;
            ws.Cell(row, 8).Value = emp.CssEmpleado;
            ws.Cell(row, 9).Value = emp.SeEmpleado;
            ws.Cell(row, 10).Value = emp.Isr;
            ws.Cell(row, 11).Value = emp.TotalAcreedores;
            ws.Cell(row, 12).Value = emp.SalarioNeto;
            ws.Cell(row, 13).Value = emp.TuvoLimitacion ? "SI" : "";
            ws.Cell(row, 14).Value = emp.RazonLimitacion ?? "";

            ws.Range(row, 3, row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(row, 7, row, 12).Style.NumberFormat.Format = "#,##0.00";

            if (emp.TuvoLimitacion)
                ws.Cell(row, 13).Style.Fill.BackgroundColor = XLColor.Orange;

            row++;

            // Sub-tabla desglose de horas
            if (emp.DesgloseHoras.Count > 0)
            {
                // Encabezado desglose
                ws.Cell(row, 2).Value = "  Tipo/Concepto";
                ws.Cell(row, 3).Value = "Horas";
                ws.Cell(row, 4).Value = "Tarifa x Hora";
                ws.Cell(row, 5).Value = "Valor";
                var desgloseHeaderRange = ws.Range(row, 2, row, 5);
                desgloseHeaderRange.Style.Font.Bold = true;
                desgloseHeaderRange.Style.Font.FontSize = 8;
                desgloseHeaderRange.Style.Fill.BackgroundColor = XLColor.LightCyan;
                row++;

                foreach (var linea in emp.DesgloseHoras)
                {
                    ws.Cell(row, 2).Value = $"    {linea.TipoConcepto}";
                    ws.Cell(row, 3).Value = linea.Horas;
                    ws.Cell(row, 4).Value = linea.TarifaPorHora;
                    ws.Cell(row, 5).Value = linea.Valor;
                    ws.Cell(row, 2).Style.Font.FontSize = 8;
                    ws.Cell(row, 2).Style.Font.Italic = true;
                    ws.Range(row, 3, row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Range(row, 3, row, 5).Style.Font.FontSize = 8;
                    ws.Range(row, 3, row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    row++;
                }
            }
        }

        ws.Cell(row, 2).Value = "TOTALES";
        ws.Cell(row, 3).Value = reporte.Totales.TotalEmpleados;
        ws.Cell(row, 7).Value = reporte.Totales.TotalBruto;
        ws.Cell(row, 8).Value = reporte.Totales.TotalCss;
        ws.Cell(row, 9).Value = reporte.Totales.TotalSe;
        ws.Cell(row, 10).Value = reporte.Totales.TotalIsr;
        ws.Cell(row, 11).Value = reporte.Totales.TotalAcreedores;
        ws.Cell(row, 12).Value = reporte.Totales.TotalNeto;
        AplicarEstiloTotales(ws, row, cols);
        ws.Range(row, 7, row, 12).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ====================================================================
    // REPORTE 1: Planilla Regular — PDF
    // ====================================================================

    /// <summary>Exporta la Planilla Regular a PDF en orientación Landscape.</summary>
    public byte[] ExportarPdfPlanillaRegular(ReportePlanillaRegularDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text(reporte.NombreEmpresa).FontSize(14).Bold();
                    col.Item().Text($"RUC: {reporte.Ruc}").FontSize(9);
                    col.Item().Text($"PLANILLA REGULAR — {reporte.NumeroPlanilla} — {reporte.Periodo} — {reporte.Estado}").FontSize(11).Bold();
                    col.Item().Text($"Fecha de Pago: {reporte.FechaPago:dd/MM/yyyy}").FontSize(9);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f); // Cédula
                        cols.RelativeColumn(3);    // Nombre
                        cols.RelativeColumn(1.2f); // H. Reg
                        cols.RelativeColumn(1.2f); // H. Dom
                        cols.RelativeColumn(1.2f); // H. Fer
                        cols.RelativeColumn(1.2f); // H. Ext
                        cols.RelativeColumn(2);    // Bruto
                        cols.RelativeColumn(1.8f); // CSS
                        cols.RelativeColumn(1.8f); // SE
                        cols.RelativeColumn(1.8f); // ISR
                        cols.RelativeColumn(2);    // Acreed
                        cols.RelativeColumn(2);    // Neto
                    });

                    table.Header(header =>
                    {
                        var bg = Colors.Grey.Lighten2;
                        header.Cell().Background(bg).Padding(4).Text("Cédula").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).Text("Nombre").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignCenter().Text("H.Reg").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignCenter().Text("H.Dom").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignCenter().Text("H.Fer").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignCenter().Text("H.Ext").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Bruto").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("CSS").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("SE").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("ISR").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Acreed.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Neto").Bold().FontSize(8);
                    });

                    foreach (var emp in reporte.Empleados)
                    {
                        var rowBg = emp.TuvoLimitacion ? Colors.Orange.Lighten4 : Colors.White;
                        table.Cell().Background(rowBg).Padding(3).Text(emp.Cedula).FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).Text(emp.NombreCompleto).FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignCenter().Text(emp.HorasRegulares > 0 ? $"{emp.HorasRegulares:N1}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignCenter().Text(emp.HorasDomingo > 0 ? $"{emp.HorasDomingo:N1}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignCenter().Text(emp.HorasFeriado > 0 ? $"{emp.HorasFeriado:N1}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignCenter().Text(emp.HorasExtra > 0 ? $"{emp.HorasExtra:N1}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text($"B/.{emp.SalarioBruto:N2}").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text($"B/.{emp.CssEmpleado:N2}").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text($"B/.{emp.SeEmpleado:N2}").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text(emp.Isr > 0 ? $"B/.{emp.Isr:N2}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text(emp.TotalAcreedores > 0 ? $"B/.{emp.TotalAcreedores:N2}" : "").FontSize(8);
                        table.Cell().Background(rowBg).Padding(3).AlignRight().Text($"B/.{emp.SalarioNeto:N2}").FontSize(8);

                        // Desglose de horas: ocupa las 12 columnas con una mini-tabla
                        if (emp.DesgloseHoras.Count > 0)
                        {
                            table.Cell().ColumnSpan(12).PaddingLeft(20).PaddingBottom(4).Table(inner =>
                            {
                                inner.ColumnsDefinition(ic =>
                                {
                                    ic.RelativeColumn(4);   // Tipo/Concepto
                                    ic.RelativeColumn(1.5f); // Horas
                                    ic.RelativeColumn(2);   // Tarifa
                                    ic.RelativeColumn(2);   // Valor
                                });

                                inner.Header(ih =>
                                {
                                    var hbg = Colors.LightBlue.Lighten3;
                                    ih.Cell().Background(hbg).Padding(2).Text("Tipo/Concepto").Bold().FontSize(7);
                                    ih.Cell().Background(hbg).Padding(2).AlignCenter().Text("Horas").Bold().FontSize(7);
                                    ih.Cell().Background(hbg).Padding(2).AlignRight().Text("Tarifa x Hora").Bold().FontSize(7);
                                    ih.Cell().Background(hbg).Padding(2).AlignRight().Text("Valor").Bold().FontSize(7);
                                });

                                foreach (var linea in emp.DesgloseHoras)
                                {
                                    inner.Cell().Padding(2).Text(linea.TipoConcepto).FontSize(7);
                                    inner.Cell().Padding(2).AlignCenter().Text($"{linea.Horas:N2}").FontSize(7);
                                    inner.Cell().Padding(2).AlignRight().Text($"B/.{linea.TarifaPorHora:N4}").FontSize(7);
                                    inner.Cell().Padding(2).AlignRight().Text($"B/.{linea.Valor:N2}").FontSize(7);
                                }
                            });
                        }
                    }

                    // Fila totales
                    var totBg = Colors.Yellow.Lighten3;
                    table.Cell().Background(totBg).Padding(3).Text($"Total: {reporte.Totales.TotalEmpleados} emp.").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("TOTALES").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalBruto:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalCss:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalSe:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalIsr:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalAcreedores:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalNeto:N2}").Bold().FontSize(8);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    // ====================================================================
    // REPORTE 2: Mensual — Excel
    // ====================================================================

    /// <summary>Exporta el Reporte Mensual consolidado a Excel.</summary>
    public byte[] ExportarExcelMensual(ReporteMensualDto reporte)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reporte Mensual");

        const int cols = 8;
        AplicarEstiloEncabezado(ws, $"REPORTE MENSUAL — {reporte.NombreMes} {reporte.Anio}", reporte.NombreEmpresa, reporte.Ruc, $"{reporte.NombreMes} {reporte.Anio}", cols);

        if (reporte.PeriodosIncluidos.Any())
            ws.Cell("A6").Value = $"Planillas incluidas: {string.Join(", ", reporte.PeriodosIncluidos)}";

        var hr = 8;
        ws.Cell(hr, 1).Value = "Cédula";
        ws.Cell(hr, 2).Value = "Nombre";
        ws.Cell(hr, 3).Value = "Total Bruto";
        ws.Cell(hr, 4).Value = "Total CSS";
        ws.Cell(hr, 5).Value = "Total SE";
        ws.Cell(hr, 6).Value = "Total ISR";
        ws.Cell(hr, 7).Value = "Acreedores";
        ws.Cell(hr, 8).Value = "Total Neto";
        AplicarEstiloHeaderFila(ws, hr, cols);

        var row = hr + 1;
        foreach (var emp in reporte.Empleados)
        {
            ws.Cell(row, 1).Value = emp.Cedula;
            ws.Cell(row, 2).Value = emp.NombreCompleto;
            ws.Cell(row, 3).Value = emp.TotalBruto;
            ws.Cell(row, 4).Value = emp.TotalCss;
            ws.Cell(row, 5).Value = emp.TotalSe;
            ws.Cell(row, 6).Value = emp.TotalIsr;
            ws.Cell(row, 7).Value = emp.TotalAcreedores;
            ws.Cell(row, 8).Value = emp.TotalNeto;
            ws.Range(row, 3, row, 8).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws.Cell(row, 2).Value = "TOTALES";
        ws.Cell(row, 3).Value = reporte.Totales.GranTotalBruto;
        ws.Cell(row, 4).Value = reporte.Totales.GranTotalCss;
        ws.Cell(row, 5).Value = reporte.Totales.GranTotalSe;
        ws.Cell(row, 6).Value = reporte.Totales.GranTotalIsr;
        ws.Cell(row, 7).Value = reporte.Totales.GranTotalAcreedores;
        ws.Cell(row, 8).Value = reporte.Totales.GranTotalNeto;
        AplicarEstiloTotales(ws, row, cols);
        ws.Range(row, 3, row, 8).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ====================================================================
    // REPORTE 2: Mensual — PDF
    // ====================================================================

    /// <summary>Exporta el Reporte Mensual consolidado a PDF.</summary>
    public byte[] ExportarPdfMensual(ReporteMensualDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text(reporte.NombreEmpresa).FontSize(14).Bold();
                    col.Item().Text($"RUC: {reporte.Ruc}").FontSize(9);
                    col.Item().Text($"REPORTE MENSUAL — {reporte.NombreMes} {reporte.Anio}").FontSize(12).Bold();
                    if (reporte.PeriodosIncluidos.Any())
                        col.Item().Text($"Planillas: {string.Join(", ", reporte.PeriodosIncluidos)}").FontSize(8);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        var bg = Colors.Grey.Lighten2;
                        header.Cell().Background(bg).Padding(4).Text("Cédula").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).Text("Nombre").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Total Bruto").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("CSS").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("SE").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("ISR").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Acreed.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Total Neto").Bold().FontSize(8);
                    });

                    foreach (var emp in reporte.Empleados)
                    {
                        table.Cell().Padding(3).Text(emp.Cedula).FontSize(8);
                        table.Cell().Padding(3).Text(emp.NombreCompleto).FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.TotalBruto:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.TotalCss:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.TotalSe:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text(emp.TotalIsr > 0 ? $"B/.{emp.TotalIsr:N2}" : "").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text(emp.TotalAcreedores > 0 ? $"B/.{emp.TotalAcreedores:N2}" : "").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.TotalNeto:N2}").FontSize(8);
                    }

                    var totBg = Colors.Yellow.Lighten3;
                    table.Cell().Background(totBg).Padding(3).Text($"{reporte.Totales.TotalEmpleados} emp.").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("TOTALES").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalBruto:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalCss:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalSe:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalIsr:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalAcreedores:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalNeto:N2}").Bold().FontSize(8);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    // ====================================================================
    // REPORTE 3: Acreedores — Excel
    // ====================================================================

    /// <summary>
    /// Exporta el Reporte de Acreedores a Excel.
    /// Sheet 1: resumen por acreedor con datos bancarios.
    /// Sheet 2: detalle por acreedor con desglose de empleados.
    /// </summary>
    public byte[] ExportarExcelAcreedores(ReporteAcreedoresDto reporte)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Consolidado
        var ws1 = workbook.Worksheets.Add("Acreedores");
        AplicarEstiloEncabezado(ws1, "REPORTE DE ACREEDORES", reporte.NombreEmpresa, reporte.Ruc, reporte.Periodo, 8);
        ws1.Cell("A5").Value = $"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";

        var hr = 7;
        ws1.Cell(hr, 1).Value = "Acreedor";
        ws1.Cell(hr, 2).Value = "Tipo";
        ws1.Cell(hr, 3).Value = "Identificación";
        ws1.Cell(hr, 4).Value = "Banco";
        ws1.Cell(hr, 5).Value = "N° Cuenta";
        ws1.Cell(hr, 6).Value = "# Empleados";
        ws1.Cell(hr, 7).Value = "Total a Transferir";
        ws1.Cell(hr, 8).Value = "Notas";
        AplicarEstiloHeaderFila(ws1, hr, 8);

        var row = hr + 1;
        foreach (var acr in reporte.Acreedores)
        {
            ws1.Cell(row, 1).Value = acr.NombreAcreedor;
            ws1.Cell(row, 2).Value = acr.TipoAcreedor ?? "";
            ws1.Cell(row, 3).Value = acr.Identificacion ?? "";
            ws1.Cell(row, 4).Value = acr.Banco ?? "";
            ws1.Cell(row, 5).Value = acr.NumeroCuenta ?? "";
            ws1.Cell(row, 6).Value = acr.CantidadEmpleados;
            ws1.Cell(row, 7).Value = acr.TotalATransferir;
            ws1.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws1.Cell(row, 1).Value = "TOTALES";
        ws1.Cell(row, 6).Value = reporte.TotalAcreedores;
        ws1.Cell(row, 7).Value = reporte.GranTotal;
        ws1.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        AplicarEstiloTotales(ws1, row, 8);
        ws1.Columns().AdjustToContents();

        // Sheet 2: Detalle
        var ws2 = workbook.Worksheets.Add("Detalle por Acreedor");
        ws2.Cell("A1").Value = reporte.NombreEmpresa;
        ws2.Cell("A1").Style.Font.Bold = true;
        ws2.Cell("A1").Style.Font.FontSize = 14;
        ws2.Range("A1:H1").Merge();
        ws2.Cell("A2").Value = $"Período: {reporte.Periodo}  |  Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";

        var detRow = 4;
        foreach (var acr in reporte.Acreedores)
        {
            ws2.Cell(detRow, 1).Value = $"{acr.NombreAcreedor}  — Total: B/.{acr.TotalATransferir:N2}";
            ws2.Cell(detRow, 1).Style.Font.Bold = true;
            ws2.Cell(detRow, 1).Style.Font.FontSize = 11;
            detRow++;

            ws2.Cell(detRow, 1).Value = "Empleado";
            ws2.Cell(detRow, 2).Value = "Cédula";
            ws2.Cell(detRow, 3).Value = "Tipo";
            ws2.Cell(detRow, 4).Value = "Descripción";
            ws2.Cell(detRow, 5).Value = "Solicitado";
            ws2.Cell(detRow, 6).Value = "Aplicado";
            ws2.Cell(detRow, 7).Value = "Limitado";
            ws2.Cell(detRow, 8).Value = "Razón";
            ws2.Range(detRow, 1, detRow, 8).Style.Font.Bold = true;
            ws2.Range(detRow, 1, detRow, 8).Style.Fill.BackgroundColor = XLColor.LightBlue;
            detRow++;

            foreach (var det in acr.Empleados)
            {
                ws2.Cell(detRow, 1).Value = det.NombreEmpleado;
                ws2.Cell(detRow, 2).Value = det.Cedula ?? "";
                ws2.Cell(detRow, 3).Value = det.TipoDeduccion;
                ws2.Cell(detRow, 4).Value = det.Descripcion;
                ws2.Cell(detRow, 5).Value = det.MontoSolicitado;
                ws2.Cell(detRow, 6).Value = det.MontoAplicado;
                ws2.Cell(detRow, 7).Value = det.FueLimitado ? "SI" : "";
                ws2.Cell(detRow, 8).Value = det.RazonLimitacion ?? "";
                ws2.Range(detRow, 5, detRow, 6).Style.NumberFormat.Format = "#,##0.00";
                if (det.FueLimitado)
                    ws2.Cell(detRow, 7).Style.Fill.BackgroundColor = XLColor.Orange;
                detRow++;
            }
            detRow++;
        }

        ws2.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ====================================================================
    // REPORTE 3: Acreedores — PDF
    // ====================================================================

    /// <summary>Exporta el Reporte de Acreedores a PDF con tabla resumen y detalle por acreedor.</summary>
    public byte[] ExportarPdfAcreedores(ReporteAcreedoresDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text(reporte.NombreEmpresa).FontSize(14).Bold();
                    col.Item().Text($"RUC: {reporte.Ruc}").FontSize(9);
                    col.Item().Text($"REPORTE DE ACREEDORES — {reporte.Periodo}").FontSize(12).Bold();
                    col.Item().Text($"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}  |  Total a transferir: B/.{reporte.GranTotal:N2}").FontSize(9);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Column(contentCol =>
                {
                    // Tabla resumen
                    contentCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            var bg = Colors.Grey.Lighten2;
                            header.Cell().Background(bg).Padding(4).Text("Acreedor").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).Text("Tipo").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).Text("Identificación").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).Text("Banco").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).Text("N° Cuenta").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).AlignCenter().Text("Emp.").Bold().FontSize(8);
                            header.Cell().Background(bg).Padding(4).AlignRight().Text("Total").Bold().FontSize(8);
                        });

                        foreach (var acr in reporte.Acreedores)
                        {
                            table.Cell().Padding(3).Text(acr.NombreAcreedor).FontSize(8);
                            table.Cell().Padding(3).Text(acr.TipoAcreedor ?? "").FontSize(8);
                            table.Cell().Padding(3).Text(acr.Identificacion ?? "").FontSize(8);
                            table.Cell().Padding(3).Text(acr.Banco ?? "").FontSize(8);
                            table.Cell().Padding(3).Text(acr.NumeroCuenta ?? "").FontSize(8);
                            table.Cell().Padding(3).AlignCenter().Text(acr.CantidadEmpleados.ToString()).FontSize(8);
                            table.Cell().Padding(3).AlignRight().Text($"B/.{acr.TotalATransferir:N2}").Bold().FontSize(8);
                        }

                        var totBg = Colors.Yellow.Lighten3;
                        table.Cell().Background(totBg).Padding(3).Text("TOTALES").Bold().FontSize(8);
                        table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                        table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                        table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                        table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                        table.Cell().Background(totBg).Padding(3).AlignCenter().Text(reporte.TotalAcreedores.ToString()).Bold().FontSize(8);
                        table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.GranTotal:N2}").Bold().FontSize(8);
                    });

                    contentCol.Item().PaddingTop(16);

                    // Detalle por acreedor
                    foreach (var acr in reporte.Acreedores)
                    {
                        contentCol.Item().PaddingBottom(4)
                            .Text($"Acreedor: {acr.NombreAcreedor}  —  Total: B/.{acr.TotalATransferir:N2}").FontSize(10).Bold();

                        contentCol.Item().Table(detTable =>
                        {
                            detTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            detTable.Header(header =>
                            {
                                var bg = Colors.LightBlue.Medium;
                                header.Cell().Background(bg).Padding(3).Text("Empleado").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).Text("Cédula").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).Text("Tipo").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).Text("Descripción").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).AlignRight().Text("Solicitado").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).AlignRight().Text("Aplicado").Bold().FontSize(7);
                                header.Cell().Background(bg).Padding(3).Text("Razón Limitación").Bold().FontSize(7);
                            });

                            foreach (var det in acr.Empleados)
                            {
                                var rowBg = det.FueLimitado ? Colors.Orange.Lighten4 : Colors.White;
                                detTable.Cell().Background(rowBg).Padding(2).Text(det.NombreEmpleado).FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).Text(det.Cedula ?? "").FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).Text(det.TipoDeduccion).FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).Text(det.Descripcion).FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).AlignRight().Text($"B/.{det.MontoSolicitado:N2}").FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).AlignRight().Text($"B/.{det.MontoAplicado:N2}").FontSize(7);
                                detTable.Cell().Background(rowBg).Padding(2).Text(det.RazonLimitacion ?? "").FontSize(7);
                            }
                        });

                        contentCol.Item().PaddingBottom(10);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm} | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    // ====================================================================
    // REPORTE 4: SIP — Excel
    // ====================================================================

    /// <summary>Exporta el Reporte SIP para la plataforma CSS a Excel.</summary>
    public byte[] ExportarExcelSip(ReporteSipDto reporte)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("SIP CSS");

        const int cols = 10;
        AplicarEstiloEncabezado(ws, "REPORTE SIP — CAJA DE SEGURO SOCIAL", reporte.NombreEmpresa, reporte.Ruc, reporte.Periodo, cols);
        ws.Cell("A5").Value = $"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}  |  N° Planilla: {reporte.NumeroPlanilla}";

        var hr = 7;
        ws.Cell(hr, 1).Value = "Cédula";
        ws.Cell(hr, 2).Value = "Nombre";
        ws.Cell(hr, 3).Value = "Sal. Bruto";
        ws.Cell(hr, 4).Value = "Base CSS";
        ws.Cell(hr, 5).Value = "CSS Empl.";
        ws.Cell(hr, 6).Value = "CSS Patron.";
        ws.Cell(hr, 7).Value = "SE Empl.";
        ws.Cell(hr, 8).Value = "SE Patron.";
        ws.Cell(hr, 9).Value = "Riesgo Prof.";
        ws.Cell(hr, 10).Value = "Total SIP";
        AplicarEstiloHeaderFila(ws, hr, cols);

        var row = hr + 1;
        foreach (var emp in reporte.Empleados)
        {
            ws.Cell(row, 1).Value = emp.Cedula;
            ws.Cell(row, 2).Value = emp.NombreCompleto;
            ws.Cell(row, 3).Value = emp.SalarioBruto;
            ws.Cell(row, 4).Value = emp.BaseCss;
            ws.Cell(row, 5).Value = emp.CssEmpleado;
            ws.Cell(row, 6).Value = emp.CssPatronal;
            ws.Cell(row, 7).Value = emp.SeEmpleado;
            ws.Cell(row, 8).Value = emp.SePatronal;
            ws.Cell(row, 9).Value = emp.RiesgoProfesional;
            ws.Cell(row, 10).Value = emp.TotalSip;
            ws.Range(row, 3, row, 10).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws.Cell(row, 2).Value = "TOTALES";
        ws.Cell(row, 3).Value = reporte.Totales.TotalSalarios;
        ws.Cell(row, 4).Value = reporte.Totales.TotalBaseCss;
        ws.Cell(row, 5).Value = reporte.Totales.TotalCssEmpleado;
        ws.Cell(row, 6).Value = reporte.Totales.TotalCssPatronal;
        ws.Cell(row, 7).Value = reporte.Totales.TotalSeEmpleado;
        ws.Cell(row, 8).Value = reporte.Totales.TotalSePatronal;
        ws.Cell(row, 9).Value = reporte.Totales.TotalRiesgo;
        ws.Cell(row, 10).Value = reporte.Totales.GranTotalSip;
        AplicarEstiloTotales(ws, row, cols);
        ws.Range(row, 3, row, 10).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ====================================================================
    // REPORTE 4: SIP — PDF
    // ====================================================================

    /// <summary>Exporta el Reporte SIP para la CSS a PDF.</summary>
    public byte[] ExportarPdfSip(ReporteSipDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text(reporte.NombreEmpresa).FontSize(14).Bold();
                    col.Item().Text($"RUC: {reporte.Ruc}").FontSize(9);
                    col.Item().Text($"REPORTE SIP — CAJA DE SEGURO SOCIAL — {reporte.NumeroPlanilla}").FontSize(12).Bold();
                    col.Item().Text($"Período: {reporte.Periodo}  |  Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}").FontSize(9);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f); // Cédula
                        cols.RelativeColumn(3);    // Nombre
                        cols.RelativeColumn(2);    // Bruto
                        cols.RelativeColumn(2);    // Base CSS
                        cols.RelativeColumn(2);    // CSS Emp
                        cols.RelativeColumn(2);    // CSS Pat
                        cols.RelativeColumn(1.8f); // SE Emp
                        cols.RelativeColumn(1.8f); // SE Pat
                        cols.RelativeColumn(1.8f); // Riesgo
                        cols.RelativeColumn(2);    // Total SIP
                    });

                    table.Header(header =>
                    {
                        var bg = Colors.Grey.Lighten2;
                        header.Cell().Background(bg).Padding(4).Text("Cédula").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).Text("Nombre").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Sal. Bruto").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Base CSS").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("CSS Emp.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("CSS Pat.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("SE Emp.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("SE Pat.").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Riesgo").Bold().FontSize(8);
                        header.Cell().Background(bg).Padding(4).AlignRight().Text("Total SIP").Bold().FontSize(8);
                    });

                    foreach (var emp in reporte.Empleados)
                    {
                        table.Cell().Padding(3).Text(emp.Cedula).FontSize(8);
                        table.Cell().Padding(3).Text(emp.NombreCompleto).FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.SalarioBruto:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.BaseCss:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.CssEmpleado:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.CssPatronal:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.SeEmpleado:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.SePatronal:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.RiesgoProfesional:N2}").FontSize(8);
                        table.Cell().Padding(3).AlignRight().Text($"B/.{emp.TotalSip:N2}").Bold().FontSize(8);
                    }

                    var totBg = Colors.Yellow.Lighten3;
                    table.Cell().Background(totBg).Padding(3).Text("").FontSize(8);
                    table.Cell().Background(totBg).Padding(3).Text("TOTALES").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalSalarios:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalBaseCss:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalCssEmpleado:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalCssPatronal:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalSeEmpleado:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalSePatronal:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.TotalRiesgo:N2}").Bold().FontSize(8);
                    table.Cell().Background(totBg).Padding(3).AlignRight().Text($"B/.{reporte.Totales.GranTotalSip:N2}").Bold().FontSize(8);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm} | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    // ====================================================================
    // REPORTE 5: Comprobantes de Pago — PDF (una página por empleado)
    // ====================================================================

    /// <summary>
    /// Exporta los Comprobantes de Pago a PDF.
    /// Genera una página por empleado en formato Letter Portrait.
    /// Layout: encabezado empresa | datos empleado | tabla ingresos/deducciones | neto a recibir | décimo referencial.
    /// </summary>
    public byte[] ExportarPdfComprobantes(ReporteComprobantesDto reporte)
    {
        var document = Document.Create(container =>
        {
            foreach (var comp in reporte.Comprobantes)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(1.5f, Unit.Centimetre);

                    // ----------------------------------------
                    // Encabezado empresa
                    // ----------------------------------------
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(reporte.NombreEmpresa).FontSize(14).Bold();
                                c.Item().Text($"RUC: {reporte.Ruc}").FontSize(9);
                            });
                            row.ConstantItem(180).AlignRight().Column(c =>
                            {
                                c.Item().Text("COMPROBANTE DE PAGO").FontSize(12).Bold();
                                c.Item().Text($"N° Planilla: {reporte.NumeroPlanilla}").FontSize(9);
                                c.Item().Text($"Período: {reporte.Periodo}").FontSize(9);
                                c.Item().Text($"Fecha Pago: {reporte.FechaPago:dd/MM/yyyy}").FontSize(9).Bold();
                            });
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        col.Item().PaddingBottom(6);
                    });

                    // ----------------------------------------
                    // Contenido: datos empleado + tabla
                    // ----------------------------------------
                    page.Content().Column(content =>
                    {
                        // Datos del empleado
                        content.Item().Background(Colors.Grey.Lighten3).Padding(8).Row(row =>
                        {
                            row.RelativeItem(2).Column(c =>
                            {
                                c.Item().Text(comp.NombreCompleto).FontSize(13).Bold();
                                c.Item().Text($"Cédula: {comp.Cedula}").FontSize(10);
                            });
                            row.RelativeItem(1).Column(c =>
                            {
                                c.Item().Text($"Cargo: {comp.Cargo ?? "N/A"}").FontSize(9);
                                c.Item().Text($"Depto.: {comp.Departamento ?? "N/A"}").FontSize(9);
                            });
                            if (comp.TuvoLimitacion)
                            {
                                row.ConstantItem(120).AlignRight()
                                    .Text("APLICÓ LÍMITE SAL. MÍNIMO")
                                    .FontColor(Colors.Orange.Darken2).Bold().FontSize(8);
                            }
                        });

                        content.Item().PaddingTop(12);

                        // Tabla ingresos | deducciones en dos columnas
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3); // Concepto ingreso
                                cols.RelativeColumn(2); // Monto ingreso
                                cols.RelativeColumn(3); // Concepto deducción
                                cols.RelativeColumn(2); // Monto deducción
                            });

                            // Encabezado de tabla
                            table.Header(header =>
                            {
                                header.Cell().ColumnSpan(2).Background(Colors.Green.Lighten3)
                                    .Padding(5).AlignCenter().Text("INGRESOS").Bold().FontSize(10);
                                header.Cell().ColumnSpan(2).Background(Colors.Red.Lighten3)
                                    .Padding(5).AlignCenter().Text("DEDUCCIONES").Bold().FontSize(10);
                            });

                            // Salario base
                            table.Cell().Padding(4).Text("Salario Base").FontSize(9);
                            table.Cell().Padding(4).AlignRight().Text($"B/.{comp.SalarioBase:N2}").FontSize(9);
                            table.Cell().Padding(4).Text("CSS (9.75%)").FontSize(9);
                            table.Cell().Padding(4).AlignRight().Text($"B/.{comp.CssEmpleado:N2}").FontSize(9);

                            // Horas dominicales (si aplica)
                            if (comp.PagoHorasDomingo > 0)
                            {
                                table.Cell().Padding(4).Text("Pago Horas Domingo (+50%)").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{comp.PagoHorasDomingo:N2}").FontSize(9);
                            }
                            else
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                            }
                            table.Cell().Padding(4).Text("Seg. Educativo (1.25%)").FontSize(9);
                            table.Cell().Padding(4).AlignRight().Text($"B/.{comp.SeEmpleado:N2}").FontSize(9);

                            // Horas feriado (si aplica)
                            if (comp.PagoHorasFeriado > 0)
                            {
                                table.Cell().Padding(4).Text("Pago Horas Feriado (+150%)").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{comp.PagoHorasFeriado:N2}").FontSize(9);
                            }
                            else
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                            }
                            // ISR (si aplica)
                            if (comp.Isr > 0)
                            {
                                table.Cell().Padding(4).Text("I.S.R. (Imp. Renta)").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{comp.Isr:N2}").FontSize(9);
                            }
                            else
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                            }

                            // Horas extra (si aplica)
                            if (comp.PagoHorasExtra > 0)
                            {
                                table.Cell().Padding(4).Text("Pago Horas Extra").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{comp.PagoHorasExtra:N2}").FontSize(9);
                            }
                            else
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                            }
                            // Primera deducción acreedor (si aplica)
                            var primeraDeduccion = comp.DeduccionesAcreedores.FirstOrDefault();
                            if (primeraDeduccion != null)
                            {
                                table.Cell().Padding(4).Text($"{primeraDeduccion.NombreAcreedor}").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{primeraDeduccion.Monto:N2}").FontSize(9);
                            }
                            else
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                            }

                            // Acreedores adicionales (desde el segundo en adelante)
                            foreach (var ded in comp.DeduccionesAcreedores.Skip(1))
                            {
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text("").FontSize(9);
                                table.Cell().Padding(4).Text($"{ded.NombreAcreedor}").FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"B/.{ded.Monto:N2}").FontSize(9);
                            }

                            // Separador totales
                            table.Cell().ColumnSpan(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                            table.Cell().ColumnSpan(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                            // Total ingresos | Total deducciones
                            table.Cell().Background(Colors.Green.Lighten4).Padding(4)
                                .Text("TOTAL INGRESOS").Bold().FontSize(9);
                            table.Cell().Background(Colors.Green.Lighten4).Padding(4).AlignRight()
                                .Text($"B/.{comp.SalarioBruto:N2}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Red.Lighten4).Padding(4)
                                .Text("TOTAL DEDUCCIONES").Bold().FontSize(9);
                            table.Cell().Background(Colors.Red.Lighten4).Padding(4).AlignRight()
                                .Text($"B/.{comp.TotalDeducciones:N2}").Bold().FontSize(9);
                        });

                        content.Item().PaddingTop(12);

                        // Neto a recibir destacado
                        content.Item().Background(Colors.Blue.Lighten4).Padding(10).Row(row =>
                        {
                            row.RelativeItem().Text("NETO A RECIBIR:").FontSize(14).Bold();
                            row.ConstantItem(200).AlignRight().Text($"B/.{comp.SalarioNeto:N2}").FontSize(16).Bold();
                        });

                        content.Item().PaddingTop(8);

                        // Reserva décimo referencial
                        content.Item().Background(Colors.Grey.Lighten4).Padding(6).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Provisión Décimo Tercer Mes (referencial):").FontSize(9).Italic();
                                c.Item().Text("[referencial - no deducción]").FontSize(7).FontColor(Colors.Grey.Medium);
                            });
                            row.ConstantItem(120).AlignRight().Text($"B/.{comp.ReservaDecimo:N2}").FontSize(9).Italic();
                        });

                        content.Item().PaddingTop(20);

                        // Línea de firma
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                                c.Item().AlignCenter().Text("Firma del Empleado").FontSize(8);
                            });
                            row.ConstantItem(40);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                                c.Item().AlignCenter().Text("Firma RRHH").FontSize(8);
                            });
                        });
                    });

                    // Footer con número de página
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span($"Planilla SaaS | {reporte.NombreEmpresa} | Generado: {DateTime.Now:dd/MM/yyyy HH:mm} | Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            }
        });

        return document.GeneratePdf();
    }
}
