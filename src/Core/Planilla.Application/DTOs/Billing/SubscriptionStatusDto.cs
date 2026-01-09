using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Billing;

public class SubscriptionStatusDto
{
    public SubscriptionPlan Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public decimal MonthlyPrice { get; set; }

    // Limits
    public int MaxEmployees { get; set; }
    public int MaxUsers { get; set; }
    public int CurrentEmployees { get; set; }
    public int CurrentUsers { get; set; }

    // Features
    public bool CanExportExcel { get; set; }
    public bool CanExportPdf { get; set; }
    public bool CanUseApi { get; set; }
    public bool HasAuditLog { get; set; }
}
