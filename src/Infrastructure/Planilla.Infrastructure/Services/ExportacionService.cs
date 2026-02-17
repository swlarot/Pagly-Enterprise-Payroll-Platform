// ====================================================================
// Planilla - ExportacionService
// Creado: 2025-12-28
// Descripción: Servicio para exportar reportes a Excel y PDF
// Usa ClosedXML para Excel y QuestPDF para PDF
// ====================================================================

using ClosedXML.Excel;
using Vorluno.Planilla.Application.DTOs.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para exportar reportes a diferentes formatos (Excel, PDF)
/// </summary>
public class ExportacionService
{
    public ExportacionService()
    {
        // Configurar licencia de QuestPDF (Community es gratuita)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    #region Excel - CSS

    /// <summary>
    /// Exporta el reporte de CSS a formato Excel
    /// </summary>
    public byte[] ExportarExcelCss(ReporteCssDto reporte)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Planilla CSS");

        // Encabezado
        worksheet.Cell("A1").Value = reporte.NombreEmpresa;
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A1").Style.Font.FontSize = 14;
        worksheet.Range("A1:H1").Merge();

        worksheet.Cell("A2").Value = $"RUC: {reporte.Ruc}";
        worksheet.Cell("A3").Value = $"Período: {reporte.Periodo}";
        worksheet.Cell("A4").Value = "REPORTE PLANILLA CSS";
        worksheet.Cell("A4").Style.Font.Bold = true;
        worksheet.Cell("A5").Value = $"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";

        // Headers de tabla
        var headerRow = 7;
        worksheet.Cell(headerRow, 1).Value = "Cédula";
        worksheet.Cell(headerRow, 2).Value = "Nombre";
        worksheet.Cell(headerRow, 3).Value = "Salario Bruto";
        worksheet.Cell(headerRow, 4).Value = "Base CSS";
        worksheet.Cell(headerRow, 5).Value = "CSS Empleado";
        worksheet.Cell(headerRow, 6).Value = "CSS Patrono";
        worksheet.Cell(headerRow, 7).Value = "Riesgo Prof.";
        worksheet.Cell(headerRow, 8).Value = "Total CSS";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Datos
        var row = headerRow + 1;
        foreach (var emp in reporte.Empleados)
        {
            worksheet.Cell(row, 1).Value = emp.Cedula;
            worksheet.Cell(row, 2).Value = emp.NombreCompleto;
            worksheet.Cell(row, 3).Value = emp.SalarioBruto;
            worksheet.Cell(row, 4).Value = emp.BaseCss;
            worksheet.Cell(row, 5).Value = emp.CssEmpleado;
            worksheet.Cell(row, 6).Value = emp.CssPatrono;
            worksheet.Cell(row, 7).Value = emp.RiesgoProfesional;
            worksheet.Cell(row, 8).Value = emp.TotalCss;

            // Formato moneda
            worksheet.Range(row, 3, row, 8).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        // Totales
        worksheet.Cell(row, 2).Value = "TOTALES";
        worksheet.Cell(row, 2).Style.Font.Bold = true;
        worksheet.Cell(row, 3).Value = reporte.Totales.TotalSalarios;
        worksheet.Cell(row, 5).Value = reporte.Totales.TotalCssEmpleado;
        worksheet.Cell(row, 6).Value = reporte.Totales.TotalCssPatrono;
        worksheet.Cell(row, 7).Value = reporte.Totales.TotalRiesgo;
        worksheet.Cell(row, 8).Value = reporte.Totales.GranTotal;

        var totalRange = worksheet.Range(row, 1, row, 8);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
        worksheet.Range(row, 3, row, 8).Style.NumberFormat.Format = "#,##0.00";

        // Ajustar anchos
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region PDF - CSS

    /// <summary>
    /// Exporta el reporte de CSS a formato PDF
    /// </summary>
    public byte[] ExportarPdfCss(ReporteCssDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(column =>
                {
                    column.Item().Text(reporte.NombreEmpresa).FontSize(16).Bold();
                    column.Item().Text($"RUC: {reporte.Ruc}").FontSize(10);
                    column.Item().Text($"PLANILLA CSS - {reporte.Periodo}").FontSize(12).Bold();
                    column.Item().PaddingBottom(10);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Cédula
                        columns.RelativeColumn(3); // Nombre
                        columns.RelativeColumn(2); // Salario
                        columns.RelativeColumn(2); // Base CSS
                        columns.RelativeColumn(2); // CSS Emp
                        columns.RelativeColumn(2); // CSS Pat
                        columns.RelativeColumn(2); // Riesgo
                        columns.RelativeColumn(2); // Total
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cédula").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Nombre").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Salario").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Base CSS").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("CSS Emp.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("CSS Pat.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Riesgo").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold();
                    });

                    // Data rows
                    foreach (var emp in reporte.Empleados)
                    {
                        table.Cell().Padding(3).Text(emp.Cedula);
                        table.Cell().Padding(3).Text(emp.NombreCompleto);
                        table.Cell().Padding(3).AlignRight().Text($"${emp.SalarioBruto:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.BaseCss:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.CssEmpleado:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.CssPatrono:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.RiesgoProfesional:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.TotalCss:N2}");
                    }

                    // Totals row
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("TOTALES").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalSalarios:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalCssEmpleado:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalCssPatrono:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalRiesgo:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.GranTotal:N2}").Bold();
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

    #endregion

    #region Excel - Seguro Educativo

    /// <summary>
    /// Exporta el reporte de Seguro Educativo a formato Excel
    /// </summary>
    public byte[] ExportarExcelSe(ReporteSeDto reporte)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Seguro Educativo");

        // Encabezado
        worksheet.Cell("A1").Value = reporte.NombreEmpresa;
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A1").Style.Font.FontSize = 14;
        worksheet.Range("A1:F1").Merge();

        worksheet.Cell("A2").Value = $"RUC: {reporte.Ruc}";
        worksheet.Cell("A3").Value = $"Período: {reporte.Periodo}";
        worksheet.Cell("A4").Value = "REPORTE SEGURO EDUCATIVO";
        worksheet.Cell("A4").Style.Font.Bold = true;

        // Headers
        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "Cédula";
        worksheet.Cell(headerRow, 2).Value = "Nombre";
        worksheet.Cell(headerRow, 3).Value = "Salario Bruto";
        worksheet.Cell(headerRow, 4).Value = "SE Empleado";
        worksheet.Cell(headerRow, 5).Value = "SE Patrono";
        worksheet.Cell(headerRow, 6).Value = "Total SE";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Datos
        var row = headerRow + 1;
        foreach (var emp in reporte.Empleados)
        {
            worksheet.Cell(row, 1).Value = emp.Cedula;
            worksheet.Cell(row, 2).Value = emp.NombreCompleto;
            worksheet.Cell(row, 3).Value = emp.SalarioBruto;
            worksheet.Cell(row, 4).Value = emp.SeEmpleado;
            worksheet.Cell(row, 5).Value = emp.SePatrono;
            worksheet.Cell(row, 6).Value = emp.TotalSe;

            worksheet.Range(row, 3, row, 6).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        // Totales
        worksheet.Cell(row, 2).Value = "TOTALES";
        worksheet.Cell(row, 2).Style.Font.Bold = true;
        worksheet.Cell(row, 3).Value = reporte.Totales.TotalSalarios;
        worksheet.Cell(row, 4).Value = reporte.Totales.TotalSeEmpleado;
        worksheet.Cell(row, 5).Value = reporte.Totales.TotalSePatrono;
        worksheet.Cell(row, 6).Value = reporte.Totales.GranTotal;

        var totalRange = worksheet.Range(row, 1, row, 6);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
        worksheet.Range(row, 3, row, 6).Style.NumberFormat.Format = "#,##0.00";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region PDF - Seguro Educativo

    public byte[] ExportarPdfSe(ReporteSeDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(column =>
                {
                    column.Item().Text(reporte.NombreEmpresa).FontSize(16).Bold();
                    column.Item().Text($"RUC: {reporte.Ruc}").FontSize(10);
                    column.Item().Text($"SEGURO EDUCATIVO - {reporte.Periodo}").FontSize(12).Bold();
                    column.Item().PaddingBottom(10);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cédula").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Nombre").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Salario").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("SE Emp.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("SE Pat.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold();
                    });

                    foreach (var emp in reporte.Empleados)
                    {
                        table.Cell().Padding(3).Text(emp.Cedula);
                        table.Cell().Padding(3).Text(emp.NombreCompleto);
                        table.Cell().Padding(3).AlignRight().Text($"${emp.SalarioBruto:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.SeEmpleado:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.SePatrono:N2}");
                        table.Cell().Padding(3).AlignRight().Text($"${emp.TotalSe:N2}");
                    }

                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("TOTALES").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalSalarios:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalSeEmpleado:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.TotalSePatrono:N2}").Bold();
                    table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.Totales.GranTotal:N2}").Bold();
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

    #endregion

    // Los métodos para ISR y Planilla Detallada siguen el mismo patrón
    // Se pueden implementar según necesidad

    #region Excel - Consolidado Acreedores

    /// <summary>
    /// Exporta el reporte consolidado de acreedores a Excel.
    /// Sheet 1: resumen por acreedor con datos bancarios.
    /// Sheet 2: detalle por acreedor listando cada empleado.
    /// </summary>
    public byte[] ExportarExcelConsolidadoAcreedor(ReporteConsolidadoAcreedorDto reporte)
    {
        using var workbook = new XLWorkbook();

        // ----------------------------------------------------------------
        // Sheet 1: Consolidado Acreedores
        // ----------------------------------------------------------------
        var ws1 = workbook.Worksheets.Add("Consolidado Acreedores");

        ws1.Cell("A1").Value = reporte.NombreEmpresa;
        ws1.Cell("A1").Style.Font.Bold = true;
        ws1.Cell("A1").Style.Font.FontSize = 14;
        ws1.Range("A1:I1").Merge();

        ws1.Cell("A2").Value = $"RUC: {reporte.Ruc}";
        ws1.Cell("A3").Value = $"Período: {reporte.Periodo}";
        ws1.Cell("A4").Value = "REPORTE CONSOLIDADO DE ACREEDORES";
        ws1.Cell("A4").Style.Font.Bold = true;
        ws1.Cell("A5").Value = $"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";

        var headerRow = 7;
        ws1.Cell(headerRow, 1).Value = "Acreedor";
        ws1.Cell(headerRow, 2).Value = "RUC/Cédula";
        ws1.Cell(headerRow, 3).Value = "Banco";
        ws1.Cell(headerRow, 4).Value = "Cuenta";
        ws1.Cell(headerRow, 5).Value = "Tipo";
        ws1.Cell(headerRow, 6).Value = "# Empleados";
        ws1.Cell(headerRow, 7).Value = "Solicitado";
        ws1.Cell(headerRow, 8).Value = "Aplicado";
        ws1.Cell(headerRow, 9).Value = "Limitado";

        var h1Range = ws1.Range(headerRow, 1, headerRow, 9);
        h1Range.Style.Font.Bold = true;
        h1Range.Style.Fill.BackgroundColor = XLColor.LightGray;
        h1Range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        h1Range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = headerRow + 1;
        foreach (var acreedor in reporte.Acreedores)
        {
            ws1.Cell(row, 1).Value = acreedor.NombreAcreedor;
            ws1.Cell(row, 2).Value = acreedor.Identificacion ?? "";
            ws1.Cell(row, 3).Value = acreedor.Banco ?? "";
            ws1.Cell(row, 4).Value = acreedor.NumeroCuenta ?? "";
            ws1.Cell(row, 5).Value = acreedor.TipoAcreedor ?? "";
            ws1.Cell(row, 6).Value = acreedor.CantidadEmpleados;
            ws1.Cell(row, 7).Value = acreedor.TotalSolicitado;
            ws1.Cell(row, 8).Value = acreedor.TotalAplicado;
            ws1.Cell(row, 9).Value = acreedor.TotalLimitado;

            ws1.Range(row, 7, row, 9).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        // Fila de TOTALES
        ws1.Cell(row, 1).Value = "TOTALES";
        ws1.Cell(row, 1).Style.Font.Bold = true;
        ws1.Cell(row, 6).Value = reporte.TotalAcreedores;
        ws1.Cell(row, 7).Value = reporte.GranTotalSolicitado;
        ws1.Cell(row, 8).Value = reporte.GranTotalAplicado;
        ws1.Cell(row, 9).Value = reporte.GranTotalLimitado;
        var totals1 = ws1.Range(row, 1, row, 9);
        totals1.Style.Font.Bold = true;
        totals1.Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws1.Range(row, 7, row, 9).Style.NumberFormat.Format = "#,##0.00";

        ws1.Columns().AdjustToContents();

        // ----------------------------------------------------------------
        // Sheet 2: Detalle por Acreedor
        // ----------------------------------------------------------------
        var ws2 = workbook.Worksheets.Add("Detalle por Acreedor");

        ws2.Cell("A1").Value = reporte.NombreEmpresa;
        ws2.Cell("A1").Style.Font.Bold = true;
        ws2.Cell("A1").Style.Font.FontSize = 14;
        ws2.Range("A1:H1").Merge();

        ws2.Cell("A2").Value = $"Período: {reporte.Periodo}  |  Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";

        var detRow = 4;
        foreach (var acreedor in reporte.Acreedores)
        {
            // Subtítulo del acreedor
            ws2.Cell(detRow, 1).Value = acreedor.NombreAcreedor;
            ws2.Cell(detRow, 1).Style.Font.Bold = true;
            ws2.Cell(detRow, 1).Style.Font.FontSize = 12;
            ws2.Cell(detRow, 6).Value = $"Total aplicado: {acreedor.TotalAplicado:N2}";
            ws2.Cell(detRow, 6).Style.Font.Bold = true;
            detRow++;

            // Encabezados de columna
            ws2.Cell(detRow, 1).Value = "Empleado";
            ws2.Cell(detRow, 2).Value = "Cédula";
            ws2.Cell(detRow, 3).Value = "Tipo";
            ws2.Cell(detRow, 4).Value = "Descripción";
            ws2.Cell(detRow, 5).Value = "Solicitado";
            ws2.Cell(detRow, 6).Value = "Aplicado";
            ws2.Cell(detRow, 7).Value = "Limitado";
            ws2.Cell(detRow, 8).Value = "Razón Limitación";

            var subHeader = ws2.Range(detRow, 1, detRow, 8);
            subHeader.Style.Font.Bold = true;
            subHeader.Style.Fill.BackgroundColor = XLColor.LightBlue;
            detRow++;

            foreach (var det in acreedor.Detalle)
            {
                ws2.Cell(detRow, 1).Value = det.EmpleadoNombre;
                ws2.Cell(detRow, 2).Value = det.EmpleadoCedula ?? "";
                ws2.Cell(detRow, 3).Value = det.TipoDeduccion;
                ws2.Cell(detRow, 4).Value = det.Descripcion;
                ws2.Cell(detRow, 5).Value = det.MontoSolicitado;
                ws2.Cell(detRow, 6).Value = det.MontoAplicado;
                ws2.Cell(detRow, 7).Value = det.MontoLimitado;
                ws2.Cell(detRow, 8).Value = det.RazonLimitacion ?? "";
                ws2.Range(detRow, 5, detRow, 7).Style.NumberFormat.Format = "#,##0.00";
                detRow++;
            }

            detRow++; // Separador entre acreedores
        }

        ws2.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region PDF - Consolidado Acreedores

    /// <summary>
    /// Exporta el reporte consolidado de acreedores a PDF usando QuestPDF.
    /// </summary>
    public byte[] ExportarPdfConsolidadoAcreedor(ReporteConsolidadoAcreedorDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(column =>
                {
                    column.Item().Text(reporte.NombreEmpresa).FontSize(16).Bold();
                    column.Item().Text($"RUC: {reporte.Ruc}").FontSize(10);
                    column.Item().Text($"CONSOLIDADO DE ACREEDORES - {reporte.Periodo}").FontSize(12).Bold();
                    column.Item().PaddingBottom(8);
                });

                page.Content().Column(contentCol =>
                {
                    // Tabla resumen
                    contentCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3); // Acreedor
                            cols.RelativeColumn(2); // RUC/Cédula
                            cols.RelativeColumn(2); // Banco
                            cols.RelativeColumn(2); // Cuenta
                            cols.RelativeColumn(1); // # Emp
                            cols.RelativeColumn(2); // Solicitado
                            cols.RelativeColumn(2); // Aplicado
                            cols.RelativeColumn(2); // Limitado
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Acreedor").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("RUC/Cédula").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Banco").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Cuenta").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignCenter().Text("# Emp.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignRight().Text("Solicitado").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignRight().Text("Aplicado").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignRight().Text("Limitado").Bold();
                        });

                        foreach (var acreedor in reporte.Acreedores)
                        {
                            table.Cell().Padding(3).Text(acreedor.NombreAcreedor);
                            table.Cell().Padding(3).Text(acreedor.Identificacion ?? "");
                            table.Cell().Padding(3).Text(acreedor.Banco ?? "");
                            table.Cell().Padding(3).Text(acreedor.NumeroCuenta ?? "");
                            table.Cell().Padding(3).AlignCenter().Text(acreedor.CantidadEmpleados.ToString());
                            table.Cell().Padding(3).AlignRight().Text($"${acreedor.TotalSolicitado:N2}");
                            table.Cell().Padding(3).AlignRight().Text($"${acreedor.TotalAplicado:N2}");
                            table.Cell().Padding(3).AlignRight().Text($"${acreedor.TotalLimitado:N2}");
                        }

                        // Fila de totales
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("TOTALES").Bold();
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).Text("");
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignCenter().Text(reporte.TotalAcreedores.ToString()).Bold();
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.GranTotalSolicitado:N2}").Bold();
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.GranTotalAplicado:N2}").Bold();
                        table.Cell().Background(Colors.Yellow.Lighten3).Padding(3).AlignRight().Text($"${reporte.GranTotalLimitado:N2}").Bold();
                    });

                    contentCol.Item().PaddingTop(16);

                    // Detalle por acreedor
                    foreach (var acreedor in reporte.Acreedores)
                    {
                        contentCol.Item().PaddingBottom(4)
                            .Text($"Acreedor: {acreedor.NombreAcreedor}").FontSize(11).Bold();

                        contentCol.Item().Table(detTable =>
                        {
                            detTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3); // Empleado
                                cols.RelativeColumn(2); // Cédula
                                cols.RelativeColumn(2); // Tipo
                                cols.RelativeColumn(3); // Descripción
                                cols.RelativeColumn(2); // Solicitado
                                cols.RelativeColumn(2); // Aplicado
                                cols.RelativeColumn(2); // Limitado
                                cols.RelativeColumn(3); // Razón
                            });

                            detTable.Header(header =>
                            {
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).Text("Empleado").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).Text("Cédula").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).Text("Tipo").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).Text("Descripción").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).AlignRight().Text("Solicitado").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).AlignRight().Text("Aplicado").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).AlignRight().Text("Limitado").Bold().FontSize(8);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(3).Text("Razón").Bold().FontSize(8);
                            });

                            foreach (var det in acreedor.Detalle)
                            {
                                detTable.Cell().Padding(2).Text(det.EmpleadoNombre).FontSize(8);
                                detTable.Cell().Padding(2).Text(det.EmpleadoCedula ?? "").FontSize(8);
                                detTable.Cell().Padding(2).Text(det.TipoDeduccion).FontSize(8);
                                detTable.Cell().Padding(2).Text(det.Descripcion).FontSize(8);
                                detTable.Cell().Padding(2).AlignRight().Text($"${det.MontoSolicitado:N2}").FontSize(8);
                                detTable.Cell().Padding(2).AlignRight().Text($"${det.MontoAplicado:N2}").FontSize(8);
                                detTable.Cell().Padding(2).AlignRight().Text($"${det.MontoLimitado:N2}").FontSize(8);
                                detTable.Cell().Padding(2).Text(det.RazonLimitacion ?? "").FontSize(8);
                            }
                        });

                        contentCol.Item().PaddingBottom(8);
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

    #endregion

    #region Excel - Deducciones por Empleado

    /// <summary>
    /// Exporta el reporte de deducciones adicionales por empleado a Excel.
    /// Genera un bloque por empleado mostrando la cascada de prelación
    /// con saldos disponibles antes/después de cada deducción.
    /// </summary>
    public byte[] ExportarExcelDeduccionesEmpleado(ReporteDeduccionesEmpleadoDto reporte)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Deducciones por Empleado");

        // Encabezado del reporte
        ws.Cell("A1").Value = reporte.NombreEmpresa;
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range("A1:J1").Merge();

        ws.Cell("A2").Value = $"RUC: {reporte.Ruc}";
        ws.Cell("A3").Value = $"Período: {reporte.Periodo}";
        ws.Cell("A4").Value = "REPORTE DE DEDUCCIONES ADICIONALES POR EMPLEADO";
        ws.Cell("A4").Style.Font.Bold = true;
        ws.Cell("A5").Value = $"Generado: {reporte.FechaGeneracion:dd/MM/yyyy HH:mm}";
        ws.Cell("A6").Value = $"Empleados con deducciones: {reporte.EmpleadosConDeducciones}  |  Con limitación: {reporte.EmpleadosConLimitacion}";

        var currentRow = 8;

        foreach (var emp in reporte.Empleados)
        {
            // Cabecera del empleado
            ws.Cell(currentRow, 1).Value = emp.Nombre;
            ws.Cell(currentRow, 1).Style.Font.Bold = true;
            ws.Cell(currentRow, 1).Style.Font.FontSize = 11;
            ws.Cell(currentRow, 4).Value = $"Cédula: {emp.Cedula ?? "N/A"}";
            ws.Cell(currentRow, 6).Value = emp.Departamento ?? "";
            ws.Cell(currentRow, 8).Value = emp.TuvoLimitacion ? "CON LIMITACION" : "";
            if (emp.TuvoLimitacion)
                ws.Cell(currentRow, 8).Style.Fill.BackgroundColor = XLColor.Orange;
            currentRow++;

            // Línea resumen financiero
            ws.Cell(currentRow, 1).Value = "Bruto";
            ws.Cell(currentRow, 2).Value = emp.SalarioBruto;
            ws.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currentRow, 3).Value = "Deduc. Legales";
            ws.Cell(currentRow, 4).Value = emp.DeduccionesLegales;
            ws.Cell(currentRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currentRow, 5).Value = "Neto Post-Legal";
            ws.Cell(currentRow, 6).Value = emp.NetoPostLegal;
            ws.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currentRow, 7).Value = "Sal. Mínimo";
            ws.Cell(currentRow, 8).Value = emp.SalarioMinimoAplicado;
            ws.Cell(currentRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(currentRow, 1, currentRow, 9).Style.Fill.BackgroundColor = XLColor.LightGray;
            currentRow++;

            // Encabezados de cascada
            ws.Cell(currentRow, 1).Value = "#";
            ws.Cell(currentRow, 2).Value = "Categoría";
            ws.Cell(currentRow, 3).Value = "Tipo";
            ws.Cell(currentRow, 4).Value = "Descripción";
            ws.Cell(currentRow, 5).Value = "Acreedor";
            ws.Cell(currentRow, 6).Value = "Solicitado";
            ws.Cell(currentRow, 7).Value = "Aplicado";
            ws.Cell(currentRow, 8).Value = "Limitado";
            ws.Cell(currentRow, 9).Value = "Saldo Después";
            ws.Cell(currentRow, 10).Value = "Razón Limitación";
            var cascadeHeader = ws.Range(currentRow, 1, currentRow, 10);
            cascadeHeader.Style.Font.Bold = true;
            cascadeHeader.Style.Fill.BackgroundColor = XLColor.LightBlue;
            currentRow++;

            foreach (var ded in emp.Deducciones)
            {
                ws.Cell(currentRow, 1).Value = ded.Orden;
                ws.Cell(currentRow, 2).Value = ded.Categoria;
                ws.Cell(currentRow, 3).Value = ded.TipoDeduccion;
                ws.Cell(currentRow, 4).Value = ded.Descripcion;
                ws.Cell(currentRow, 5).Value = ded.NombreAcreedor ?? "";
                ws.Cell(currentRow, 6).Value = ded.MontoSolicitado;
                ws.Cell(currentRow, 7).Value = ded.MontoAplicado;
                ws.Cell(currentRow, 8).Value = ded.MontoLimitado;
                ws.Cell(currentRow, 9).Value = ded.SaldoDisponibleDespues;
                ws.Cell(currentRow, 10).Value = ded.RazonLimitacion ?? "";
                ws.Range(currentRow, 6, currentRow, 9).Style.NumberFormat.Format = "#,##0.00";
                if (ded.MontoLimitado > 0)
                    ws.Cell(currentRow, 8).Style.Fill.BackgroundColor = XLColor.Orange;
                currentRow++;
            }

            // Línea total del empleado
            ws.Cell(currentRow, 4).Value = "TOTAL ADICIONALES";
            ws.Cell(currentRow, 4).Style.Font.Bold = true;
            ws.Cell(currentRow, 7).Value = emp.TotalDeduccionesAdicionales;
            ws.Cell(currentRow, 7).Style.Font.Bold = true;
            ws.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(currentRow, 9).Value = "NETO FINAL";
            ws.Cell(currentRow, 9).Style.Font.Bold = true;
            ws.Cell(currentRow, 10).Value = emp.NetPayFinal;
            ws.Cell(currentRow, 10).Style.Font.Bold = true;
            ws.Cell(currentRow, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(currentRow, 1, currentRow, 10).Style.Fill.BackgroundColor = XLColor.LightYellow;
            currentRow += 2; // Separador
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region PDF - Deducciones por Empleado

    /// <summary>
    /// Exporta el reporte de deducciones adicionales por empleado a PDF usando QuestPDF.
    /// </summary>
    public byte[] ExportarPdfDeduccionesEmpleado(ReporteDeduccionesEmpleadoDto reporte)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(column =>
                {
                    column.Item().Text(reporte.NombreEmpresa).FontSize(16).Bold();
                    column.Item().Text($"RUC: {reporte.Ruc}").FontSize(10);
                    column.Item().Text($"DEDUCCIONES ADICIONALES POR EMPLEADO - {reporte.Periodo}").FontSize(12).Bold();
                    column.Item().Text($"Empleados con deducciones: {reporte.EmpleadosConDeducciones}  |  Con limitación: {reporte.EmpleadosConLimitacion}").FontSize(9);
                    column.Item().PaddingBottom(8);
                });

                page.Content().Column(contentCol =>
                {
                    foreach (var emp in reporte.Empleados)
                    {
                        // Bloque de encabezado del empleado
                        contentCol.Item().Background(Colors.Grey.Lighten3).Padding(4).Row(row =>
                        {
                            row.RelativeItem(3).Text(emp.Nombre).Bold().FontSize(10);
                            row.RelativeItem(2).Text($"Cédula: {emp.Cedula ?? "N/A"}").FontSize(9);
                            row.RelativeItem(2).Text(emp.Departamento ?? "").FontSize(9);
                            row.RelativeItem(1).AlignRight()
                                .Text(emp.TuvoLimitacion ? "CON LIMITACION" : "")
                                .FontColor(Colors.Orange.Darken2).Bold().FontSize(9);
                        });

                        // Línea resumen financiero
                        contentCol.Item().Background(Colors.Grey.Lighten4).Padding(3).Row(row =>
                        {
                            row.RelativeItem().Text($"Bruto: ${emp.SalarioBruto:N2}").FontSize(8);
                            row.RelativeItem().Text($"Legales: ${emp.DeduccionesLegales:N2}").FontSize(8);
                            row.RelativeItem().Text($"Neto Post-Legal: ${emp.NetoPostLegal:N2}").FontSize(8);
                            row.RelativeItem().Text($"Sal. Mínimo: ${emp.SalarioMinimoAplicado:N2}").FontSize(8);
                        });

                        // Tabla de cascada de deducciones
                        contentCol.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(20); // #
                                cols.RelativeColumn(2);  // Categoría
                                cols.RelativeColumn(2);  // Tipo
                                cols.RelativeColumn(3);  // Descripción
                                cols.RelativeColumn(2);  // Acreedor
                                cols.RelativeColumn(2);  // Solicitado
                                cols.RelativeColumn(2);  // Aplicado
                                cols.RelativeColumn(2);  // Limitado
                                cols.RelativeColumn(2);  // Saldo Después
                                cols.RelativeColumn(3);  // Razón
                            });

                            t.Header(header =>
                            {
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("#").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("Categoría").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("Tipo").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("Descripción").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("Acreedor").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).AlignRight().Text("Solicitado").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).AlignRight().Text("Aplicado").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).AlignRight().Text("Limitado").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).AlignRight().Text("Saldo Dpés.").Bold().FontSize(7);
                                header.Cell().Background(Colors.LightBlue.Medium).Padding(2).Text("Razón").Bold().FontSize(7);
                            });

                            foreach (var ded in emp.Deducciones)
                            {
                                var limitedBg = ded.MontoLimitado > 0 ? Colors.Orange.Lighten4 : Colors.White;
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.Orden.ToString()).FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.Categoria).FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.TipoDeduccion).FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.Descripcion).FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.NombreAcreedor ?? "").FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).AlignRight().Text($"${ded.MontoSolicitado:N2}").FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).AlignRight().Text($"${ded.MontoAplicado:N2}").FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).AlignRight().Text($"${ded.MontoLimitado:N2}").FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).AlignRight().Text($"${ded.SaldoDisponibleDespues:N2}").FontSize(7);
                                t.Cell().Background(limitedBg).Padding(2).Text(ded.RazonLimitacion ?? "").FontSize(7);
                            }

                            // Fila total del empleado
                            t.Cell().ColumnSpan(5).Background(Colors.Yellow.Lighten3).Padding(2)
                                .AlignRight().Text("TOTAL ADICIONALES / NETO FINAL:").Bold().FontSize(7);
                            t.Cell().Background(Colors.Yellow.Lighten3).Padding(2)
                                .AlignRight().Text($"${emp.TotalDeduccionesAdicionales:N2}").Bold().FontSize(7);
                            t.Cell().Background(Colors.Yellow.Lighten3).Padding(2).Text("").FontSize(7);
                            t.Cell().Background(Colors.Yellow.Lighten3).Padding(2).Text("").FontSize(7);
                            t.Cell().Background(Colors.Yellow.Lighten3).Padding(2)
                                .AlignRight().Text($"${emp.NetPayFinal:N2}").Bold().FontSize(7);
                            t.Cell().Background(Colors.Yellow.Lighten3).Padding(2).Text("").FontSize(7);
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

    #endregion
}
