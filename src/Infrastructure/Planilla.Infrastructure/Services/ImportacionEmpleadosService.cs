// ====================================================================
// Planilla - ImportacionEmpleadosService
// Onboarding de una empresa: valida la plantilla contra lo que ya existe
// y, cuando el usuario confirma desde la pantalla de revisión, escribe todo
// en una sola transacción. Si una fila falla al confirmar, no se escribe
// nada: el usuario corrige y vuelve a confirmar.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.DTOs.Importacion;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public interface IImportacionEmpleadosService
{
    Task<ResultadoValidacionDto> ValidarAsync(Stream archivo, DateTime hoy, CancellationToken ct = default);
    Task<ResultadoValidacionDto> RevalidarAsync(IReadOnlyList<FilaImportacionDto> filas, DateTime hoy, CancellationToken ct = default);
    Task<ResumenImportacionDto> ConfirmarAsync(ConfirmarImportacionRequest request, DateTime hoy, CancellationToken ct = default);
}

public class ImportacionEmpleadosService : IImportacionEmpleadosService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ImportacionEmpleadosService> _logger;

    public ImportacionEmpleadosService(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IAuditLogService auditLog,
        ILogger<ImportacionEmpleadosService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<ResultadoValidacionDto> ValidarAsync(Stream archivo, DateTime hoy, CancellationToken ct = default)
    {
        var lectura = PlantillaImportacion.Leer(archivo, hoy.Year);
        var resultado = await RevalidarAsync(lectura.Filas, hoy, ct);
        resultado.ProblemasDelArchivo.AddRange(lectura.ProblemasDelArchivo);
        resultado.MesesDelArchivo = lectura.MesesDelArchivo;
        return resultado;
    }

    public async Task<ResultadoValidacionDto> RevalidarAsync(IReadOnlyList<FilaImportacionDto> filas, DateTime hoy, CancellationToken ct = default)
    {
        var existentes = await CedulasExistentesAsync(ct);
        return new ResultadoValidacionDto
        {
            Filas = ImportacionEmpleadosValidator.Validar(filas, existentes, hoy)
        };
    }

    public async Task<ResumenImportacionDto> ConfirmarAsync(ConfirmarImportacionRequest request, DateTime hoy, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var omitidas = request.Omitidas.Select(ImportacionEmpleadosValidator.NormalizarCedula).ToHashSet();
        var filas = request.Filas
            .Where(f => !omitidas.Contains(ImportacionEmpleadosValidator.NormalizarCedula(f.Cedula)))
            .ToList();

        // Se vuelve a validar en el servidor: la pantalla puede haberse saltado algo.
        var validadas = ImportacionEmpleadosValidator.Validar(filas, await CedulasExistentesAsync(ct), hoy);
        var conErrores = validadas.Where(v => v.TieneErrores).ToList();
        if (conErrores.Count > 0)
        {
            var detalle = string.Join("; ", conErrores.Take(3).Select(v =>
                $"{v.Datos.Cedula}: {v.Problemas.First(p => p.Tipo == TipoProblema.Error).Mensaje}"));
            throw new InvalidOperationException(
                $"Hay {conErrores.Count} empleado(s) con errores; no se guardó nada. {detalle}");
        }

        var resumen = new ResumenImportacionDto { Omitidos = omitidas.Count };

        // Catálogos por nombre, una sola vez
        var departamentos = await _context.Departamentos.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var posiciones = await _context.Posiciones.Where(p => p.TenantId == tenantId).ToListAsync(ct);
        var empleadosExistentes = await _context.Empleados
            .Include(e => e.HistorialSalarial)
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .ToListAsync(ct);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var v in validadas)
            {
                var f = v.Datos;
                var existente = empleadosExistentes.FirstOrDefault(e =>
                    ImportacionEmpleadosValidator.NormalizarCedula(e.NumeroIdentificacion) == f.Cedula);

                var empleado = existente ?? new Empleado
                {
                    TenantId = tenantId,
                    NumeroIdentificacion = f.Cedula,
                    Nombre = f.Nombre,
                    Apellido = f.Apellido,
                    EstaActivo = true
                };

                var salarioAnterior = existente?.SalarioBase;
                AplicarDatos(empleado, f, departamentos, posiciones, tenantId);

                if (existente is null)
                {
                    empleado.HistorialSalarial.Add(new HistorialSalarial
                    {
                        SalarioMensual = empleado.SalarioBase,
                        FechaVigencia = empleado.FechaContratacion,
                        Motivo = "Contratación (importación)",
                        TenantId = tenantId
                    });
                    _context.Empleados.Add(empleado);
                    resumen.Creados++;
                }
                else
                {
                    if (salarioAnterior != empleado.SalarioBase)
                    {
                        empleado.HistorialSalarial.Add(new HistorialSalarial
                        {
                            SalarioMensual = empleado.SalarioBase,
                            FechaVigencia = hoy.Date,
                            Motivo = "Ajuste salarial (importación)",
                            TenantId = tenantId
                        });
                    }
                    resumen.Actualizados++;
                }

                // Necesitamos el Id para los meses y los saldos.
                await _context.SaveChangesAsync(ct);

                resumen.MesesGuardados += await GuardarMesesAsync(empleado, f, tenantId, ct);
                if (await GuardarSaldosAsync(empleado, f, hoy, tenantId, ct)) resumen.SaldosGuardados++;

                resumen.Empleados.Add(new EmpleadoImportadoDto
                {
                    Id = empleado.Id,
                    Cedula = empleado.NumeroIdentificacion,
                    NombreCompleto = $"{empleado.Nombre} {empleado.Apellido}",
                    Creado = existente is null
                });
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await _auditLog.LogAsync("IMPORTAR_EMPLEADOS", "Empleado", null, new Dictionary<string, string>
        {
            ["Archivo"] = request.NombreArchivo ?? "plantilla.xlsx",
            ["Creados"] = resumen.Creados.ToString(),
            ["Actualizados"] = resumen.Actualizados.ToString(),
            ["Omitidos"] = resumen.Omitidos.ToString(),
            ["MesesGuardados"] = resumen.MesesGuardados.ToString(),
            ["SaldosGuardados"] = resumen.SaldosGuardados.ToString()
        });

        _logger.LogInformation("Importación de empleados en tenant {Tenant}: {Creados} creados, {Actualizados} actualizados, {Omitidos} omitidos",
            tenantId, resumen.Creados, resumen.Actualizados, resumen.Omitidos);

        return resumen;
    }

    // ────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> CedulasExistentesAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var cedulas = await _context.Empleados
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => e.NumeroIdentificacion)
            .ToListAsync(ct);
        return cedulas.Select(ImportacionEmpleadosValidator.NormalizarCedula).ToHashSet();
    }

    private static void AplicarDatos(Empleado e, FilaImportacionDto f,
        List<Departamento> departamentos, List<Posicion> posiciones, int tenantId)
    {
        e.Nombre = f.Nombre;
        e.Apellido = f.Apellido;
        if (!string.IsNullOrWhiteSpace(f.Email)) e.Email = f.Email;
        e.SalarioBase = f.SalarioBase ?? e.SalarioBase;
        e.FechaContratacion = f.FechaContratacion ?? e.FechaContratacion;

        e.PayPeriodType = f.TipoPeriodo switch
        {
            "Semanal" => PayPeriodType.Semanal,
            "Bisemanal" => PayPeriodType.Bisemanal,
            "Mensual" => PayPeriodType.Mensual,
            _ => PayPeriodType.Quincenal
        };
        e.TipoContrato = f.TipoContrato switch
        {
            "Definido" => TipoContratoDuracion.Definido,
            "PorObra" => TipoContratoDuracion.PorObra,
            _ => TipoContratoDuracion.Indefinido
        };

        if (f.Dependientes is not null) e.Dependents = f.Dependientes.Value;
        if (f.RiesgoProfesional is not null) e.CssRiskPercentage = f.RiesgoProfesional.Value;
        if (f.GastoRepresentacionMensual is not null) e.GastoRepresentacionMensual = f.GastoRepresentacionMensual.Value;
        if (f.SujetoCss is not null) e.IsSubjectToCss = f.SujetoCss.Value;
        if (f.SujetoSe is not null) e.IsSubjectToEducationalInsurance = f.SujetoSe.Value;
        if (f.SujetoIsr is not null) e.IsSubjectToIncomeTax = f.SujetoIsr.Value;

        // Departamento y posición por nombre; se crean si no existen (es onboarding).
        if (!string.IsNullOrWhiteSpace(f.Departamento))
        {
            var dep = departamentos.FirstOrDefault(d => PlantillaImportacion.Normalizar(d.Nombre) == PlantillaImportacion.Normalizar(f.Departamento));
            if (dep is null)
            {
                dep = new Departamento { TenantId = tenantId, Nombre = f.Departamento.Trim(), Codigo = Codigo(f.Departamento, departamentos.Select(d => d.Codigo)) };
                departamentos.Add(dep);
            }
            e.Departamento = dep;
        }
        if (!string.IsNullOrWhiteSpace(f.Posicion))
        {
            var pos = posiciones.FirstOrDefault(p => PlantillaImportacion.Normalizar(p.Nombre) == PlantillaImportacion.Normalizar(f.Posicion));
            if (pos is null)
            {
                pos = new Posicion { TenantId = tenantId, Nombre = f.Posicion.Trim(), Codigo = Codigo(f.Posicion, posiciones.Select(p => p.Codigo)) };
                posiciones.Add(pos);
            }
            e.Posicion = pos;
        }

        // Mismo camino que el alta manual: horas del período y tarifa por hora.
        if (e.HoursPerWeek <= 0) e.HoursPerWeek = 48;
        e.HoursPerPeriod = Empleado.CalculateSuggestedHoursPerPeriod(e.HoursPerWeek, e.PayPeriodType);
        e.RecalculateHourlyRate();
        e.SyncPayFrequencyFromType();
    }

    /// <summary>Código corto y único a partir del nombre ("Administración" → "ADM", "ADM2"…).</summary>
    private static string Codigo(string nombre, IEnumerable<string> usados)
    {
        var set = usados.Select(u => u.ToUpperInvariant()).ToHashSet();
        var basico = new string(PlantillaImportacion.Normalizar(nombre).Take(3).ToArray()).ToUpperInvariant();
        if (basico.Length == 0) basico = "GEN";
        var candidato = basico;
        var n = 2;
        while (set.Contains(candidato)) candidato = $"{basico}{n++}";
        return candidato;
    }

    private async Task<int> GuardarMesesAsync(Empleado e, FilaImportacionDto f, int tenantId, CancellationToken ct)
    {
        var meses = f.Meses.Where(m => m.Monto is not null && m.Mes is >= 1 and <= 12).ToList();
        if (meses.Count == 0) return 0;

        var existentes = await _context.DevengadosMensuales
            .Where(d => d.EmpleadoId == e.Id)
            .ToDictionaryAsync(d => (d.Anio, d.Mes), ct);

        var guardados = 0;
        foreach (var m in meses)
        {
            if (existentes.TryGetValue((m.Anio, m.Mes), out var d))
            {
                d.Salario = m.Monto!.Value;
                d.Vacaciones = 0m; d.Extras = 0m; d.Comision = 0m;
                d.Origen = OrigenDevengado.Importado;
                d.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.DevengadosMensuales.Add(new DevengadoMensual
                {
                    TenantId = tenantId,
                    EmpleadoId = e.Id,
                    Anio = m.Anio,
                    Mes = m.Mes,
                    Salario = m.Monto!.Value,
                    Origen = OrigenDevengado.Importado
                });
            }
            guardados++;
        }
        return guardados;
    }

    /// <summary>
    /// Saldo inicial de renta del año en curso: lo retenido y el décimo vienen de la
    /// hoja Saldos renta. El ingreso del año NO se guarda aquí: ya está mes a mes en
    /// DevengadoMensual, y la ficha y el motor lo leen de ahí con sus períodos.
    /// Guardarlo también como saldo lo contaría dos veces.
    /// </summary>
    private async Task<bool> GuardarSaldosAsync(Empleado e, FilaImportacionDto f, DateTime hoy, int tenantId, CancellationToken ct)
    {
        var anio = hoy.Year;
        var s = f.Saldos;
        var hayAlgo = (s is not null && (
            (s.IsrRetenido ?? 0) > 0 || (s.DecimoPagado ?? 0) > 0 || (s.PartidasDecimo ?? 0) > 0 ||
            (s.GastoRepresentacionPagado ?? 0) > 0 || (s.IsrGastoRepresentacion ?? 0) > 0));
        if (!hayAlgo) return false;

        var saldo = await _context.AcumuladosFiscalesEmpleados
            .FirstOrDefaultAsync(a => a.EmpleadoId == e.Id && a.Anio == anio, ct);
        if (saldo is null)
        {
            saldo = new AcumuladoFiscalEmpleado { TenantId = tenantId, EmpleadoId = e.Id, Anio = anio };
            _context.AcumuladosFiscalesEmpleados.Add(saldo);
        }
        else saldo.UpdatedAt = DateTime.UtcNow;

        saldo.IngresoGravableInicial = 0m;
        saldo.DecimoInicial = s?.DecimoPagado ?? 0m;
        saldo.PartidasDecimoInicial = Math.Clamp(s?.PartidasDecimo ?? 0, 0, 3);
        saldo.IsrRetenidoInicial = s?.IsrRetenido ?? 0m;
        saldo.GastoRepresentacionInicial = s?.GastoRepresentacionPagado ?? 0m;
        saldo.IsrGastoRepresentacionInicial = s?.IsrGastoRepresentacion ?? 0m;
        return true;
    }
}
