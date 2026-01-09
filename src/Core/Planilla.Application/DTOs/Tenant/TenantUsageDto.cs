namespace Vorluno.Planilla.Application.DTOs.Tenant;

/// <summary>
/// Métricas de uso de recursos del tenant
/// </summary>
public class TenantUsageDto
{
    /// <summary>
    /// Cantidad actual de usuarios activos en el tenant
    /// </summary>
    public int UsersCount { get; set; }

    /// <summary>
    /// Cantidad actual de empleados activos
    /// </summary>
    public int EmployeesCount { get; set; }

    /// <summary>
    /// Cantidad de compañías creadas
    /// </summary>
    public int CompaniesCount { get; set; }

    /// <summary>
    /// Cantidad de invitaciones pendientes
    /// </summary>
    public int PendingInvitationsCount { get; set; }

    /// <summary>
    /// Límites del plan actual
    /// </summary>
    public int MaxUsers { get; set; }
    public int MaxEmployees { get; set; }
    public int MaxCompanies { get; set; }

    /// <summary>
    /// Indica si el tenant ha alcanzado el límite de usuarios
    /// </summary>
    public bool HasReachedUserLimit => UsersCount >= MaxUsers;

    /// <summary>
    /// Indica si el tenant ha alcanzado el límite de empleados
    /// </summary>
    public bool HasReachedEmployeeLimit => EmployeesCount >= MaxEmployees;

    /// <summary>
    /// Indica si el tenant ha alcanzado el límite de compañías
    /// </summary>
    public bool HasReachedCompanyLimit => CompaniesCount >= MaxCompanies;
}
