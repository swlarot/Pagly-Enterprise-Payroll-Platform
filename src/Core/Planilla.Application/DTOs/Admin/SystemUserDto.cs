using System.Text.Json.Serialization;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para usuario del sistema con todas sus membresías de tenants
/// IMPORTANTE: Este DTO NO filtra por tenant - muestra TODOS los usuarios del sistema
/// Solo accesible por SystemAdmin
/// </summary>
public class SystemUserDto
{
    /// <summary>
    /// ID del usuario en ASP.NET Identity
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Email del usuario
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del usuario
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de creación del usuario en el sistema
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Indica si el usuario tiene acceso al sistema
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indica si el usuario es SystemAdmin
    /// </summary>
    public bool IsSystemAdmin { get; set; }

    /// <summary>
    /// Lista de tenants a los que pertenece el usuario
    /// Puede estar vacío si el usuario aún no ha sido asignado a ningún tenant
    /// </summary>
    [JsonPropertyName("tenants")]
    public List<UserTenantMembershipDto> Tenants { get; set; } = new();
}

/// <summary>
/// DTO para representar la membresía de un usuario en un tenant específico
/// </summary>
public class UserTenantMembershipDto
{
    /// <summary>
    /// ID del tenant
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Rol del usuario dentro del tenant
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Fecha en que el usuario se unió al tenant
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Indica si la membresía está activa
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Fecha del último login en este tenant
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// DTO para respuesta paginada de usuarios del sistema
/// </summary>
public class SystemUserPagedResultDto
{
    /// <summary>
    /// Lista de usuarios en la página actual
    /// </summary>
    [JsonPropertyName("data")]
    public List<SystemUserDto> Data { get; set; } = new();

    /// <summary>
    /// Total de usuarios en el sistema (sin paginar)
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Página actual (1-indexed)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Tamaño de página
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total de páginas
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);

    /// <summary>
    /// Indica si hay página anterior
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Indica si hay página siguiente
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
}
