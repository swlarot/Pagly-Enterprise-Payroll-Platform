using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO para actualizar un usuario del tenant (cambiar rol o estado)
/// </summary>
public class UpdateTenantUserDto
{
    /// <summary>
    /// Nuevo rol del usuario (opcional)
    /// </summary>
    public TenantRole? Role { get; set; }

    /// <summary>
    /// Estado activo del usuario (opcional)
    /// </summary>
    public bool? IsActive { get; set; }
}
