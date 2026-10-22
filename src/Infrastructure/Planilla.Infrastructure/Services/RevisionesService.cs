// ====================================================================
// Revisiones entre el equipo: quien prepara algo pide que otra persona
// lo revise; el revisor recibe un aviso y un correo, y aprueba o devuelve
// con un comentario. Quien pidió recibe la respuesta de la misma forma.
// Todo queda como historial; nada se borra.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public record RevisorDto(string UserId, string Nombre, string Email);

public record SolicitudRevisionDto(
    int Id,
    TipoRevision Tipo,
    int EntidadId,
    string Referencia,
    string Enlace,
    string SolicitadoPorId,
    string SolicitadoPor,
    string RevisorId,
    string Revisor,
    string? Comentario,
    EstadoRevision Estado,
    string? Respuesta,
    DateTime? RespondidaEn,
    DateTime CreatedAt);

/// <summary>Resultado de una operación: el dato o la frase de por qué no se pudo.</summary>
public record ResultadoRevision(SolicitudRevisionDto? Solicitud, string? Error, bool NoEncontrada = false)
{
    public static ResultadoRevision Falla(string error) => new(null, error);
    public static ResultadoRevision NoExiste(string error) => new(null, error, true);
}

public class RevisionesService
{
    private static readonly string[] NombresMes =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IBrevoEmailService _correo;
    private readonly IAuditLogService _audit;
    private readonly ILogger<RevisionesService> _logger;

    public RevisionesService(
        ApplicationDbContext context,
        ITenantContext tenant,
        IBrevoEmailService correo,
        IAuditLogService audit,
        ILogger<RevisionesService> logger)
    {
        _context = context;
        _tenant = tenant;
        _correo = correo;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Permisos con los que se puede ver (y por tanto revisar) cada cosa.</summary>
    public static string[] PermisosParaVer(TipoRevision tipo) => tipo == TipoRevision.HorasExtra
        ? new[] { SystemPermission.OvertimeManage, SystemPermission.OvertimeApprove }
        : new[] { SystemPermission.PayrollView };

    /// <summary>¿Puede esta persona ver lo que se revisa? Misma regla que RequirePermission.</summary>
    public async Task<bool> PuedeVerAsync(string usuarioId, TipoRevision tipo)
    {
        var tenantId = _tenant.TenantId;
        var tu = await _context.TenantUsers.AsNoTracking()
            .Include(x => x.CustomRole).ThenInclude(r => r!.Permissions)
            .FirstOrDefaultAsync(x => x.UserId == usuarioId && x.TenantId == tenantId && x.IsActive);
        if (tu == null) return false;
        var permisos = tu.CustomTenantRoleId.HasValue && tu.CustomRole != null
            ? tu.CustomRole.Permissions.Where(p => p.IsActive).Select(p => p.Permission).ToList()
            : SystemPermission.GetDefaultPermissionsForSystemRole(tu.Role);
        return PermisosParaVer(tipo).Any(permisos.Contains);
    }

    /// <summary>
    /// Personas activas de la empresa (menos quien pide) que pueden ver lo que
    /// se va a revisar: a nadie se le pide revisar algo que no puede abrir.
    /// </summary>
    public async Task<List<RevisorDto>> RevisoresAsync(string usuarioActual, TipoRevision tipo)
    {
        var tenantId = _tenant.TenantId;
        var candidatos = await _context.TenantUsers
            .AsNoTracking()
            .Where(tu => tu.TenantId == tenantId && tu.IsActive && !tu.IsPendingInvitation
                      && tu.UserId != usuarioActual && tu.User != null && !tu.User.IsDeleted)
            .OrderBy(tu => tu.User!.NombreCompleto ?? tu.User.Email)
            .Select(tu => new RevisorDto(
                tu.UserId,
                tu.User!.NombreCompleto ?? tu.User.Email ?? "",
                tu.User.Email ?? ""))
            .ToListAsync();

        var habilitados = new List<RevisorDto>();
        foreach (var c in candidatos)
            if (await PuedeVerAsync(c.UserId, tipo)) habilitados.Add(c);
        return habilitados;
    }

    /// <summary>Historial de revisiones de una cosa, la más reciente primero.</summary>
    public async Task<List<SolicitudRevisionDto>> HistorialAsync(TipoRevision tipo, int entidadId)
    {
        var tenantId = _tenant.TenantId;
        var lista = await _context.SolicitudesRevision.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Tipo == tipo && r.EntidadId == entidadId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync();
        return await ADtosAsync(lista);
    }

    /// <summary>Lo que el usuario tiene pendiente de revisar.</summary>
    public async Task<List<SolicitudRevisionDto>> PendientesParaAsync(string usuarioId)
    {
        var tenantId = _tenant.TenantId;
        var lista = await _context.SolicitudesRevision.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.RevisorId == usuarioId && r.Estado == EstadoRevision.Pendiente)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return await ADtosAsync(lista);
    }

