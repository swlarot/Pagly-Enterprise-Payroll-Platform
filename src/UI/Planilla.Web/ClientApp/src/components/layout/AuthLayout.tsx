import { ReactNode, useEffect, useRef } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { TenantRole } from '../../types/api';
import { canAccessModule } from '../../services/permissionService';
import { PaglyLogo } from '../ui/PaglyLogo';
import { GlobalSearch } from '../ui/GlobalSearch';
import { useState } from 'react';
import {
  LayoutDashboard, Users, Users2, User, Building2, Building, Briefcase,
  DollarSign, CreditCard, Minus, Clock, XCircle, TreePalm,
  ClipboardList, BarChart3, ShieldCheck, FileText, Settings,
  ChevronDown, ChevronRight, Search, Bell, LogOut, Banknote, Check, Loader2,
  Key,
} from 'lucide-react';

interface AuthLayoutProps {
  children: ReactNode;
}

// Rutas primarias — aparecen como tabs en el top navbar
const PRIMARY_ROUTES = ['/dashboard', '/empleados', '/departamentos', '/posiciones'];

export default function AuthLayout({ children }: AuthLayoutProps) {
  const { user, tenant, availableTenants, logout, hasRole, isSystemAdmin, permissions, selectTenant } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const [searchOpen, setSearchOpen] = useState(false);
  const [userDropdownOpen, setUserDropdownOpen] = useState(false);
  const [switchingTenant, setSwitchingTenant] = useState(false);
  const userDropdownRef = useRef<HTMLDivElement>(null);

  // Cerrar dropdown al hacer click fuera
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (userDropdownRef.current && !userDropdownRef.current.contains(e.target as Node)) {
        setUserDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSwitchTenant = async (tenantId: number) => {
    if (switchingTenant) return;
    try {
      setSwitchingTenant(true);
      setUserDropdownOpen(false);
      await selectTenant(tenantId);
      // Full page navigation a /dashboard: garantiza que todos los componentes
      // re-fetchen datos del nuevo tenant (useEffect([]) no re-corre en cambios de contexto).
      window.location.href = '/dashboard';
    } catch {
      // error silencioso — api.ts ya muestra toast
      setSwitchingTenant(false);
    }
  };

  // Shortcut global Ctrl+K / ⌘K para abrir búsqueda
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setSearchOpen(prev => !prev);
      }
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, []);

  // Ambos grupos expandidos por defecto
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(
    new Set(['novedades', 'asistencia'])
  );

  const toggleGroup = (groupKey: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev);
      next.has(groupKey) ? next.delete(groupKey) : next.add(groupKey);
      return next;
    });
  };

  // Verificar acceso a módulos usando permisos granulares
  const canAccessModuleCheck = (module: string): boolean => {
    if (!user) return false;
    const role = user.role;
    if (role === TenantRole.Owner) return true;
    if (permissions && permissions.length > 0) return canAccessModule(module, permissions);
    return false;
  };

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const getPageTitle = () => {
    const routes: Record<string, string> = {
      '/dashboard': 'Dashboard',
      '/empleados': 'Gestión de Empleados',
      '/departamentos': 'Gestión de Departamentos',
      '/posiciones': 'Gestión de Posiciones',
      '/prestamos': 'Gestión de Préstamos',
      '/deducciones': 'Gestión de Deducciones',
      '/acreedores': 'Catálogo de Acreedores',
      '/anticipos': 'Gestión de Anticipos',
      '/horas-extra': 'Gestión de Horas Extra',
      '/ausencias': 'Gestión de Ausencias',
      '/vacaciones': 'Gestión de Vacaciones',
      '/planillas': 'Gestión de Planillas',
      '/decimo': 'Planilla de Décimo Tercer Mes',
      '/liquidaciones': 'Liquidaciones Laborales',
      '/reportes': 'Reportes de Planilla',
      '/configuracion': 'Configuración del Sistema',
      '/roles': 'Roles y Permisos',
      '/audit': 'Registro de Auditoría',
      '/settings/api-keys': 'API Keys',
      '/mi-perfil': 'Mi Perfil',
    };
    return routes[location.pathname] || 'Dashboard';
  };

  const getParentSection = (): string | null => {
    const path = location.pathname;
    // En rutas primarias no hay breadcrumb — el tab activo es suficiente contexto
    if (PRIMARY_ROUTES.includes(path)) return null;
    if (['/anticipos', '/prestamos', '/deducciones', '/acreedores'].includes(path)) return 'Novedades';
    if (['/horas-extra', '/ausencias', '/vacaciones'].includes(path)) return 'Asistencia';
    if (['/roles', '/audit', '/configuracion', '/settings/api-keys'].includes(path)) return 'Admin';
    return null;
  };

  // ── Clase de tab del top navbar (Linear-style pill) ──
  const topNavTabClass = ({ isActive }: { isActive: boolean }) =>
    `relative flex items-center gap-1.5 px-3 py-1.5 rounded-md text-[13px] font-medium
     transition-all duration-150 ${
      isActive
        ? 'bg-white/[0.10] text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.06)]'
        : 'text-gray-400 hover:text-gray-100 hover:bg-white/[0.06]'
    }`;

  // ── Clases de navegación sidebar ──

  // Mi Perfil y otros ítems primarios del sidebar
  const primaryItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-medium text-[13px] transition-all duration-150 ${
      isActive
        ? 'bg-white/[0.08] border-l-2 border-primary-400 text-primary-300'
        : 'text-gray-400 hover:bg-white/[0.04] hover:text-gray-100'
    }`;

  // Planillas — prominente con acento azul
  const prominentPlanillasClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-semibold text-[13px] transition-all duration-150 border-l-2 ${
      isActive
        ? 'bg-white/[0.08] border-blue-400 text-white'
        : 'border-transparent text-gray-400 hover:bg-white/[0.04] hover:text-gray-100 hover:border-blue-500/40'
    }`;

  // Reportes — prominente con acento violet
  const prominentReportesClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-semibold text-[13px] transition-all duration-150 border-l-2 ${
      isActive
        ? 'bg-white/[0.08] border-violet-400 text-white'
        : 'border-transparent text-gray-400 hover:bg-white/[0.04] hover:text-gray-100 hover:border-violet-500/40'
    }`;

  // Sub-ítems dentro de grupos colapsables
  const subItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2 w-full px-2.5 py-1.5 rounded-md transition-all duration-150 cursor-pointer text-[12.5px] font-medium ${
      isActive
        ? 'text-white bg-white/[0.06]'
        : 'text-gray-400 hover:text-gray-100 hover:bg-white/[0.04]'
    }`;

  // Admin items (Owner) — ícono violet para distinguirlos
  const adminItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2 px-2.5 py-1.5 rounded-md transition-all duration-150 text-[12.5px] font-medium ${
      isActive
        ? 'bg-white/[0.08] text-violet-300'
        : 'text-gray-400 hover:bg-white/[0.04] hover:text-gray-100'
    }`;

  // Datos derivados
  const tenantInitials = tenant?.name?.substring(0, 2).toUpperCase() || 'PL';
  const userInitials = user?.fullName
    ? user.fullName.split(' ').map((n: string) => n[0]).slice(0, 2).join('').toUpperCase()
    : user?.email?.substring(0, 2).toUpperCase() || 'U';
  const userName = user?.fullName || user?.email?.split('@')[0] || '';
  const parentSection = getParentSection();
  const pageTitle = getPageTitle();
  const isPrimaryRoute = PRIMARY_ROUTES.includes(location.pathname);

  return (
    <div className="flex h-screen bg-navy-950 flex-col">
      <GlobalSearch open={searchOpen} onClose={() => setSearchOpen(false)} />

      {/* System Admin Banner */}
      {isSystemAdmin && (
        <div className="bg-gradient-to-r from-blue-600 to-blue-700 text-white px-6 py-2 flex items-center justify-between flex-shrink-0">
          <div className="flex items-center gap-2">
            <ShieldCheck className="w-5 h-5" />
            <span className="text-sm font-medium">Acceso de Administrador del Sistema</span>
          </div>
          <button
            onClick={() => navigate('/system-admin/dashboard')}
            className="px-3 py-1 bg-white/20 hover:bg-white/30 rounded-lg text-sm font-medium transition-colors"
          >
            Ir al Panel de Admin
          </button>
        </div>
      )}

      {/* ══════════════════════════════════════════
          TOP NAVBAR — tabs primarios + utility zone
      ══════════════════════════════════════════ */}
      <header className="h-12 sticky top-0 z-50 bg-navy-950/90 backdrop-blur-xl border-b border-white/[0.06] flex items-center flex-shrink-0">

        {/* Logo compacto */}
        <div className="flex items-center gap-1.5 pl-3 pr-2 flex-shrink-0">
          <PaglyLogo variant="icon" theme="dark" size="sm" />
          <span className="text-[13px] font-semibold text-white tracking-tight hidden sm:block">Pagly</span>
        </div>

        {/* Divisor vertical */}
        <div className="w-px h-4 bg-white/[0.08] flex-shrink-0" />

        {/* Spacer — empuja los tabs hacia la derecha */}
        <div className="flex-1" />

        {/* Tabs de navegación primaria */}
        <nav className="flex items-center gap-0.5 px-2 overflow-x-auto flex-shrink-0">
          {canAccessModuleCheck('dashboard') && (
            <NavLink to="/dashboard" className={topNavTabClass}>
              <LayoutDashboard className="w-4 h-4 flex-shrink-0" />
              <span>Dashboard</span>
            </NavLink>
          )}

          {canAccessModuleCheck('empleados') && (
            <NavLink to="/empleados" className={topNavTabClass}>
              <Users className="w-4 h-4 flex-shrink-0" />
              <span>Empleados</span>
            </NavLink>
          )}

          {canAccessModuleCheck('departamentos') && (
            <NavLink to="/departamentos" className={topNavTabClass}>
              <Building2 className="w-4 h-4 flex-shrink-0" />
              <span>Departamentos</span>
            </NavLink>
          )}

          {canAccessModuleCheck('posiciones') && (
            <NavLink to="/posiciones" className={topNavTabClass}>
              <Briefcase className="w-4 h-4 flex-shrink-0" />
              <span>Posiciones</span>
            </NavLink>
          )}
        </nav>

        {/* Utility zone — derecha */}
        <div className="flex items-center gap-1 pr-3 flex-shrink-0">

          {/* Search pill ⌘K */}
          <button
            onClick={() => setSearchOpen(true)}
            className="hidden md:flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-white/[0.04] border border-white/[0.06] text-gray-500 text-[11px] hover:bg-white/[0.06] hover:text-gray-300 hover:border-white/[0.10] transition-all duration-150"
          >
            <Search className="w-3 h-3 flex-shrink-0" />
            <span>Buscar...</span>
            <kbd className="text-[10px] text-gray-600 bg-white/[0.06] px-1.5 py-0.5 rounded">⌘K</kbd>
          </button>

          {/* Notificaciones */}
          <button className="relative p-1.5 rounded-lg text-gray-500 hover:text-gray-300 hover:bg-white/[0.06] transition-all duration-150">
            <Bell className="w-3.5 h-3.5" />
            <span className="absolute top-1 right-1 w-1 h-1 bg-primary-500 rounded-full" />
          </button>

          {/* Fecha */}
          <span className="text-[10px] text-gray-600 hidden md:block px-0.5">
            {new Date().toLocaleDateString('es-PA', { weekday: 'short', day: '2-digit', month: 'short' })}
          </span>

          {/* Divider */}
          <div className="w-px h-4 bg-white/[0.08]" />

          {/* Usuario — dropdown */}
          <div className="relative" ref={userDropdownRef}>
            <button
              onClick={() => setUserDropdownOpen(prev => !prev)}
              aria-haspopup="menu"
              aria-expanded={userDropdownOpen}
              aria-label="Menú de usuario"
              className="flex items-center gap-1.5 pl-0.5 pr-1 py-1 rounded-lg hover:bg-white/[0.06] transition-all duration-150"
            >
              <div className="w-6 h-6 rounded-md bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center ring-1 ring-white/10">
                {switchingTenant
                  ? <Loader2 className="w-3 h-3 text-white animate-spin" />
                  : <span className="text-white text-[9px] font-bold">{userInitials}</span>
                }
              </div>
              <div className="leading-none hidden lg:block">
                <p className="text-[11px] font-semibold text-gray-100">{userName}</p>
                <p className="text-[9px] text-gray-500 mt-0.5">{user?.roleName}</p>
              </div>
              <ChevronDown className={`w-2.5 h-2.5 text-gray-600 transition-transform duration-150 ${userDropdownOpen ? 'rotate-180' : ''}`} />
            </button>

            {userDropdownOpen && (
              <div role="menu" className="absolute right-0 top-full mt-1.5 w-56 bg-navy-900 border border-white/[0.08] rounded-xl shadow-2xl shadow-black/40 z-50 overflow-hidden">
                {/* Header */}
                <div className="px-3 py-2.5 border-b border-white/[0.06]">
                  <p className="text-[12px] font-semibold text-gray-100 truncate">{userName || user?.email}</p>
                  <p className="text-[10px] text-gray-500 truncate mt-0.5">{user?.email}</p>
                </div>

                {/* Tenant switcher — solo si tiene más de 1 tenant */}
                {availableTenants.length > 1 && (
                  <>
                    <div className="px-3 pt-2 pb-1">
                      <p className="text-[10px] font-semibold text-gray-500 uppercase tracking-wider">Cambiar empresa</p>
                    </div>
                    <div className="px-1.5 pb-1 flex flex-col gap-0.5">
                      {availableTenants.map((t) => {
                        const isActive = t.id === tenant?.id;
                        return (
                          <button
                            key={t.id}
                            role="menuitem"
                            onClick={() => !isActive && handleSwitchTenant(t.id)}
                            disabled={isActive || switchingTenant}
                            className={`w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-left transition-all duration-150 ${
                              isActive
                                ? 'bg-primary-500/15 text-primary-300 cursor-default'
                                : 'text-gray-300 hover:bg-white/[0.06] hover:text-gray-100'
                            }`}
                          >
                            <div className="w-5 h-5 rounded-md bg-gradient-to-br from-primary-600 to-accent-600 flex items-center justify-center flex-shrink-0">
                              <span className="text-white text-[8px] font-bold">
                                {t.name.substring(0, 2).toUpperCase()}
                              </span>
                            </div>
                            <div className="flex-1 min-w-0">
                              <p className="text-[11px] font-medium truncate">{t.name}</p>
                              <p className="text-[9px] text-gray-500 truncate">{t.roleName}</p>
                            </div>
                            {isActive && <Check className="w-3 h-3 text-primary-400 flex-shrink-0" />}
                          </button>
                        );
                      })}
                    </div>
                    {/* DEV-166: aviso solo visible cuando el switcher está visible */}
                    <div className="px-3 py-1.5">
                      <p className="text-[9px] text-gray-600 leading-tight">
                        ¿No ves todas tus empresas?{' '}
                        <button
                          onClick={() => { setUserDropdownOpen(false); handleLogout(); }}
                          className="text-gray-500 underline hover:text-gray-300"
                        >
                          Reinicia sesión
                        </button>
                      </p>
                    </div>
                    <div className="mx-3 border-t border-white/[0.06]" />
                  </>
                )}

                {/* Cerrar sesión */}
                <div className="p-1.5">
                  <button
                    role="menuitem"
                    onClick={() => { setUserDropdownOpen(false); handleLogout(); }}
                    className="w-full flex items-center gap-2 px-2.5 py-2 rounded-lg text-[12px] text-red-400 hover:bg-red-500/10 hover:text-red-300 transition-all duration-150"
                  >
                    <LogOut className="w-3.5 h-3.5 flex-shrink-0" />
                    Cerrar sesión
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* ══════════════════════════════════════════
          BODY: Sidebar + Main content
      ══════════════════════════════════════════ */}
      <div className="flex flex-1 overflow-hidden">

        {/* ══════════════════════════════════════════
            SIDEBAR — navegación secundaria/avanzada
        ══════════════════════════════════════════ */}
        <aside className="w-[220px] bg-gradient-to-b from-navy-950 via-navy-950 to-[#0a1929] border-r border-white/[0.06] flex flex-col flex-shrink-0">

          {/* Navegación principal */}
          <nav className="flex-1 px-2.5 py-2 space-y-0.5 overflow-y-auto">

            {/* Mi Perfil / Perfiles de Empleados */}
            <NavLink to="/mi-perfil" className={primaryItemClass}>
              {hasRole(TenantRole.Owner) ? (
                <Users2 className="w-4 h-4 flex-shrink-0" />
              ) : (
                <User className="w-4 h-4 flex-shrink-0" />
              )}
              <span>{hasRole(TenantRole.Owner) ? 'Perfiles de Empleados' : 'Mi Perfil'}</span>
            </NavLink>

            {/* Separador */}
            <div className="py-1">
              <div className="border-t border-white/[0.05]" />
            </div>

            {/* ── GRUPO: NOVEDADES ── */}
            {canAccessModuleCheck('anticipos') && (
              <div>
                <button
                  onClick={() => toggleGroup('novedades')}
                  className="w-full flex items-center justify-between px-3 py-2 rounded-md transition-all duration-150 hover:bg-white/[0.03]"
                >
                  <div className="flex items-center gap-2">
                    <Banknote className="w-4 h-4 text-rose-400 flex-shrink-0" />
                    <span className="text-[11px] font-semibold tracking-widest uppercase text-gray-500">
                      Novedades
                    </span>
                  </div>
                  <ChevronDown
                    className={`w-3.5 h-3.5 text-gray-600 transition-transform duration-200 ${
                      expandedGroups.has('novedades') ? 'rotate-180' : ''
                    }`}
                  />
                </button>

                {expandedGroups.has('novedades') && (
                  <div className="mt-0.5 px-1 flex flex-col gap-0.5">
                    <NavLink to="/anticipos" className={subItemClass}>
                      <DollarSign className="w-4 h-4 flex-shrink-0" />
                      Anticipos
                    </NavLink>
                    <NavLink to="/prestamos" className={subItemClass}>
                      <CreditCard className="w-4 h-4 flex-shrink-0" />
                      Préstamos
                    </NavLink>
                    <NavLink to="/deducciones" className={subItemClass}>
                      <Minus className="w-4 h-4 flex-shrink-0" />
                      Deducciones
                    </NavLink>
                    <NavLink to="/acreedores" className={subItemClass}>
                      <Building className="w-4 h-4 flex-shrink-0" />
                      Acreedores
                    </NavLink>
                  </div>
                )}
              </div>
            )}

            {/* ── GRUPO: ASISTENCIA ── */}
            {canAccessModuleCheck('horas-extra') && (
              <div>
                <button
                  onClick={() => toggleGroup('asistencia')}
                  className="w-full flex items-center justify-between px-3 py-2 rounded-md transition-all duration-150 hover:bg-white/[0.03]"
                >
                  <div className="flex items-center gap-2">
                    <Clock className="w-4 h-4 text-sky-400 flex-shrink-0" />
                    <span className="text-[11px] font-semibold tracking-widest uppercase text-gray-500">
                      Asistencia
                    </span>
                  </div>
                  <ChevronDown
                    className={`w-3.5 h-3.5 text-gray-600 transition-transform duration-200 ${
                      expandedGroups.has('asistencia') ? 'rotate-180' : ''
                    }`}
                  />
                </button>

                {expandedGroups.has('asistencia') && (
                  <div className="mt-0.5 px-1 flex flex-col gap-0.5">
                    <NavLink to="/horas-extra" className={subItemClass}>
                      <Clock className="w-4 h-4 flex-shrink-0" />
                      Horas Extra
                    </NavLink>
                    <NavLink to="/ausencias" className={subItemClass}>
                      <XCircle className="w-4 h-4 flex-shrink-0" />
                      Ausencias
                    </NavLink>
                    <NavLink to="/vacaciones" className={subItemClass}>
                      <TreePalm className="w-4 h-4 flex-shrink-0" />
                      Vacaciones
                    </NavLink>
                  </div>
                )}
              </div>
            )}

            {/* Separador antes de módulos prominentes */}
            <div className="py-1">
              <div className="border-t border-white/[0.05]" />
            </div>

            {/* Planillas */}
            {canAccessModuleCheck('planillas') && (
              <NavLink to="/planillas" className={prominentPlanillasClass}>
                <ClipboardList className="w-[19px] h-[19px] flex-shrink-0 text-blue-400" />
                <span>Planillas</span>
              </NavLink>
            )}

            {/* Décimo Tercer Mes */}
            {canAccessModuleCheck('planillas') && (
              <NavLink to="/decimo" className={({ isActive }) =>
                `flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-semibold text-[13px] transition-all duration-150 border-l-2 ${
                  isActive
                    ? 'bg-white/[0.08] border-amber-400 text-white'
                    : 'border-transparent text-gray-400 hover:bg-white/[0.04] hover:text-gray-100 hover:border-amber-500/40'
                }`}
              >
                <Banknote className="w-[19px] h-[19px] flex-shrink-0 text-amber-400" />
                <span>Décimo 13°</span>
              </NavLink>
            )}

            {/* Liquidaciones */}
            {canAccessModuleCheck('planillas') && (
              <NavLink to="/liquidaciones" className={({ isActive }) =>
                `flex items-center gap-2.5 px-2.5 py-2 rounded-lg font-semibold text-[13px] transition-all duration-150 border-l-2 ${
                  isActive
                    ? 'bg-white/[0.08] border-red-400 text-white'
                    : 'border-transparent text-gray-400 hover:bg-white/[0.04] hover:text-gray-100 hover:border-red-500/40'
                }`}
              >
                <FileText className="w-[19px] h-[19px] flex-shrink-0 text-red-400" />
                <span>Liquidaciones</span>
              </NavLink>
            )}

            {/* Reportes */}
            {canAccessModuleCheck('reportes') && (
              <NavLink to="/reportes" className={prominentReportesClass}>
                <BarChart3 className="w-[19px] h-[19px] flex-shrink-0 text-violet-400" />
                <span>Reportes</span>
              </NavLink>
            )}

            {/* Auditoría para no-Owner con acceso */}
            {!hasRole(TenantRole.Owner) && canAccessModuleCheck('audit') && (
              <NavLink to="/audit" className={primaryItemClass}>
                <FileText className="w-[19px] h-[19px] flex-shrink-0" />
                <span>Auditoría</span>
              </NavLink>
            )}

            {/* Configuración para no-Owner con acceso */}
            {!hasRole(TenantRole.Owner) && canAccessModuleCheck('configuracion') && (
              <NavLink to="/configuracion" className={primaryItemClass}>
                <Settings className="w-[19px] h-[19px] flex-shrink-0" />
                <span>Configuración</span>
              </NavLink>
            )}

          </nav>

          {/* ── FOOTER: Admin items + Tenant card ── */}
          <div className="px-2.5 pt-1.5 pb-2 border-t border-white/[0.05]">

            {/* Administración (solo Owner) */}
            {hasRole(TenantRole.Owner) && (
              <div className="mb-2 flex flex-col gap-0.5">
                <NavLink to="/roles" className={adminItemClass}>
                  <ShieldCheck className="w-4 h-4 flex-shrink-0 text-violet-400" />
                  Roles
                </NavLink>

                <NavLink to="/settings/api-keys" className={adminItemClass}>
                  <Key className="w-4 h-4 flex-shrink-0 text-violet-400" />
                  API Keys
                </NavLink>

                {canAccessModuleCheck('audit') && (
                  <NavLink to="/audit" className={adminItemClass}>
                    <FileText className="w-4 h-4 flex-shrink-0 text-violet-400" />
                    Auditoría
                  </NavLink>
                )}

                {canAccessModuleCheck('configuracion') && (
                  <NavLink to="/configuracion" className={adminItemClass}>
                    <Settings className="w-4 h-4 flex-shrink-0 text-violet-400" />
                    Configuración
                  </NavLink>
                )}

                <div className="mt-1 border-t border-white/[0.05]" />
              </div>
            )}

            {/* Tenant Card */}
            <div className="p-1.5 rounded-lg bg-white/[0.04] border border-white/[0.06] flex items-center gap-2 group hover:bg-white/[0.06] transition-all duration-150">
              <div className="w-6 h-6 rounded-md bg-gradient-to-br from-primary-600 to-accent-600 flex items-center justify-center flex-shrink-0">
                <span className="text-white text-[9px] font-bold">{tenantInitials}</span>
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-[10px] font-semibold text-gray-200 truncate">{tenant?.name}</p>
                <p className="text-[9px] text-gray-600 truncate">RUC {tenant?.ruc}-{tenant?.dv}</p>
              </div>
              <button
                onClick={handleLogout}
                className="p-1 rounded text-gray-600 hover:text-red-400 hover:bg-red-500/10 opacity-0 group-hover:opacity-100 transition-all duration-150"
                title="Cerrar sesión"
              >
                <LogOut className="w-3 h-3" />
              </button>
            </div>
          </div>
        </aside>

        {/* ══════════════════════════════════════════
            MAIN CONTENT
        ══════════════════════════════════════════ */}
        <div className="flex-1 flex flex-col overflow-hidden">

          {/* Breadcrumb contextual — solo en rutas secundarias */}
          {!isPrimaryRoute && (
            <div className="h-8 px-4 flex items-center border-b border-white/[0.04] flex-shrink-0">
              {parentSection && (
                <>
                  <span className="text-xs text-gray-500">{parentSection}</span>
                  <ChevronRight className="w-3 h-3 text-gray-600 mx-1" />
                </>
              )}
              <span className="text-xs font-medium text-gray-300">{pageTitle}</span>
            </div>
          )}

          {/* Page Content */}
          <main className="flex-1 overflow-y-auto bg-navy-950 p-6">{children}</main>
        </div>
      </div>
    </div>
  );
}
