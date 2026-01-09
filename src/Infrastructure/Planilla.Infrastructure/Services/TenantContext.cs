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
                if (tenantId <= 0)
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
                return Domain.Enums.TenantRole.Employee;
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
            return Domain.Enums.TenantRole.Employee;
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

        // Admin tiene todos excepto Owner
        if (currentRole == Domain.Enums.TenantRole.Admin && role != Domain.Enums.TenantRole.Owner)
            return true;

        // Manager tiene Manager, Accountant y Employee
        if (currentRole == Domain.Enums.TenantRole.Manager &&
            (role == Domain.Enums.TenantRole.Manager ||
             role == Domain.Enums.TenantRole.Accountant ||
             role == Domain.Enums.TenantRole.Employee))
            return true;

        // Accountant tiene Accountant y Employee
        if (currentRole == Domain.Enums.TenantRole.Accountant &&
            (role == Domain.Enums.TenantRole.Accountant ||
             role == Domain.Enums.TenantRole.Employee))
            return true;

        // Employee solo tiene Employee
        if (currentRole == Domain.Enums.TenantRole.Employee &&
            role == Domain.Enums.TenantRole.Employee)
            return true;

        return false;
    }

    public bool IsAdminOrOwner()
    {
        var currentRole = TenantRole;
        return currentRole == Domain.Enums.TenantRole.Owner ||
               currentRole == Domain.Enums.TenantRole.Admin;
    }

    public void Clear()
    {
        // Con properties calculadas, no hay nada que limpiar
        // Este método se mantiene por compatibilidad con la interfaz
    }
}
