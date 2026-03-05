using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;


namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio que valida si un empleado puede ser eliminado, identificando bloqueadores y warnings.
/// BLOQUEADORES: Impiden la eliminación (préstamos activos, deducciones judiciales, anticipos aprobados)
/// WARNINGS: Requieren confirmación (horas extra pendientes, planillas draft, ausencias no procesadas)
/// </summary>
public class EmployeeDeletionValidationService : IEmployeeDeletionValidationService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public EmployeeDeletionValidationService(
        ApplicationDbContext context,
        ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Valida si un empleado puede ser eliminado verificando todas las dependencias.
    /// </summary>
    public async Task<EmpleadoDeletionResultDto> ValidateEmployeeDeletionAsync(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el empleado existe y pertenece al tenant
        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

        if (empleado == null)
        {
            return new EmpleadoDeletionResultDto
            {
                CanDelete = false,
                RequiresConfirmation = false,
                Blockers = new List<DeletionBlockerDto>
                {
                    new DeletionBlockerDto
                    {
                        Reason = "Empleado no encontrado",
                        Entity = "Empleado",
                        Count = 0,
                        Resolution = "Verifique que el ID del empleado sea correcto"
                    }
                }
            };
        }

        var result = new EmpleadoDeletionResultDto
        {
            EmpleadoInfo = new EmpleadoInfoDto
            {
                Id = empleado.Id,
                NombreCompleto = $"{empleado.Nombre} {empleado.Apellido}",
                NumeroIdentificacion = empleado.NumeroIdentificacion,
                EstaActivo = empleado.EstaActivo,
                TieneUsuarioVinculado = !string.IsNullOrEmpty(empleado.UserId)
            }
        };

        // ===================================================================
        // BLOQUEADORES - Impiden eliminación
        // ===================================================================

        // 1. PRÉSTAMOS ACTIVOS (Estado != Pagado)
        var prestamosActivos = await _context.Prestamos
            .Where(p => p.EmpleadoId == empleadoId &&
                       p.TenantId == tenantId &&
                       p.Estado != EstadoPrestamo.Pagado)
            .CountAsync();

        if (prestamosActivos > 0)
        {
            result.Blockers.Add(new DeletionBlockerDto
            {
                Reason = "Empleado tiene préstamos activos pendientes de pago",
                Entity = "Préstamos",
                Count = prestamosActivos,
                Resolution = "Complete el pago de los préstamos o márquelos como cancelados antes de eliminar el empleado"
            });
        }

        // 2. DEDUCCIONES JUDICIALES ACTIVAS (pensión alimenticia, embargo)
        var deduccionesJudicialesActivas = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId &&
                       d.TenantId == tenantId &&
                       d.EstaActivo &&
                       (d.TipoDeduccion == TipoDeduccion.PensionAlimenticia || d.TipoDeduccion == TipoDeduccion.Embargo))
            .CountAsync();

        if (deduccionesJudicialesActivas > 0)
        {
            result.Blockers.Add(new DeletionBlockerDto
            {
                Reason = "Empleado tiene deducciones judiciales activas (pensión alimenticia o embargo)",
                Entity = "Deducciones judiciales",
                Count = deduccionesJudicialesActivas,
                Resolution = "Desactive o finalice las deducciones judiciales antes de eliminar el empleado"
            });
        }

        // 3. ANTICIPOS APROBADOS NO DESCONTADOS (aún no tienen PlanillaId)
        var anticiposPendientes = await _context.Anticipos
            .Where(a => a.EmpleadoId == empleadoId &&
                       a.TenantId == tenantId &&
                       a.Estado == EstadoAnticipo.Aprobado &&
                       a.PlanillaId == null)
            .CountAsync();

        if (anticiposPendientes > 0)
        {
            result.Blockers.Add(new DeletionBlockerDto
            {
                Reason = "Empleado tiene anticipos aprobados pendientes de descuento",
                Entity = "Anticipos",
                Count = anticiposPendientes,
                Resolution = "Descuente los anticipos en la próxima planilla o márquelos como cancelados"
            });
        }

        // ===================================================================
        // WARNINGS - Requieren confirmación pero permiten eliminar
        // ===================================================================

        // 1. HORAS EXTRA APROBADAS PENDIENTES DE PAGO
        var horasExtraPendientes = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId &&
                       h.TenantId == tenantId &&
                       h.EstaAprobada &&
                       h.PlanillaDetailId == null)
            .CountAsync();

        if (horasExtraPendientes > 0)
        {
            result.Warnings.Add(new DeletionWarningDto
            {
                Reason = "Empleado tiene horas extra aprobadas pendientes de pago",
                Entity = "Horas Extra",
                Count = horasExtraPendientes,
                Impact = "Las horas extra no serán pagadas. Se recomienda procesar primero la planilla correspondiente."
            });
        }

        // 2. APARECE EN PLANILLAS DRAFT
        var planillasDraft = await _context.PayrollDetails
            .Include(pd => pd.PayrollHeader)
            .Where(pd => pd.EmpleadoId == empleadoId &&
                        pd.PayrollHeader != null &&
                        pd.PayrollHeader.TenantId == tenantId &&
                        pd.PayrollHeader.Status == PayrollStatus.Draft)
            .CountAsync();

        if (planillasDraft > 0)
        {
            result.Warnings.Add(new DeletionWarningDto
            {
                Reason = "Empleado aparece en planillas en estado DRAFT",
                Entity = "Planillas Draft",
                Count = planillasDraft,
                Impact = "El empleado será removido de las planillas draft. Deberá recalcularlas después de eliminar."
            });
        }

        // 3. AUSENCIAS NO PROCESADAS (sin PlanillaDetailId)
        var ausenciasPendientes = await _context.Ausencias
            .Where(a => a.EmpleadoId == empleadoId &&
                       a.TenantId == tenantId &&
                       a.AfectaSalario &&
                       a.PlanillaDetailId == null)
            .CountAsync();

        if (ausenciasPendientes > 0)
        {
            result.Warnings.Add(new DeletionWarningDto
            {
                Reason = "Empleado tiene ausencias que afectan salario sin procesar",
                Entity = "Ausencias",
                Count = ausenciasPendientes,
                Impact = "Los descuentos por ausencias no serán aplicados. Se recomienda procesarlas en la próxima planilla."
            });
        }

        // ===================================================================
        // DETERMINAR SI PUEDE ELIMINAR
        // ===================================================================

        result.CanDelete = result.Blockers.Count == 0;
        result.RequiresConfirmation = result.CanDelete && result.Warnings.Count > 0;

        return result;
    }
}
