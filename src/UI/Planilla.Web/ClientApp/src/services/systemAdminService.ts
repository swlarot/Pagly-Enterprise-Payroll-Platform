import { api } from './api';
import type {
  SystemMetricsDto,
  AdminTenantDto,
  AdminTenantUserDto,
  CreateTenantDto,
  UpdateAdminTenantDto,
  UpdateTenantSubscriptionDto,
} from '../types/api';

export const systemAdminService = {
  // Dashboard metrics
  getMetrics: () => api.get<SystemMetricsDto>('/api/admin/metrics'),

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
};
