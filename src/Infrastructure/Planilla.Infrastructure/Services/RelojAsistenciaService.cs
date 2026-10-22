// ====================================================================
// Reloj de asistencia: del archivo (o del reloj) a Pagly.
//   1. Leer el archivo y enseñar qué trae, sin guardar.
//   2. Importar sin duplicar y asociar el número del reloj a cada persona.
//   3. Comparar contra su horario: entrada, salida, llegadas tarde,
//      ausencias y posibles horas extra.
//   4. Proponer esas horas extra al director, como pendientes.
// ====================================================================

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public sealed record CodigoEnArchivoDto(string Codigo, string? NombreEnArchivo, int Marcas, DateTime Desde, DateTime Hasta,
    int? EmpleadoId, string? Empleado);

public sealed record VistaPreviaRelojDto(string Formato, string? Columnas, int TotalMarcas, int Nuevas, int YaImportadas,
    DateTime? Desde, DateTime? Hasta, List<CodigoEnArchivoDto> Codigos, List<ProblemaLectura> Problemas);

public sealed record ResultadoImportacionRelojDto(int Nuevas, int Repetidas, int SinAsignar, int CodigosAsignados);

public sealed record DiaAsistenciaDto(DateTime Fecha, string Estado, string? Entrada, string? Salida, int Marcas,
    int MinutosTarde, int MinutosExtra, bool HoraExtraPropuesta);

public sealed record EmpleadoAsistenciaDto(int EmpleadoId, string Nombre, string? CodigoReloj, string? Horario, int? HorarioAsignadoId,
    int DiasTrabajados, int Tardanzas, int MinutosTarde, int Ausencias, int SinSalida, int MinutosExtra,
    int HorasExtraPorProponer, List<DiaAsistenciaDto> Dias);

public sealed record ResumenAsistenciaDto(DateTime Desde, DateTime Hasta, List<EmpleadoAsistenciaDto> Empleados,
    List<CodigoEnArchivoDto> CodigosSinAsignar, int EmpleadosSinCodigo, bool HayHorario);

