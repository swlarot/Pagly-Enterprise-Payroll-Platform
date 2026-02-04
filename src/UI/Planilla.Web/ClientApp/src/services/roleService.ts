import { api } from './api';
import type {
  CustomTenantRoleDto,
  CreateCustomTenantRoleDto,
  UpdateCustomTenantRoleDto,
  PermissionDto,
  UpdateRolePermissionsDto,
  AssignRoleToUserDto,
} from '../types/api';

/**
 * Servicio para gestión de roles personalizados
 */
export const roleService = {
  /**
   * Obtener todos los roles del tenant (sistema + personalizados)
   */
  async getRoles(): Promise<CustomTenantRoleDto[]> {
    return api.get<CustomTenantRoleDto[]>('/api/tenants/roles');
  },

  /**
   * Crear un nuevo rol personalizado
   */
  async createRole(dto: CreateCustomTenantRoleDto): Promise<CustomTenantRoleDto> {
    return api.post<CustomTenantRoleDto>('/api/tenants/roles', dto);
  },

  /**
   * Actualizar un rol personalizado existente
   */
  async updateRole(roleId: number, dto: UpdateCustomTenantRoleDto): Promise<CustomTenantRoleDto> {
    return api.put<CustomTenantRoleDto>(`/api/tenants/roles/${roleId}`, dto);
  },

  /**
   * Eliminar un rol personalizado
   */
  async deleteRole(roleId: number): Promise<void> {
    return api.delete<void>(`/api/tenants/roles/${roleId}`);
  },

  /**
   * Obtener permisos de un rol específico
   */
  async getRolePermissions(roleId: number): Promise<string[]> {
    return api.get<string[]>(`/api/tenants/roles/${roleId}/permissions`);
  },

  /**
   * Actualizar permisos de un rol
   */
  async updateRolePermissions(roleId: number, permissions: string[]): Promise<void> {
    const dto: UpdateRolePermissionsDto = { permissions };
    return api.put<void>(`/api/tenants/roles/${roleId}/permissions`, dto);
  },

  /**
   * Obtener todos los permisos disponibles
   */
  async getAvailablePermissions(): Promise<PermissionDto[]> {
    return api.get<PermissionDto[]>('/api/permissions');
  },

  /**
   * Obtener permisos del usuario actual
   */
  async getMyPermissions(): Promise<string[]> {
    return api.get<string[]>('/api/permissions/me');
  },

  /**
   * Verificar si el usuario tiene un permiso específico
   */
  async checkPermission(permission: string): Promise<boolean> {
    return api.get<boolean>(`/api/permissions/check/${permission}`);
  },

  /**
   * Asignar un rol a un usuario (rol del sistema User o un rol personalizado).
   * - systemRole: 1 = User sin rol personalizado
   * - customRoleId: ID del CustomTenantRole a asignar
   */
  async assignRoleToUser(userId: string, dto: AssignRoleToUserDto): Promise<void> {
    return api.put<void>(`/api/tenant/users/${userId}/role`, dto);
  },
};
