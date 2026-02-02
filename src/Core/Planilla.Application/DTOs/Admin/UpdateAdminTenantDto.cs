using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.Admin;

/// <summary>
/// DTO para actualizar un tenant desde el Admin Panel
/// </summary>
public record UpdateAdminTenantDto(
    /// <summary>
    /// Nombre de la empresa/tenant
    /// </summary>
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    string? Nombre,

    /// <summary>
    /// Tipo de contribuyente: "Natural" o "Juridico"
    /// </summary>
    [StringLength(20)]
    string? TipoContribuyente,

    /// <summary>
    /// Teléfono de contacto
    /// </summary>
    [StringLength(20)]
    string? Telefono,

    /// <summary>
    /// Email de contacto de la empresa
    /// </summary>
    [EmailAddress]
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
    string? SitioWeb,

    /// <summary>
    /// URL del logo de la empresa
    /// </summary>
    [StringLength(512)]
    string? LogoUrl,

    /// <summary>
    /// Activar o desactivar el tenant
    /// </summary>
    bool? IsActive
);
