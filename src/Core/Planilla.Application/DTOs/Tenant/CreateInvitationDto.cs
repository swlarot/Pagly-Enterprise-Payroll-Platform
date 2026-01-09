using System.ComponentModel.DataAnnotations;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// DTO para crear una invitación de usuario al tenant
/// </summary>
public class CreateInvitationDto
{
    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de email inválido")]
    [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio")]
    public TenantRole Role { get; set; } = TenantRole.Employee;
}
