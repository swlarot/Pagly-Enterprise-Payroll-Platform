using Vorluno.Planilla.Application.DTOs;

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio para validar la eliminación de empleados verificando dependencias y bloqueos
/// </summary>
public interface IEmployeeDeletionValidationService
{
    /// <summary>
    /// Valida si un empleado puede ser eliminado, verificando bloqueadores y warnings
    /// </summary>
    /// <param name="empleadoId">ID del empleado a validar</param>
    /// <returns>Resultado con bloqueadores, warnings y si puede ser eliminado</returns>
    Task<EmpleadoDeletionResultDto> ValidateEmployeeDeletionAsync(int empleadoId);
}
