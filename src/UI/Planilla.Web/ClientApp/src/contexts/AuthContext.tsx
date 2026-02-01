import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { authService } from '../services/authService';
import type {
  UserInfoDto,
  TenantInfoDto,
  SubscriptionInfoDto,
  TenantRole,
  TenantSummaryDto,
} from '../types/api';
import { isTokenExpired, parseJwt } from '../utils/jwt';

// PAGLY: Auto-registro deshabilitado - usuarios creados solo via Admin Panel
interface AuthContextType {
  user: UserInfoDto | null;
  tenant: TenantInfoDto | null;
  subscription: SubscriptionInfoDto | null;
  availableTenants: TenantSummaryDto[];
  isAuthenticated: boolean;
  isSystemAdmin: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<{ requiresTenantSelection: boolean; availableTenants?: TenantSummaryDto[] }>;
  selectTenant: (tenantId: number) => Promise<void>;
  logout: () => void;
  acceptInvite: (token: string, password: string, confirmPassword: string) => Promise<void>;
  canAccessFeature: (feature: keyof SubscriptionInfoDto) => boolean;
  hasRole: (...roles: TenantRole[]) => boolean;
  canWrite: () => boolean;
  canDelete: () => boolean;
  isReadOnly: () => boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfoDto | null>(null);
  const [tenant, setTenant] = useState<TenantInfoDto | null>(null);
  const [subscription, setSubscription] = useState<SubscriptionInfoDto | null>(null);
  const [availableTenants, setAvailableTenants] = useState<TenantSummaryDto[]>([]);
  const [isSystemAdmin, setIsSystemAdmin] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  // Auto-login on mount if token exists
  useEffect(() => {
    const token = localStorage.getItem('auth_token');
    if (token && !isTokenExpired(token)) {
      validateAndSetUser(token);
    } else {
      setIsLoading(false);
      if (token) {
        // Token expired, clean up
        localStorage.removeItem('auth_token');
      }
    }
  }, []);

  const validateAndSetUser = async (token: string) => {
    try {
      const data = await authService.me();
      setUser(data.user);
      setTenant(data.tenant);
      setSubscription(data.subscription);
      setAvailableTenants(data.availableTenants || []);

      // Extract isSystemAdmin from token
      const payload = parseJwt(token);
      setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');
    } catch (error) {
      console.error('Auth validation failed:', error);
      localStorage.removeItem('auth_token');
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (email: string, password: string) => {
    const data = await authService.login({ email, password });

    // Si requiere selección de tenant, guardamos el token TEMPORAL para que pueda autenticarse al seleccionar
    if (data.requiresTenantSelection) {
      // Guardar token temporal (necesario para autenticar la llamada a select-tenant)
      localStorage.setItem('auth_token', data.token);
      if (data.refreshToken) {
        localStorage.setItem('refresh_token', data.refreshToken);
      }

      // Guardar temporalmente los datos del usuario
      setUser(data.user);
      setAvailableTenants(data.availableTenants || []);

      // Retornar para que LoginPage pueda redirigir al selector
      return {
        requiresTenantSelection: true,
        availableTenants: data.availableTenants || []
      };
    }

    // Login normal (un solo tenant o SystemAdmin)
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('refresh_token', data.refreshToken);
    setUser(data.user);
    setTenant(data.tenant);
    setSubscription(data.subscription);
    setAvailableTenants(data.availableTenants || []);

    // Extract isSystemAdmin from token
    const payload = parseJwt(data.token);
    setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');

    return { requiresTenantSelection: false };
  };

  // PAGLY: register function removed - users created only via Admin Panel

  const selectTenant = async (tenantId: number) => {
    const data = await authService.selectTenant({ tenantId });

    // Guardar el nuevo token con el tenant seleccionado
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('refresh_token', data.refreshToken);
    setUser(data.user);
    setTenant(data.tenant);
    setSubscription(data.subscription);
    setAvailableTenants(data.availableTenants || []);

    // Extract isSystemAdmin from token
    const payload = parseJwt(data.token);
    setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');
  };

  const logout = () => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('refresh_token');
    setUser(null);
    setTenant(null);
    setSubscription(null);
    setAvailableTenants([]);
    setIsSystemAdmin(false);
  };

  const acceptInvite = async (token: string, password: string, confirmPassword: string) => {
    const data = await authService.acceptInvite({ token, password, confirmPassword });
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('refresh_token', data.refreshToken);
    setUser(data.user);
    setTenant(data.tenant);
    setSubscription(data.subscription);
  };

  const canAccessFeature = (feature: keyof SubscriptionInfoDto): boolean => {
    if (!subscription) return false;
    const value = subscription[feature];
    return typeof value === 'boolean' ? value : false;
  };

  const hasRole = (...roles: TenantRole[]): boolean => {
    // Retornar false si no hay usuario o si está cargando
    if (!user || isLoading) return false;
    return roles.includes(user.role);
  };

  const canWrite = (): boolean => {
    if (!user || isLoading) return false;
    // Owner, Admin y Manager pueden escribir
    return user.role === TenantRole.Owner
        || user.role === TenantRole.Admin
        || user.role === TenantRole.Manager;
  };

  const canDelete = (): boolean => {
    if (!user || isLoading) return false;
    // Solo Owner y Admin pueden eliminar
    return user.role === TenantRole.Owner || user.role === TenantRole.Admin;
  };

  const isReadOnly = (): boolean => {
    if (!user || isLoading) return true;
    // Accountant y Employee son solo lectura
    return user.role === TenantRole.Accountant || user.role === TenantRole.Employee;
  };

  const value: AuthContextType = {
    user,
    tenant,
    subscription,
    availableTenants,
    isAuthenticated: !!user,
    isSystemAdmin,
    isLoading,
    login,
    selectTenant,
    logout,
    acceptInvite,
    canAccessFeature,
    hasRole,
    canWrite,
    canDelete,
    isReadOnly,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
