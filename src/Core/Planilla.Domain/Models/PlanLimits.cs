namespace Vorluno.Planilla.Domain.Models;

/// <summary>
/// Define los límites y capacidades de un plan de suscripción
/// </summary>
public class PlanLimits
{
    /// <summary>
    /// Número máximo de empleados permitidos
    /// </summary>
    public int MaxEmployees { get; set; }

    /// <summary>
    /// Número máximo de usuarios del tenant permitidos
    /// </summary>
    public int MaxUsers { get; set; }

    /// <summary>
    /// Número máximo de empresas/compañías permitidas
    /// </summary>
    public int MaxCompanies { get; set; }

    /// <summary>
    /// Permite exportar reportes a Excel
    /// </summary>
    public bool CanExportExcel { get; set; }

    /// <summary>
    /// Permite exportar reportes a PDF
    /// </summary>
    public bool CanExportPdf { get; set; }

    /// <summary>
    /// Permite acceso a la API
    /// </summary>
    public bool CanUseApi { get; set; }

    /// <summary>
    /// Tiene notificaciones por email habilitadas
    /// </summary>
    public bool HasEmailNotifications { get; set; }

    /// <summary>
    /// Tiene registro de auditoría (audit log)
    /// </summary>
    public bool HasAuditLog { get; set; }

    /// <summary>
    /// Días de retención de datos históricos
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// Precio mensual en USD
    /// </summary>
    public decimal PricePerMonth { get; set; }
}
