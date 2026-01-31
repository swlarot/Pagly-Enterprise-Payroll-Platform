using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planilla.Application.DTOs.Tenant;
using Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Domain.Models;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services
{
    public class PlanUsageService : IPlanUsageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlanUsageService> _logger;

        public PlanUsageService(
            ApplicationDbContext context,
            ILogger<PlanUsageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PlanUsageDto> GetPlanUsageAsync(int tenantId)
        {
            try
            {
                var tenant = await _context.Tenants
                    .Include(t => t.Subscription)
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null)
                    throw new Exception("Tenant no encontrado");

                if (tenant.Subscription == null)
                    throw new Exception("Tenant no tiene suscripción asignada");

                var plan = tenant.Subscription.Plan;
                var limits = PlanFeatures.GetLimits(plan);

                // Contar uso actual
                var activeEmployees = await _context.Empleados
                    .Where(e => e.TenantId == tenantId && e.EstaActivo)
                    .CountAsync();

                var activeUsers = await _context.TenantUsers
                    .Where(tu => tu.TenantId == tenantId && tu.IsActive)
                    .CountAsync();

                var pendingInvitations = await _context.TenantInvitations
                    .Where(ti => ti.TenantId == tenantId && ti.AcceptedAt == null && !ti.IsRevoked && ti.ExpiresAt > DateTime.UtcNow && ti.IsActive)
                    .CountAsync();

                // Para companies, como no existe la entidad, usamos 1 (el propio tenant)
                // En futuras versiones se puede extender para multi-company por tenant
                var activeCompanies = 1;

                // Obtener límites efectivos (puede tener límites personalizados)
                var maxEmployees = tenant.Subscription.CustomMaxEmployees > 0
                    ? tenant.Subscription.CustomMaxEmployees
                    : limits.MaxEmployees;

                var maxUsers = tenant.Subscription.CustomMaxUsers > 0
                    ? tenant.Subscription.CustomMaxUsers
                    : limits.MaxUsers;

                var maxCompanies = limits.MaxCompanies;

                // Calcular remainders
                var remainingEmployees = maxEmployees == int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, maxEmployees - activeEmployees);

                var totalUserSlots = activeUsers + pendingInvitations;
                var remainingUsers = maxUsers == int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, maxUsers - totalUserSlots);

                var remainingCompanies = maxCompanies == int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, maxCompanies - activeCompanies);

                // Calcular porcentajes (0-100)
                var employeesPercentage = CalculatePercentage(activeEmployees, maxEmployees);
                var usersPercentage = CalculatePercentage(totalUserSlots, maxUsers);
                var companiesPercentage = CalculatePercentage(activeCompanies, maxCompanies);

                // Determinar si debe actualizar
                // Activar warning al 80% o si quedan menos de 2 slots
                var shouldUpgrade =
                    (remainingEmployees != int.MaxValue && (remainingEmployees <= 2 || employeesPercentage >= 80)) ||
                    (remainingUsers != int.MaxValue && (remainingUsers <= 1 || usersPercentage >= 80));

                string? upgradeMessage = null;
                if (shouldUpgrade)
                {
                    if (remainingEmployees <= 2 && remainingEmployees != int.MaxValue)
                        upgradeMessage = $"Estás cerca del límite de empleados ({activeEmployees}/{maxEmployees}). Considera actualizar tu plan.";
                    else if (remainingUsers <= 1 && remainingUsers != int.MaxValue)
                        upgradeMessage = $"Estás cerca del límite de usuarios ({totalUserSlots}/{maxUsers}). Considera actualizar tu plan.";
                    else
                        upgradeMessage = "Estás cerca del límite de tu plan. Considera actualizar para obtener más capacidad.";
                }

                return new PlanUsageDto
                {
                    PlanName = plan.ToString(),
                    PlanDisplayName = GetPlanDisplayName(plan),
                    MonthlyPrice = tenant.Subscription.MonthlyPrice,

                    Limits = new PlanLimitsDto
                    {
                        MaxEmployees = maxEmployees,
                        MaxUsers = maxUsers,
                        MaxCompanies = maxCompanies
                    },

                    Usage = new PlanUsageStatsDto
                    {
                        ActiveEmployees = activeEmployees,
                        ActiveUsers = activeUsers,
                        PendingInvitations = pendingInvitations,
                        ActiveCompanies = activeCompanies
                    },

                    Remaining = new PlanRemainingDto
                    {
                        Employees = remainingEmployees,
                        Users = remainingUsers,
                        Companies = remainingCompanies,
                        EmployeesPercentage = employeesPercentage,
                        UsersPercentage = usersPercentage,
                        CompaniesPercentage = companiesPercentage
                    },

                    Features = GetFeaturesList(limits),

                    CanInviteUsers = remainingUsers > 0,
                    CanCreateEmployees = remainingEmployees > 0,
                    CanCreateCompanies = remainingCompanies > 0,
                    ShouldUpgrade = shouldUpgrade,
                    UpgradeMessage = upgradeMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener uso del plan para tenant {TenantId}", tenantId);
                throw;
            }
        }

        private decimal CalculatePercentage(int used, int max)
        {
            if (max == int.MaxValue || max == 0)
                return 0;

            return Math.Round((decimal)used / max * 100, 2);
        }

        private string GetPlanDisplayName(SubscriptionPlan plan)
        {
            return plan switch
            {
                SubscriptionPlan.Free => "Plan Gratuito",
                SubscriptionPlan.Starter => "Plan Starter",
                SubscriptionPlan.Professional => "Plan Profesional",
                SubscriptionPlan.Enterprise => "Plan Enterprise",
                _ => "Plan Desconocido"
            };
        }

        private List<FeatureAvailabilityDto> GetFeaturesList(PlanLimits limits)
        {
            return new List<FeatureAvailabilityDto>
            {
                new FeatureAvailabilityDto
                {
                    FeatureName = "Exportar a Excel",
                    IsAvailable = limits.CanExportExcel,
                    Description = "Exporta reportes de planilla a formato Excel"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = "Exportar a PDF",
                    IsAvailable = limits.CanExportPdf,
                    Description = "Genera reportes de planilla en formato PDF"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = "Acceso a API",
                    IsAvailable = limits.CanUseApi,
                    Description = "Acceso programático a la API REST"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = "Notificaciones por Email",
                    IsAvailable = limits.HasEmailNotifications,
                    Description = "Recibe notificaciones automáticas por correo electrónico"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = "Registro de Auditoría",
                    IsAvailable = limits.HasAuditLog,
                    Description = "Registro completo de todas las acciones del sistema"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = "Soporte Prioritario",
                    IsAvailable = limits.PricePerMonth >= 199.99m, // Enterprise
                    Description = "Respuesta prioritaria en menos de 24 horas"
                },
                new FeatureAvailabilityDto
                {
                    FeatureName = $"Retención de Datos ({limits.RetentionDays} días)",
                    IsAvailable = true,
                    Description = limits.RetentionDays == int.MaxValue
                        ? "Retención ilimitada de datos históricos"
                        : $"Datos históricos almacenados por {limits.RetentionDays} días"
                }
            };
        }
    }
}
