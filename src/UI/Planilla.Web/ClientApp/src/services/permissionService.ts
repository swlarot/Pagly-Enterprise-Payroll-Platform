/**
 * Servicio para verificar permisos y acceso a módulos del sistema
 */

// Mapeo de módulos del menú a permisos requeridos
const MODULE_PERMISSIONS: Record<string, string> = {
  'dashboard': 'dashboard.view',
  'empleados': 'employees.read',
  'departamentos': 'departments.manage',
  'posiciones': 'positions.manage',
  'prestamos': 'loans.manage',
  'deducciones': 'deductions.manage',
  'anticipos': 'advances.manage',
  'horas-extra': 'overtime.manage',
  'ausencias': 'absences.manage',
  'vacaciones': 'vacations.manage',
  'planillas': 'payroll.view',
  'reportes': 'reports.view',
  'configuracion': 'settings.taxes', // Mínimo requerido para ver configuración
  'roles': 'settings.roles',
  'audit': 'audit.view',
};

/**
 * Verifica si el usuario tiene acceso a un módulo específico basado en sus permisos
 * @param module Nombre del módulo (ej: 'empleados', 'departamentos')
 * @param permissions Lista de permisos del usuario (puede ser undefined o null)
 * @returns true si el usuario tiene acceso al módulo
 */
export function canAccessModule(module: string, permissions?: string[] | null): boolean {
  // Si permissions no está definido o es null, denegar acceso (seguridad por defecto)
  if (!permissions || !Array.isArray(permissions)) {
    return false;
  }
  
  const requiredPermission = MODULE_PERMISSIONS[module];
  
  if (!requiredPermission) {
    // Si el módulo no está en el mapeo, permitir acceso por defecto (para compatibilidad)
    return true;
  }
  
  return permissions.includes(requiredPermission);
}

/**
 * Verifica si el usuario tiene un permiso específico
 * @param permission Permiso a verificar (ej: 'employees.read')
 * @param permissions Lista de permisos del usuario (puede ser undefined o null)
 * @returns true si el usuario tiene el permiso
 */
export function hasPermission(permission: string, permissions?: string[] | null): boolean {
  if (!permissions || !Array.isArray(permissions)) {
    return false;
  }
  return permissions.includes(permission);
}

/**
 * Verifica si el usuario tiene al menos uno de los permisos especificados
 * @param permissionList Lista de permisos a verificar
 * @param permissions Lista de permisos del usuario
 * @returns true si el usuario tiene al menos uno de los permisos
 */
export function hasAnyPermission(permissionList: string[], permissions: string[]): boolean {
  return permissionList.some(perm => permissions.includes(perm));
}

/**
 * Verifica si el usuario tiene todos los permisos especificados
 * @param permissionList Lista de permisos a verificar
 * @param permissions Lista de permisos del usuario (puede ser undefined o null)
 * @returns true si el usuario tiene todos los permisos
 */
export function hasAllPermissions(permissionList: string[], permissions?: string[] | null): boolean {
  if (!permissions || !Array.isArray(permissions)) {
    return false;
  }
  return permissionList.every(perm => permissions.includes(perm));
}
