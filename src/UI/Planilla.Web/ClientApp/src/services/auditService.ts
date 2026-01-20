import { api } from './api';
import type { AuditLogDto, PagedResultDto } from '../types/api';

export interface AuditLogFilters {
  page?: number;
  pageSize?: number;
  action?: string;
  entityType?: string;
  actorUserId?: string;
  startDate?: string;
  endDate?: string;
}

export const auditService = {
  async getAuditLogs(filters: AuditLogFilters = {}): Promise<PagedResultDto<AuditLogDto>> {
    const params = new URLSearchParams();

    if (filters.page) params.append('page', filters.page.toString());
    if (filters.pageSize) params.append('pageSize', filters.pageSize.toString());
    if (filters.action) params.append('action', filters.action);
    if (filters.entityType) params.append('entityType', filters.entityType);
    if (filters.actorUserId) params.append('actorUserId', filters.actorUserId);
    if (filters.startDate) params.append('startDate', filters.startDate);
    if (filters.endDate) params.append('endDate', filters.endDate);

    const queryString = params.toString();
    const endpoint = queryString ? `/api/tenant/audit?${queryString}` : '/api/tenant/audit';

    return api.get<PagedResultDto<AuditLogDto>>(endpoint);
  },
};