    public async Task<ResultadoRevision> PedirAsync(
        TipoRevision tipo, int entidadId, string revisorId, string? comentario, string solicitante, string? baseUrl)
    {
        var tenantId = _tenant.TenantId;
        if (string.IsNullOrWhiteSpace(revisorId))
            return ResultadoRevision.Falla("Elige a quién le pides la revisión.");
        if (revisorId == solicitante)
            return ResultadoRevision.Falla("La revisión la tiene que hacer otra persona.");
        if (comentario?.Length > 1000)
            return ResultadoRevision.Falla("El comentario no puede pasar de 1000 caracteres.");

        var revisor = await _context.TenantUsers.AsNoTracking()
            .Where(tu => tu.TenantId == tenantId && tu.UserId == revisorId && tu.IsActive && !tu.IsPendingInvitation
                      && tu.User != null && !tu.User.IsDeleted)
            .Select(tu => new { tu.UserId, Nombre = tu.User!.NombreCompleto ?? tu.User.Email ?? "", Email = tu.User.Email ?? "" })
            .FirstOrDefaultAsync();
        if (revisor == null)
            return ResultadoRevision.Falla("Esa persona no tiene acceso activo a esta empresa.");
        if (!await PuedeVerAsync(revisor.UserId, tipo))
            return ResultadoRevision.Falla("Esa persona no tiene permiso para ver esto; elige a alguien que pueda abrirlo.");

        var (referencia, enlace, error) = await ResolverAsync(tipo, entidadId);
        if (error != null)
            return ResultadoRevision.NoExiste(error);

        var abierta = await _context.SolicitudesRevision.AnyAsync(r =>
            r.TenantId == tenantId && r.Tipo == tipo && r.EntidadId == entidadId && r.Estado == EstadoRevision.Pendiente);
        if (abierta)
            return ResultadoRevision.Falla("Ya hay una revisión pendiente para esto. Espera la respuesta o retírala.");

        var ahora = DateTime.UtcNow;
        var solicitud = new SolicitudRevision
        {
            TenantId = tenantId,
            Tipo = tipo,
            EntidadId = entidadId,
            Referencia = referencia!,
            Enlace = enlace!,
            SolicitadoPor = solicitante,
            RevisorId = revisor.UserId,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            Estado = EstadoRevision.Pendiente,
            CreatedAt = ahora,
        };
        _context.SolicitudesRevision.Add(solicitud);
        await _context.SaveChangesAsync();

        var nombreSolicitante = await NombreDeAsync(solicitante);
        var mensaje = $"{nombreSolicitante} te pidió revisar {referencia}."
            + (solicitud.Comentario != null ? $" Comentario: «{solicitud.Comentario}»" : "");
        await AvisarAsync(revisor.UserId, revisor.Nombre, revisor.Email, solicitud,
            titulo: $"Revisión pendiente: {referencia}",
            mensaje: mensaje,
            textoBoton: "Revisar en Pagly",
            baseUrl: baseUrl);

        await _audit.LogAsync("RevisionPedida", "SolicitudRevision", solicitud.Id.ToString(), new Dictionary<string, string>
        {
            ["referencia"] = referencia!,
            ["revisor"] = revisor.UserId,
        });

        return new ResultadoRevision((await ADtosAsync(new List<SolicitudRevision> { solicitud }))[0], null);
    }

