// ====================================================================
// Cambios sensibles con confirmación de otra persona: si la empresa lo
// activa, un cambio de salario o una baja no se aplican al guardarlos;
// quedan pendientes hasta que otra persona con permiso los confirme (o los
// rechace con su razón). Avisos en la campana y por correo; todo queda en
// el historial y en la auditoría.
// ====================================================================

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public sealed record CambioPendienteDto(
    int Id, int EmpleadoId, string Empleado, string Tipo, decimal? SalarioAnterior, decimal? SalarioNuevo,
    string SolicitadoPor, bool EsMio, bool PuedoConfirmar, string Estado, DateTime CreatedAt,
    string? ResueltoPor, DateTime? ResueltoEn, string? Comentario);

public class CambiosSensiblesService
{
    private const string Clave = "confirmarCambiosSensibles";

    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IBrevoEmailService _correo;
    private readonly IAuditLogService _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<CambiosSensiblesService> _logger;

    public CambiosSensiblesService(ApplicationDbContext context, ITenantContext tenant, IBrevoEmailService correo,
        IAuditLogService audit, IConfiguration config, ILogger<CambiosSensiblesService> logger)
    {
        _context = context;
        _tenant = tenant;
        _correo = correo;
        _audit = audit;
        _config = config;
        _logger = logger;
    }

    // ----------------------------------------------------------------
    // La opción de la empresa (en Tenant.Settings, JSON)
    // ----------------------------------------------------------------

