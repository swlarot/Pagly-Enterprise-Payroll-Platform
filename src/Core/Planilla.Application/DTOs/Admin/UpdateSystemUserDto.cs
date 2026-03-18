namespace Vorluno.Planilla.Application.DTOs.Admin;

public class UpdateSystemUserDto
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
}
