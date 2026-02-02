using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Implementación del contexto de tenant que obtiene información del usuario autenticado
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Lazy<ApplicationDbContext> _context;

    public TenantContext(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        // Use lazy loading to break circular dependency
        _context = new Lazy<ApplicationDbContext>(() =>
            serviceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public int TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
            if (claim != null && int.TryParse(claim.Value, out var tenantId))
            {
                // SystemAdmins pueden tener tenant_id = 0
                var isSystemAdmin = IsSystemAdmin;
                if (tenantId <= 0 && !isSystemAdmin)
                {
                    throw new UnauthorizedAccessException("Invalid tenant context: TenantId must be greater than 0");
                }
                return tenantId;
            }
            return 0; // Unauthenticated requests (login/register endpoints)
        }
    }

    public TenantRole TenantRole
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_role");
            if (claim == null || string.IsNullOrWhiteSpace(claim.Value))
            {
                return Domain.Enums.TenantRole.User;
            }

            // Intentar parsear como string (ej: "Admin")
            if (Enum.TryParse<Domain.Enums.TenantRole>(claim.Value, ignoreCase: true, out var roleFromString))
            {
                return roleFromString;
            }

            // Intentar parsear como número (ej: "1")
            if (int.TryParse(claim.Value, out var roleNumber) &&
                Enum.IsDefined(typeof(Domain.Enums.TenantRole), roleNumber))
            {
                return (Domain.Enums.TenantRole)roleNumber;
            }

            // Default a Employee si no se puede parsear
            return Domain.Enums.TenantRole.User;
        }
    }

    public string? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim?.Value;
        }
    }

    public bool IsSystemAdmin
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("is_system_admin");
            return claim?.Value == "true";
        }
    }

    public bool HasTenant => TenantId > 0;

    public async Task SetTenantAsync(int tenantId)
    {
        // Verificar que el tenant existe y está activo
        var tenant = await _context.Value.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} no existe o no está activo");
        }

        // En una implementación más completa, aquí podríamos actualizar claims
        // Por ahora, solo validamos que existe
    }

    public async Task<Tenant?> GetCurrentTenantAsync()
    {
        if (TenantId == 0)
            return null;

        return await _context.Value.Tenants
            .Include(t => t.Subscription)
            .FirstOrDefaultAsync(t => t.Id == TenantId && t.IsActive);
    }

    public bool HasRole(Domain.Enums.TenantRole role)
    {
        var currentRole = TenantRole;

        // Owner tiene todos los permisos
        if (currentRole == Domain.Enums.TenantRole.Owner)
            return true;

        // Verificación exacta para otros roles
        return currentRole == role;
    }

    public bool IsOwner()
    {
        return TenantRole == Domain.Enums.TenantRole.Owner;
    }

    [Obsolete("Use IsOwner() instead")]
    public bool IsAdminOrOwner()
    {
        // Por compatibilidad, retorna IsOwner()
        return IsOwner();
    }

    public void Clear()
    {
        // Con properties calculadas, no hay nada que limpiar
        // Este método se mantiene por compatibilidad con la interfaz
    }
}