public class RelojAsistenciaService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly RegistroHorasExtraService _registro;
    private readonly IOvertimeFactorService _factores;
    private readonly PanamaHolidayService _feriados;

    public RelojAsistenciaService(ApplicationDbContext context, ITenantContext tenant,
        RegistroHorasExtraService registro, IOvertimeFactorService factores, PanamaHolidayService feriados)
    {
        _context = context;
        _tenant = tenant;
        _registro = registro;
        _factores = factores;
        _feriados = feriados;
    }

    // ----------------------------------------------------------------
    // 1. Leer
    // ----------------------------------------------------------------

    // Topes de la hoja de un Excel: filas y columnas.
    private const int MaximoFilas = 200_000;
    private const int MaximoColumnas = 60;

    /// <summary>Lee el archivo según su tipo. El .xls antiguo no se lee: se pide guardarlo como .xlsx o .csv.</summary>
    public static ResultadoLectura LeerArchivo(Stream stream, string nombreArchivo)
    {
        var ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (ext == ".xls")
            return Falla("Este Excel es del formato antiguo (.xls). Ábrelo en Excel y guárdalo como .xlsx o .csv.");

        if (ext == ".xlsx")
        {
            try
            {
                // Abrir con tope de tamaño descomprimido (un .xlsx es un zip).
                using var libro = ExcelSubido.Abrir(stream, out var problema);
                if (libro == null)
                    return Falla(problema == "demasiado grande"
                        ? "El Excel es demasiado grande. Exporta solo el período que necesitas o guárdalo como .csv."
                        : "No se pudo abrir el Excel. Revisa que no esté protegido ni dañado.");
                var hoja = libro.Worksheets.First();
                var rango = hoja.RangeUsed();
                if (rango == null) return Falla("La hoja está vacía.");
                if (rango.RowCount() > MaximoFilas || rango.ColumnCount() > MaximoColumnas)
                    return Falla($"La hoja pasa de {MaximoFilas:N0} filas o {MaximoColumnas} columnas. Exporta solo el período que necesitas.");
                var filas = rango.Rows()
                    .Select(f => (IReadOnlyList<object?>)f.Cells(1, rango.ColumnCount()).Select(ValorCelda).ToList())
                    .ToList();
                return LectorMarcaciones.LeerTabla(filas, "Excel");
            }
            catch (Exception)
            {
                return Falla("No se pudo abrir el Excel. Revisa que no esté protegido ni dañado.");
            }
        }

        // .dat, .txt, .csv: texto en UTF-8 o, si no lo es, en el de Windows.
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        var texto = new UTF8Encoding(false, false).GetString(bytes);
        if (texto.Contains('�')) texto = Encoding.Latin1.GetString(bytes);
        return LectorMarcaciones.LeerTexto(texto);
    }

    private static object? ValorCelda(IXLCell c) => c.Value.Type switch
    {
        XLDataType.DateTime => c.Value.GetDateTime(),
        XLDataType.TimeSpan => c.Value.GetTimeSpan(),
        XLDataType.Number => c.Value.GetNumber(),
        XLDataType.Blank => null,
        _ => c.Value.ToString(CultureInfo.InvariantCulture),
    };

    private static ResultadoLectura Falla(string mensaje) => new(new(), new() { new(0, mensaje) }, "desconocido", null);

    /// <summary>Lo que trae el archivo y a quién corresponde cada número, sin guardar nada.</summary>
    public async Task<VistaPreviaRelojDto> PrevisualizarAsync(ResultadoLectura lectura)
    {
        var marcas = lectura.Marcas;
        var codigos = await CodigosAsync(marcas);
        var yaImportadas = await ContarExistentesAsync(marcas);

        return new VistaPreviaRelojDto(
            lectura.Formato, lectura.Columnas, marcas.Count, marcas.Count - yaImportadas, yaImportadas,
            marcas.Count > 0 ? marcas.Min(m => m.FechaHora) : null,
            marcas.Count > 0 ? marcas.Max(m => m.FechaHora) : null,
            codigos, lectura.Problemas.Take(50).ToList());
    }

    // ----------------------------------------------------------------
    // 2. Importar y asociar
    // ----------------------------------------------------------------

    public async Task<(ResultadoImportacionRelojDto? Resultado, string? Error)> ImportarAsync(
        ResultadoLectura lectura, string fuente, IReadOnlyDictionary<string, int>? asignaciones)
    {
        var tenantId = _tenant.TenantId;
        var asignados = 0;

        // Primero las asociaciones que se hicieron en la vista previa.
        foreach (var (codigo, empleadoId) in asignaciones ?? new Dictionary<string, int>())
        {
            var error = await AsignarCodigoAsync(codigo, empleadoId, guardar: false);
            if (error != null) return (null, error);
            asignados++;
        }
        await _context.SaveChangesAsync();

        var porCodigo = await _context.Empleados.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CodigoReloj != null)
            .ToDictionaryAsync(e => e.CodigoReloj!, e => e.Id);

        var existentes = await ClavesExistentesAsync(lectura.Marcas);
        var ahora = DateTime.UtcNow;
        var nuevas = lectura.Marcas
            .Where(m => !existentes.Contains((m.Codigo, Utc(m.FechaHora))))
            .Select(m => new Marcacion
            {
                TenantId = tenantId,
                CodigoReloj = m.Codigo,
                EmpleadoId = porCodigo.TryGetValue(m.Codigo, out var id) ? id : null,
                FechaHora = Utc(m.FechaHora),
                Origen = "Archivo",
                Fuente = fuente.Length > 200 ? fuente[..200] : fuente,
                CreatedAt = ahora,
            })
            .ToList();
        _context.Marcaciones.AddRange(nuevas);
        await _context.SaveChangesAsync();

        return (new ResultadoImportacionRelojDto(
            nuevas.Count,
            lectura.Marcas.Count - nuevas.Count,
            nuevas.Count(n => n.EmpleadoId == null),
            asignados), null);
    }

    /// <summary>
    /// Asocia (o quita, con null) el número del reloj a una persona y pasa a
    /// su ficha las marcaciones ya importadas con ese número.
    /// </summary>
    public async Task<string?> AsignarCodigoAsync(string codigoCrudo, int? empleadoId, bool guardar = true)
    {
        var tenantId = _tenant.TenantId;
        var codigo = LectorMarcaciones.NormalizarCodigo(codigoCrudo);
        if (codigo == null) return "Falta el número del reloj.";

        var anterior = await _context.Empleados.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CodigoReloj == codigo);
        if (empleadoId.HasValue)
        {
            var empleado = await _context.Empleados.FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);
            if (empleado == null) return "Esa persona no existe.";
            if (anterior != null && anterior.Id != empleado.Id)
                return $"El número {codigo} ya es de {anterior.Nombre} {anterior.Apellido}. Quítaselo primero.";
            empleado.CodigoReloj = codigo;
        }
        else if (anterior != null)
        {
            anterior.CodigoReloj = null;
        }

        var marcas = await _context.Marcaciones.Where(m => m.TenantId == tenantId && m.CodigoReloj == codigo).ToListAsync();
        foreach (var m in marcas) m.EmpleadoId = empleadoId;

        if (guardar) await _context.SaveChangesAsync();
        return null;
    }

    // ----------------------------------------------------------------
    // 3. Resumen contra el horario
    // ----------------------------------------------------------------

    public async Task<ResumenAsistenciaDto> ResumenAsync(DateTime desde, DateTime hasta)
    {
        var tenantId = _tenant.TenantId;
        desde = desde.Date;
        hasta = hasta.Date;
        var (ini, fin) = (Utc(desde), Utc(hasta.AddDays(1)));

        var marcas = await _context.Marcaciones.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.FechaHora >= ini && m.FechaHora < fin)
            .Select(m => new { m.EmpleadoId, m.CodigoReloj, m.FechaHora })
            .ToListAsync();

        var horarios = await _context.HorariosLaborales.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Activo).ToListAsync();
        var predeterminado = horarios.FirstOrDefault(h => h.EsPredeterminado) ?? horarios.FirstOrDefault();

        var conMarcas = marcas.Where(m => m.EmpleadoId != null).Select(m => m.EmpleadoId!.Value).ToHashSet();
        var empleados = await _context.Empleados.AsNoTracking()
            .Where(e => e.TenantId == tenantId && (e.EstaActivo || conMarcas.Contains(e.Id))
                     && (e.CodigoReloj != null || conMarcas.Contains(e.Id)))
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre)
            .Select(e => new { e.Id, e.Nombre, e.Apellido, e.CodigoReloj, e.HorarioLaboralId })
            .ToListAsync();
        var sinCodigo = await _context.Empleados.CountAsync(e => e.TenantId == tenantId && e.EstaActivo && e.CodigoReloj == null);

        // Días que no son ausencia aunque no haya marcas.
        var ids = empleados.Select(e => e.Id).ToList();
        var licencias = await _context.LicenciasMaternidad.AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.Anulada && ids.Contains(l.EmpleadoId) && l.FechaInicio <= hasta && l.FechaFin >= desde)
            .Select(l => new { l.EmpleadoId, l.FechaInicio, l.FechaFin }).ToListAsync();
        var vacaciones = await _context.SolicitudesVacaciones.AsNoTracking()
            .Where(v => ids.Contains(v.EmpleadoId) && v.FechaInicio <= hasta && v.FechaFin >= desde
                     && (v.Estado == EstadoVacaciones.Aprobada || v.Estado == EstadoVacaciones.EnCurso || v.Estado == EstadoVacaciones.Completada))
            .Select(v => new { v.EmpleadoId, v.FechaInicio, v.FechaFin }).ToListAsync();
        var ausencias = await _context.Ausencias.AsNoTracking()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.EmpleadoId) && a.FechaInicio <= hasta && a.FechaFin >= desde)
            .Select(a => new { a.EmpleadoId, a.FechaInicio, a.FechaFin }).ToListAsync();
        var propuestas = await _context.HorasExtra.AsNoTracking()
            .Where(h => h.TenantId == tenantId && ids.Contains(h.EmpleadoId) && h.Origen == "Reloj" && h.Fecha >= ini && h.Fecha < fin)
            .Select(h => new { h.EmpleadoId, h.Fecha }).ToListAsync();

        bool Cubre<T>(IEnumerable<T> lista, Func<T, bool> f) => lista.Any(f);

        // Solo se puede decir «ausente» de los días que el reloj ya cubrió:
        // entre la primera y la última marca importada del rango, y nunca hoy
        // ni días que todavía no ocurren.
        var hoy = DateTimeHelper.NowPanama().Date;
        var cubiertoDesde = marcas.Count > 0 ? marcas.Min(m => m.FechaHora).Date : DateTime.MaxValue;
        var cubiertoHasta = marcas.Count > 0 ? marcas.Max(m => m.FechaHora).Date : DateTime.MinValue;
        if (cubiertoHasta >= hoy) cubiertoHasta = hoy.AddDays(-1);

        var resultado = new List<EmpleadoAsistenciaDto>();
        foreach (var e in empleados)
        {
            var horario = (e.HorarioLaboralId.HasValue ? horarios.FirstOrDefault(h => h.Id == e.HorarioLaboralId) : null) ?? predeterminado;
            var suyas = marcas.Where(m => m.EmpleadoId == e.Id).Select(m => m.FechaHora).OrderBy(x => x).ToList();
            var dias = new List<DiaAsistenciaDto>();

            for (var d = desde; d <= hasta; d = d.AddDays(1))
            {
                var delDia = suyas.Where(x => x.Date == d).ToList();
                var feriado = _feriados.IsNationalHoliday(d);
                var laborable = horario != null && horario.EsLaborable(d.DayOfWeek) && !feriado;
                var entrada = delDia.Count > 0 ? delDia[0] : (DateTime?)null;
                var salida = delDia.Count > 1 ? delDia[^1] : (DateTime?)null;

                var tarde = 0;
                if (laborable && entrada.HasValue && entrada.Value.TimeOfDay > horario!.HoraEntrada.Add(TimeSpan.FromMinutes(horario.ToleranciaMinutos)))
                    tarde = (int)(entrada.Value.TimeOfDay - horario.HoraEntrada).TotalMinutes;

                var extra = 0;
                if (horario != null && salida.HasValue)
                {
                    var minutos = laborable
                        ? (int)(salida.Value.TimeOfDay - horario.HoraSalida).TotalMinutes
                        : (int)(salida.Value - entrada!.Value).TotalMinutes;
                    if (minutos >= horario.MinutosParaHoraExtra) extra = minutos;
                }

                string estado;
                if (delDia.Count == 0)
                {
                    estado = Cubre(licencias, l => l.EmpleadoId == e.Id && l.FechaInicio.Date <= d && l.FechaFin.Date >= d) ? "Licencia"
                        : Cubre(vacaciones, v => v.EmpleadoId == e.Id && v.FechaInicio.Date <= d && v.FechaFin.Date >= d) ? "Vacaciones"
                        : Cubre(ausencias, a => a.EmpleadoId == e.Id && a.FechaInicio.Date <= d && a.FechaFin.Date >= d) ? "Ausencia registrada"
                        : feriado ? "Feriado"
                        : !laborable ? "Libre"
                        : d < cubiertoDesde || d > cubiertoHasta ? "Sin datos"
                        : "Ausente";
                }
                else if (delDia.Count == 1) estado = d >= hoy ? "En curso" : "Sin salida";
                else if (!laborable) estado = "Día libre trabajado";
                else estado = tarde > 0 ? "Tarde" : "Completo";

                dias.Add(new DiaAsistenciaDto(
                    d, estado, entrada?.ToString("HH:mm"), salida?.ToString("HH:mm"), delDia.Count, tarde, extra,
                    propuestas.Any(p => p.EmpleadoId == e.Id && p.Fecha.Date == d)));
            }

            resultado.Add(new EmpleadoAsistenciaDto(
                e.Id, $"{e.Nombre} {e.Apellido}", e.CodigoReloj, horario?.Nombre, e.HorarioLaboralId,
                dias.Count(x => x.Marcas > 0),
                dias.Count(x => x.MinutosTarde > 0),
                dias.Sum(x => x.MinutosTarde),
                dias.Count(x => x.Estado == "Ausente"),
                dias.Count(x => x.Estado == "Sin salida"),
                dias.Sum(x => x.MinutosExtra),
                dias.Count(x => x.MinutosExtra > 0 && !x.HoraExtraPropuesta),
                dias));
        }

        var sinAsignar = marcas.Where(m => m.EmpleadoId == null)
            .GroupBy(m => m.CodigoReloj)
            .Select(g => new CodigoEnArchivoDto(g.Key, null, g.Count(), g.Min(x => x.FechaHora), g.Max(x => x.FechaHora), null, null))
            .OrderBy(c => c.Codigo).ToList();

        return new ResumenAsistenciaDto(desde, hasta, resultado, sinAsignar, sinCodigo, predeterminado != null);
    }

    // ----------------------------------------------------------------
    // 4. Proponer horas extra al director
    // ----------------------------------------------------------------

    /// <summary>
    /// Crea como pendientes las horas extra que el reloj muestra y que todavía
    /// no se propusieron. El director decide después; nada se paga solo.
    /// </summary>
    public async Task<int> ProponerHorasExtraAsync(DateTime desde, DateTime hasta)
    {
        var tenantId = _tenant.TenantId;
        var resumen = await ResumenAsync(desde, hasta);
        var horarios = await _context.HorariosLaborales.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Activo).ToListAsync();
        var predeterminado = horarios.FirstOrDefault(h => h.EsPredeterminado) ?? horarios.FirstOrDefault();

        var creadas = 0;
        foreach (var e in resumen.Empleados.Where(x => x.HorasExtraPorProponer > 0))
        {
            var empleado = await _context.Empleados.FirstAsync(x => x.Id == e.EmpleadoId && x.TenantId == tenantId);
            var horario = (empleado.HorarioLaboralId.HasValue ? horarios.FirstOrDefault(h => h.Id == empleado.HorarioLaboralId) : null) ?? predeterminado;
            if (horario == null) continue;

            foreach (var dia in e.Dias.Where(d => d.MinutosExtra > 0 && !d.HoraExtraPropuesta && d.Salida != null && d.Entrada != null))
            {
                var laborable = dia.Estado != "Día libre trabajado";
                var inicio = laborable ? horario.HoraSalida : TimeSpan.Parse(dia.Entrada!, CultureInfo.InvariantCulture);
                var fin = TimeSpan.Parse(dia.Salida!, CultureInfo.InvariantCulture);
                var tipo = _factores.DetermineOvertimeType(dia.Fecha, inicio, fin);
                var motivo = laborable
                    ? $"Reloj: salió a las {dia.Salida}; su horario termina a las {horario.HoraSalida:hh\\:mm}."
                    : $"Reloj: trabajó un día libre, de {dia.Entrada} a {dia.Salida}.";

                var he = await _registro.ConstruirAsync(empleado, tenantId, dia.Fecha, tipo, inicio, fin, motivo, origen: "Reloj");
                he.Fecha = Utc(dia.Fecha);
                _context.HorasExtra.Add(he);
                await _context.SaveChangesAsync(); // una a una: los límites de la semana cuentan las anteriores
                creadas++;
            }
        }
        return creadas;
    }

    // ----------------------------------------------------------------
    // 5. Lo que envía el reloj solo (ADMS)
    // ----------------------------------------------------------------

    /// <summary>
    /// Reloj registrado y activo con ese número de serie, o null. La petición
    /// del reloj no trae sesión: la empresa sale del dispositivo, no del usuario.
    /// </summary>
    public Task<DispositivoReloj?> DispositivoAsync(string? numeroSerie)
    {
        var sn = numeroSerie?.Trim();
        if (string.IsNullOrEmpty(sn)) return Task.FromResult<DispositivoReloj?>(null);
        return _context.DispositivosReloj.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.NumeroSerie == sn && d.Activo);
    }

    /// <summary>
    /// ¿Se acepta esta petición del reloj? Solo desde la IP confirmada. La
    /// primera conexión dentro de la ventana de vinculación deja la IP
    /// pendiente de confirmar por alguien de la empresa; cualquier otra se
    /// rechaza y queda anotada para que la empresa la vea. Guarda los cambios.
    /// </summary>
    public async Task<bool> AceptarConexionAsync(DispositivoReloj dispositivo, string ip)
    {
        var ahora = DateTime.UtcNow;
        bool aceptada;

        if (dispositivo.IpVinculada != null)
        {
            aceptada = dispositivo.IpVinculada == ip;
            if (!aceptada) { dispositivo.IpRechazada = ip; dispositivo.IpRechazadaEn = ahora; }
        }
        else
        {
            // Sin vincular todavía: solo dentro de la ventana, y queda pendiente de confirmar.
            if (dispositivo.EsperandoVinculoHasta >= ahora && dispositivo.IpPendiente == null)
                dispositivo.IpPendiente = ip;
            else if (dispositivo.IpPendiente != ip)
            {
                dispositivo.IpRechazada = ip;
                dispositivo.IpRechazadaEn = ahora;
            }
            aceptada = false;
        }

        if (aceptada) dispositivo.UltimaConexion = ahora;
        await _context.SaveChangesAsync();
        return aceptada;
    }

    /// <summary>
    /// Guarda las marcaciones que envió el reloj (tabla ATTLOG: «número⇥fecha hora⇥…»),
    /// sin duplicar, en la empresa del dispositivo. Devuelve cuántas líneas recibió.
    /// </summary>
    public async Task<int> RecibirDelRelojAsync(DispositivoReloj dispositivo, string cuerpo)
    {
        var tenantId = dispositivo.TenantId;
        var lectura = LectorMarcaciones.LeerTexto(cuerpo);
        var ahora = DateTime.UtcNow;
        dispositivo.UltimaConexion = ahora;

        if (lectura.Marcas.Count > 0)
        {
            var codigos = lectura.Marcas.Select(m => m.Codigo).Distinct().ToList();
            var (min, max) = (Utc(lectura.Marcas.Min(m => m.FechaHora)), Utc(lectura.Marcas.Max(m => m.FechaHora)));

            // Sin sesión los filtros por empresa no actúan: se filtra a mano por la del reloj.
            var existentes = (await _context.Marcaciones.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.TenantId == tenantId && codigos.Contains(m.CodigoReloj) && m.FechaHora >= min && m.FechaHora <= max)
                    .Select(m => new { m.CodigoReloj, m.FechaHora })
                    .ToListAsync())
                .Select(x => (x.CodigoReloj, Utc(x.FechaHora))).ToHashSet();
            var porCodigo = await _context.Empleados.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.CodigoReloj != null && codigos.Contains(e.CodigoReloj))
                .ToDictionaryAsync(e => e.CodigoReloj!, e => e.Id);

            var nuevas = lectura.Marcas
                .Where(m => !existentes.Contains((m.Codigo, Utc(m.FechaHora))))
                .Select(m => new Marcacion
                {
                    TenantId = tenantId,
                    CodigoReloj = m.Codigo,
                    EmpleadoId = porCodigo.TryGetValue(m.Codigo, out var id) ? id : null,
                    FechaHora = Utc(m.FechaHora),
                    Origen = "Reloj",
                    Fuente = dispositivo.NumeroSerie,
                    CreatedAt = ahora,
                })
                .ToList();
            _context.Marcaciones.AddRange(nuevas);
            dispositivo.MarcasRecibidas += nuevas.Count;
            if (nuevas.Count > 0) dispositivo.UltimaMarcacion = nuevas.Max(n => n.FechaHora);
        }

        await _context.SaveChangesAsync();
        return lectura.Marcas.Count;
    }

    // ----------------------------------------------------------------
    // Ayudantes
    // ----------------------------------------------------------------

    /// <summary>La hora del reloj se guarda tal cual, marcada como UTC solo por la base de datos.</summary>
    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private async Task<List<CodigoEnArchivoDto>> CodigosAsync(List<MarcacionLeida> marcas)
    {
        var tenantId = _tenant.TenantId;
        var codigos = marcas.Select(m => m.Codigo).Distinct().ToList();
        var asignados = await _context.Empleados.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.CodigoReloj != null && codigos.Contains(e.CodigoReloj))
            .ToDictionaryAsync(e => e.CodigoReloj!, e => new { e.Id, Nombre = e.Nombre + " " + e.Apellido });

        return marcas.GroupBy(m => m.Codigo)
            .Select(g => new CodigoEnArchivoDto(
                g.Key,
                g.Select(m => m.Nombre).FirstOrDefault(n => n != null),
                g.Count(), g.Min(m => m.FechaHora), g.Max(m => m.FechaHora),
                asignados.TryGetValue(g.Key, out var a) ? a.Id : null,
                asignados.TryGetValue(g.Key, out var b) ? b.Nombre : null))
            .OrderBy(c => c.EmpleadoId.HasValue).ThenBy(c => c.Codigo)
            .ToList();
    }

    private async Task<HashSet<(string, DateTime)>> ClavesExistentesAsync(List<MarcacionLeida> marcas)
    {
        if (marcas.Count == 0) return new();
        var tenantId = _tenant.TenantId;
        var codigos = marcas.Select(m => m.Codigo).Distinct().ToList();
        var (min, max) = (Utc(marcas.Min(m => m.FechaHora)), Utc(marcas.Max(m => m.FechaHora)));
        var existentes = await _context.Marcaciones.AsNoTracking()
            .Where(m => m.TenantId == tenantId && codigos.Contains(m.CodigoReloj) && m.FechaHora >= min && m.FechaHora <= max)
            .Select(m => new { m.CodigoReloj, m.FechaHora })
            .ToListAsync();
        return existentes.Select(x => (x.CodigoReloj, Utc(x.FechaHora))).ToHashSet();
    }

    private async Task<int> ContarExistentesAsync(List<MarcacionLeida> marcas)
    {
        var existentes = await ClavesExistentesAsync(marcas);
        return marcas.Count(m => existentes.Contains((m.Codigo, Utc(m.FechaHora))));
    }
}
