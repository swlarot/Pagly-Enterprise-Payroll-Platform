// ====================================================================
// Planilla - TaxBracketDto
// Source: Core360 Stage 2
// Portado: 2025-12-26
// Descripción: DTO de tramos ISR para servicios de cálculo
// ====================================================================

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// DTO de tramo de Impuesto Sobre la Renta (ISR).
/// Contiene solo los campos necesarios para cálculos (sin auditoría).
/// </summary>
public record TaxBracketDto(
    int Id,
    int TenantId,
    int Year,
    int Order,
    string Description,
    decimal MinIncome,
    decimal? MaxIncome,
    decimal Rate,
    decimal FixedAmount
);
