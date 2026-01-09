using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Domain.Models;

/// <summary>
/// Configuración de características y límites por plan de suscripción
/// </summary>
public static class PlanFeatures
{
    /// <summary>
    /// Diccionario de límites por plan
    /// </summary>
    public static readonly Dictionary<SubscriptionPlan, PlanLimits> Limits = new()
    {
        [SubscriptionPlan.Free] = new PlanLimits
        {
            MaxEmployees = 5,
            MaxUsers = 1,
            MaxCompanies = 1,
            CanExportExcel = false,
            CanExportPdf = false,
            CanUseApi = false,
            HasEmailNotifications = false,
            HasAuditLog = false,
            RetentionDays = 90,
            PricePerMonth = 0m
        },

        [SubscriptionPlan.Starter] = new PlanLimits
        {
            MaxEmployees = 25,
            MaxUsers = 3,
            MaxCompanies = 1,
            CanExportExcel = true,
            CanExportPdf = false,
            CanUseApi = false,
            HasEmailNotifications = true,
            HasAuditLog = false,
            RetentionDays = 365,
            PricePerMonth = 29.99m
        },

        [SubscriptionPlan.Professional] = new PlanLimits
        {
            MaxEmployees = 100,
            MaxUsers = 10,
            MaxCompanies = 3,
            CanExportExcel = true,
            CanExportPdf = true,
            CanUseApi = true,
            HasEmailNotifications = true,
            HasAuditLog = true,
            RetentionDays = 730,  // 2 años
            PricePerMonth = 79.99m
        },

        [SubscriptionPlan.Enterprise] = new PlanLimits
        {
            MaxEmployees = int.MaxValue,
            MaxUsers = int.MaxValue,
            MaxCompanies = int.MaxValue,
            CanExportExcel = true,
            CanExportPdf = true,
            CanUseApi = true,
            HasEmailNotifications = true,
            HasAuditLog = true,
            RetentionDays = int.MaxValue,
            PricePerMonth = 199.99m  // Precio base, puede ser personalizado para Enterprise
        }
    };

    /// <summary>
    /// Obtiene los límites de un plan específico
    /// </summary>
    public static PlanLimits GetLimits(SubscriptionPlan plan)
    {
        return Limits[plan];
    }

    /// <summary>
    /// Verifica si un plan permite una característica específica
    /// </summary>
    public static bool CanExport(SubscriptionPlan plan, string format)
    {
        var limits = GetLimits(plan);
        return format.ToLower() switch
        {
            "excel" => limits.CanExportExcel,
            "pdf" => limits.CanExportPdf,
            _ => false
        };
    }
}
