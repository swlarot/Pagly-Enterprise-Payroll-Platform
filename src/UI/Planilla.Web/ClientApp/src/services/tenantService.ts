import { api } from './api';
import type {
  TenantDto,
  TenantUserDto,
  CreateInvitationDto,
  InvitationDto,
  TenantUsageDto,
  UpdateTenantUserDto,
} from '../types/api';

export const tenantService = {
  async getTenant(): Promise<TenantDto> {
    return api.get<TenantDto>('/api/tenant');
  },

  async getUsers(): Promise<TenantUserDto[]> {
    return api.get<TenantUserDto[]>('/api/tenant/users');
  },

  async updateUser(userId: number, dto: UpdateTenantUserDto): Promise<void> {
    return api.patch<void>(`/api/tenant/users/${userId}`, dto);
  },

  async removeUser(userId: number): Promise<void> {
    return api.delete<void>(`/api/tenant/users/${userId}`);
  },

  async inviteUser(dto: CreateInvitationDto): Promise<InvitationDto> {
    return api.post<InvitationDto>('/api/tenant/invite', dto);
  },

  async getInvitations(): Promise<InvitationDto[]> {
    return api.get<InvitationDto[]>('/api/tenant/invitations');
  },

  async revokeInvitation(invitationId: number): Promise<void> {
    return api.delete<void>(`/api/tenant/invitations/${invitationId}`);
  },

  async getUsage(): Promise<TenantUsageDto> {
    return api.get<TenantUsageDto>('/api/tenant/usage');
  },
};
