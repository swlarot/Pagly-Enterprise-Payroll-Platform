using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Services;

/// <summary>
/// Servicio para generación de tokens JWT
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public JwtTokenService(IConfiguration configuration, ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    /// <summary>
    /// Genera un token JWT para un usuario autenticado en un tenant
    /// </summary>
    public string GenerateToken(string userId, string email, int tenantId, TenantRole role, string plan, bool isSystemAdmin = false, string? nombreCompleto = null)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("tenant_role", role.ToString()),
            new Claim("plan", plan),
            new Claim("is_system_admin", isSystemAdmin.ToString().ToLower()),
            new Claim(ClaimTypes.Role, role.ToString())
        };

        if (!string.IsNullOrEmpty(nombreCompleto))
            claims.Add(new Claim("nombre_completo", nombreCompleto));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(24);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Genera un refresh token único y seguro (256 bits de entropía)
    /// </summary>
    public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, int tenantId, string ipAddress)
    {
        // Generar token aleatorio de 256 bits
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);

        // Obtener duración del refresh token desde configuración (default: 7 días)
        var refreshTokenExpirationDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    /// <summary>
    /// Valida un refresh token y devuelve sus datos si es válido
    /// </summary>
    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .Include(rt => rt.Tenant)
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (refreshToken == null)
            return null;

        // Verificar si el token está activo (no revocado y no expirado)
        if (!refreshToken.IsActive)
            return null;

        return refreshToken;
    }

    /// <summary>
    /// Revoca un refresh token
    /// </summary>
    public async Task<bool> RevokeRefreshTokenAsync(string token, string reason, Guid? replacedByTokenId = null)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (refreshToken == null)
            return false;

        // Marcar como revocado
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedReason = reason;
        refreshToken.ReplacedByTokenId = replacedByTokenId;

        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Revoca todos los refresh tokens de un usuario
    /// </summary>
    public async Task<int> RevokeAllUserTokensAsync(string userId, string reason)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        _context.RefreshTokens.UpdateRange(activeTokens);
        await _context.SaveChangesAsync();

        return activeTokens.Count;
    }
}
