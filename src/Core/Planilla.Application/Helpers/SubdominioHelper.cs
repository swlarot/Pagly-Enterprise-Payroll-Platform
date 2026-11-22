using System.Globalization;
using System.Text;

namespace Vorluno.Planilla.Application.Helpers;

/// <summary>
/// Convierte el nombre de una empresa en la base de su subdominio: solo
/// letras a-z, dígitos y guiones, sin tildes ni «ñ» (no son válidas en una
/// dirección web). «Colegio Armonía (prueba)» → «colegio-armonia-prueba».
/// </summary>
public static class SubdominioHelper
{
    public const int Largo = 20;

    public static string Base(string? nombre)
    {
        var sinTildes = (nombre ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in sinTildes)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var l = char.ToLowerInvariant(c);
            if (l is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(l);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }

        var base_ = sb.ToString().Trim('-');
        if (base_.Length > Largo)
        {
            // Se corta en el último guion para no partir una palabra.
            var corte = base_[Largo] == '-' ? Largo : base_.LastIndexOf('-', Largo - 1);
            base_ = (corte > 0 ? base_[..corte] : base_[..Largo]).TrimEnd('-');
        }
        return base_.Length == 0 ? "empresa" : base_;
    }
}
