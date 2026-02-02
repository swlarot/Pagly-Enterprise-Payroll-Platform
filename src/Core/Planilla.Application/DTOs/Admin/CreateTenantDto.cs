using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para crear un nuevo tenant desde el Admin Panel (sin owner - se asigna después)
/// </summary>
public record CreateTenantDto(
    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    [Required(ErrorMessage = "El nombre de la empresa es requerido")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    string Nombre,

    /// <summary>
    /// Registro Único de Contribuyente (RUC) de Panamá
    /// </summary>
    [Required(ErrorMessage = "El RUC es requerido")]
    [StringLength(50, ErrorMessage = "El RUC no puede exceder 50 caracteres")]
    string RUC,

    /// <summary>
    /// Dígito Verificador del RUC
    /// </summary>
    [Required(ErrorMessage = "El DV es requerido")]
    [StringLength(5, ErrorMessage = "El DV no puede exceder 5 caracteres")]
    string DV,

    /// <summary>
    /// Tipo de contribuyente: "Natural" o "Juridico"
    /// </summary>
    [Required(ErrorMessage = "El tipo de contribuyente es requerido")]
    [StringLength(20)]
    string TipoContribuyente,

    /// <summary>
    /// Teléfono de contacto de la empresa
    /// </summary>
    [StringLength(20)]
    string? Telefono,

    /// <summary>
    /// Email de contacto de la empresa
    /// </summary>
    [EmailAddress(ErrorMessage = "El formato del correo no es válido")]
    [StringLength(256)]
    string? Correo,

    /// <summary>
    /// País donde se ubica la empresa
    /// </summary>
    [StringLength(100)]
    string? Pais,

    /// <summary>
    /// Dirección física de la empresa
    /// </summary>
    [StringLength(500)]
    string? Direccion,

    /// <summary>
    /// Sitio web de la empresa
    /// </summary>
    [StringLength(256)]
    string? SitioWeb
);
