using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs.Auth;

/// <summary>
/// DTO con información de la suscripción del tenant
/// </summary>
public class SubscriptionInfoDto
{
    public SubscriptionPlan Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? TrialEndsAt { get; set; }
    public int MaxEmployees { get; set; }
    public int MaxUsers { get; set; }
    public int MaxCompanies { get; set; }
    public bool CanExportExcel { get; set; }
    public bool CanExportPdf { get; set; }
    public bool CanUseApi { get; set; }
    public decimal MonthlyPrice { get; set; }
}
