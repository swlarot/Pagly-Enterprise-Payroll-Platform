namespace Vorluno.Planilla.Application.DTOs;

/// <summary>
/// Resultado de la validación de eliminación de un empleado
/// </summary>
public class EmpleadoDeletionResultDto
{
    /// <summary>
    /// Indica si el empleado puede ser eliminado
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Indica si se requiere confirmación del usuario (solo hay warnings, no bloqueadores)
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Lista de bloqueadores que impiden la eliminación
    /// </summary>
    public List<DeletionBlockerDto> Blockers { get; set; } = new();

    /// <summary>
    /// Lista de warnings que requieren confirmación
    /// </summary>
    public List<DeletionWarningDto> Warnings { get; set; } = new();

    /// <summary>
    /// Información del empleado
    /// </summary>
    public EmpleadoInfoDto? EmpleadoInfo { get; set; }
}

/// <summary>
/// Bloqueador que impide la eliminación de un empleado
/// </summary>
public class DeletionBlockerDto
{
    /// <summary>
    /// Razón del bloqueador
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Entidad relacionada que causa el bloqueo
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad de registros que causan el bloqueo
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Acción recomendada para resolver el bloqueo
    /// </summary>
    public string Resolution { get; set; } = string.Empty;
}

/// <summary>
/// Advertencia que requiere confirmación del usuario
/// </summary>
public class DeletionWarningDto
{
    /// <summary>
    /// Razón de la advertencia
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Entidad relacionada
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad de registros afectados
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Mensaje adicional o consecuencia de continuar
    /// </summary>
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// Información básica del empleado para el resultado de eliminación
/// </summary>
public class EmpleadoInfoDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string NumeroIdentificacion { get; set; } = string.Empty;
    public bool EstaActivo { get; set; }
    public bool TieneUsuarioVinculado { get; set; }
}
