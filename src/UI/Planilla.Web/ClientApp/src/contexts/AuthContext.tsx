import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { authService } from '../services/authService';
import type {
  UserInfoDto,
  TenantInfoDto,
  SubscriptionInfoDto,
  TenantRole,
} from '../types/api';
import { isTokenExpired, parseJwt } from '../utils/jwt';

// PAGLY: Auto-registro deshabilitado - usuarios creados solo via Admin Panel
interface AuthContextType {
  user: UserInfoDto | null;
  tenant: TenantInfoDto | null;
  subscription: SubscriptionInfoDto | null;
  isAuthenticated: boolean;
  isSystemAdmin: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  acceptInvite: (token: string, password: string, confirmPassword: string) => Promise<void>;
  canAccessFeature: (feature: keyof SubscriptionInfoDto) => boolean;
  hasRole: (...roles: TenantRole[]) => boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfoDto | null>(null);
  const [tenant, setTenant] = useState<TenantInfoDto | null>(null);
  const [subscription, setSubscription] = useState<SubscriptionInfoDto | null>(null);
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
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('refresh_token', data.refreshToken);
    setUser(data.user);
    setTenant(data.tenant);
    setSubscription(data.subscription);

    // Extract isSystemAdmin from token
    const payload = parseJwt(data.token);
    setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');
  };

  // PAGLY: register function removed - users created only via Admin Panel

  const logout = () => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('refresh_token');
    setUser(null);
    setTenant(null);
    setSubscription(null);
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
    if (!user) return false;
    return roles.includes(user.role);
  };

  const value: AuthContextType = {
    user,
    tenant,
    subscription,
    isAuthenticated: !!user,
    isSystemAdmin,
    isLoading,
    login,
    logout,
    acceptInvite,
    canAccessFeature,
    hasRole,
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
