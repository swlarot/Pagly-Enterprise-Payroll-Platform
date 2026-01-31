import { api } from './api';
import type {
  LoginDto,
  AcceptInvitationDto,
  AuthResponseDto,
  ValidateInviteResponseDto,
  SelectTenantDto,
} from '../types/api';

// PAGLY: Auto-registro deshabilitado - usuarios creados solo via Admin Panel
export const authService = {
  async login(dto: LoginDto): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/login', dto);
  },

  async me(): Promise<AuthResponseDto> {
    return api.get<AuthResponseDto>('/api/auth/me');
  },

  async validateInvite(token: string): Promise<ValidateInviteResponseDto> {
    return api.get<ValidateInviteResponseDto>(`/api/auth/validate-invite?token=${token}`);
  },

  async acceptInvite(dto: AcceptInvitationDto): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/accept-invite', dto);
  },

  async refreshToken(refreshToken: string): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/refresh', { refreshToken });
  },

  async selectTenant(dto: SelectTenantDto): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/select-tenant', dto);
  },
};
