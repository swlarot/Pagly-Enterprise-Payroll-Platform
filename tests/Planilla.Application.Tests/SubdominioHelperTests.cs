using FluentAssertions;
using Vorluno.Planilla.Application.Helpers;
using Xunit;

namespace Vorluno.Planilla.Application.Tests;

public class SubdominioHelperTests
{
    [Theory]
    [InlineData("Colegio Armonía (prueba)", "colegio-armonia")]
    [InlineData("P.H. PLAZA VALENCIA", "p-h-plaza-valencia")]
    [InlineData("Cáceres López Auditores", "caceres-lopez")]
    [InlineData("Peña & Cedeño, S.A.", "pena-cedeno-s-a")]
    [InlineData("Supercalifragilisticoespialidoso", "supercalifragilistic")]
    [InlineData("  ---  ", "empresa")]
    [InlineData(null, "empresa")]
    public void SoloLetrasSinTildesDigitosYGuiones(string? nombre, string esperado)
    {
        var base_ = SubdominioHelper.Base(nombre);

        base_.Should().Be(esperado);
        base_.Should().MatchRegex("^[a-z0-9]+(-[a-z0-9]+)*$");
        base_.Length.Should().BeLessThanOrEqualTo(SubdominioHelper.Largo);
    }
}
