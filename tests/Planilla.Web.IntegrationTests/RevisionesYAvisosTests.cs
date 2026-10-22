// ====================================================================
// Revisiones entre el equipo, de punta a punta por HTTP: pedir que otra
// persona revise, el aviso en su campana y su correo, devolver con
// comentario, aprobar, retirar, y que cada quien vea solo lo suyo.
// ====================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Xunit;

namespace Planilla.Web.IntegrationTests;

public class RevisionesYAvisosTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RevisionesYAvisosTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient Cliente(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>
    /// Otra persona de la misma empresa, con su propio usuario y un rol con
    /// esos permisos (sin permisos es como un empleado de autoservicio).
    /// </summary>
    private async Task<(string Token, string UserId, string Email)> CompaneroAsync(string tokenDueno, string nombre, params string[] permisos)
    {
        var tenantId = int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokenDueno).Claims.First(c => c.Type == "tenant_id").Value);
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = $"companero-{Guid.NewGuid():N}@example.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, NombreCompleto = nombre };
        (await users.CreateAsync(user, "Test@1234")).Succeeded.Should().BeTrue();
        CustomTenantRole? rol = null;
        if (permisos.Length > 0)
        {
            rol = new CustomTenantRole { TenantId = tenantId, Name = $"Rol de {nombre}" };
            foreach (var permiso in permisos)
                rol.Permissions.Add(new RolePermission { TenantId = tenantId, Permission = permiso });
            db.CustomTenantRoles.Add(rol);
            await db.SaveChangesAsync();
        }
        db.TenantUsers.Add(new TenantUser
        {
            TenantId = tenantId, UserId = user.Id, Role = TenantRole.User, CustomTenantRoleId = rol?.Id,
            IsActive = true, JoinedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-must-be-at-least-32-characters-long-for-hmacsha256"));
        var jwt = new JwtSecurityToken(
            issuer: "https://test.planilla.vorluno.dev", audience: "https://test.planilla.vorluno.dev",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("tenant_role", "User"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim("plan", "Professional"),
                new Claim("is_system_admin", "false"),
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), user.Id, email);
    }

    private static string IdDe(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

    private static async Task<int> PlanillaBorradorAsync(HttpClient client)
    {
        var r = await client.PostAsJsonAsync("/api/payrollheaders", new
        {
            PayrollNumber = "", PeriodStartDate = "2026-10-01", PeriodEndDate = "2026-10-15", PayPeriodType = 2,
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> AvisosAsync(HttpClient c) =>
        await c.GetFromJsonAsync<JsonElement>("/api/avisos");

    [Fact]
    public async Task PedirRevision_AvisoYCorreo_DevolverConComentario_Aprobar()
    {
        var tokenAdmin = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Colegio Revisiones E2E");
        var (tokenDirectora, directoraId, directoraEmail) = await CompaneroAsync(tokenAdmin, "Directora Rivas", SystemPermission.PayrollView);
        var (tokenEmpleado, empleadoId, _) = await CompaneroAsync(tokenAdmin, "Docente sin permisos");
        var admin = Cliente(tokenAdmin);
        var directora = Cliente(tokenDirectora);
        var planillaId = await PlanillaBorradorAsync(admin);

        // A quién se le puede pedir: a la directora; no a uno mismo ni a quien no puede ver planillas.
        var revisores = (await admin.GetFromJsonAsync<JsonElement>("/api/revisiones/revisores?tipo=1")).EnumerateArray().ToList();
        revisores.Select(r => r.GetProperty("userId").GetString()).Should().Contain(directoraId)
            .And.NotContain(IdDe(tokenAdmin)).And.NotContain(empleadoId);
        (await admin.PostAsJsonAsync("/api/revisiones", new { tipo = 1, entidadId = planillaId, revisorId = empleadoId }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "no puede revisar lo que no puede abrir");

        // Un empleado sin permisos no ve la lista de usuarios ni el historial, ni pide revisiones.
        var empleado = Cliente(tokenEmpleado);
        (await empleado.GetAsync("/api/revisiones/revisores?tipo=1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await empleado.GetAsync($"/api/revisiones?tipo=1&entidadId={planillaId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await empleado.PostAsJsonAsync("/api/revisiones", new { tipo = 1, entidadId = planillaId, revisorId = directoraId }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 1) Pedir la revisión: le llega el aviso a la campana y el correo.
        var pedir = await admin.PostAsJsonAsync("/api/revisiones", new
        {
            tipo = (int)TipoRevision.Planilla, entidadId = planillaId, revisorId = directoraId, comentario = "Revisa las horas de Marta",
        });
        pedir.StatusCode.Should().Be(HttpStatusCode.Created);
        var solicitud = await pedir.Content.ReadFromJsonAsync<JsonElement>();
        var solicitudId = solicitud.GetProperty("id").GetInt32();
        solicitud.GetProperty("revisor").GetString().Should().Be("Directora Rivas");

        (await admin.PostAsJsonAsync("/api/revisiones", new { tipo = 1, entidadId = planillaId, revisorId = directoraId }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "ya hay una pendiente para esa planilla");

        var avisosDirectora = await AvisosAsync(directora);
        avisosDirectora.GetProperty("noLeidos").GetInt32().Should().Be(1);
        var aviso = avisosDirectora.GetProperty("items")[0];
        aviso.GetProperty("titulo").GetString().Should().StartWith("Revisión pendiente: Planilla");
        aviso.GetProperty("enlace").GetString().Should().Be($"/planillas?anio=2026&mes=10&abrir={planillaId}");

        _factory.Correos.Avisos.Should().Contain(c => c.Para == directoraEmail && c.Enlace.EndsWith($"/planillas?anio=2026&mes=10&abrir={planillaId}"));
        (await AvisosAsync(admin)).GetProperty("noLeidos").GetInt32().Should().Be(0, "quien pide no recibe su propio aviso");

        (await directora.GetFromJsonAsync<JsonElement>("/api/revisiones/para-mi")).GetArrayLength().Should().Be(1);

        // 2) Solo la directora responde, y para devolver hace falta decir qué corregir.
        (await admin.PostAsJsonAsync($"/api/revisiones/{solicitudId}/aprobar", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await directora.PostAsJsonAsync($"/api/revisiones/{solicitudId}/devolver", new { comentario = " " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var devolver = await directora.PostAsJsonAsync($"/api/revisiones/{solicitudId}/devolver", new { comentario = "Faltan las horas del día 7" });
        devolver.StatusCode.Should().Be(HttpStatusCode.OK);
        (await devolver.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetInt32().Should().Be((int)EstadoRevision.Devuelta);

        var avisosAdmin = await AvisosAsync(admin);
        avisosAdmin.GetProperty("noLeidos").GetInt32().Should().Be(1);
        avisosAdmin.GetProperty("items")[0].GetProperty("mensaje").GetString().Should().Contain("Faltan las horas del día 7");
        (await directora.GetFromJsonAsync<JsonElement>("/api/revisiones/para-mi")).GetArrayLength().Should().Be(0);

        // 3) Corregida, se pide otra vez y esta vez se aprueba.
        var otra = await admin.PostAsJsonAsync("/api/revisiones", new { tipo = 1, entidadId = planillaId, revisorId = directoraId });
        otra.StatusCode.Should().Be(HttpStatusCode.Created);
        var otraId = (await otra.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await directora.PostAsJsonAsync($"/api/revisiones/{otraId}/aprobar", new { comentario = "Todo bien" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var historial = (await admin.GetFromJsonAsync<JsonElement>($"/api/revisiones?tipo=1&entidadId={planillaId}")).EnumerateArray().ToList();
        historial.Select(h => h.GetProperty("estado").GetInt32()).Should().Equal((int)EstadoRevision.Aprobada, (int)EstadoRevision.Devuelta);

        // 4) Leer todo deja la campana en cero.
        (await admin.PostAsync("/api/avisos/leer-todos", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AvisosAsync(admin)).GetProperty("noLeidos").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task RetirarHorasExtraDelMes_YCadaQuienVeSoloLoSuyo()
    {
        var tokenAdmin = await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Colegio Retirar E2E");
        var (tokenDirectora, directoraId, _) = await CompaneroAsync(tokenAdmin, "Director Pinzón", SystemPermission.OvertimeApprove, SystemPermission.PayrollView);
        var admin = Cliente(tokenAdmin);
        var directora = Cliente(tokenDirectora);

        var pedir = await admin.PostAsJsonAsync("/api/revisiones", new { tipo = (int)TipoRevision.HorasExtra, entidadId = 202610, revisorId = directoraId });
        pedir.StatusCode.Should().Be(HttpStatusCode.Created);
        var s = await pedir.Content.ReadFromJsonAsync<JsonElement>();
        s.GetProperty("referencia").GetString().Should().Be("Horas extra de octubre 2026");
        var id = s.GetProperty("id").GetInt32();

        // Otra empresa no ve ni la solicitud ni el aviso.
        var ajeno = Cliente(await TestTenantHelper.CreateTenantAndGetTokenAsync(_factory, "Empresa Ajena E2E"));
        (await ajeno.GetFromJsonAsync<JsonElement>("/api/revisiones?tipo=4&entidadId=202610")).GetArrayLength().Should().Be(0);
        (await ajeno.PostAsync($"/api/revisiones/{id}/retirar", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // La directora no puede retirarla; quien la pidió sí, y su aviso queda leído.
        (await directora.PostAsync($"/api/revisiones/{id}/retirar", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AvisosAsync(directora)).GetProperty("noLeidos").GetInt32().Should().Be(1);
        var retirar = await admin.PostAsync($"/api/revisiones/{id}/retirar", null);
        retirar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await retirar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetInt32().Should().Be((int)EstadoRevision.Cancelada);
        (await AvisosAsync(directora)).GetProperty("noLeidos").GetInt32().Should().Be(0);

        // Un mes inválido o una planilla que no existe no se pueden revisar.
        (await admin.PostAsJsonAsync("/api/revisiones", new { tipo = 4, entidadId = 202613, revisorId = directoraId }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync("/api/revisiones", new { tipo = 1, entidadId = 999999, revisorId = directoraId }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
