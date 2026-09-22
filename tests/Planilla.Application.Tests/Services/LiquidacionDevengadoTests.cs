// ====================================================================
// La liquidación como la hoja del contador
//
// Caso real: Belisa Belloso, contratada el 01/06/2013 y liquidada el
// 30/09/2026 por mutuo acuerdo (13.33 años). La hoja
// «2025-06-GRACIELITA-03-LIQUIDACION-BELISA BELLOSO.xls» da:
//   Prima          (35,221.58 + 449.97) ÷ 260 × 13.33 = 1,828.78
//   Indemnización  37.33 semanas × (700.96 ÷ 4.3333)  = 6,038.46
//   Vacaciones     4,949.70 ÷ 11                      =   449.97
//   Décimo         (1,320.20 + 449.97) ÷ 12           =   147.51
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Xunit;

namespace Vorluno.Planilla.Application.Tests.Services;

public class LiquidacionDevengadoTests
{
    private static readonly DateTime Contratacion = new(2013, 6, 1);
    private static readonly DateTime Terminacion = new(2026, 9, 30);

    /// <summary>Los 60 meses de la hoja suman 35,221.58; aquí se reparten parejo salvo el último.</summary>
    private static BasesDevengadasLiquidacion BasesDeLaHoja()
    {
        const decimal total60 = 35_221.58m;
        var meses = new List<MesLiquidacion>();
        var cursor = new DateTime(2021, 10, 1);
        var parejo = Math.Round(total60 / 60m, 2);
        decimal acumulado = 0m;
        for (var i = 0; i < 60; i++)
        {
            var monto = i < 59 ? parejo : total60 - acumulado;
            acumulado += monto;
            meses.Add(new MesLiquidacion(cursor.Year, cursor.Month, monto, "Importado"));
            cursor = cursor.AddMonths(1);
        }

        return new BasesDevengadasLiquidacion
        {
            Meses60 = meses,
            Devengado6Meses = 614.18m * 6m,      // promedio de 6 meses de la hoja
            Meses6ConDatos = 6,
            UltimoMesDevengado = 700.96m,        // último salario, más favorable
            DevengadoDesdeUltimaVacacion = 4_949.70m,
            VacacionesDesde = new DateTime(2026, 2, 1),
            DevengadoDesdeUltimaPartidaDecimo = 1_320.20m,
            DecimoDesde = new DateTime(2026, 9, 1),
        };
    }

    private static Empleado Belisa() => new()
    {
        Id = 1, TenantId = 1, Nombre = "Belisa", Apellido = "Belloso", NumeroIdentificacion = "8-809-1415",
        SalarioBase = 700.96m, FechaContratacion = Contratacion,
        TipoContrato = TipoContratoDuracion.Indefinido,
        IsSubjectToEducationalInsurance = true, IsSubjectToIncomeTax = true,
    };

    private static LiquidacionCalculationResult Calcular(decimal diasSalarioPendiente = 0m)
        => new LiquidacionCalculationService().Calcular(
            Belisa(),
            new CreateLiquidacionRequest(
                EmpleadoId: 1,
                FechaTerminacion: Terminacion,
                TipoTerminacion: TipoTerminacion.MutuoAcuerdo,
                DiasSalarioPendiente: diasSalarioPendiente),
            ultimaFechaVacaciones: null,
            bases: BasesDeLaHoja());

