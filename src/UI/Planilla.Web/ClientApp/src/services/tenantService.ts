import { api } from './api';
import type {
  TenantDto,
  TenantUserDto,
  CreateInvitationDto,
  InvitationDto,
  TenantUsageDto,
} from '../types/api';

export const tenantService = {
  async getTenant(): Promise<TenantDto> {
    return api.get<TenantDto>('/api/tenant');
  },

  async getUsers(): Promise<TenantUserDto[]> {
    return api.get<TenantUserDto[]>('/api/tenant/users');
  },

  async inviteUser(dto: CreateInvitationDto): Promise<InvitationDto> {
    return api.post<InvitationDto>('/api/tenant/invite', dto);
  },

  async removeUser(userId: number): Promise<void> {
    return api.delete<void>(`/api/tenant/users/${userId}`);
  },

  async getUsage(): Promise<TenantUsageDto> {
    return api.get<TenantUsageDto>('/api/tenant/usage');
  },
};
