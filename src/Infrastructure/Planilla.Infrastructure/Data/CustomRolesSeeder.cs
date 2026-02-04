// ====================================================================
// Planilla - CustomRolesSeeder
// Estrategia: No hay roles predefinidos a nivel tenant.
// Solo existen Owner y User a nivel sistema (TenantUser.Role).
// Cada empresa (tenant) crea sus propios roles personalizados; el Owner los gestiona.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vorluno.Planilla.Infrastructure.Data;

/// <summary>
/// Seeder de roles. No crea roles predefinidos por tenant.
/// Owner y User son roles a nivel sistema (enum TenantRole); los roles por empresa
/// los crea el Owner como CustomTenantRole.
/// </summary>
public static class CustomRolesSeeder
{
    /// <summary>
    /// No-op: no se crean roles predefinidos. Cada tenant gestiona sus roles desde la UI.
    /// </summary>
    public static Task SeedAsync(
        ApplicationDbContext context,
        ILogger? logger = null)
    {
        logger?.LogDebug("CustomRolesSeeder: sin roles predefinidos por tenant (estrategia: solo Owner/User a nivel sistema).");
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op por tenant.
    /// </summary>
    public static Task SeedTenantSystemRolesAsync(
        ApplicationDbContext context,
        int tenantId,
        ILogger? logger = null)
    {
        return Task.CompletedTask;
    }
}
