// ====================================================================
// Planilla - OvertimeClassifierTests
// Creado: 2026-06-18 — Factores de los 12 tipos de hora extra y regla de
//   las 3 horas nocturnas (Código de Trabajo Art. 30).
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Tests.Services;

public class OvertimeClassifierTests
{
    // ── Catálogo completo de 12 factores (Arts. 33, 36, 48-50) ──
    [Fact]
    public void FactorBase__Catalogo12Tipos()
    {
        OvertimeClassifier.FactorBase(TipoHoraExtra.Diurna).Should().Be(1.25m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.Nocturna).Should().Be(1.50m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.MixtaDiurnaNocturna).Should().Be(1.50m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.MixtaNocturnaDiurna).Should().Be(1.75m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.DomingoFeriado).Should().Be(1.50m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.DominicalHEDiurna).Should().Be(1.875m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.NocturnaDomingoFeriado).Should().Be(2.25m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.FeriadoOrdinario).Should().Be(2.50m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.DiaSustituto).Should().Be(1.50m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.FiestaNacionalDiurna).Should().Be(3.125m);
        OvertimeClassifier.FactorBase(TipoHoraExtra.FiestaNacionalNocturna).Should().Be(3.75m);
    }

    // ── Conteo de horas en período nocturno (6pm–6am), con cruce de medianoche ──
    [Theory]
    [InlineData(8, 12, 0)]    // toda diurna
    [InlineData(16, 20, 2)]   // 18:00–20:00 nocturnas
    [InlineData(15, 22, 4)]   // 18:00–22:00 nocturnas
    [InlineData(20, 23, 3)]   // toda nocturna
    [InlineData(22, 2, 4)]    // cruza medianoche: 22:00–02:00
    [InlineData(4, 8, 2)]     // 04:00–06:00 nocturnas
    public void HorasEnPeriodoNocturno__CuentaCorrecto(int ini, int fin, double esperado)
    {
        OvertimeClassifier.HorasEnPeriodoNocturno(TimeSpan.FromHours(ini), TimeSpan.FromHours(fin))
            .Should().Be(esperado);
    }

    // ── Clasificación de jornada regular con la regla del Art. 30 ──
    [Theory]
    [InlineData(8, 12, TipoHoraExtra.Diurna)]                  // toda diurna
    [InlineData(20, 23, TipoHoraExtra.Nocturna)]               // toda nocturna
    [InlineData(16, 20, TipoHoraExtra.MixtaDiurnaNocturna)]    // 2h nocturnas, inicia diurno
    [InlineData(4, 8, TipoHoraExtra.MixtaNocturnaDiurna)]      // 2h nocturnas, inicia nocturno
    [InlineData(15, 22, TipoHoraExtra.Nocturna)]               // 4h nocturnas (>3) ⇒ NOCTURNA (Art. 30)
    [InlineData(2, 9, TipoHoraExtra.Nocturna)]                 // 4h nocturnas (>3) ⇒ NOCTURNA
    [InlineData(22, 2, TipoHoraExtra.Nocturna)]                // cruza medianoche, toda nocturna
    public void ClasificarJornadaRegular__AplicaRegla3Horas(int ini, int fin, TipoHoraExtra esperado)
    {
        OvertimeClassifier.ClasificarJornadaRegular(TimeSpan.FromHours(ini), TimeSpan.FromHours(fin))
            .Should().Be(esperado);
    }

    [Fact]
    public void ClasificarJornadaRegular__Exactamente3HorasNocturnas__SigueMixta()
    {
        // 15:00–21:00: 3h diurnas (15-18) + 3h nocturnas (18-21). 3h NO supera el umbral → mixta.
        OvertimeClassifier.ClasificarJornadaRegular(TimeSpan.FromHours(15), TimeSpan.FromHours(21))
            .Should().Be(TipoHoraExtra.MixtaDiurnaNocturna);
    }
}
