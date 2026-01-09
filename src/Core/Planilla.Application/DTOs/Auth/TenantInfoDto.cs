namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO con información del tenant actual
/// </summary>
public class TenantInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? DV { get; set; }
}
