// ====================================================================
// Planilla - CustomRolesSeeder
// Created: 2026-01-31
// Descripción: Seeder para crear roles predeterminados del sistema
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Infrastructure.Data;

/// <summary>
/// Seeder para crear los roles del sistema en cada tenant.
/// Crea roles predeterminados (Owner, Admin, Manager, Accountant, Employee) con sus permisos correspondientes.
/// </summary>
public static class CustomRolesSeeder
{
    /// <summary>
    /// Crea los roles del sistema para todos los tenants que no los tengan.
    /// </summary>
    public static async Task SeedAsync(
        ApplicationDbContext context,
        ILogger? logger = null)
    {
        logger?.LogInformation("Iniciando seed de roles del sistema...");

        try
        {
            // Obtener todos los tenants activos
            var tenants = await context.Tenants
                .Where(t => t.IsActive)
                .ToListAsync();

            logger?.LogInformation("Encontrados {Count} tenants activos", tenants.Count);

            foreach (var tenant in tenants)
            {
                await SeedTenantSystemRolesAsync(context, tenant.Id, logger);
            }

            await context.SaveChangesAsync();

            logger?.LogInformation("✓ Seed de roles del sistema completado");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al ejecutar seed de roles del sistema");
            // NO relanzar para permitir que la app arranque
            logger?.LogWarning("Continuando startup a pesar del error de seeding de roles");
        }
    }

    /// <summary>
    /// Crea los roles del sistema para un tenant específico si no existen.
    /// </summary>
    public static async Task SeedTenantSystemRolesAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger = null)
    {
        // Verificar si el tenant ya tiene roles del sistema
        var hasSystemRoles = await context.Set<CustomTenantRole>()
            .AnyAsync(r => r.TenantId == tenantId && r.IsSystem);

        if (hasSystemRoles)
        {
            logger?.LogDebug("Tenant {TenantId} ya tiene roles del sistema. Saltando.", tenantId);
            return;
        }

        logger?.LogInformation("Creando roles del sistema para tenant {TenantId}...", tenantId);

        // Definir los roles del sistema con sus colores
        var systemRoles = new[]
        {
            new { Role = TenantRole.Owner, Name = "Owner", Description = "Propietario del tenant - Acceso total al sistema", Color = "#DC2626", DisplayOrder = 0 },
            new { Role = TenantRole.Admin, Name = "Admin", Description = "Administrador - Gestión completa excepto billing", Color = "#EA580C", DisplayOrder = 1 },
            new { Role = TenantRole.Manager, Name = "Manager", Description = "Gerente - Gestión de planillas, empleados y reportes", Color = "#CA8A04", DisplayOrder = 2 },
            new { Role = TenantRole.Accountant, Name = "Accountant", Description = "Contador - Solo lectura de reportes y consultas", Color = "#16A34A", DisplayOrder = 3 },
            new { Role = TenantRole.Employee, Name = "Employee", Description = "Empleado - Solo puede ver su propia información", Color = "#6B7280", DisplayOrder = 4 }
        };

        foreach (var roleInfo in systemRoles)
        {
            // Crear el rol
            var role = new CustomTenantRole
            {
                TenantId = tenantId,
                Name = roleInfo.Name,
                Description = roleInfo.Description,
                IsSystem = true,
                Color = roleInfo.Color,
                DisplayOrder = roleInfo.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Set<CustomTenantRole>().Add(role);
            await context.SaveChangesAsync(); // Guardar para obtener el ID del rol

            // Obtener los permisos predeterminados para este rol
            var permissions = SystemPermission.GetDefaultPermissionsForSystemRole(roleInfo.Role);

            // Crear los permisos para el rol
            foreach (var permissionKey in permissions)
            {
                var permission = new RolePermission
                {
                    TenantId = tenantId,
                    CustomTenantRoleId = role.Id,
                    Permission = permissionKey,
                    GrantedAt = DateTime.UtcNow,
                    IsActive = true
                };

                context.Set<RolePermission>().Add(permission);
            }

            logger?.LogInformation(
                "✓ Rol del sistema '{RoleName}' creado con {PermissionCount} permisos para tenant {TenantId}",
                roleInfo.Name, permissions.Count, tenantId);
        }

        await context.SaveChangesAsync();
    }
}
