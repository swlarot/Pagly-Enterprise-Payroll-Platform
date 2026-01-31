using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO resumido de tenant para selección multi-tenant
/// Usado cuando un usuario pertenece a múltiples empresas
/// </summary>
public class TenantSummaryDto
{
    /// <summary>
    /// ID del tenant
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nombre de la empresa
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rol del usuario en este tenant
    /// </summary>
    public TenantRole Role { get; set; }

    /// <summary>
    /// Nombre legible del rol
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Subdomain del tenant
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;
}
