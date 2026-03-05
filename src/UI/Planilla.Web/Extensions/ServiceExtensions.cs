// RISK: Removing ReactJsInterop reference - Blazor interop no longer needed for Web API + React SPA
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Infrastructure.Repositories;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Services;

namespace Vorluno.Planilla.Web.Extensions
{
    /// <summary>
    /// Clase est�tica que contiene m�todos de extensi�n para configurar los servicios de la aplicaci�n.
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Registra los servicios de la capa de aplicaci�n e infraestructura en el contenedor de dependencias.
        /// </summary>
        /// <param name="services">La colecci�n de servicios a la que se agregar�n los registros.</param>
        public static void ConfigureApplicationServices(this IServiceCollection services)
        {
            // Registra la IUnitOfWork. Cuando una clase pida una IUnitOfWork,
            // el sistema le entregar� una instancia de nuestra clase UnitOfWork.
            // Se registra como 'Scoped', lo que significa que se crea una nueva instancia por cada petici�n HTTP.

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // REGISTRAR NUESTRO NUEVO SERVICIO
            services.AddScoped<PlanillaService>();

            // Phase A: Proveedor de configuraci�n de planilla (tasas CSS, SE, ISR)
            services.AddScoped<IPayrollConfigProvider, PayrollConfigProvider>();

            // Phase B: Servicios de c�lculo de planilla (CSS, SE, ISR)
            services.AddScoped<CssCalculationServicePortable>();
            services.AddScoped<EducationalInsuranceServicePortable>();
            services.AddScoped<IncomeTaxCalculationServicePortable>();

            // Phase D: Workflow de planilla y orquestador
            services.AddScoped<PayrollStateMachine>();
            services.AddScoped<PayrollCalculationOrchestratorPortable>();
            services.AddScoped<Vorluno.Planilla.Application.Interfaces.IDeduccionPrioridadEngine, DeduccionPrioridadEngine>();
            services.AddScoped<PayrollProcessingService>();

            // Phase E: Multi-tenancy y auditor�a
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Phase F: Asistencia (horas extra, ausencias, vacaciones) — registradas en Program.cs con interfaz


            // Phase G: Reportes y exportaci�n
            services.AddScoped<ReportesService>();
            services.AddScoped<ExportacionService>();

            // TODO: React SPA will communicate via REST API endpoints, no server-side interop needed
        }
    }
}