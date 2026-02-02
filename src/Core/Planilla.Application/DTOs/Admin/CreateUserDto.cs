using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para crear un nuevo usuario en el sistema (sin contraseña)
/// La contraseña predefinida "Planilla2024!Temp" se asigna automáticamente
/// </summary>
public record CreateUserDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    string Nombre,

    [Required(ErrorMessage = "El apellido es obligatorio")]
    [StringLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres")]
    string Apellido,

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    [StringLength(256, ErrorMessage = "El correo no puede exceder 256 caracteres")]
    string Correo,

    [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
    string? Telefono
);

/// <summary>
/// DTO para asignar un usuario existente a un tenant
/// </summary>
public record AssignUserToTenantDto(
    [Required(ErrorMessage = "El email del usuario es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    string UserEmail,

    [Required(ErrorMessage = "El rol es obligatorio")]
    Vorluno.Planilla.Domain.Enums.TenantRole Role  // Owner o User
);

/// <summary>
/// DTO para invitar un usuario a un tenant (incluye creación si no existe)
/// </summary>
public record InviteUserToTenantDto(
    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    [StringLength(256)]
    string Email,

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    string Nombre,

    [Required(ErrorMessage = "El apellido es obligatorio")]
    [StringLength(100)]
    string Apellido,

    [StringLength(20)]
    string? Telefono,

    [Required(ErrorMessage = "El rol es obligatorio")]
    Vorluno.Planilla.Domain.Enums.TenantRole Role  // Owner o User
);
