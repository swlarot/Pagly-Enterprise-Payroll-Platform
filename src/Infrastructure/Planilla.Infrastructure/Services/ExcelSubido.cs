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
    public const long MaximoDescomprimido = 60L * 1024 * 1024;

    /// <summary>
    /// Abre el libro, o devuelve null con el motivo: no es un .xlsx válido, o
    /// descomprimido pasa del tope.
    /// </summary>
    public static XLWorkbook? Abrir(Stream archivo, out string? problema)
    {
        problema = null;
        var copia = new MemoryStream();
        archivo.CopyTo(copia);
        copia.Position = 0;

        try
        {
            using (var zip = new ZipArchive(copia, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (zip.Entries.Sum(e => e.Length) > MaximoDescomprimido)
                {
                    problema = "demasiado grande";
                    return null;
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
