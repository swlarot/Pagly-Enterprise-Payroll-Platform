import { api } from './api';
import type {
  SystemMetricsDto,
  AdminTenantDto,
  AdminTenantUserDto,
  CreateTenantDto,
  UpdateAdminTenantDto,
  UpdateTenantSubscriptionDto,
  InviteUserRequest,
  AssignUserToTenantDto,
  AuditLogPagedResultDto,
  SystemUserPagedResultDto,
} from '../types/api';

export const systemAdminService = {
  // Dashboard metrics
  getMetrics: () => api.get<SystemMetricsDto>('/api/admin/metrics'),

  // System-wide user management
  getAllSystemUsers: (params?: {
    search?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const queryParams = new URLSearchParams();
    if (params?.search) queryParams.append('search', params.search);
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());

    const queryString = queryParams.toString();
    const url = `/api/admin/system/users${queryString ? `?${queryString}` : ''}`;
    return api.get<SystemUserPagedResultDto>(url);
  },

  createUser: (data: {
    nombre: string;
    apellido: string;
    correo: string;
    telefono?: string;
  }) => api.post('/api/admin/users', data),

  // Tenant management
  getAllTenants: (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    plan?: string;
    status?: string;
    isActive?: boolean;
  }) => {
    // Backend doesn't support pagination/filters yet, returns AdminTenantDto[]
    return api.get<AdminTenantDto[]>('/api/admin/tenants');
  },

  getTenantById: (id: number) => api.get<AdminTenantDto>(`/api/admin/tenants/${id}`),

  createTenant: (data: CreateTenantDto) => api.post<AdminTenantDto>('/api/admin/tenants', data),

  updateTenant: (id: number, data: UpdateAdminTenantDto) =>
    api.put<AdminTenantDto>(`/api/admin/tenants/${id}`, data),

  updateTenantSubscription: (id: number, data: UpdateTenantSubscriptionDto) =>
    api.put<AdminTenantDto>(`/api/admin/tenants/${id}/subscription`, data),

  deactivateTenant: (id: number) => api.delete(`/api/admin/tenants/${id}`),

  reactivateTenant: (id: number) => api.post(`/api/admin/tenants/${id}/reactivate`),

  // Tenant users management
  getTenantUsers: (tenantId: number) =>
    api.get<AdminTenantUserDto[]>(`/api/admin/tenants/${tenantId}/users`),

  inviteUserToTenant: (tenantId: number, data: InviteUserRequest) =>
    api.post<AdminTenantUserDto>(`/api/admin/tenants/${tenantId}/users`, data),

  assignUserToTenant: (tenantId: number, data: AssignUserToTenantDto) =>
    api.post<AdminTenantUserDto>(`/api/admin/tenants/${tenantId}/users`, data),

  // Audit log
  getTenantAuditLog: (
    tenantId: number,
    params?: {
      page?: number;
      pageSize?: number;
      from?: string;
      to?: string;
      action?: string;
      userId?: string;
    }
  ) => {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.from) queryParams.append('from', params.from);
    if (params?.to) queryParams.append('to', params.to);
    if (params?.action) queryParams.append('action', params.action);
    if (params?.userId) queryParams.append('userId', params.userId);

    const queryString = queryParams.toString();
    const url = `/api/admin/tenants/${tenantId}/audit${queryString ? `?${queryString}` : ''}`;
    return api.get<AuditLogPagedResultDto>(url);
  },
};