    public async Task<ResultadoRevision> ResponderAsync(int id, bool aprobar, string? respuesta, string usuarioId, string? baseUrl)
    {
        var tenantId = _tenant.TenantId;
        var solicitud = await _context.SolicitudesRevision.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (solicitud == null)
            return ResultadoRevision.NoExiste("Esa revisión ya no existe.");
        if (solicitud.RevisorId != usuarioId)
            return ResultadoRevision.Falla("Solo la persona a quien se le pidió puede responder esta revisión.");
        if (solicitud.Estado != EstadoRevision.Pendiente)
            return ResultadoRevision.Falla("Esta revisión ya fue respondida o retirada.");

        var texto = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim();
        if (!aprobar && texto == null)
            return ResultadoRevision.Falla("Para devolverla escribe qué hay que corregir.");
        if (texto?.Length > 1000)
            return ResultadoRevision.Falla("La respuesta no puede pasar de 1000 caracteres.");

        solicitud.Estado = aprobar ? EstadoRevision.Aprobada : EstadoRevision.Devuelta;
        solicitud.Respuesta = texto;
        solicitud.RespondidaEn = DateTime.UtcNow;
        solicitud.UpdatedAt = solicitud.RespondidaEn;
        await _context.SaveChangesAsync();

        var nombreRevisor = await NombreDeAsync(usuarioId);
        var solicitante = await _context.Users.AsNoTracking()
            .Where(u => u.Id == solicitud.SolicitadoPor)
            .Select(u => new { Nombre = u.NombreCompleto ?? u.Email ?? "", Email = u.Email ?? "" })
            .FirstOrDefaultAsync();
        if (solicitante != null)
        {
            var titulo = aprobar
                ? $"{solicitud.Referencia}: revisada y aprobada"
                : $"{solicitud.Referencia}: devuelta para corregir";
            var mensaje = aprobar
                ? $"{nombreRevisor} aprobó la revisión de {solicitud.Referencia}." + (texto != null ? $" Comentario: «{texto}»" : "")
                : $"{nombreRevisor} devolvió {solicitud.Referencia}: «{texto}»";
            await AvisarAsync(solicitud.SolicitadoPor, solicitante.Nombre, solicitante.Email, solicitud,
                titulo, mensaje, textoBoton: "Abrir en Pagly", baseUrl);
        }

        await _audit.LogAsync(aprobar ? "RevisionAprobada" : "RevisionDevuelta", "SolicitudRevision", solicitud.Id.ToString(),
            new Dictionary<string, string> { ["referencia"] = solicitud.Referencia, ["respuesta"] = texto ?? string.Empty });

        return new ResultadoRevision((await ADtosAsync(new List<SolicitudRevision> { solicitud }))[0], null);
    }

    public async Task<ResultadoRevision> RetirarAsync(int id, string usuarioId)
    {
        var tenantId = _tenant.TenantId;
        var solicitud = await _context.SolicitudesRevision.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (solicitud == null)
            return ResultadoRevision.NoExiste("Esa revisión ya no existe.");
        if (solicitud.SolicitadoPor != usuarioId)
            return ResultadoRevision.Falla("Solo quien pidió la revisión puede retirarla.");
        if (solicitud.Estado != EstadoRevision.Pendiente)
            return ResultadoRevision.Falla("Esta revisión ya fue respondida o retirada.");

        solicitud.Estado = EstadoRevision.Cancelada;
        solicitud.UpdatedAt = DateTime.UtcNow;

        // El aviso que le llegó al revisor ya no pide nada: queda leído.
        var avisos = await _context.Avisos
            .Where(a => a.TenantId == tenantId && a.SolicitudRevisionId == solicitud.Id && a.UsuarioId == solicitud.RevisorId && !a.Leido)
            .ToListAsync();
        foreach (var a in avisos) { a.Leido = true; a.LeidoEn = solicitud.UpdatedAt; }

        await _context.SaveChangesAsync();
        await _audit.LogAsync("RevisionRetirada", "SolicitudRevision", solicitud.Id.ToString(),
            new Dictionary<string, string> { ["referencia"] = solicitud.Referencia });

        return new ResultadoRevision((await ADtosAsync(new List<SolicitudRevision> { solicitud }))[0], null);
    }

