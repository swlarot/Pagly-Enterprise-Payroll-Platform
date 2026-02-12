import React, { ReactNode, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { TenantRole } from '../../types/api';
import { canAccessModule } from '../../services/permissionService';
import { PaglyLogo } from '../ui/PaglyLogo';

interface AuthLayoutProps {
  children: ReactNode;
}

export default function AuthLayout({ children }: AuthLayoutProps) {
  const { user, tenant, logout, hasRole, isSystemAdmin, canWrite, permissions } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  // Ambos grupos expandidos por defecto — cada uno se puede retraer independientemente
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

    // Owner (0) tiene acceso a todo
    if (role === TenantRole.Owner) {
      return true;
    }

    // User (1) necesita permisos personalizados asignados mediante CustomTenantRole
    if (permissions && permissions.length > 0) {
      return canAccessModule(module, permissions);
    }

    // User sin permisos personalizados asignados: sin acceso
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
      '/anticipos': 'Gestión de Anticipos',
      '/horas-extra': 'Gestión de Horas Extra',
      '/ausencias': 'Gestión de Ausencias',
      '/vacaciones': 'Gestión de Vacaciones',
      '/planillas': 'Gestión de Planillas',
      '/reportes': 'Reportes de Planilla',
      '/configuracion': 'Configuración del Sistema',
      '/roles': 'Roles y Permisos',
      '/audit': 'Registro de Auditoría',
    };
    return routes[location.pathname] || 'Dashboard';
  };

  // Clases para los ítems primarios (Dashboard, Mi Perfil)
  const primaryItemClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 px-4 py-2.5 rounded-xl font-semibold text-[14px] transition-all duration-200 ${
      isActive
        ? 'border-l-2 border-primary-500 bg-primary-500/10 text-primary-400 shadow-sm shadow-primary-500/10'
        : 'text-gray-300 hover:bg-navy-800/60 hover:text-gray-100'
    }`;

  // Bloques prominentes — dark glass, línea izquierda de acento al estar activo
  const prominentPlanillasClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 px-4 py-3.5 rounded-xl font-bold text-[14.5px] transition-all duration-200 border border-l-[3px] ${
      isActive
        ? 'bg-navy-800/80 border-navy-700/60 border-l-blue-400 text-white shadow-sm'
        : 'bg-navy-800/30 border-navy-700/40 border-l-transparent text-gray-400 hover:bg-navy-800/60 hover:text-gray-100 hover:border-l-blue-500/40'
    }`;

  const prominentReportesClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 px-4 py-3.5 rounded-xl font-bold text-[14.5px] transition-all duration-200 border border-l-[3px] ${
      isActive
        ? 'bg-navy-800/80 border-navy-700/60 border-l-violet-400 text-white shadow-sm'
        : 'bg-navy-800/30 border-navy-700/40 border-l-transparent text-gray-400 hover:bg-navy-800/60 hover:text-gray-100 hover:border-l-violet-500/40'
    }`;

  // Header nav tabs: texto blanco, línea abajo en hover, fondo verde en activo
  const headerTabClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2 px-4 text-[13.5px] font-semibold transition-all duration-150 select-none whitespace-nowrap border-b-2 ${
      isActive
        ? 'bg-emerald-500/15 text-emerald-300 border-emerald-400'
        : 'text-gray-200 border-transparent hover:text-white hover:border-gray-500'
    }`;

  // Ítems verticales — sin fondo, sin barra
  const novedadesTileClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2.5 w-full px-3 py-2 rounded-lg transition-all duration-150 cursor-pointer text-[13px] font-medium ${
      isActive
        ? 'text-white'
        : 'text-gray-400 hover:text-gray-100'
    }`;

  const asistenciaTileClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2.5 w-full px-3 py-2 rounded-lg transition-all duration-150 cursor-pointer text-[13px] font-medium ${
      isActive
        ? 'text-white'
        : 'text-gray-400 hover:text-gray-100'
    }`;

  // Admin footer tabs — mismo estilo dark glass + border-l que el resto del sidebar
  const adminFooterTabClass = (isActive: boolean) =>
    `flex items-center gap-1.5 w-full px-3 py-2 rounded-lg transition-all duration-150 cursor-pointer border border-l-[3px] text-[12px] font-medium ${
      isActive
        ? 'bg-navy-800/80 border-navy-700/60 border-l-violet-400 text-white'
        : 'bg-navy-800/20 border-navy-700/30 border-l-transparent text-gray-400 hover:bg-navy-800/50 hover:text-gray-100 hover:border-l-violet-500/40'
    }`;

  return (
    <div className="flex h-screen bg-navy-950">
      {/* Sidebar */}
      <aside className="w-72 bg-navy-950 border-r border-navy-700 shadow-2xl flex flex-col">

        {/* Logo */}
        <div className="px-5 py-4 border-b border-navy-700">
          <div className="flex items-center gap-3">
            <PaglyLogo variant="full" theme="dark" size="md" />
          </div>
          <span className="text-[11px] text-gray-500 font-body mt-0.5 block">Planilla Inteligente</span>
        </div>

        {/* Navigation principal */}
        <nav className="flex-1 px-3 py-3 space-y-1 overflow-y-auto">

          {/* Mi Perfil */}
          <NavLink to="/mi-perfil" className={primaryItemClass}>
            <svg className="w-5 h-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
            <span>Mi Perfil</span>
          </NavLink>

          {/* Separador */}
          <div className="py-1.5 px-1">
            <div className="h-px bg-gradient-to-r from-navy-700 to-transparent" />
          </div>

          {/* ── GRUPO: NOVEDADES ── */}
          {canAccessModuleCheck('anticipos') && (
            <div>
              <button
                onClick={() => toggleGroup('novedades')}
                className="w-full flex items-center justify-between px-3 py-2 rounded-xl transition-all duration-200 bg-navy-800/30 border border-navy-700/40 border-l-[3px] border-l-rose-400 hover:bg-navy-800/60 hover:text-gray-100"
              >
                <div className="flex items-center gap-2">
                  <svg className="w-3.5 h-3.5 text-rose-400 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span className="text-[10.5px] font-bold tracking-widest uppercase text-gray-400">
                    Novedades
                  </span>
                </div>
                <svg
                  className={`w-3 h-3 text-gray-600 transition-transform duration-200 ${expandedGroups.has('novedades') ? 'rotate-180' : ''}`}
                  fill="none" viewBox="0 0 24 24" stroke="currentColor"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {expandedGroups.has('novedades') && (
                <div className="mt-1 px-1 flex flex-col gap-0.5">
                  <NavLink to="/anticipos" className={novedadesTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                    </svg>
                    Anticipos
                  </NavLink>
                  <NavLink to="/prestamos" className={novedadesTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                    </svg>
                    Préstamos
                  </NavLink>
                  <NavLink to="/deducciones" className={novedadesTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    Deducciones
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
                className="w-full flex items-center justify-between px-3 py-2 rounded-xl transition-all duration-200 bg-navy-800/30 border border-navy-700/40 border-l-[3px] border-l-sky-400 hover:bg-navy-800/60 hover:text-gray-100"
              >
                <div className="flex items-center gap-2">
                  <svg className="w-3.5 h-3.5 text-sky-400 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span className="text-[10.5px] font-bold tracking-widest uppercase text-gray-400">
                    Asistencia
                  </span>
                </div>
                <svg
                  className={`w-3 h-3 text-gray-600 transition-transform duration-200 ${expandedGroups.has('asistencia') ? 'rotate-180' : ''}`}
                  fill="none" viewBox="0 0 24 24" stroke="currentColor"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {expandedGroups.has('asistencia') && (
                <div className="mt-1 px-1 flex flex-col gap-0.5">
                  <NavLink to="/horas-extra" className={asistenciaTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    Horas Extra
                  </NavLink>
                  <NavLink to="/ausencias" className={asistenciaTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M6 18L18 6M6 6l12 12" />
                    </svg>
                    Ausencias
                  </NavLink>
                  <NavLink to="/vacaciones" className={asistenciaTileClass}>
                    <svg className="w-3.5 h-3.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" />
                    </svg>
                    Vacaciones
                  </NavLink>
                </div>
              )}
            </div>
          )}

          {/* Separador antes de módulos prominentes */}
          <div className="py-1.5 px-1">
            <div className="h-px bg-gradient-to-r from-navy-700 to-transparent" />
          </div>

          {/* ── PLANILLAS ── */}
          {canAccessModuleCheck('planillas') && (
            <NavLink to="/planillas" className={prominentPlanillasClass}>
              <svg className="w-5 h-5 flex-shrink-0 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
              </svg>
              <span>Planillas</span>
            </NavLink>
          )}

          {/* ── REPORTES ── */}
          {canAccessModuleCheck('reportes') && (
            <NavLink to="/reportes" className={prominentReportesClass}>
              <svg className="w-5 h-5 flex-shrink-0 text-violet-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              <span>Reportes</span>
            </NavLink>
          )}

          {/* Auditoría para no-Owner que tiene acceso (dentro del nav, antes del footer) */}
          {!hasRole(TenantRole.Owner) && canAccessModuleCheck('audit') && (
            <NavLink to="/audit" className={primaryItemClass}>
              <svg className="w-5 h-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              <span>Auditoría</span>
            </NavLink>
          )}

          {/* Configuración para no-Owner que tiene acceso */}
          {!hasRole(TenantRole.Owner) && canAccessModuleCheck('configuracion') && (
            <NavLink to="/configuracion" className={primaryItemClass}>
              <svg className="w-5 h-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
              <span>Configuración</span>
            </NavLink>
          )}

        </nav>

        {/* ── FOOTER: Admin tabs + Tenant info ── */}
        <div className="px-3 pt-2 pb-3 border-t border-navy-700">

          {/* Administración (solo Owner) — lista vertical igual que Novedades/Asistencia */}
          {hasRole(TenantRole.Owner) && (
            <div className="mb-3">
              <div className="flex flex-col gap-0.5">
                <NavLink to="/roles" className={({ isActive }) => adminFooterTabClass(isActive)}>
                  <svg className="w-3.5 h-3.5 shrink-0 text-violet-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                  </svg>
                  Roles
                </NavLink>

                {canAccessModuleCheck('audit') && (
                  <NavLink to="/audit" className={({ isActive }) => adminFooterTabClass(isActive)}>
                    <svg className="w-3.5 h-3.5 shrink-0 text-violet-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    Auditoría
                  </NavLink>
                )}

                {canAccessModuleCheck('configuracion') && (
                  <NavLink to="/configuracion" className={({ isActive }) => adminFooterTabClass(isActive)}>
                    <svg className="w-3.5 h-3.5 shrink-0 text-violet-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                        d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                    Configuración
                  </NavLink>
                )}
              </div>
            </div>
          )}

          {/* Tenant info */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2 min-w-0">
              <div className="w-7 h-7 rounded-full bg-gradient-to-br from-primary-600 to-primary-500 flex items-center justify-center flex-shrink-0">
                <span className="text-white text-[10px] font-bold font-display">
                  {tenant?.name?.substring(0, 2).toUpperCase() || 'PL'}
                </span>
              </div>
              <div className="min-w-0">
                <p className="text-xs font-semibold text-gray-200 truncate max-w-[140px]">{tenant?.name}</p>
                <p className="text-[10px] text-gray-600 truncate">{tenant?.ruc}-{tenant?.dv}</p>
              </div>
            </div>
            <button
              onClick={handleLogout}
              className="p-1.5 text-gray-600 hover:text-red-400 hover:bg-navy-800 rounded-lg transition flex-shrink-0"
              title="Cerrar sesión"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
              </svg>
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* System Admin Banner */}
        {isSystemAdmin && (
          <div className="bg-gradient-to-r from-blue-600 to-blue-700 text-white px-6 py-2 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
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

        {/* Header — tabs full-height, sin breadcrumb */}
        <header className="h-[60px] flex items-stretch justify-between border-b border-navy-700/80 bg-navy-900">

          {/* Izquierda: tabs de navegación (full height para que border-b quede al fondo) */}
          <nav className="flex items-stretch pl-2">

            {/* Dashboard */}
            {canAccessModuleCheck('dashboard') && (
              <NavLink to="/dashboard" className={headerTabClass}>
                <svg className="w-[15px] h-[15px] shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                </svg>
                Dashboard
              </NavLink>
            )}

            {/* Divisor sutil entre Dashboard y Organización */}
            {canAccessModuleCheck('dashboard') && canAccessModuleCheck('empleados') && (
              <div className="flex items-center px-1">
                <div className="w-px h-4 bg-navy-700" />
              </div>
            )}

            {/* Empleados */}
            {canAccessModuleCheck('empleados') && (
              <NavLink to="/empleados" className={headerTabClass}>
                <svg className="w-[15px] h-[15px] shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                    d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
                Empleados
              </NavLink>
            )}

            {/* Departamentos */}
            {canAccessModuleCheck('departamentos') && (
              <NavLink to="/departamentos" className={headerTabClass}>
                <svg className="w-[15px] h-[15px] shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                    d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                </svg>
                Departamentos
              </NavLink>
            )}

            {/* Posiciones */}
            {canAccessModuleCheck('posiciones') && (
              <NavLink to="/posiciones" className={headerTabClass}>
                <svg className="w-[15px] h-[15px] shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                    d="M21 13.255A23.931 23.931 0 0 1 12 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2m4 6h.01M5 20h14a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2z" />
                </svg>
                Posiciones
              </NavLink>
            )}
          </nav>

          {/* Derecha: fecha + usuario */}
          <div className="flex items-center gap-3 pr-5">

            {/* Fecha */}
            <span className="text-[11.5px] text-gray-600 font-medium hidden md:block">
              {new Date().toLocaleDateString('es-PA', { weekday: 'short', day: '2-digit', month: 'short' })}
            </span>

            <div className="w-px h-5 bg-navy-700" />

            {/* Usuario */}
            <div className="flex items-center gap-2.5">
              <div className="relative">
                <div className="w-8 h-8 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-full flex items-center justify-center ring-2 ring-navy-800">
                  <span className="text-white text-[11px] font-bold tracking-tight">
                    {user?.fullName
                      ? user.fullName.split(' ').map((n: string) => n[0]).slice(0, 2).join('').toUpperCase()
                      : user?.email.substring(0, 2).toUpperCase()}
                  </span>
                </div>
                <span className="absolute bottom-0 right-0 w-2 h-2 bg-emerald-400 rounded-full ring-1 ring-navy-900" />
              </div>
              <div className="leading-tight hidden lg:block">
                <p className="text-[12px] font-semibold text-gray-100 leading-none">
                  {user?.fullName || user?.email.split('@')[0]}
                </p>
                <p className="text-[10px] text-gray-500 mt-0.5">{user?.roleName}</p>
              </div>
              <button
                onClick={handleLogout}
                className="p-1.5 text-gray-600 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-all"
                title="Cerrar sesión"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>
              </button>
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 overflow-y-auto bg-navy-950 p-6">{children}</main>
      </div>
    </div>
  );
}