    public async Task<bool> ActivoAsync()
    {
        var settings = await _context.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId).Select(t => t.Settings).FirstOrDefaultAsync();
        try
        {
            return !string.IsNullOrWhiteSpace(settings)
                && JsonNode.Parse(settings)?[Clave]?.GetValue<bool>() == true;
        }
        catch (Exception) { return false; }
    }

    public async Task ActivarAsync(bool activo, string usuario)
    {
        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenant.TenantId);
        JsonObject obj;
        try { obj = string.IsNullOrWhiteSpace(tenant.Settings) ? new JsonObject() : JsonNode.Parse(tenant.Settings) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { obj = new JsonObject(); }
        obj[Clave] = activo;
        tenant.Settings = obj.ToJsonString();
        await _context.SaveChangesAsync();
        await _audit.LogAsync(activo ? "CambiosSensiblesActivados" : "CambiosSensiblesDesactivados", "Tenant", tenant.Id.ToString(),
            new Dictionary<string, string> { ["usuario"] = usuario });
    }

    // ----------------------------------------------------------------
    // Pedir
    // ----------------------------------------------------------------

    public async Task<(CambioPendiente? Cambio, string? Error)> PedirSalarioAsync(Empleado empleado, decimal anterior, decimal nuevo, string solicitante)
    {
        if (await HayPendienteAsync(empleado.Id, CambioPendiente.TipoSalario))
            return (null, "Ya hay un cambio de salario de esta persona esperando confirmación.");
        var cambio = await CrearAsync(empleado, CambioPendiente.TipoSalario, anterior, nuevo, solicitante);
        return (cambio, null);
    }

    public async Task<(CambioPendiente? Cambio, string? Error)> PedirBajaAsync(Empleado empleado, string solicitante)
    {
        if (await HayPendienteAsync(empleado.Id, CambioPendiente.TipoBaja))
            return (null, "La baja de esta persona ya está esperando confirmación.");
        var cambio = await CrearAsync(empleado, CambioPendiente.TipoBaja, null, null, solicitante);
        return (cambio, null);
    }

    private Task<bool> HayPendienteAsync(int empleadoId, string tipo) =>
        _context.CambiosPendientes.AnyAsync(c => c.TenantId == _tenant.TenantId && c.EmpleadoId == empleadoId && c.Tipo == tipo && c.Estado == "Pendiente");

    private async Task<CambioPendiente> CrearAsync(Empleado empleado, string tipo, decimal? anterior, decimal? nuevo, string solicitante)
    {
        var cambio = new CambioPendiente
        {
            TenantId = _tenant.TenantId, EmpleadoId = empleado.Id, Tipo = tipo,
            SalarioAnterior = anterior, SalarioNuevo = nuevo, SolicitadoPor = solicitante, CreatedAt = DateTime.UtcNow,
        };
        _context.CambiosPendientes.Add(cambio);
        await _context.SaveChangesAsync();

        var quien = await NombreDeAsync(solicitante);
        var persona = $"{empleado.Nombre} {empleado.Apellido}";
        var titulo = tipo == CambioPendiente.TipoSalario ? $"Cambio de salario por confirmar: {persona}" : $"Baja por confirmar: {persona}";
        var mensaje = tipo == CambioPendiente.TipoSalario
            ? $"{quien} cambió el salario de {persona} de {anterior:N2} a {nuevo:N2}. No se aplica hasta que otra persona lo confirme."
            : $"{quien} dio de baja a {persona}. No se aplica hasta que otra persona lo confirme.";
        var permiso = tipo == CambioPendiente.TipoSalario ? SystemPermission.EmployeesUpdate : SystemPermission.EmployeesDelete;

        foreach (var u in await ConPermisoAsync(permiso, excepto: solicitante, afectado: empleado.UserId))
            await AvisarAsync(u.Id, u.Nombre, u.Email, titulo, mensaje, "Confirmar en Pagly", cambio.Id);

        await _audit.LogAsync(tipo == CambioPendiente.TipoSalario ? "CambioSalarioPedido" : "BajaPedida", "CambioPendiente", cambio.Id.ToString(),
            new Dictionary<string, string>
            {
                ["empleadoId"] = empleado.Id.ToString(),
                ["anterior"] = anterior?.ToString("N2") ?? string.Empty,
                ["nuevo"] = nuevo?.ToString("N2") ?? string.Empty,
            });
        return cambio;
    }

    // ----------------------------------------------------------------
    // Ver y resolver
    // ----------------------------------------------------------------

    /// <summary>Los pendientes de la empresa (con si puedo confirmarlos) y los que pedí.</summary>
    public async Task<List<CambioPendienteDto>> ListarAsync(string usuario)
    {
        var tenantId = _tenant.TenantId;
        var lista = await _context.CambiosPendientes.AsNoTracking().Include(c => c.Empleado)
            .Where(c => c.TenantId == tenantId && (c.Estado == "Pendiente" || c.SolicitadoPor == usuario))
            .OrderByDescending(c => c.Estado == "Pendiente").ThenByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync();

        var puedeSalario = await TienePermisoAsync(usuario, SystemPermission.EmployeesUpdate);
        var puedeBaja = await TienePermisoAsync(usuario, SystemPermission.EmployeesDelete);

        // Cada quien ve solo lo que puede resolver o lo que pidió: quien solo da
        // de baja no ve montos de salario.
        lista = lista.Where(c => c.SolicitadoPor == usuario
                              || (c.Tipo == CambioPendiente.TipoSalario ? puedeSalario : puedeBaja)).ToList();
        var ids = lista.SelectMany(c => new[] { c.SolicitadoPor, c.ResueltoPor }).Where(x => x != null).Select(x => x!).Distinct().ToList();
        var nombres = await _context.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto ?? u.Email ?? "");
        string N(string? id) => id != null && nombres.TryGetValue(id, out var n) && n != "" ? n : "Alguien de tu equipo";

        return lista.Select(c => new CambioPendienteDto(
            c.Id, c.EmpleadoId, $"{c.Empleado.Nombre} {c.Empleado.Apellido}", c.Tipo, c.SalarioAnterior, c.SalarioNuevo,
            N(c.SolicitadoPor), c.SolicitadoPor == usuario,
            c.Estado == "Pendiente" && c.SolicitadoPor != usuario && c.Empleado.UserId != usuario
                && (c.Tipo == CambioPendiente.TipoSalario ? puedeSalario : puedeBaja),
            c.Estado, c.CreatedAt, c.ResueltoPor == null ? null : N(c.ResueltoPor), c.ResueltoEn, c.Comentario)).ToList();
    }

    public async Task<string?> ConfirmarAsync(int id, string usuario)
    {
        var (cambio, error) = await PendienteParaResolverAsync(id, usuario);
        if (error != null) return error;

        var empleado = await _context.Empleados.FirstAsync(e => e.Id == cambio!.EmpleadoId && e.TenantId == cambio.TenantId);
        var ahora = DateTime.UtcNow;
        if (cambio!.Tipo == CambioPendiente.TipoSalario)
        {
            empleado.SalarioBase = cambio.SalarioNuevo!.Value;
            _context.HistorialSalarial.Add(new HistorialSalarial
            {
                EmpleadoId = empleado.Id, SalarioMensual = empleado.SalarioBase, FechaVigencia = ahora,
                Motivo = "Ajuste salarial confirmado por otra persona", CreatedAt = ahora, TenantId = empleado.TenantId,
            });
        }
        else
        {
            empleado.IsDeleted = true;
            empleado.DeletedAt = ahora;
            empleado.DeletedBy = cambio.SolicitadoPor;
            empleado.EstaActivo = false;
        }

        cambio.Estado = "Confirmado";
        cambio.ResueltoPor = usuario;
        cambio.ResueltoEn = ahora;
        await _context.SaveChangesAsync();
        await AvisarAlSolicitanteAsync(cambio, empleado, confirmado: true, usuario);
        await _audit.LogAsync(cambio.Tipo == CambioPendiente.TipoSalario ? "CambioSalarioConfirmado" : "BajaConfirmada", "CambioPendiente", id.ToString(),
            new Dictionary<string, string> { ["empleadoId"] = empleado.Id.ToString(), ["confirmadoPor"] = usuario });
        return null;
    }

    public async Task<string?> RechazarAsync(int id, string usuario, string? comentario)
    {
        var texto = comentario?.Trim();
        if (string.IsNullOrEmpty(texto)) return "Escribe por qué no se aplica.";
        if (texto.Length > 500) return "La razón puede tener hasta 500 caracteres.";
        var (cambio, error) = await PendienteParaResolverAsync(id, usuario);
        if (error != null) return error;

        cambio!.Estado = "Rechazado";
        cambio.ResueltoPor = usuario;
        cambio.ResueltoEn = DateTime.UtcNow;
        cambio.Comentario = texto;
        await _context.SaveChangesAsync();
        var empleado = await _context.Empleados.AsNoTracking().FirstAsync(e => e.Id == cambio.EmpleadoId);
        await AvisarAlSolicitanteAsync(cambio, empleado, confirmado: false, usuario);
        await _audit.LogAsync("CambioSensibleRechazado", "CambioPendiente", id.ToString(),
            new Dictionary<string, string> { ["tipo"] = cambio.Tipo, ["razon"] = texto });
        return null;
    }

    public async Task<string?> RetirarAsync(int id, string usuario)
    {
        var cambio = await _context.CambiosPendientes.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenant.TenantId);
        if (cambio == null) return "Ese cambio no existe.";
        if (cambio.SolicitadoPor != usuario) return "Solo quien lo pidió puede retirarlo.";
        if (cambio.Estado != "Pendiente") return "Ese cambio ya se resolvió.";
        cambio.Estado = "Retirado";
        cambio.ResueltoPor = usuario;
        cambio.ResueltoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _audit.LogAsync("CambioSensibleRetirado", "CambioPendiente", id.ToString(), new Dictionary<string, string> { ["tipo"] = cambio.Tipo });
        return null;
    }

    /// <summary>Pendiente, de esta empresa, y lo resuelve otra persona con el permiso que toca.</summary>
    private async Task<(CambioPendiente? Cambio, string? Error)> PendienteParaResolverAsync(int id, string usuario)
    {
        var cambio = await _context.CambiosPendientes.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenant.TenantId);
        if (cambio == null) return (null, "Ese cambio no existe.");
        if (cambio.Estado != "Pendiente") return (null, "Ese cambio ya se resolvió.");
        if (cambio.SolicitadoPor == usuario) return (null, "Lo tiene que confirmar otra persona, no quien lo pidió.");
        var usuarioAfectado = await _context.Empleados.AsNoTracking()
            .Where(e => e.Id == cambio.EmpleadoId && e.TenantId == cambio.TenantId).Select(e => e.UserId).FirstOrDefaultAsync();
        if (usuarioAfectado != null && usuarioAfectado == usuario)
            return (null, "Un cambio sobre tu propia ficha lo tiene que resolver otra persona.");
        var permiso = cambio.Tipo == CambioPendiente.TipoSalario ? SystemPermission.EmployeesUpdate : SystemPermission.EmployeesDelete;
        if (!await TienePermisoAsync(usuario, permiso)) return (null, "No tienes permiso para confirmar este cambio.");
        return (cambio, null);
    }

    // ----------------------------------------------------------------
    // Permisos, avisos y nombres
    // ----------------------------------------------------------------

    private async Task<bool> TienePermisoAsync(string usuarioId, string permiso)
    {
        var tu = await _context.TenantUsers.AsNoTracking()
            .Include(x => x.CustomRole).ThenInclude(r => r!.Permissions)
            .FirstOrDefaultAsync(x => x.UserId == usuarioId && x.TenantId == _tenant.TenantId && x.IsActive);
        if (tu == null) return false;
        var permisos = tu.CustomTenantRoleId.HasValue && tu.CustomRole != null
            ? tu.CustomRole.Permissions.Where(p => p.IsActive).Select(p => p.Permission).ToList()
            : SystemPermission.GetDefaultPermissionsForSystemRole(tu.Role);
        return permisos.Contains(permiso);
    }

    private async Task<List<(string Id, string Nombre, string Email)>> ConPermisoAsync(string permiso, string excepto, string? afectado)
    {
        var candidatos = await _context.TenantUsers.AsNoTracking()
            .Where(tu => tu.TenantId == _tenant.TenantId && tu.IsActive && !tu.IsPendingInvitation
                      && tu.UserId != excepto && (afectado == null || tu.UserId != afectado)
                      && tu.User != null && !tu.User.IsDeleted)
            .Select(tu => new { tu.UserId, Nombre = tu.User!.NombreCompleto ?? tu.User.Email ?? "", Email = tu.User.Email ?? "" })
            .ToListAsync();
        var lista = new List<(string, string, string)>();
        foreach (var c in candidatos)
            if (await TienePermisoAsync(c.UserId, permiso)) lista.Add((c.UserId, c.Nombre, c.Email));
        return lista;
    }

    private async Task AvisarAlSolicitanteAsync(CambioPendiente cambio, Empleado empleado, bool confirmado, string resolvio)
    {
        var solicitante = await _context.Users.AsNoTracking().Where(u => u.Id == cambio.SolicitadoPor)
            .Select(u => new { Nombre = u.NombreCompleto ?? u.Email ?? "", Email = u.Email ?? "" }).FirstOrDefaultAsync();
        if (solicitante == null) return;
        var persona = $"{empleado.Nombre} {empleado.Apellido}";
        var que = cambio.Tipo == CambioPendiente.TipoSalario ? $"El cambio de salario de {persona}" : $"La baja de {persona}";
        var quien = await NombreDeAsync(resolvio);
        await AvisarAsync(cambio.SolicitadoPor, solicitante.Nombre, solicitante.Email,
            confirmado ? $"{que}: confirmado" : $"{que}: no se aplicó",
            confirmado ? $"{quien} confirmó: {que.ToLowerInvariant()} ya está aplicado." : $"{quien} no lo aplicó: «{cambio.Comentario}».",
            "Abrir en Pagly", cambio.Id);
    }

    private async Task AvisarAsync(string usuarioId, string nombre, string email, string titulo, string mensaje, string boton, int cambioId)
    {
        _context.Avisos.Add(new Aviso
        {
            TenantId = _tenant.TenantId, UsuarioId = usuarioId,
            Titulo = titulo.Length > 200 ? titulo[..200] : titulo,
            Mensaje = mensaje.Length > 1000 ? mensaje[..1000] : mensaje,
            Enlace = "/avisos", CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        // El enlace del correo sale solo de la configuración, nunca de la petición.
        var baseUrl = _config["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        try
        {
            var empresa = (await _tenant.GetCurrentTenantAsync())?.Name ?? "tu empresa";
            await _correo.SendAvisoEmailAsync(email, nombre, $"{titulo} · {empresa}", titulo,
                new[] { mensaje, $"Empresa: {empresa}." }, boton, baseUrl.TrimEnd('/') + "/avisos");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El aviso del cambio {Id} quedó en la campana pero el correo no salió", cambioId);
        }
    }

    private async Task<string> NombreDeAsync(string usuarioId) =>
        await _context.Users.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.NombreCompleto ?? u.Email).FirstOrDefaultAsync()
        ?? "Alguien de tu equipo";
}
