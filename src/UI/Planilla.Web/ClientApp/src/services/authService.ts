import { api } from './api';
import type {
  RegisterDto,
  LoginDto,
  AcceptInvitationDto,
  AuthResponseDto,
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

  async acceptInvite(dto: AcceptInvitationDto): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/accept-invite', dto);
  },

  async refreshToken(): Promise<AuthResponseDto> {
    return api.post<AuthResponseDto>('/api/auth/refresh');
  },
};
