// ====================================================================
// Planilla - DecimoCalculationService (DEV-173)
// Extracción de la lógica de cálculo del décimo tercer mes
// desde DecimoController a un servicio de infraestructura
// ====================================================================

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.DTOs.Reportes;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class DecimoCalculationService : IDecimoCalculationService
{
    private readonly ApplicationDbContext _context;
    private readonly IPayrollConfigProvider _configProvider;

    public DecimoCalculationService(
        ApplicationDbContext context,
        IPayrollConfigProvider configProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public async Task<DecimoCalculationSummary> CalcularAsync(int planillaDecimoId, int tenantId)
    {
        var planilla = await _context.PlanillasDecimo
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == planillaDecimoId && p.TenantId == tenantId)
            ?? throw new InvalidOperationException("Planilla de décimo no encontrada");

        if (planilla.Estado == EstadoDecimo.Pagada)
            throw new InvalidOperationException("No se puede recalcular una planilla ya pagada");

        var empleados = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo)
            .ToListAsync();

        var taxConfig = await _configProvider.GetTaxConfigAsync(tenantId, planilla.FechaPago);
        var taxBrackets = await _configProvider.GetTaxBracketsAsync(tenantId, planilla.FechaPago.Year);

        // Limpiar detalles existentes para recalcular
        if (planilla.Detalles.Any())
        {
            _context.DetallesDecimo.RemoveRange(planilla.Detalles);
            planilla.Detalles.Clear();
        }

        decimal totalDevengado = 0, totalDecimo = 0;
        decimal totalCssEmp = 0, totalCssPat = 0;
        decimal totalSeEmp = 0, totalSePat = 0;
        decimal totalIsr = 0, totalNeto = 0;
        int empleadosProcesados = 0;

        foreach (var empleado in empleados)
        {
            // 1. PayrollDetails del período — solo planillas Approved/Paid (DEV-172)
            var details = await _context.PayrollDetails
                .Include(d => d.PayrollHeader)
                .Where(d => d.TenantId == tenantId
                         && d.EmpleadoId == empleado.Id
                         && d.PayrollHeader.PeriodStartDate >= planilla.PeriodoDesde
                         && d.PayrollHeader.PeriodEndDate <= planilla.PeriodoHasta
                         && (d.PayrollHeader.Status == PayrollStatus.Approved
                             || d.PayrollHeader.Status == PayrollStatus.Paid))
                .ToListAsync();

            if (details.Count == 0)
                continue;

            // 2. Desglose mensual agrupado por año+mes
            var desgloseMensual = details
                .GroupBy(d => new { d.PayrollHeader.PeriodStartDate.Year, d.PayrollHeader.PeriodStartDate.Month })
                .Select(g => new DesgloseMensualItem(g.Key.Year, g.Key.Month, g.Sum(d => d.GrossPay)))
                .OrderBy(x => x.Ano).ThenBy(x => x.Mes)
                .ToList();

            decimal totalDev = details.Sum(d => d.GrossPay);

            // 3. MontoDecimo = TotalDevengado / 12
            decimal montoDecimo = RoundingPolicy.Round(totalDev / 12m);

            // 4. CSS reducida del décimo: 7.25% empleado / 10.75% patronal
            //    (Ley 51/2005 Art. 96.4-96.5, Texto Único modif. Ley 462). El SE NO se reduce.
            decimal cssEmp = RoundingPolicy.Round(montoDecimo * PayrollConstants.CssTasaDecimoEmpleado);
            decimal cssPat = RoundingPolicy.Round(montoDecimo * PayrollConstants.CssTasaDecimoPatronal);

            // 5. Seguro Educativo (1.25% / 1.50%, sin reducción sobre el décimo)
            bool seActivo = empleado.IsSubjectToEducationalInsurance;
            decimal seEmp = seActivo ? RoundingPolicy.Round(montoDecimo * PayrollConstants.SeTasaEmpleado) : 0;
            decimal sePat = seActivo ? RoundingPolicy.Round(montoDecimo * PayrollConstants.SeTasaPatronal) : 0;

            // 6. ISR del décimo: (salario mensual + décimo) × 13 → tramos → / 13
            decimal isr = 0;
            if (empleado.IsSubjectToIncomeTax && taxBrackets.Count > 0)
            {
                // DEV-185: se obtiene el GrossPay del último período y se convierte a mensual
                // usando PayPeriodType del empleado, para no subestimar el ISR en empleados quincenales.
                var ultimoPeriodoGross = await _context.PayrollDetails
                    .Where(d => d.TenantId == tenantId
                             && d.EmpleadoId == empleado.Id
                             && d.PayrollHeader.PeriodEndDate <= planilla.PeriodoHasta
                             && (d.PayrollHeader.Status == PayrollStatus.Approved
                                 || d.PayrollHeader.Status == PayrollStatus.Paid))
                    .OrderByDescending(d => d.PayrollHeader.PeriodEndDate)
                    .Select(d => d.GrossPay)
                    .FirstOrDefaultAsync();

                int periodsPerYear = PayrollConstants.GetPeriodsPerYear(empleado.PayPeriodType);
                decimal salarioMensual = ultimoPeriodoGross * periodsPerYear / 12m;
                decimal baseDecimo = salarioMensual + montoDecimo;
                decimal annualBase = baseDecimo * 13m;

                decimal depDeduccion = 0;
                if (taxConfig != null)
                {
                    var validDeps = Math.Min(empleado.Dependents, taxConfig.MaxDependents);
                    depDeduccion = validDeps * taxConfig.DependentDeductionAmount;
                }

                // Seguro Educativo deducible de la base del ISR (Art. 709 núm. 4 Código Fiscal),
                // consistente con la planilla regular (IncomeTaxCalculationServicePortable).
                decimal seDeduccion = seActivo && taxConfig != null
                    ? annualBase * taxConfig.EducationalInsuranceEmployeeRate / 100m
                    : 0m;
                decimal netGravable = Math.Max(0, annualBase - depDeduccion - seDeduccion);
                decimal isrAnual = CalcularIsrAnual(netGravable, taxBrackets);
                isr = RoundingPolicy.Round(isrAnual / 13m);
            }

            // 7. Totales del empleado
            decimal totalDedEmp = cssEmp + seEmp + isr;
            decimal neto = montoDecimo - totalDedEmp;

            _context.DetallesDecimo.Add(new DetalleDecimo
            {
                TenantId = tenantId,
                PlanillaDecimoId = planilla.Id,
                EmpleadoId = empleado.Id,
                DesgloseMensualJson = JsonSerializer.Serialize(desgloseMensual),
                TotalDevengado = totalDev,
                MontoDecimo = montoDecimo,
                CssEmpleado = cssEmp,
                CssPatrono = cssPat,
                SeEmpleado = seEmp,
                SePatrono = sePat,
                ISR = isr,
                TotalDeducciones = totalDedEmp,
                NetoPago = neto,
                CreatedAt = DateTime.UtcNow
            });

            totalDevengado += totalDev;
            totalDecimo += montoDecimo;
            totalCssEmp += cssEmp;
            totalCssPat += cssPat;
            totalSeEmp += seEmp;
            totalSePat += sePat;
            totalIsr += isr;
            totalNeto += neto;
            empleadosProcesados++;
        }

        // 8. Actualizar cabecera
        planilla.TotalDevengado = totalDevengado;
        planilla.TotalDecimo = totalDecimo;
        planilla.TotalCssEmpleado = totalCssEmp;
        planilla.TotalCssPatrono = totalCssPat;
        planilla.TotalSeEmpleado = totalSeEmp;
        planilla.TotalSePatrono = totalSePat;
        planilla.TotalISR = totalIsr;
        planilla.TotalNetoPago = totalNeto;
        planilla.Estado = EstadoDecimo.Calculada;
        planilla.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new DecimoCalculationSummary(empleadosProcesados, totalDecimo);
    }

    private static decimal CalcularIsrAnual(decimal netGravable, List<TaxBracketDto> brackets)
    {
        var ordered = brackets.OrderBy(b => b.MinIncome).ToList();

        TaxBracketDto? bracket = null;
        foreach (var b in ordered)
        {
            if (netGravable > b.MinIncome)
                bracket = b;
            else
                break;
        }

        if (bracket == null) return 0m;

        var excess = netGravable - bracket.MinIncome;
        var bracketTax = RoundingPolicy.CalculatePercentage(excess, bracket.Rate);
        return RoundingPolicy.Round(bracket.FixedAmount + bracketTax);
    }
}
