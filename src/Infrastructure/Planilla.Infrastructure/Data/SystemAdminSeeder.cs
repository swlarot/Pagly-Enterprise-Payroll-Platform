// ====================================================================
// Planilla - SystemAdminSeeder
// Source: System Administration Setup
// Created: 2026-01-28
// Descripción: Seeder para crear usuarios administradores del sistema
// ====================================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Vorluno.Planilla.Domain.Entities;

namespace Vorluno.Planilla.Infrastructure.Data;

/// <summary>
/// Seeder para crear usuarios administradores del sistema (SuperAdmins).
/// Los SuperAdmins pueden gestionar todos los tenants y crear empresas.
/// </summary>
public static class SystemAdminSeeder
{
    private static readonly List<(string Email, string Password, string FullName)> SystemAdmins = new()
    {
        ("gjoseluisgonzalez507@gmail.com", "HATSUKIMINARA*", "Jose Luis Gonzalez"),
        ("contacto@vorluno.dev", "HatsukiMinara507*", "Vorluno Admin")
    };

    /// <summary>
    /// Crea los usuarios administradores del sistema si no existen.
    /// </summary>
    public static async Task SeedAsync(
        UserManager<AppUser> userManager,
        ILogger? logger = null)
    {
        logger?.LogInformation("Iniciando seed de administradores del sistema...");

        try
        {
            foreach (var (email, password, fullName) in SystemAdmins)
            {
                await CreateSystemAdminAsync(userManager, email, password, fullName, logger);
            }

            logger?.LogInformation("✓ Seed de administradores del sistema completado");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al ejecutar seed de administradores del sistema");
            // NO relanzar para permitir que la app arranque
            logger?.LogWarning("Continuando startup a pesar del error de seeding de admins");
        }
    }

    private static async Task CreateSystemAdminAsync(
        UserManager<AppUser> userManager,
        string email,
        string password,
        string fullName,
        ILogger? logger)
    {
        // Verificar si el usuario ya existe
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // Si existe pero no es admin, hacerlo admin
            if (!existingUser.IsSystemAdmin)
            {
                existingUser.IsSystemAdmin = true;
                await userManager.UpdateAsync(existingUser);
                logger?.LogInformation("✓ Usuario {Email} promovido a SystemAdmin", email);
            }
            else
            {
                logger?.LogInformation("Usuario {Email} ya es SystemAdmin. Saltando.", email);
            }
            return;
        }

        // Crear nuevo usuario administrador
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = fullName,
            IsSystemAdmin = true,
            LockoutEnabled = false // Los admins no pueden ser bloqueados
        };

        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            logger?.LogInformation("✓ SystemAdmin creado: {Email} ({FullName})", email, fullName);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger?.LogWarning("⚠ No se pudo crear SystemAdmin {Email}: {Errors}", email, errors);
        }
    }
}
