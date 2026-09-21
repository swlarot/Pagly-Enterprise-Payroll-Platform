// ====================================================================
// Planilla - ImportacionEmpleadosValidator
// Todas las reglas de la importación de empleados, sin base de datos.
// La pantalla aplica las mismas reglas en el cliente (utils/validacionImportacion.ts)
// para corregir en tiempo real; esta es la versión que manda al confirmar.
//
// Un ERROR impide importar esa fila. Un AVISO deja pasar pero señala algo
// que el usuario debería mirar. Nunca se bloquea el archivo entero por una
// fila: cada empleado se decide por separado.
// ====================================================================

using System.Text.RegularExpressions;
using Vorluno.Planilla.Application.DTOs.Importacion;

namespace Vorluno.Planilla.Application.Services;

public static class ImportacionEmpleadosValidator
{
    public static readonly string[] TiposPeriodo = { "Semanal", "Bisemanal", "Quincenal", "Mensual" };
    public static readonly string[] TiposContrato = { "Indefinido", "Definido", "PorObra" };

    /// <summary>Las cinco clases de riesgo profesional del Acuerdo N°2 de 1995.</summary>
    public static readonly decimal[] ClasesRiesgo = { 0.56m, 0.98m, 2.10m, 3.64m, 5.67m };

    // Cédula panameña: provincia (1-13, o PE/E/N/NT) - tomo - asiento. Se aceptan también
    // pasaportes (letras y números, 6 a 20) para extranjeros sin cédula.
    private static readonly Regex CedulaPanama = new(
        @"^(?:\d{1,2}|PE|E|N|NT|\d{1,2}AV|\d{1,2}PI)-\d{1,5}-\d{1,7}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Pasaporte = new(@"^[A-Z0-9]{6,20}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>
    /// Valida todas las filas. <paramref name="cedulasExistentes"/> son las que ya están
    /// en la empresa (para avisar que se actualizarán, no crearán).
    /// </summary>
    public static List<FilaValidadaDto> Validar(
        IReadOnlyList<FilaImportacionDto> filas,
        ISet<string> cedulasExistentes,
        DateTime hoy)
    {
        var repetidasEnArchivo = filas
            .Select(f => NormalizarCedula(f.Cedula))
            .Where(c => c.Length > 0)
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        return filas.Select(f => ValidarFila(f, cedulasExistentes, repetidasEnArchivo, hoy)).ToList();
    }

    public static FilaValidadaDto ValidarFila(
        FilaImportacionDto f,
        ISet<string> cedulasExistentes,
        ISet<string> repetidasEnArchivo,
        DateTime hoy)
    {
        var p = new List<ProblemaImportacionDto>();
        void Error(string campo, string msg) => p.Add(new() { Campo = campo, Tipo = TipoProblema.Error, Mensaje = msg });
        void Aviso(string campo, string msg) => p.Add(new() { Campo = campo, Tipo = TipoProblema.Aviso, Mensaje = msg });

        // ── Cédula ──
        var cedula = NormalizarCedula(f.Cedula);
        f.Cedula = cedula;
        if (cedula.Length == 0)
            Error("cedula", "Falta la cédula.");
        else if (!CedulaPanama.IsMatch(cedula) && !Pasaporte.IsMatch(cedula))
            Error("cedula", "La cédula no tiene un formato válido (ej. 8-123-4567, PE-12-345 o un pasaporte).");
        else if (repetidasEnArchivo.Contains(cedula))
            Error("cedula", "Esta cédula aparece más de una vez en el archivo.");
        else if (cedulasExistentes.Contains(cedula))
        {
            f.YaExiste = true;
            Aviso("cedula", "Ya existe en la empresa: se actualizarán sus datos, no se creará otro.");
        }

        // ── Nombre ──
        f.Nombre = (f.Nombre ?? string.Empty).Trim();
        f.Apellido = (f.Apellido ?? string.Empty).Trim();
        if (f.Nombre.Length == 0) Error("nombre", "Falta el nombre.");
        if (f.Apellido.Length == 0) Error("apellido", "Falta el apellido.");
        if (f.Nombre.Length > 100) Error("nombre", "El nombre no puede pasar de 100 caracteres.");
        if (f.Apellido.Length > 100) Error("apellido", "El apellido no puede pasar de 100 caracteres.");

        // ── Email ──
        if (!string.IsNullOrWhiteSpace(f.Email))
        {
            f.Email = f.Email.Trim();
            if (!Email.IsMatch(f.Email)) Error("email", "El correo no tiene un formato válido.");
        }
        else f.Email = null;

        // ── Salario ──
        if (f.SalarioBase is null) Error("salarioBase", "Falta el salario base mensual.");
        else if (f.SalarioBase <= 0) Error("salarioBase", "El salario base debe ser mayor que cero.");
        else if (f.SalarioBase > 100_000) Aviso("salarioBase", "Salario mensual muy alto; revisa que no sea anual.");

        // ── Fecha de contratación ──
        if (f.FechaContratacion is null) Error("fechaContratacion", "Falta la fecha de contratación.");
        else if (f.FechaContratacion.Value.Date > hoy.Date) Error("fechaContratacion", "La fecha de contratación no puede ser futura.");
        else if (f.FechaContratacion.Value.Year < 1950) Error("fechaContratacion", "La fecha de contratación no es válida.");

        // ── Listas ──
        if (string.IsNullOrWhiteSpace(f.TipoPeriodo))
            Error("tipoPeriodo", "Falta el tipo de período de pago.");
        else
        {
            var tp = TiposPeriodo.FirstOrDefault(t => string.Equals(t, f.TipoPeriodo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (tp is null) Error("tipoPeriodo", $"El tipo de período debe ser uno de: {string.Join(", ", TiposPeriodo)}.");
            else f.TipoPeriodo = tp;
        }

        if (!string.IsNullOrWhiteSpace(f.TipoContrato))
        {
            var tc = TiposContrato.FirstOrDefault(t => string.Equals(t, f.TipoContrato.Trim().Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
            if (tc is null) Error("tipoContrato", $"El tipo de contrato debe ser uno de: {string.Join(", ", TiposContrato)}.");
            else f.TipoContrato = tc;
        }
        else f.TipoContrato = "Indefinido";

        // ── Números opcionales ──
        if (f.Dependientes is < 0 or > 20) Error("dependientes", "Los dependientes deben estar entre 0 y 20.");

        if (f.RiesgoProfesional is not null && !ClasesRiesgo.Contains(f.RiesgoProfesional.Value))
            Error("riesgoProfesional", "El riesgo profesional debe ser una de las cinco clases: 0.56, 0.98, 2.10, 3.64 o 5.67.");

        if (f.GastoRepresentacionMensual is < 0)
            Error("gastoRepresentacionMensual", "El gasto de representación no puede ser negativo.");
        else if (f.GastoRepresentacionMensual > 0 && f.SalarioBase > 0 && f.GastoRepresentacionMensual > f.SalarioBase)
            Error("gastoRepresentacionMensual", "El gasto de representación no puede ser mayor que el salario.");

        // ── Meses de salario ──
        var mesesConMonto = f.Meses.Where(m => m.Monto is not null).ToList();
        foreach (var m in mesesConMonto)
        {
            if (m.Mes is < 1 or > 12 || m.Anio < 1990)
                Error("meses", $"El mes {m.Anio}-{m.Mes:D2} no es válido.");
            else if (m.Monto < 0)
                Error("meses", $"El salario de {NombreMes(m.Mes)} {m.Anio} es negativo.");
        }

        if (f.FechaContratacion is { } contratacion)
        {
            var inicio = new DateTime(contratacion.Year, contratacion.Month, 1);
            var antes = mesesConMonto
                .Where(m => m.Monto > 0 && new DateTime(m.Anio, Math.Clamp(m.Mes, 1, 12), 1) < inicio)
                .ToList();
            if (antes.Count > 0)
                Aviso("meses", $"Hay {antes.Count} mes(es) con salario antes de la fecha de contratación; revisa la fecha o esos meses.");

            // Huecos entre la contratación (o el más antiguo del archivo) y el mes de hoy.
            var desde = Max(inicio, f.Meses.Count > 0
                ? f.Meses.Min(m => new DateTime(m.Anio, Math.Clamp(m.Mes, 1, 12), 1))
                : inicio);
            var hasta = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-1);
            var faltantes = 0;
            for (var c = desde; c <= hasta; c = c.AddMonths(1))
                if (!mesesConMonto.Any(m => m.Anio == c.Year && m.Mes == c.Month)) faltantes++;

            if (faltantes > 0 && mesesConMonto.Count > 0)
                Aviso("meses", $"Faltan {faltantes} mes(es) de salario desde la contratación. La prima de antigüedad, la indemnización y el décimo se calcularán con los meses que haya.");
            else if (mesesConMonto.Count == 0 && inicio < hasta)
                Aviso("meses", "No trae salarios históricos. Hasta que haya planillas en Pagly, la liquidación y el décimo no tendrán con qué calcularse.");
        }

        // ── Saldos de renta ──
        if (f.Saldos is { } s)
        {
            if (s.IsrRetenido is < 0 || s.DecimoPagado is < 0 || s.GastoRepresentacionPagado is < 0 || s.IsrGastoRepresentacion is < 0)
                Error("saldos", "Los saldos de renta no pueden ser negativos.");
            if (s.PartidasDecimo is < 0 or > 3)
                Error("saldos", "Las partidas de décimo pagadas deben estar entre 0 y 3.");
            if (s.DecimoPagado > 0 && (s.PartidasDecimo ?? 0) == 0)
                Aviso("saldos", "Hay décimo pagado pero 0 partidas: indica cuántas partidas se pagaron (la columna PERIODOS de la ficha depende de eso).");
            if (s.IsrGastoRepresentacion > 0 && (s.GastoRepresentacionPagado ?? 0) == 0)
                Aviso("saldos", "Hay ISR sobre gastos de representación pero no gastos pagados.");
        }

        return new FilaValidadaDto { Datos = f, Problemas = p };
    }

    public static string NormalizarCedula(string? cedula)
        => (cedula ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "");

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    public static string NombreMes(int mes) => mes switch
    {
        1 => "enero", 2 => "febrero", 3 => "marzo", 4 => "abril", 5 => "mayo", 6 => "junio",
        7 => "julio", 8 => "agosto", 9 => "septiembre", 10 => "octubre", 11 => "noviembre", 12 => "diciembre",
        _ => mes.ToString()
    };
}
