namespace Vorluno.Planilla.Domain.Enums;

/// <summary>
/// Define todos los permisos disponibles en el sistema.
/// Estos permisos se asignan a roles personalizados para control de acceso granular.
/// </summary>
public static class SystemPermission
{
    // ====================================================================
    // GESTIÓN DE EMPLEADOS
    // ====================================================================
    /// <summary>Ver lista de empleados y sus detalles</summary>
    public const string EmployeesRead = "employees.read";

    /// <summary>Crear nuevos empleados</summary>
    public const string EmployeesCreate = "employees.create";

    /// <summary>Modificar información de empleados existentes</summary>
    public const string EmployeesUpdate = "employees.update";

    /// <summary>Eliminar o desactivar empleados</summary>
    public const string EmployeesDelete = "employees.delete";

    // ====================================================================
    // GESTIÓN DE ESTRUCTURA ORGANIZACIONAL
    // ====================================================================
    /// <summary>Gestionar departamentos (crear, editar, eliminar)</summary>
    public const string DepartmentsManage = "departments.manage";

    /// <summary>Gestionar posiciones/cargos (crear, editar, eliminar)</summary>
    public const string PositionsManage = "positions.manage";

    // ====================================================================
    // GESTIÓN DE PLANILLA/NÓMINA
    // ====================================================================
    /// <summary>Ver planillas procesadas</summary>
    public const string PayrollView = "payroll.view";

    /// <summary>Calcular y procesar planillas</summary>
    public const string PayrollCalculate = "payroll.calculate";

    /// <summary>Aprobar planillas calculadas para pago</summary>
    public const string PayrollApprove = "payroll.approve";

    /// <summary>Eliminar planillas en borrador</summary>
    public const string PayrollDelete = "payroll.delete";

    // ====================================================================
    // GESTIÓN DE CONCEPTOS DE NÓMINA
    // ====================================================================
    /// <summary>Gestionar préstamos de empleados</summary>
    public const string LoansManage = "loans.manage";

    /// <summary>Gestionar deducciones fijas</summary>
    public const string DeductionsManage = "deductions.manage";

    /// <summary>Gestionar horas extra</summary>
    public const string OvertimeManage = "overtime.manage";

    /// <summary>Gestionar ausencias e inasistencias</summary>
    public const string AbsencesManage = "absences.manage";

    /// <summary>Gestionar vacaciones y solicitudes</summary>
    public const string VacationsManage = "vacations.manage";

    /// <summary>Gestionar anticipos de salario</summary>
    public const string AdvancesManage = "advances.manage";

    // ====================================================================
    // REPORTES Y EXPORTACIONES
    // ====================================================================
    /// <summary>Ver reportes de CSS, ISR, SE</summary>
    public const string ReportsView = "reports.view";

    /// <summary>Exportar reportes a Excel/PDF</summary>
    public const string ReportsExport = "reports.export";

    // ====================================================================
    // CONFIGURACIÓN Y ADMINISTRACIÓN
    // ====================================================================
    /// <summary>Configurar tasas de CSS, SE, ISR</summary>
    public const string SettingsTaxes = "settings.taxes";

    /// <summary>Gestionar roles personalizados y permisos (SOLO OWNER)</summary>
    public const string SettingsRoles = "settings.roles";

    /// <summary>Invitar y gestionar usuarios del tenant</summary>
    public const string SettingsUsers = "settings.users";

    /// <summary>Ver configuración de suscripción y billing</summary>
    public const string SettingsBilling = "settings.billing";

    // ====================================================================
    // AUDITORÍA
    // ====================================================================
    /// <summary>Ver logs de auditoría del sistema</summary>
    public const string AuditView = "audit.view";

    /// <summary>
    /// Retorna todos los permisos disponibles en el sistema
    /// </summary>
    public static List<PermissionDefinition> GetAllPermissions()
    {
        return new List<PermissionDefinition>
        {
            // Empleados
            new(EmployeesRead, "Ver Empleados", "Ver lista de empleados y sus detalles", "employees"),
            new(EmployeesCreate, "Crear Empleados", "Agregar nuevos empleados al sistema", "employees"),
            new(EmployeesUpdate, "Editar Empleados", "Modificar información de empleados", "employees"),
            new(EmployeesDelete, "Eliminar Empleados", "Desactivar o eliminar empleados", "employees"),

            // Estructura
            new(DepartmentsManage, "Gestionar Departamentos", "Crear, editar y eliminar departamentos", "structure"),
            new(PositionsManage, "Gestionar Posiciones", "Crear, editar y eliminar posiciones/cargos", "structure"),

            // Planilla
            new(PayrollView, "Ver Planillas", "Consultar planillas procesadas", "payroll"),
            new(PayrollCalculate, "Calcular Planillas", "Procesar y calcular planillas", "payroll"),
            new(PayrollApprove, "Aprobar Planillas", "Aprobar planillas para pago", "payroll"),
            new(PayrollDelete, "Eliminar Planillas", "Eliminar planillas en borrador", "payroll"),

            // Conceptos de nómina
            new(LoansManage, "Gestionar Préstamos", "Administrar préstamos de empleados", "payroll-concepts"),
            new(DeductionsManage, "Gestionar Deducciones", "Administrar deducciones fijas", "payroll-concepts"),
            new(OvertimeManage, "Gestionar Horas Extra", "Registrar y aprobar horas extra", "payroll-concepts"),
            new(AbsencesManage, "Gestionar Ausencias", "Registrar ausencias e inasistencias", "payroll-concepts"),
            new(VacationsManage, "Gestionar Vacaciones", "Aprobar solicitudes de vacaciones", "payroll-concepts"),
            new(AdvancesManage, "Gestionar Anticipos", "Administrar anticipos de salario", "payroll-concepts"),

            // Reportes
            new(ReportsView, "Ver Reportes", "Consultar reportes de CSS, ISR, SE", "reports"),
            new(ReportsExport, "Exportar Reportes", "Exportar reportes a Excel/PDF", "reports"),

            // Configuración
            new(SettingsTaxes, "Configurar Impuestos", "Ajustar tasas de CSS, SE, ISR", "settings"),
            new(SettingsRoles, "Gestionar Roles", "Crear y modificar roles personalizados (solo Owner)", "settings"),
            new(SettingsUsers, "Gestionar Usuarios", "Invitar y administrar usuarios", "settings"),
            new(SettingsBilling, "Ver Facturación", "Consultar suscripción y pagos", "settings"),

            // Auditoría
            new(AuditView, "Ver Auditoría", "Consultar logs de actividad del sistema", "audit"),
        };
    }

    /// <summary>
    /// Retorna los permisos predeterminados para un rol del sistema
    /// </summary>
    public static List<string> GetDefaultPermissionsForSystemRole(TenantRole role)
    {
        return role switch
        {
            // Owner tiene todos los permisos
            TenantRole.Owner => GetAllPermissions().Select(p => p.Key).ToList(),

            // User no tiene permisos predeterminados - se asignan mediante CustomTenantRole
            TenantRole.User => new List<string>(),

            _ => new List<string>()
        };
    }
}

/// <summary>
/// Define un permiso del sistema con metadatos
/// </summary>
public record PermissionDefinition(
    string Key,
    string Name,
    string Description,
    string Category
);
