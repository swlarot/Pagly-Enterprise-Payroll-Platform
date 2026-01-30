using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Subscription;

/// <summary>
/// DTO que contiene el uso actual de la suscripción y límites del plan
/// Usado para dashboards y verificación de límites en el frontend
/// </summary>
public class SubscriptionUsageDto
{
    /// <summary>
    /// Plan actual
    /// </summary>
    public SubscriptionPlan Plan { get; set; }

    /// <summary>
    /// Nombre del plan
    /// </summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Status de la suscripción
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// Nombre del status
    /// </summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de fin del trial (si aplica)
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }

    // ===========================
    // EMPLOYEE USAGE
    // ===========================

    /// <summary>
    /// Cantidad actual de empleados activos
    /// </summary>
    public int EmployeeCount { get; set; }

    /// <summary>
    /// Límite de empleados del plan
    /// </summary>
    public int EmployeeLimit { get; set; }

    /// <summary>
    /// Porcentaje de uso de empleados (0-100)
    /// </summary>
    public decimal EmployeePercentage { get; set; }

    /// <summary>
    /// Indica si se alcanzó el límite de empleados
    /// </summary>
    public bool EmployeeLimitReached => EmployeeCount >= EmployeeLimit;

    // ===========================
    // USER USAGE
    // ===========================

    /// <summary>
    /// Cantidad actual de usuarios activos
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// Límite de usuarios del plan
    /// </summary>
    public int UserLimit { get; set; }

    /// <summary>
    /// Porcentaje de uso de usuarios (0-100)
    /// </summary>
    public decimal UserPercentage { get; set; }

    /// <summary>
    /// Indica si se alcanzó el límite de usuarios
    /// </summary>
    public bool UserLimitReached => UserCount >= UserLimit;

    // ===========================
    // FEATURES
    // ===========================

    /// <summary>
    /// Puede exportar archivos Excel
    /// </summary>
    public bool CanExportExcel { get; set; }

    /// <summary>
    /// Puede exportar archivos PDF
    /// </summary>
    public bool CanExportPdf { get; set; }

    /// <summary>
    /// Puede usar API
    /// </summary>
    public bool CanUseApi { get; set; }

    /// <summary>
    /// Tiene notificaciones por email
    /// </summary>
    public bool HasEmailNotifications { get; set; }

    /// <summary>
    /// Tiene audit log
    /// </summary>
    public bool HasAuditLog { get; set; }

    // ===========================
    // BILLING
    // ===========================

    /// <summary>
    /// Precio mensual del plan
    /// </summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>
    /// URL para actualizar la suscripción
    /// </summary>
    public string? UpgradeUrl { get; set; }

    /// <summary>
    /// URL para gestionar billing (Stripe portal)
    /// </summary>
    public string? BillingUrl { get; set; }

    // ===========================
    // WARNINGS
    // ===========================

    /// <summary>
    /// Indica si hay warnings o acciones requeridas
    /// </summary>
    public bool HasWarnings => WarningMessages.Any();

    /// <summary>
    /// Mensajes de advertencia (trial ending, past due, etc.)
    /// </summary>
    public List<string> WarningMessages { get; set; } = new();
}
