using System.ComponentModel.DataAnnotations;

namespace Vorluno.Planilla.Application.DTOs.ApiKeys;

/// <summary>
/// Request DTO para <c>POST /api/api-keys</c>.
/// </summary>
public class CreateApiKeyRequest
{
    /// <summary>
    /// Nombre amigable para identificar la key en el dashboard
    /// (ej: "Urbis Production", "Dev Laptop Juan").
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "Test" o "Live". Default "Live".
    /// </summary>
    [RegularExpression("^(Test|Live)$", ErrorMessage = "mode debe ser 'Test' o 'Live'")]
    public string Mode { get; set; } = "Live";

    /// <summary>
    /// Días hasta que expire. Null = sin expiración. 1–3650 (10 años máx).
    /// </summary>
    [Range(1, 3650, ErrorMessage = "expiresInDays debe estar entre 1 y 3650 (null = sin expiración)")]
    public int? ExpiresInDays { get; set; }
}
