using FluentAssertions;
using Vorluno.Planilla.Application.Security;

namespace Vorluno.Planilla.Application.Tests.Security;

public class ApiKeyFormatTests
{
    // ====================================================================
    // TryParse
    // ====================================================================

    [Fact]
    public void TryParse_WellFormedLiveKey_ReturnsTrue()
    {
        var plaintext = "pk_live_abcdef01" + new string('1', 32);

        var ok = ApiKeyFormat.TryParse(plaintext, out var mode, out var prefix, out var secret);

        ok.Should().BeTrue();
        mode.Should().Be("Live");
        prefix.Should().Be("abcdef01");
        secret.Should().HaveLength(32);
    }

    [Fact]
    public void TryParse_WellFormedTestKey_ReturnsTrue()
    {
        var plaintext = "pk_test_12345678" + new string('a', 32);

        var ok = ApiKeyFormat.TryParse(plaintext, out var mode, out var prefix, out var secret);

        ok.Should().BeTrue();
        mode.Should().Be("Test");
        prefix.Should().Be("12345678");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not_a_key_at_all")]
    [InlineData("sk_live_abc12345abcdefabcdefabcdefabcdefab")] // wrong public prefix (sk vs pk)
    [InlineData("pk_prod_abc12345abcdefabcdefabcdefabcdefab")] // invalid mode
    [InlineData("pk_live_abc12345")]                            // too short
    [InlineData("pk_live_")]                                    // no body
    [InlineData("pk_livex12345678abcdefabcdefabcdefabcdefab")]  // missing underscore separator
    public void TryParse_Malformed_ReturnsFalse(string plaintext)
    {
        var ok = ApiKeyFormat.TryParse(plaintext, out _, out _, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParse_NonHexCharInBody_ReturnsFalse()
    {
        // 'z' no es hex
        var plaintext = "pk_live_abcdefgh" + new string('1', 32);
        var ok = ApiKeyFormat.TryParse(plaintext, out _, out _, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParse_UppercaseMode_Rejected()
    {
        // El formato canónico es lowercase en la parte del mode.
        var plaintext = "pk_LIVE_abcdef01" + new string('1', 32);
        var ok = ApiKeyFormat.TryParse(plaintext, out _, out _, out _);
        ok.Should().BeFalse();
    }

    // ====================================================================
    // Compose <-> TryParse roundtrip
    // ====================================================================

    [Fact]
    public void Compose_ThenTryParse_Roundtrip()
    {
        var prefix = ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.PrefixLength);
        var secret = ApiKeyFormat.GenerateRandomHex(ApiKeyFormat.SecretLength);
        var plaintext = ApiKeyFormat.Compose("Live", prefix, secret);

        ApiKeyFormat.TryParse(plaintext, out var mode, out var parsedPrefix, out var parsedSecret).Should().BeTrue();
        mode.Should().Be("Live");
        parsedPrefix.Should().Be(prefix);
        parsedSecret.Should().Be(secret);
    }

    // ====================================================================
    // GenerateRandomHex
    // ====================================================================

    [Fact]
    public void GenerateRandomHex_ReturnsRequestedLength()
    {
        ApiKeyFormat.GenerateRandomHex(8).Should().HaveLength(8);
        ApiKeyFormat.GenerateRandomHex(32).Should().HaveLength(32);
    }

    [Fact]
    public void GenerateRandomHex_ProducesDifferentValues()
    {
        var a = ApiKeyFormat.GenerateRandomHex(32);
        var b = ApiKeyFormat.GenerateRandomHex(32);
        a.Should().NotBe(b, "dos llamadas consecutivas deben producir valores distintos");
    }

    [Fact]
    public void GenerateRandomHex_OnlyHexChars()
    {
        var value = ApiKeyFormat.GenerateRandomHex(64);
        value.Should().MatchRegex("^[0-9a-f]+$");
    }

    // ====================================================================
    // ComputeSha256Hex
    // ====================================================================

    [Fact]
    public void ComputeSha256Hex_KnownVector()
    {
        // "" (empty string) → SHA256 conocido
        ApiKeyFormat.ComputeSha256Hex("")
            .Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void ComputeSha256Hex_DifferentInputs_DifferentOutputs()
    {
        var a = ApiKeyFormat.ComputeSha256Hex("password1");
        var b = ApiKeyFormat.ComputeSha256Hex("password2");
        a.Should().NotBe(b);
        a.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeSha256Hex_SameInput_SameOutput()
    {
        var a = ApiKeyFormat.ComputeSha256Hex("deterministic");
        var b = ApiKeyFormat.ComputeSha256Hex("deterministic");
        a.Should().Be(b);
    }

    // ====================================================================
    // FixedTimeEquals
    // ====================================================================

    [Fact]
    public void FixedTimeEquals_SameStrings_ReturnsTrue()
    {
        ApiKeyFormat.FixedTimeEquals("abc123", "abc123").Should().BeTrue();
    }

    [Fact]
    public void FixedTimeEquals_DifferentStrings_ReturnsFalse()
    {
        ApiKeyFormat.FixedTimeEquals("abc123", "abc124").Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_DifferentLengths_ReturnsFalse()
    {
        ApiKeyFormat.FixedTimeEquals("abc", "abcd").Should().BeFalse();
    }

    [Fact]
    public void FixedTimeEquals_EmptyStrings_ReturnsTrue()
    {
        ApiKeyFormat.FixedTimeEquals("", "").Should().BeTrue();
    }

    [Fact]
    public void FixedTimeEquals_NullInputs_ReturnsFalse()
    {
        ApiKeyFormat.FixedTimeEquals(null!, "abc").Should().BeFalse();
        ApiKeyFormat.FixedTimeEquals("abc", null!).Should().BeFalse();
        ApiKeyFormat.FixedTimeEquals(null!, null!).Should().BeFalse();
    }
}
