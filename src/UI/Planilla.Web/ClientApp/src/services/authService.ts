import { api } from './api';
import type {
  RegisterDto,
  LoginDto,
  AcceptInvitationDto,
  AuthResponseDto,
  ValidateInviteResponseDto,
} from '../types/api';

export const authService = {
  async register(dto: RegisterDto): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/register', dto);
  },

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

  async refreshToken(): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/refresh');
  },
};
