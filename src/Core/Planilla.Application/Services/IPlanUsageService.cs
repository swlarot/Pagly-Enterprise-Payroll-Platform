using Planilla.Application.DTOs.Tenant;

namespace Planilla.Application.Services
{
    public interface IPlanUsageService
    {
        Task<PlanUsageDto> GetPlanUsageAsync(int tenantId);
    }
}
