using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.ApiKeys;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// CRUD de API keys para el dashboard del SaaS. Solo Owner del tenant puede
/// gestionar keys — cada key pertenece a un tenant (ITenantEntity) y el
/// global query filter garantiza aislamiento cross-tenant.
///
/// <para>
/// Auth: JWT Bearer (el dashboard React envía el token del Owner).
/// El scheme "ApiKey" es para consumers del API Platform, NO para este CRUD.
/// </para>
///
/// <para>
/// Rutas bajo <c>/api/api-keys</c> (NO <c>/v1/*</c> — esto es admin del SaaS,
/// no endpoint del API Platform).
/// </para>
/// </summary>
[ApiController]
[Route("api/api-keys")]
[Authorize(Roles = "Owner")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlanLimitService _planLimitService;
    private readonly ApplicationDbContext _db;

    public ApiKeysController(
        IApiKeyService apiKeyService,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        IPlanLimitService planLimitService,
        ApplicationDbContext db)
    {
        _apiKeyService = apiKeyService;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _planLimitService = planLimitService;
        _db = db;
    }

    /// <summary>
    /// Genera una nueva API key para el tenant actual.
    /// El plaintext retornado solo se puede ver UNA VEZ — no se puede recuperar después.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponse), 201)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<CreateApiKeyResponse>> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId <= 0)
        {
            return BadRequest(new { message = "No se pudo determinar el tenant actual." });
        }

        // Plan gate: solo planes con CanUseApi=true pueden generar keys.
        // Hoy: Professional + Enterprise. Free y Starter reciben 403.
        // El check también valida que la suscripción esté Active o en Trial.
        var canUseApi = await _planLimitService.CanUseApiAsync(tenantId);
        if (!canUseApi)
        {
            return StatusCode(403, new
            {
                error = "plan_does_not_include_api",
                message = "Tu plan actual no incluye acceso al API Platform. " +
                          "Actualiza a Professional o Enterprise para generar API keys.",
                upgradeUrl = "/billing"
            });
        }

        var expiresAt = request.ExpiresInDays.HasValue
            ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
            : (DateTime?)null;

        var (entity, plaintextKey) = await _apiKeyService.GenerateAsync(
            tenantId: tenantId,
            name: request.Name,
            mode: request.Mode,
            expiresAt: expiresAt,
            createdByUserId: _currentUserService.UserId,
            cancellationToken: cancellationToken);

        var response = new CreateApiKeyResponse(
            Key: ToDto(entity),
            PlaintextKey: plaintextKey);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, response);
    }

    /// <summary>
    /// Lista todas las API keys del tenant actual (global query filter aplica).
    /// Ordenadas por fecha de creación desc (más recientes primero).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> List(CancellationToken cancellationToken)
    {
        var keys = await _db.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = keys.Select(ToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Detalle de una key del tenant actual. 404 si no existe o no pertenece.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiKeyDto), 200)]
    public async Task<ActionResult<ApiKeyDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var key = await _db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

        if (key is null)
        {
            return NotFound(new { message = "API key no encontrada." });
        }

        return Ok(ToDto(key));
    }

    /// <summary>
    /// Revoca una API key por id. Idempotente (revocar dos veces retorna 204 ambas).
    /// Si la key pertenece a otro tenant, retorna 404 (no revela existencia).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId <= 0)
        {
            return BadRequest(new { message = "No se pudo determinar el tenant actual." });
        }

        var ok = await _apiKeyService.RevokeAsync(
            keyId: id,
            tenantId: tenantId,
            reason: "revoked_by_owner",
            cancellationToken: cancellationToken);

        if (!ok)
        {
            return NotFound(new { message = "API key no encontrada." });
        }

        return NoContent();
    }

    private static ApiKeyDto ToDto(ApiKey entity) => new(
        Id: entity.Id,
        Name: entity.Name,
        KeyPrefix: entity.KeyPrefix,
        Mode: entity.Mode,
        ExpiresAt: entity.ExpiresAt,
        LastUsedAt: entity.LastUsedAt,
        RevokedAt: entity.RevokedAt,
        RevocationReason: entity.RevocationReason,
        IsActive: entity.IsActive,
        TotalRequests: entity.TotalRequests,
        CreatedAt: entity.CreatedAt
    );
}
