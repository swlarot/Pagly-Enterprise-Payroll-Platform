// ====================================================================
// Planilla - DTOs de configuración de factores de horas extra
// ====================================================================

using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// Factor de hora extra tal como lo ve la UI de Configuración.
/// Expone el factor vigente junto al mínimo legal para que el usuario
/// vea de qué se está apartando al editarlo.
/// </summary>
public record OvertimeFactorDto(
    TipoHoraExtra Tipo,
    string Nombre,
    string BaseLegal,
    decimal FactorLegal,
    decimal FactorVigente,
    bool EsPersonalizado
);

/// <summary>Respuesta completa del tab de horas extra.</summary>
public record OvertimeFactorConfigDto(
    int TenantId,
    IReadOnlyList<OvertimeFactorDto> Factores,
    decimal FactorExcesoLegal,
    decimal FactorExcesoVigente,
    bool FactorExcesoEsPersonalizado
);

/// <summary>Un factor a guardar. Factor null = volver al valor legal.</summary>
public record UpdateOvertimeFactorItem(
    TipoHoraExtra Tipo,
    decimal? Factor
);

/// <summary>Payload de guardado del tab de horas extra.</summary>
public record UpdateOvertimeFactorsRequest(
    IReadOnlyList<UpdateOvertimeFactorItem> Factores,
    decimal? FactorExceso
);
