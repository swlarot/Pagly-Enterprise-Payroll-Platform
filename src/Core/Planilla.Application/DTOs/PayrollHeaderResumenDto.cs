using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// Cabecera de planilla para la vista por mes: sin Details. La cantidad de
/// empleados viene contada en la base; los empleados se piden paginados.
/// </summary>
public sealed record PayrollHeaderResumenDto(
    int Id,
    string PayrollNumber,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    DateTime PayDate,
    PayPeriodType PayPeriodType,
    TipoPlanilla TipoPlanilla,
    PayrollStatus Status,
    decimal TotalGrossPay,
    decimal TotalDeductions,
    decimal TotalNetPay,
    decimal TotalEmployerCost,
    int CantidadEmpleados,
    DateTime? ProcessedDate,
    DateTime? ApprovedDate,
    DateTime? PaidDate,
    DateTime CreatedAt);
