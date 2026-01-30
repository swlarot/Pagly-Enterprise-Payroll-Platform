using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para generación de tokens JWT y gestión de refresh tokens
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Genera un token JWT (access token) para un usuario autenticado en un tenant
    /// </summary>
    string GenerateToken(string userId, string email, int tenantId, TenantRole role, string plan, bool isSystemAdmin = false);

    /// <summary>
    /// Genera un refresh token y lo almacena en la base de datos
    /// </summary>
    /// <param name="userId">ID del usuario</param>
    /// <param name="tenantId">ID del tenant</param>
    /// <param name="ipAddress">IP desde donde se genera el token</param>
    /// <returns>Token único de 256 bits</returns>
    Task<RefreshToken> GenerateRefreshTokenAsync(string userId, int tenantId, string ipAddress);

    /// <summary>
    /// Valida un refresh token y devuelve la información del usuario
    /// </summary>
    /// <param name="token">El refresh token a validar</param>
    /// <returns>RefreshToken si es válido, null si no existe o está revocado/expirado</returns>
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token);

    /// <summary>
    /// Revoca un refresh token (usado en logout o cambio de contraseña)
    /// </summary>
    /// <param name="token">El token a revocar</param>
    /// <param name="reason">Razón de revocación</param>
    /// <param name="replacedByTokenId">ID del token que lo reemplaza (si aplica)</param>
    Task<bool> RevokeRefreshTokenAsync(string token, string reason, Guid? replacedByTokenId = null);

    /// <summary>
    /// Revoca todos los refresh tokens de un usuario (usado en cambio de contraseña o sospecha de compromiso)
    /// </summary>
    Task<int> RevokeAllUserTokensAsync(string userId, string reason);
}
