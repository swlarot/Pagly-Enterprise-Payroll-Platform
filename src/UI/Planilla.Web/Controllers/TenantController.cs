using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.DTOs.Tenant;
using Vorluno.Planilla.Application.Interfaces;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador para gestión del tenant actual (actualización, usuarios, audit log)
/// SEGURIDAD: Todos los endpoints requieren autenticación
/// CRÍTICO: Todas las queries filtradas por TenantId automáticamente
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ITenantManagementService _tenantService;
    private readonly IInvitationService _invitationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TenantController> _logger;

    public TenantController(
        ITenantManagementService tenantService,
        IInvitationService invitationService,
        IAuditLogService auditLogService,
        ILogger<TenantController> logger)
    {
        _tenantService = tenantService;
        _invitationService = invitationService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/tenant - Obtiene información del tenant actual con suscripción y métricas de uso
    /// Roles: Todos los autenticados pueden ver info de su tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCurrentTenant()
    {
        var result = await _tenantService.GetCurrentTenantAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// PUT /api/tenant - Actualiza información del tenant actual
    /// Roles: Owner, Admin
    /// Audit Log: TenantUpdated
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> UpdateTenant([FromBody] UpdateTenantDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _tenantService.UpdateTenantAsync(dto);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// GET /api/tenant/users - Lista usuarios del tenant con rol, email, estado
    /// Roles: Owner, Admin
    /// </summary>
    [HttpGet("users")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> GetTenantUsers()
    {
        var result = await _tenantService.GetTenantUsersAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// PATCH /api/tenant/users/{tenantUserId} - Cambia rol o activa/desactiva usuario
    /// Roles: Owner, Admin
    /// Validación: Owner no puede cambiar su propio rol si es el único Owner
    /// Audit Log: UserRoleChanged o UserStatusChanged
    /// </summary>
    [HttpPatch("users/{tenantUserId}")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> UpdateTenantUser(int tenantUserId, [FromBody] UpdateTenantUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _tenantService.UpdateTenantUserAsync(tenantUserId, dto);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// DELETE /api/tenant/users/{tenantUserId} - Elimina (soft delete) usuario del tenant
    /// Roles: Owner, Admin
    /// Validación: Owner no puede eliminarse a sí mismo
    /// Audit Log: UserRemovedFromTenant
    /// </summary>
    [HttpDelete("users/{tenantUserId}")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> RemoveTenantUser(int tenantUserId)
    {
        var result = await _tenantService.RemoveTenantUserAsync(tenantUserId);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// POST /api/tenant/invite - Crea invitación para nuevo usuario
    /// Roles: Owner, Admin
    /// Validación: Verifica límite MaxUsers del plan
    /// Audit Log: InviteCreated
    /// Respuesta: Token e InviteUrl para compartir
    /// </summary>
    [HttpPost("invite")]
    [Authorize(Policy = "TenantInvite")]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _invitationService.CreateInvitationAsync(dto);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetPendingInvitations), new { }, result.Value);
    }

    /// <summary>
    /// GET /api/tenant/invitations - Lista invitaciones pendientes del tenant
    /// Roles: Owner, Admin
    /// </summary>
    [HttpGet("invitations")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> GetPendingInvitations()
    {
        var result = await _invitationService.GetPendingInvitationsAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// DELETE /api/tenant/invitations/{invitationId} - Revoca una invitación
    /// Roles: Owner, Admin
    /// Audit Log: InviteRevoked
    /// </summary>
    [HttpDelete("invitations/{invitationId}")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> RevokeInvitation(int invitationId)
    {
        var result = await _invitationService.RevokeInvitationAsync(invitationId);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// GET /api/tenant/audit - Obtiene audit log del tenant con filtros y paginación
    /// Roles: Owner, Admin
    /// Query params: from, to, action, entityType, actorEmail, page, pageSize
    /// </summary>
    [HttpGet("audit")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> GetAuditLog([FromQuery] AuditLogFilterDto filter)
    {
        var result = await _auditLogService.GetAuditLogAsync(filter);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// GET /api/tenant/audit/{entityType}/{entityId} - Obtiene audit log de una entidad específica
    /// Roles: Owner, Admin
    /// </summary>
    [HttpGet("audit/{entityType}/{entityId}")]
    [Authorize(Policy = "TenantManageUsers")]
    public async Task<IActionResult> GetEntityAuditLog(string entityType, string entityId)
    {
        var result = await _auditLogService.GetEntityAuditLogAsync(entityType, entityId);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Value);
    }
}
