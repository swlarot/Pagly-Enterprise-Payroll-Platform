using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO para seleccionar un tenant cuando el usuario pertenece a múltiples empresas
/// </summary>
public class SelectTenantDto
{
    /// <summary>
    /// ID del tenant a seleccionar
    /// </summary>
    [Required(ErrorMessage = "El ID del tenant es requerido")]
    [Range(1, int.MaxValue, ErrorMessage = "El ID del tenant debe ser mayor a 0")]
    public int TenantId { get; set; }
}
