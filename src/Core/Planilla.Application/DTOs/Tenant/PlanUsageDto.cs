namespace Planilla.Application.DTOs.Tenant
{
    public class PlanUsageDto
    {
        public string PlanName { get; set; } = string.Empty;
        public string PlanDisplayName { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }

        public PlanLimitsDto Limits { get; set; } = null!;
        public PlanUsageStatsDto Usage { get; set; } = null!;
        public PlanRemainingDto Remaining { get; set; } = null!;

        public List<FeatureAvailabilityDto> Features { get; set; } = new();

        public bool CanInviteUsers { get; set; }
        public bool CanCreateEmployees { get; set; }
        public bool CanCreateCompanies { get; set; }
        public bool ShouldUpgrade { get; set; }
        public string? UpgradeMessage { get; set; }
    }

    public class PlanLimitsDto
    {
        public int MaxEmployees { get; set; }
        public int MaxUsers { get; set; }
        public int MaxCompanies { get; set; }
    }

    public class PlanUsageStatsDto
    {
        public int ActiveEmployees { get; set; }
        public int ActiveUsers { get; set; }
        public int PendingInvitations { get; set; }
        public int ActiveCompanies { get; set; }
    }

    public class PlanRemainingDto
    {
        public int Employees { get; set; }
        public int Users { get; set; }
        public int Companies { get; set; }

        public decimal EmployeesPercentage { get; set; }
        public decimal UsersPercentage { get; set; }
        public decimal CompaniesPercentage { get; set; }
    }

    public class FeatureAvailabilityDto
    {
        public string FeatureName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
