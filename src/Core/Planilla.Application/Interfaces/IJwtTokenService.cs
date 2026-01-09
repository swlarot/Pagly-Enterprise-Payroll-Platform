using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para generación de tokens JWT
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Genera un token JWT para un usuario autenticado en un tenant
    /// </summary>
    string GenerateToken(string userId, string email, int tenantId, TenantRole role, string plan);
}
