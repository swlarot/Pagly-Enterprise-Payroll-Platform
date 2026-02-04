using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.DTOs.Roles;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para gestión de roles personalizados del tenant
/// </summary>
[Authorize]
[ApiController]
[Route("api/tenants/roles")]
public class CustomRolesController : ControllerBase
{
    private readonly ICustomTenantRoleService _roleService;
    private readonly ILogger<CustomRolesController> _logger;

    public CustomRolesController(
        ICustomTenantRoleService roleService,
        ILogger<CustomRolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los roles del tenant actual (sistema + personalizados)
    /// </summary>
    [HttpGet]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> GetAllRoles()
    {
        var result = await _roleService.GetAllRolesAsync();

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene un rol específico por su ID
    /// </summary>
    [HttpGet("{roleId}")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> GetRoleById(int roleId)
    {
        var result = await _roleService.GetRoleByIdAsync(roleId);

        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Crea un nuevo rol personalizado
    /// </summary>
    [HttpPost]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> CreateRole([FromBody] CreateCustomTenantRoleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _roleService.CreateRoleAsync(dto);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        var created = result.Value;
        if (created == null)
            return StatusCode(500, new { error = "Error al crear el rol" });

        return CreatedAtAction(
            nameof(GetRoleById),
            new { roleId = created.Id },
            created);
    }

    /// <summary>
    /// Actualiza un rol personalizado existente
    /// </summary>
    [HttpPut("{roleId}")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateCustomTenantRoleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _roleService.UpdateRoleAsync(roleId, dto);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Elimina un rol personalizado
    /// </summary>
    [HttpDelete("{roleId}")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> DeleteRole(int roleId)
    {
        var result = await _roleService.DeleteRoleAsync(roleId);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Rol eliminado exitosamente" });
    }

    /// <summary>
    /// Obtiene los permisos asignados a un rol
    /// </summary>
    [HttpGet("{roleId}/permissions")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> GetRolePermissions(int roleId)
    {
        var result = await _roleService.GetRolePermissionsAsync(roleId);

        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Actualiza los permisos de un rol
    /// </summary>
    [HttpPut("{roleId}/permissions")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> UpdateRolePermissions(
        int roleId,
        [FromBody] UpdateRolePermissionsDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _roleService.UpdateRolePermissionsAsync(roleId, dto);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Permisos actualizados exitosamente" });
    }

    /// <summary>
    /// Obtiene todos los permisos disponibles en el sistema
    /// </summary>
    [HttpGet("/api/permissions")]
    [Authorize]
    public async Task<IActionResult> GetAllPermissions()
    {
        var result = await _roleService.GetAllPermissionsAsync();

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Obtiene los permisos del usuario actual
    /// </summary>
    [HttpGet("/api/permissions/me")]
    [Authorize]
    public async Task<IActionResult> GetMyPermissions()
    {
        var result = await _roleService.GetCurrentUserPermissionsAsync();

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Value);
    }

    /// <summary>
    /// Verifica si el usuario actual tiene un permiso específico
    /// </summary>
    [HttpGet("/api/permissions/check/{permission}")]
    [Authorize]
    public async Task<IActionResult> CheckPermission(string permission)
    {
        var result = await _roleService.HasPermissionAsync(permission);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { hasPermission = result.Value });
    }
}
