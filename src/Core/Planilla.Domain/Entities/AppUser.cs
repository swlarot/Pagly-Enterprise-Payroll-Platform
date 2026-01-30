using Microsoft.AspNetCore.Identity;

namespace Vorluno.Planilla.Domain.Entities;

// La palabra 'public' es crucial para que otros proyectos puedan ver esta clase.
public class AppUser : IdentityUser
{
    // Aqu� puedes a�adir propiedades personalizadas a tus usuarios en el futuro.
    // Por ejemplo:
    public string? NombreCompleto { get; set; }

    /// <summary>
    /// Indica si el usuario es un administrador del sistema (SuperAdmin)
    /// Los SuperAdmins pueden gestionar todos los tenants y crear empresas
    /// </summary>
    public bool IsSystemAdmin { get; set; } = false;
}