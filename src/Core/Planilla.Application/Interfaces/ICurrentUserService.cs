// ====================================================================
// Planilla - ICurrentUserService
// Source: Phase E - Hardening
// Creado: 2025-12-26
// Descripción: Servicio para obtener información del usuario actual
// Provee CompanyId para multi-tenancy y filtrado global
// ====================================================================

namespace Vorluno.Planilla.Application.Interfaces;

/// <summary>
/// Servicio que provee información del usuario actualmente autenticado.
/// Utilizado para multi-tenancy (CompanyId) y global query filters.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// CompanyId del usuario actual obtenido del claim "CompanyId".
    /// Retorna null si el usuario no está autenticado o no tiene CompanyId asignado.
    /// </summary>
    int? CompanyId { get; }

    /// <summary>
    /// UserId del usuario actual obtenido del claim NameIdentifier.
    /// Retorna null si el usuario no está autenticado.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Email del usuario actual obtenido del claim Email.
    /// Retorna null si el usuario no está autenticado.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Indica si el usuario actual está autenticado.
    /// </summary>
    bool IsAuthenticated { get; }

    // ====================================================================
    // EMPLOYEE SELF-SERVICE: Métodos para detectar empleados vinculados
    // ====================================================================

    /// <summary>
    /// Obtiene el ID del tenant actual desde el claim "tenant_id".
    /// Retorna null si el usuario no está autenticado o no tiene tenant asignado.
    /// </summary>
    int? GetTenantId();

    /// <summary>
    /// Obtiene el ID del empleado vinculado desde el claim "employee_id".
    /// Retorna null si el usuario no está vinculado a un empleado.
    /// </summary>
    int? GetLinkedEmployeeId();

    /// <summary>
    /// Verifica si el usuario actual está vinculado a un empleado.
    /// </summary>
    bool IsLinkedEmployee();

    /// <summary>
    /// Verifica si el usuario actual es Owner del tenant.
    /// </summary>
    bool IsOwner();
}