    /// <summary>
    /// Nombre y ruta de lo que se revisa, o la razón por la que no se puede
    /// pedir revisión (no existe o ya está cerrado).
    /// </summary>
    private async Task<(string? Referencia, string? Enlace, string? Error)> ResolverAsync(TipoRevision tipo, int entidadId)
    {
        var tenantId = _tenant.TenantId;
        switch (tipo)
        {
            case TipoRevision.Planilla:
            {
                var p = await _context.PayrollHeaders.AsNoTracking()
                    .Where(x => x.Id == entidadId && x.TenantId == tenantId)
                    .Select(x => new { x.PayrollNumber, x.PeriodStartDate, x.Status })
                    .FirstOrDefaultAsync();
                if (p == null) return (null, null, "Esa planilla no existe.");
                if (p.Status is PayrollStatus.Paid or PayrollStatus.Cancelled)
                    return (null, null, "Una planilla pagada o anulada ya no se revisa.");
                return ($"Planilla {p.PayrollNumber}",
                        $"/planillas?anio={p.PeriodStartDate.Year}&mes={p.PeriodStartDate.Month}&abrir={entidadId}", null);
            }
            case TipoRevision.Decimo:
            {
                var d = await _context.PlanillasDecimo.AsNoTracking()
                    .Where(x => x.Id == entidadId && x.TenantId == tenantId)
                    .Select(x => new { x.Numero, x.FechaPago, x.Estado })
                    .FirstOrDefaultAsync();
                if (d == null) return (null, null, "Esa partida de décimo no existe.");
                if (d.Estado == EstadoDecimo.Pagada) return (null, null, "Una partida pagada ya no se revisa.");
                return ($"Décimo {d.Numero}", $"/decimo?anio={d.FechaPago.Year}&mes={d.FechaPago.Month}&abrir={entidadId}", null);
            }
            case TipoRevision.Liquidacion:
            {
                var l = await _context.Liquidaciones.AsNoTracking()
                    .Where(x => x.Id == entidadId && x.TenantId == tenantId)
                    .Select(x => new { x.Numero, x.FechaTerminacion, x.Estado, x.Empleado!.Nombre, x.Empleado.Apellido })
                    .FirstOrDefaultAsync();
                if (l == null) return (null, null, "Esa liquidación no existe.");
                if (l.Estado is EstadoLiquidacion.Pagada or EstadoLiquidacion.Anulada)
                    return (null, null, "Una liquidación pagada o anulada ya no se revisa.");
                return ($"Liquidación {l.Numero} ({l.Nombre} {l.Apellido})",
                        $"/liquidaciones?anio={l.FechaTerminacion.Year}&mes={l.FechaTerminacion.Month}&abrir={entidadId}", null);
            }
            case TipoRevision.HorasExtra:
            {
                var anio = entidadId / 100;
                var mes = entidadId % 100;
                if (anio is < 2000 or > 2100 || mes is < 1 or > 12)
                    return (null, null, "El mes de las horas extra no es válido.");
                return ($"Horas extra de {NombresMes[mes - 1]} {anio}", $"/horas-extra?anio={anio}&mes={mes}", null);
            }
            default:
                return (null, null, "No se puede pedir revisión de esto.");
        }
    }

    /// <summary>Deja el aviso en la campana y manda el correo. Si el correo falla, el aviso queda igual.</summary>
    private async Task AvisarAsync(string usuarioId, string nombre, string email, SolicitudRevision solicitud,
        string titulo, string mensaje, string textoBoton, string? baseUrl)
    {
        _context.Avisos.Add(new Aviso
        {
            TenantId = solicitud.TenantId,
            UsuarioId = usuarioId,
            Titulo = titulo.Length > 200 ? titulo[..200] : titulo,
            Mensaje = mensaje.Length > 1000 ? mensaje[..1000] : mensaje,
            Enlace = solicitud.Enlace,
            SolicitudRevisionId = solicitud.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        // El enlace del correo sale solo de la configuración, nunca de la
        // petición: un Host falsificado no puede llevar a otro sitio.
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("App:BaseUrl no está configurado: el aviso «{Titulo}» queda solo en la campana", titulo);
            return;
        }

        try
        {
            var empresa = (await _tenant.GetCurrentTenantAsync())?.Name ?? "tu empresa";
            await _correo.SendAvisoEmailAsync(
                email, nombre,
                asunto: $"{titulo} · {empresa}",
                titulo: titulo,
                parrafos: new[] { mensaje, $"Empresa: {empresa}." },
                textoBoton: textoBoton,
                enlace: baseUrl.TrimEnd('/') + solicitud.Enlace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El aviso {Titulo} quedó en la campana pero el correo no salió", titulo);
        }
    }

    private async Task<string> NombreDeAsync(string usuarioId) =>
        await _context.Users.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.NombreCompleto ?? u.Email)
            .FirstOrDefaultAsync() ?? "Alguien de tu equipo";

    private async Task<List<SolicitudRevisionDto>> ADtosAsync(List<SolicitudRevision> lista)
    {
        var ids = lista.SelectMany(r => new[] { r.SolicitadoPor, r.RevisorId }).Distinct().ToList();
        var nombres = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto ?? u.Email ?? "");
        string N(string id) => nombres.TryGetValue(id, out var n) && n != "" ? n : "Usuario sin nombre";

        return lista.Select(r => new SolicitudRevisionDto(
            r.Id, r.Tipo, r.EntidadId, r.Referencia, r.Enlace,
            r.SolicitadoPor, N(r.SolicitadoPor), r.RevisorId, N(r.RevisorId),
            r.Comentario, r.Estado, r.Respuesta, r.RespondidaEn, r.CreatedAt)).ToList();
    }
}
