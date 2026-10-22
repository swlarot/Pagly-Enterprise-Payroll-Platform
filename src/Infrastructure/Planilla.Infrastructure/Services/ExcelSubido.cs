// ====================================================================
// Abrir un Excel que subió una persona sin arriesgar la memoria: un .xlsx
// es un zip, y uno de pocos MB puede descomprimirse en cientos. Antes de
// abrirlo se suma el tamaño real de su contenido.
// ====================================================================

using System.IO.Compression;
using ClosedXML.Excel;

namespace Vorluno.Planilla.Infrastructure.Services;

public static class ExcelSubido
{
    /// <summary>Tope del contenido ya descomprimido.</summary>
    public const long MaximoDescomprimido = 30L * 1024 * 1024;

    /// <summary>Tope del archivo tal como llega.</summary>
    public const long MaximoArchivo = 12L * 1024 * 1024;

    /// <summary>
    /// Abre el libro, o devuelve null con el motivo: no es un .xlsx válido, o
    /// descomprimido pasa del tope.
    /// </summary>
    public static XLWorkbook? Abrir(Stream archivo, out string? problema)
    {
        problema = null;

        // Copia con tope: nunca más de MaximoArchivo en memoria.
        var copia = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = archivo.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (copia.Length + leidos > MaximoArchivo)
            {
                problema = "demasiado grande";
                return null;
            }
            copia.Write(buffer, 0, leidos);
        }
        copia.Position = 0;

        try
        {
            // El tamaño que declara el zip se puede falsificar: se descomprime
            // contando bytes y se corta en cuanto pasa del tope.
            using (var zip = new ZipArchive(copia, ZipArchiveMode.Read, leaveOpen: true))
            {
                long total = 0;
                foreach (var entrada in zip.Entries)
                {
                    using var s = entrada.Open();
                    while ((leidos = s.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += leidos;
                        if (total > MaximoDescomprimido)
                        {
                            problema = "demasiado grande";
                            return null;
                        }
                    }
                }
            }
            copia.Position = 0;
            return new XLWorkbook(copia);
        }
        catch (Exception)
        {
            problema = "no válido";
            return null;
        }
    }
}
