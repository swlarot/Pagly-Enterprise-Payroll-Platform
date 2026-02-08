import React, { createContext, useContext, useState, useEffect, useRef, ReactNode } from 'react';
import { authService } from '../services/authService';
import { api } from '../services/api';
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
  permissions: string[];
  isAuthenticated: boolean;
  isSystemAdmin: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<{ requiresTenantSelection: boolean; availableTenants?: TenantSummaryDto[] }>;
  selectTenant: (tenantId: number) => Promise<void>;
  logout: () => void;
  acceptInvite: (token: string, password: string, confirmPassword: string) => Promise<void>;
  canAccessFeature: (feature: keyof SubscriptionInfoDto) => boolean;
  hasRole: (...roles: TenantRole[]) => boolean;
  hasPermission: (permission: string) => boolean;
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
  const [permissions, setPermissions] = useState<string[]>([]);
  const [isSystemAdmin, setIsSystemAdmin] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  
  // Flag para evitar re-validación después del login
  const hasInitializedRef = React.useRef(false);

  // Auto-login on mount if token exists
  // Solo ejecutar una vez al montar el componente
  useEffect(() => {
    // Evitar ejecución múltiple
    if (hasInitializedRef.current) {
      return;
    }
    
    const token = localStorage.getItem('auth_token');
    if (token && !isTokenExpired(token)) {
      // Solo validar si no hay tenant ya cargado (evita sobrescribir después del login)
      // El login() establecerá el tenant, así que no debemos re-validar si ya existe
      if (!tenant) {
        validateAndSetUser(token);
      } else {
        // Tenant ya cargado (probablemente desde login), solo marcar como no loading
        setIsLoading(false);
      }
    } else {
      setIsLoading(false);
      if (token) {
        // Token expired, clean up
        localStorage.removeItem('auth_token');
        localStorage.removeItem('refresh_token');
      }
    }
    
    hasInitializedRef.current = true;
  }, []); // Dependencias vacías: solo ejecutar al montar

  // Función para obtener permisos del usuario actual
  const fetchUserPermissions = async (): Promise<void> => {
    try {
      const perms = await api.get<string[]>('/api/permissions/me');
      setPermissions(perms || []);
    } catch (error) {
      console.error('Error fetching user permissions:', error);
      // Si falla, usar permisos vacíos (usuario sin permisos personalizados)
      setPermissions([]);
    }
  };

  const validateAndSetUser = async (token: string) => {
    try {
      const data = await authService.me();
      setUser(data.user);
      setTenant(data.tenant ?? null);
      setSubscription(data.subscription ?? null);
      setAvailableTenants(data.availableTenants || []);

      // Si el backend indica que debe elegir tenant (token de selección), mantener token y estado
      if (data.requiresTenantSelection && (data.availableTenants?.length ?? 0) > 0) {
        // No limpiar token; el usuario debe ir a /select-tenant
        const payload = parseJwt(token);
        setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');
        setIsLoading(false);
        return;
      }

      // Extract isSystemAdmin from token
      const payload = parseJwt(token);
      setIsSystemAdmin(payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True');

      // Obtener permisos del usuario si tiene tenant seleccionado
      if (data.tenant) {
        await fetchUserPermissions();
      } else {
        setPermissions([]);
      }
    } catch (error) {
      console.error('Auth validation failed:', error);
      localStorage.removeItem('auth_token');
      localStorage.removeItem('refresh_token');
      setPermissions([]);
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
      setPermissions([]); // Sin permisos hasta seleccionar tenant

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
    
    // Establecer tenant ANTES de otras operaciones para que esté disponible inmediatamente
    console.log('[AuthContext] Setting tenant from login response:', data.tenant);
    setTenant(data.tenant);
    
    setSubscription(data.subscription);
    setAvailableTenants(data.availableTenants || []);

    // Extract isSystemAdmin from token ANTES de obtener permisos
    const payload = parseJwt(data.token);
    const adminStatus = payload?.is_system_admin === 'true' || payload?.is_system_admin === 'True';
    setIsSystemAdmin(adminStatus);

    // Obtener permisos del usuario si tiene tenant seleccionado
    if (data.tenant) {
      await fetchUserPermissions();
    } else {
      setPermissions([]);
    }

    console.log('[AuthContext] Login completed. Tenant set:', data.tenant ? `${data.tenant.name} (ID: ${data.tenant.id})` : 'null');

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

    // Obtener permisos del usuario para el tenant seleccionado
    if (data.tenant) {
      await fetchUserPermissions();
    } else {
      setPermissions([]);
    }
  };

  const logout = () => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('refresh_token');
    setUser(null);
    setTenant(null);
    setSubscription(null);
    setAvailableTenants([]);
    setPermissions([]);
    setIsSystemAdmin(false);
  };

  const acceptInvite = async (token: string, password: string, confirmPassword: string) => {
    const data = await authService.acceptInvite({ token, password, confirmPassword });
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('refresh_token', data.refreshToken);
    setUser(data.user);
    setTenant(data.tenant);
    setSubscription(data.subscription);
    
    // Obtener permisos del usuario después de aceptar invitación
    if (data.tenant) {
      await fetchUserPermissions();
    } else {
      setPermissions([]);
    }
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
    // Owner (0) y User (1) pueden escribir (User depende de permisos personalizados)
    return user.role === 0 || user.role === 1;
  };

  const canDelete = (): boolean => {
    if (!user || isLoading) return false;
    // Solo Owner (0) puede eliminar por defecto
    return user.role === 0;
  };

  const isReadOnly = (): boolean => {
    // Por ahora nadie es solo lectura - se manejan permisos personalizados
    return false;
  };

  const hasPermission = (permission: string): boolean => {
    if (!permissions || permissions.length === 0) return false;
    return permissions.includes(permission);
  };

  const value: AuthContextType = {
    user,
    tenant,
    subscription,
    availableTenants,
    permissions,
    isAuthenticated: !!user,
    isSystemAdmin,
    isLoading,
    login,
    selectTenant,
    logout,
    acceptInvite,
    canAccessFeature,
    hasRole,
    hasPermission,
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
