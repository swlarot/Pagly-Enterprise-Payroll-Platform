using System.Security.Cryptography;
using System.Text;

namespace Vorluno.Planilla.Application.Security;

/// <summary>
/// Utilidades pure-function para parsear, generar y hashear API keys del
/// API Platform B2B. Diseñado para ser testeable sin dependencias de EF/DB.
///
/// Formato Stripe-like: <c>pk_{mode}_{prefix8}{secret32}</c> (hex lowercase).
/// </summary>
public static class ApiKeyFormat
{
    public const int PrefixLength = 8;
    public const int SecretLength = 32;
    public const string PublicPrefix = "pk_";

    /// <summary>
    /// Parsea una key plaintext en sus componentes. Devuelve <c>false</c>
    /// sin throw si cualquier validación falla (caso común en auth handler).
    /// </summary>
    public static bool TryParse(string plaintext, out string mode, out string prefix, out string secret)
    {
        mode = string.Empty;
        prefix = string.Empty;
        secret = string.Empty;

        if (string.IsNullOrWhiteSpace(plaintext)) return false;
        if (!plaintext.StartsWith(PublicPrefix, StringComparison.Ordinal)) return false;

        var rest = plaintext.Substring(PublicPrefix.Length);
        var separatorIdx = rest.IndexOf('_');
        if (separatorIdx <= 0) return false;

        var modePart = rest.Substring(0, separatorIdx);
        var keyBody = rest.Substring(separatorIdx + 1);

        if (modePart != "test" && modePart != "live") return false;
        if (keyBody.Length != PrefixLength + SecretLength) return false;

        foreach (var c in keyBody)
        {
            if (!IsHexChar(c)) return false;
        }

        mode = char.ToUpperInvariant(modePart[0]) + modePart.Substring(1); // "Test" / "Live"
        prefix = keyBody.Substring(0, PrefixLength);
        secret = keyBody.Substring(PrefixLength);
        return true;
    }

    /// <summary>
    /// Genera un secret hex aleatorio criptográficamente fuerte.
    /// </summary>
    public static string GenerateRandomHex(int length)
    {
        var byteCount = (length + 1) / 2;
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex.Substring(0, length);
    }

    /// <summary>
    /// SHA256 del input en hex lowercase (64 chars).
    /// </summary>
    public static string ComputeSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Comparación constant-time de dos strings ASCII. Usa
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> internamente.
    /// Inmune a timing attacks.
    /// </summary>
    public static bool FixedTimeEquals(string a, string b)
    {
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;

        var ba = Encoding.ASCII.GetBytes(a);
        var bb = Encoding.ASCII.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    /// <summary>
    /// Compone la representación plaintext pública de una key.
    /// </summary>
    public static string Compose(string mode, string prefix, string secret)
    {
        return $"{PublicPrefix}{mode.ToLowerInvariant()}_{prefix}{secret}";
    }

    private static bool IsHexChar(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