    [Fact]
    public void LasCuatroPartidasCoincidenConLaHoja()
    {
        var r = Calcular();

        r.AnosServicio.Should().BeApproximately(13.33m, 0.01m);
        r.VacacionesProporcionales.Should().Be(449.97m, "4,949.70 ÷ 11");
        // 35,671.55 ÷ 260 = 137.1983 por semana. La hoja redondea el semanal
        // (137.20) y los años (13.33), así que imprime 1,828.78; el motor no
        // redondea en el camino y usa los años exactos (13.3306): centavos de
        // diferencia sobre la misma fórmula.
        LiquidacionCalculator.PrimaDesdeDevengado(35_221.58m, 449.97m, 60, 13.33m)
            .Should().Be(1_828.85m, "137.1983 × 13.33 sin redondeos intermedios");
        r.PrimaAntiguedad.Should().BeApproximately(1_828.78m, 0.25m, "(35,221.58 + 449.97) ÷ 260 × años reales");
        // Misma fórmula que la hoja; las semanas exactas (37.3306) frente a las
        // 37.33 redondeadas explican los centavos.
        (37.33m * LiquidacionCalculator.SemanalIndemnizacionDesdeDevengado(614.18m * 6m, 6, 700.96m))
            .Should().BeApproximately(6_038.46m, 0.06m, "37.33 × 161.7606: la hoja redondea el semanal a 161.76");
        r.Indemnizacion.Should().BeApproximately(6_038.46m, 0.25m, "semanas exactas × 161.76");
        r.IndemnizacionSemanas.Should().BeApproximately(37.33m, 0.01m);
        r.DecimoTercerMesProporcional.Should().Be(147.51m, "(1,320.20 + 449.97) ÷ 12");
    }

    [Fact]
    public void ElSalarioSemanalDeLaIndemnizacionEsElMasFavorable()
    {
        var r = Calcular();
        r.SalarioSemanal.Should().BeApproximately(161.76m, 0.01m, "700.96 ÷ 4.3333 gana al promedio de 6 meses");
    }

    [Fact]
    public void PrimaIndemnizacionYPreavisoNoCotizan__PeroVacacionesDecimoYSalariosVencidosSi()
    {
        var r = Calcular(diasSalarioPendiente: 30m);

        r.SalarioPendiente.Should().Be(700.96m, "30 días al salario diario");
        var baseCotizable = r.VacacionesProporcionales + r.SalarioPendiente;
        r.CssEmpleado.Should().Be(
            Math.Round(baseCotizable * 0.0975m + r.DecimoTercerMesProporcional * 0.0725m, 2),
            "las vacaciones y los salarios vencidos al 9.75 %, el décimo al 7.25 %");
        r.SeEmpleado.Should().Be(
            Math.Round((baseCotizable + r.DecimoTercerMesProporcional) * 0.0125m, 2));

        // Prima e indemnización entran al bruto pero no a la base de cuotas.
        r.TotalBruto.Should().BeApproximately(
            r.PrimaAntiguedad + r.Indemnizacion + r.VacacionesProporcionales + r.DecimoTercerMesProporcional + r.SalarioPendiente,
            0.02m);
        r.TotalNeto.Should().Be(Math.Round(r.TotalBruto - r.TotalDeducciones, 2));
    }

    [Fact]
    public void SinBasesDevengadas__SeMantieneElCalculoPorSalarioBase()
    {
        var r = new LiquidacionCalculationService().Calcular(
            Belisa(),
            new CreateLiquidacionRequest(1, Terminacion, TipoTerminacion.MutuoAcuerdo));

        r.PrimaAntiguedad.Should().BeGreaterThan(0m, "sin historial se usa el salario base, pero se sigue liquidando");
    }

    [Theory]
    [InlineData(60, "260.0000")]
    [InlineData(12, "52.0000")]
    [InlineData(8, "34.6667")]
    public void LasSemanasDeUnPeriodoSalenDe52Entre12(int meses, string esperadas)
    {
        Math.Round(LiquidacionCalculator.SemanasDeMeses(meses), 4)
            .Should().Be(decimal.Parse(esperadas, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConMenosDe60MesesSeDivideEntreLasSemanasQueHay()
    {
        // 12 meses de 1,000 = 12,000 ÷ 52 semanas = 230.77 por semana × 1 año.
        LiquidacionCalculator.PrimaDesdeDevengado(12_000m, vacacionesProporcionales: 0m, mesesConDatos: 12, yearsWorked: 1m)
            .Should().Be(230.77m);
    }
}
