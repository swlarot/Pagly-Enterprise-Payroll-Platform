// ====================================================================
// Tests de ImportacionEmpleadosValidator: una regla, un caso.
// ====================================================================

using FluentAssertions;
using Vorluno.Planilla.Application.DTOs.Importacion;
using Vorluno.Planilla.Application.Services;

namespace Vorluno.Planilla.Application.Tests.Services;

public class ImportacionEmpleadosValidatorTests
{
    private static readonly DateTime Hoy = new(2026, 9, 21);

    private static FilaImportacionDto FilaBuena() => new()
    {
        Fila = 2,
        Cedula = "8-809-1415",
        Nombre = "Belisa",
        Apellido = "Belloso",
        SalarioBase = 700.96m,
        FechaContratacion = new DateTime(2013, 6, 1),
        TipoPeriodo = "Quincenal",
        Meses = Enumerable.Range(0, 60)
            .Select(i => new DateTime(2021, 10, 1).AddMonths(i))
            .Select(d => new MesImportadoDto { Anio = d.Year, Mes = d.Month, Monto = 600m })
            .ToList()
    };

    private static FilaValidadaDto Validar(FilaImportacionDto f, params string[] existentes)
        => ImportacionEmpleadosValidator.Validar(new[] { f }, existentes.ToHashSet(), Hoy)[0];

    private static string[] Errores(FilaValidadaDto v) => v.Problemas.Where(p => p.Tipo == TipoProblema.Error).Select(p => p.Campo).ToArray();
    private static string[] Avisos(FilaValidadaDto v) => v.Problemas.Where(p => p.Tipo == TipoProblema.Aviso).Select(p => p.Campo).ToArray();

    [Fact]
    public void FilaCompleta__SinProblemas()
    {
        var v = Validar(FilaBuena());
        v.Problemas.Should().BeEmpty();
    }

    [Theory]
    [InlineData("8-809-1415", true)]
    [InlineData("PE-12-345", true)]
    [InlineData("E-8-123456", true)]
    [InlineData("N-20-1234", true)]
    [InlineData("8AV-1-2", true)]
    [InlineData("AB123456", true)]       // pasaporte
    [InlineData(" 8-809-1415 ", true)]   // se normaliza
    [InlineData("8-809", false)]
    [InlineData("hola", false)]
    [InlineData("", false)]
    public void Cedula__FormatoPanamenoOPasaporte(string cedula, bool valida)
    {
        var f = FilaBuena(); f.Cedula = cedula;
        var v = Validar(f);
        (Errores(v).Contains("cedula")).Should().Be(!valida);
    }

    [Fact]
    public void Cedula__RepetidaEnElArchivo_EsError()
    {
        var a = FilaBuena(); var b = FilaBuena(); b.Nombre = "Otra";
        var r = ImportacionEmpleadosValidator.Validar(new[] { a, b }, new HashSet<string>(), Hoy);
        r.Should().OnlyContain(v => v.Problemas.Any(p => p.Campo == "cedula" && p.Tipo == TipoProblema.Error));
    }

    [Fact]
    public void Cedula__YaExisteEnLaEmpresa_EsAvisoYMarcaActualizacion()
    {
        var v = Validar(FilaBuena(), "8-809-1415");
        Avisos(v).Should().Contain("cedula");
        Errores(v).Should().BeEmpty();
        v.Datos.YaExiste.Should().BeTrue();
    }

    [Fact]
    public void NombreYApellido__Obligatorios()
    {
        var f = FilaBuena(); f.Nombre = " "; f.Apellido = "";
        Errores(Validar(f)).Should().Contain(new[] { "nombre", "apellido" });
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("-5", true)]
    [InlineData("700.96", false)]
    public void Salario__DebeSerPositivo(string salario, bool esError)
    {
        var f = FilaBuena();
        f.SalarioBase = salario == "" ? null : decimal.Parse(salario, System.Globalization.CultureInfo.InvariantCulture);
        Errores(Validar(f)).Contains("salarioBase").Should().Be(esError);
    }

    [Fact]
    public void Salario__MuyAlto_EsAviso()
    {
        var f = FilaBuena(); f.SalarioBase = 150_000m;
        Avisos(Validar(f)).Should().Contain("salarioBase");
    }

    [Fact]
    public void FechaContratacion__FaltanteOFutura_EsError()
    {
        var f = FilaBuena(); f.FechaContratacion = null;
        Errores(Validar(f)).Should().Contain("fechaContratacion");

        f = FilaBuena(); f.FechaContratacion = Hoy.AddDays(1);
        Errores(Validar(f)).Should().Contain("fechaContratacion");
    }

