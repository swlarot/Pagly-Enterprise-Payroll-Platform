// ====================================================================
// LectorMarcaciones: los formatos que suelen salir de un reloj ZKTeco.
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.Services;
using Xunit;

namespace Vorluno.Planilla.Application.Tests.Services;

public class LectorMarcacionesTests
{
    [Fact]
    public void ArchivoDelUsb_Attlog()
    {
        var contenido = "        12\t2026-10-05 07:02:11\t1\t0\t1\t0\n" +
                        "        12\t2026-10-05 14:41:05\t1\t1\t1\t0\n" +
                        "       007\t2026-10-05 06:58:00\t15\t0\t1\t0\n" +
                        "basura sin fecha\n";

        var r = LectorMarcaciones.LeerTexto(contenido);

        r.Formato.Should().Contain("attlog");
        r.Marcas.Should().HaveCount(3);
        r.Marcas.Select(m => m.Codigo).Should().Contain(new[] { "12", "7" }, "007 y 7 son la misma persona");
        r.Marcas.Single(m => m.Codigo == "7").FechaHora.Should().Be(new DateTime(2026, 10, 5, 6, 58, 0));
        r.Problemas.Should().ContainSingle(p => p.Linea == 4);
    }

    [Fact]
    public void Csv_ConFechaHoraEnUnaColumna_DiaAntesQueMes()
    {
        var contenido = "Reporte de asistencia\n" +
                        "\n" +
                        "AC-No.;Nombre;Fecha/Hora;Estado\n" +
                        "1;Marta Herrera;05/10/2026 07:01:00;Entrada\n" +
                        "1;Marta Herrera;05/10/2026 14:35;Salida\n" +
                        "2;Luis Batista;;Entrada\n";

        var r = LectorMarcaciones.LeerTexto(contenido);

        r.Formato.Should().Be("CSV");
        r.Columnas.Should().Contain("AC-No.").And.Contain("Fecha/Hora");
        r.Marcas.Should().HaveCount(2);
        r.Marcas[0].Nombre.Should().Be("Marta Herrera");
        r.Marcas[1].FechaHora.Should().Be(new DateTime(2026, 10, 5, 14, 35, 0), "05/10 es 5 de octubre");
        r.Problemas.Should().ContainSingle(p => p.Mensaje.Contains("persona 2"));
    }

    [Fact]
    public void Tabla_ConFechaYHoraSeparadas_YAmPmEnEspanol()
    {
        var filas = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Employee ID", "First Name", "Date", "Time" },
            new object?[] { "0031", "Ana", "2026-10-06", "7:05 a. m." },
            new object?[] { "0031", "Ana", "2026-10-06", "3:10 p. m." },
            new object?[] { 31d, "Ana", new DateTime(2026, 10, 7), new DateTime(1899, 12, 30, 7, 0, 0) },
        };

        var r = LectorMarcaciones.LeerTabla(filas, "Excel");

        r.Marcas.Should().HaveCount(3);
        r.Marcas.Select(m => m.Codigo).Distinct().Should().Equal("31");
        r.Marcas.Select(m => m.FechaHora).Should().Contain(new[]
        {
            new DateTime(2026, 10, 6, 7, 5, 0),
            new DateTime(2026, 10, 6, 15, 10, 0),
            new DateTime(2026, 10, 7, 7, 0, 0),
        });
    }

    [Fact]
    public void SinColumnasReconocibles_LoDiceSinInventar()
    {
        var r = LectorMarcaciones.LeerTexto("a,b,c\n1,2,3\n");

        r.Marcas.Should().BeEmpty();
        r.Problemas.Should().ContainSingle().Which.Mensaje.Should().Contain("No se encontraron las columnas");
    }

    [Fact]
    public void LaMismaMarcaRepetida_SeCuentaUnaVez()
    {
        var contenido = "No.,Time\n5,2026-10-05 07:00:00\n5,2026-10-05 07:00:00\n";

        LectorMarcaciones.LeerTexto(contenido).Marcas.Should().ContainSingle();
    }
}