    [Fact]
    public void TipoPeriodo__FueraDeLista_EsError_YSeNormalizaSiCoincide()
    {
        var f = FilaBuena(); f.TipoPeriodo = "quincenal";
        var v = Validar(f);
        Errores(v).Should().NotContain("tipoPeriodo");
        v.Datos.TipoPeriodo.Should().Be("Quincenal");

        f = FilaBuena(); f.TipoPeriodo = "Diario";
        Errores(Validar(f)).Should().Contain("tipoPeriodo");
    }

    [Fact]
    public void TipoContrato__VacioEsIndefinido_YPorObraAceptaEspacio()
    {
        var f = FilaBuena(); f.TipoContrato = null;
        Validar(f).Datos.TipoContrato.Should().Be("Indefinido");

        f = FilaBuena(); f.TipoContrato = "Por Obra";
        Validar(f).Datos.TipoContrato.Should().Be("PorObra");
    }

    [Fact]
    public void RiesgoProfesional__SoloLasCincoClases()
    {
        var f = FilaBuena(); f.RiesgoProfesional = 2.50m;
        Errores(Validar(f)).Should().Contain("riesgoProfesional");

        f = FilaBuena(); f.RiesgoProfesional = 2.10m;
        Errores(Validar(f)).Should().NotContain("riesgoProfesional");
    }

    [Fact]
    public void GastoRepresentacion__NoPuedeSuperarElSalario()
    {
        var f = FilaBuena(); f.GastoRepresentacionMensual = 800m;
        Errores(Validar(f)).Should().Contain("gastoRepresentacionMensual");
    }

    [Fact]
    public void Meses__NegativoEsError_AntesDeContratacionEsAviso()
    {
        var f = FilaBuena();
        f.Meses[3].Monto = -1m;
        Errores(Validar(f)).Should().Contain("meses");

        f = FilaBuena();
        f.FechaContratacion = new DateTime(2024, 1, 1);   // pero trae salarios desde 2021
        var v = Validar(f);
        Avisos(v).Should().Contain("meses");
        v.Problemas.Single(p => p.Campo == "meses").Mensaje.Should().Contain("antes de la fecha de contratación");
    }

    [Fact]
    public void Meses__HuecosDesdeLaContratacion_EsAvisoConLaCuenta()
    {
        var f = FilaBuena();
        // Quitar julio y agosto 2026: quedan huecos hasta agosto (el mes de hoy no cuenta).
        f.Meses.RemoveAll(m => m.Anio == 2026 && (m.Mes == 7 || m.Mes == 8));
        var v = Validar(f);
        var aviso = v.Problemas.Single(p => p.Campo == "meses");
        aviso.Tipo.Should().Be(TipoProblema.Aviso);
        aviso.Mensaje.Should().Contain("Faltan 2 mes");
    }

    [Fact]
    public void Meses__SinNinguno_EsAvisoDeQueNoHayConQueCalcular()
    {
        var f = FilaBuena(); f.Meses.Clear();
        Validar(f).Problemas.Single(p => p.Campo == "meses").Mensaje.Should().Contain("No trae salarios históricos");
    }

    [Fact]
    public void Saldos__NegativosOPartidasFueraDeRango_EsError()
    {
        var f = FilaBuena();
        f.Saldos = new SaldosRentaImportadosDto { Anio = 2026, IsrRetenido = -1m, PartidasDecimo = 4 };
        Errores(Validar(f)).Should().Contain("saldos");
    }

    [Fact]
    public void Saldos__DecimoPagadoSinPartidas_EsAviso()
    {
        var f = FilaBuena();
        f.Saldos = new SaldosRentaImportadosDto { Anio = 2026, DecimoPagado = 200m, PartidasDecimo = 0 };
        Avisos(Validar(f)).Should().Contain("saldos");
    }

    [Fact]
    public void UnErrorNoContaminaOtraFila()
    {
        var mala = FilaBuena(); mala.Cedula = "??";
        var buena = FilaBuena(); buena.Cedula = "4-111-2222";
        var r = ImportacionEmpleadosValidator.Validar(new[] { mala, buena }, new HashSet<string>(), Hoy);
        r[0].TieneErrores.Should().BeTrue();
        r[1].TieneErrores.Should().BeFalse();
    }
}
