import React, { useState } from 'react';
import { Routes, Route, NavLink, useLocation } from 'react-router-dom';
import { ToastProvider } from './components/ToastContext';
import DashboardPage from './pages/DashboardPage';
import EmpleadosPage from './pages/EmpleadosPage';
import DepartamentosPage from './pages/DepartamentosPage';
import PosicionesPage from './pages/PosicionesPage';
import PrestamosPage from './pages/PrestamosPage';
import DeduccionesPage from './pages/DeduccionesPage';
import AnticiposPage from './pages/AnticiposPage';
import HorasExtraPage from './pages/HorasExtraPage';
import AusenciasPage from './pages/AusenciasPage';
import VacacionesPage from './pages/VacacionesPage';
import PlanillasPage from './pages/PlanillasPage';
import ConfiguracionPage from './pages/ConfiguracionPage';
import ReportesPage from './pages/ReportesPage';

const App = () => {
    const location = useLocation();
    const [orgMenuOpen, setOrgMenuOpen] = useState(true);
    const [conceptosMenuOpen, setConceptosMenuOpen] = useState(true);
    const [asistenciaMenuOpen, setAsistenciaMenuOpen] = useState(true);

    const getPageTitle = () => {
        const routes = {
            '/': 'Dashboard',
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
            '/configuracion': 'Configuración del Sistema'
        };
        return routes[location.pathname] || 'Dashboard';
    };

    const getBreadcrumb = () => {
        const breadcrumbs = {
            '/': 'Inicio',
            '/empleados': 'Inicio / Organización / Empleados',
            '/departamentos': 'Inicio / Organización / Departamentos',
            '/posiciones': 'Inicio / Organización / Posiciones',
            '/prestamos': 'Inicio / Conceptos / Préstamos',
            '/deducciones': 'Inicio / Conceptos / Deducciones',
            '/anticipos': 'Inicio / Conceptos / Anticipos',
            '/horas-extra': 'Inicio / Asistencia / Horas Extra',
            '/ausencias': 'Inicio / Asistencia / Ausencias',
            '/vacaciones': 'Inicio / Asistencia / Vacaciones',
            '/planillas': 'Inicio / Planillas',
            '/reportes': 'Inicio / Reportes',
            '/configuracion': 'Inicio / Configuración'
        };
        return breadcrumbs[location.pathname] || 'Inicio';
    };

    const isOrgRouteActive = ['/empleados', '/departamentos', '/posiciones'].includes(location.pathname);
    const isConceptosRouteActive = ['/prestamos', '/deducciones', '/anticipos'].includes(location.pathname);
    const isAsistenciaRouteActive = ['/horas-extra', '/ausencias', '/vacaciones'].includes(location.pathname);

    return (
        <ToastProvider>
        <div className="flex h-screen bg-gray-50">
            {/* Sidebar */}
            <aside className="w-64 bg-gradient-to-b from-slate-900 to-slate-800 shadow-2xl flex flex-col">
                {/* Logo */}
                <div className="p-6 border-b border-slate-700">
                    <div className="flex items-center gap-3">
                        <div className="w-12 h-12 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg">
                            <svg className="w-7 h-7 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                            </svg>
                        </div>
                        <div>
                            <h1 className="text-2xl font-bold text-white">Planilla</h1>
                            <p className="text-xs text-slate-400">Sistema de Nómina</p>
                        </div>
                    </div>
                </div>

                {/* Navigation */}
                <nav className="flex-1 p-4 space-y-2 overflow-y-auto">
                    <NavLink
                        to="/"
                        end
                        className={({ isActive }) =>
                            `flex items-center gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`
                        }
                    >
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                        </svg>
                        <span>Dashboard</span>
                    </NavLink>

                    {/* Organización Submenu */}
                    <div className="space-y-1">
                        <button
                            onClick={() => setOrgMenuOpen(!orgMenuOpen)}
                            className={`w-full flex items-center justify-between gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isOrgRouteActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`}
                        >
                            <div className="flex items-center gap-3">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                                </svg>
                                <span>Organización</span>
                            </div>
                            <svg
                                className={`w-5 h-5 transition-transform duration-200 ${orgMenuOpen ? 'rotate-180' : ''}`}
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke="currentColor"
                            >
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                            </svg>
                        </button>

                        {/* Submenu Items */}
                        {orgMenuOpen && (
                            <div className="ml-4 space-y-1 border-l-2 border-slate-700 pl-2">
                                <NavLink
                                    to="/empleados"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                                    </svg>
                                    <span>Empleados</span>
                                </NavLink>

                                <NavLink
                                    to="/departamentos"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                                    </svg>
                                    <span>Departamentos</span>
                                </NavLink>

                                <NavLink
                                    to="/posiciones"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 13.255A23.931 23.931 0 0112 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v2m4 6h.01M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                                    </svg>
                                    <span>Posiciones</span>
                                </NavLink>
                            </div>
                        )}
                    </div>

                    {/* Conceptos Submenu */}
                    <div className="space-y-1">
                        <button
                            onClick={() => setConceptosMenuOpen(!conceptosMenuOpen)}
                            className={`w-full flex items-center justify-between gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isConceptosRouteActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`}
                        >
                            <div className="flex items-center gap-3">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                                </svg>
                                <span>Conceptos</span>
                            </div>
                            <svg
                                className={`w-5 h-5 transition-transform duration-200 ${conceptosMenuOpen ? 'rotate-180' : ''}`}
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke="currentColor"
                            >
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                            </svg>
                        </button>

                        {/* Submenu Items */}
                        {conceptosMenuOpen && (
                            <div className="ml-4 space-y-1 border-l-2 border-slate-700 pl-2">
                                <NavLink
                                    to="/prestamos"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                                    </svg>
                                    <span>Préstamos</span>
                                </NavLink>

                                <NavLink
                                    to="/deducciones"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span>Deducciones</span>
                                </NavLink>

                                <NavLink
                                    to="/anticipos"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span>Anticipos</span>
                                </NavLink>
                            </div>
                        )}
                    </div>

                    {/* Asistencia Submenu */}
                    <div className="space-y-1">
                        <button
                            onClick={() => setAsistenciaMenuOpen(!asistenciaMenuOpen)}
                            className={`w-full flex items-center justify-between gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isAsistenciaRouteActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`}
                        >
                            <div className="flex items-center gap-3">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                <span>Asistencia</span>
                            </div>
                            <svg
                                className={`w-5 h-5 transition-transform duration-200 ${asistenciaMenuOpen ? 'rotate-180' : ''}`}
                                fill="none"
                                viewBox="0 0 24 24"
                                stroke="currentColor"
                            >
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                            </svg>
                        </button>

                        {/* Submenu Items */}
                        {asistenciaMenuOpen && (
                            <div className="ml-4 space-y-1 border-l-2 border-slate-700 pl-2">
                                <NavLink
                                    to="/horas-extra"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span>Horas Extra</span>
                                </NavLink>

                                <NavLink
                                    to="/ausencias"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                    </svg>
                                    <span>Ausencias</span>
                                </NavLink>

                                <NavLink
                                    to="/vacaciones"
                                    className={({ isActive }) =>
                                        `flex items-center gap-3 px-4 py-2 rounded-lg font-medium transition-all duration-200 text-sm ${
                                            isActive
                                                ? 'bg-slate-700 text-white'
                                                : 'text-slate-400 hover:bg-slate-700/50 hover:text-white'
                                        }`
                                    }
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span>Vacaciones</span>
                                </NavLink>
                            </div>
                        )}
                    </div>

                    <NavLink
                        to="/planillas"
                        className={({ isActive }) =>
                            `flex items-center gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`
                        }
                    >
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
                        </svg>
                        <span>Planillas</span>
                    </NavLink>

                    <NavLink
                        to="/reportes"
                        className={({ isActive }) =>
                            `flex items-center gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`
                        }
                    >
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                        <span>Reportes</span>
                    </NavLink>

                    <NavLink
                        to="/configuracion"
                        className={({ isActive }) =>
                            `flex items-center gap-3 px-4 py-3 rounded-lg font-medium transition-all duration-200 ${
                                isActive
                                    ? 'bg-gradient-to-r from-blue-600 to-blue-500 text-white shadow-lg shadow-blue-500/50'
                                    : 'text-slate-300 hover:bg-slate-700/50 hover:text-white'
                            }`
                        }
                    >
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                        <span>Configuración</span>
                    </NavLink>
                </nav>

                {/* Footer */}
                <div className="p-4 border-t border-slate-700">
                    <div className="text-center text-xs text-slate-400">
                        <p className="font-semibold mb-1">v1.0.0</p>
                        <p>© {new Date().getFullYear()} Planilla</p>
                    </div>
                </div>
            </aside>

            {/* Main Content */}
            <div className="flex-1 flex flex-col overflow-hidden">
                {/* Header */}
                <header className="h-16 bg-white shadow-sm border-b border-gray-200 flex items-center justify-between px-8">
                    <div>
                        <h2 className="text-xl font-bold text-gray-900">{getPageTitle()}</h2>
                        <p className="text-xs text-gray-500">{getBreadcrumb()}</p>
                    </div>
                    <div className="flex items-center gap-4">
                        {/* Fecha actual */}
                        <div className="flex items-center gap-2 px-3 py-2 bg-gray-100 rounded-lg">
                            <svg className="w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                            <span className="text-sm font-medium text-gray-700">
                                {new Date().toLocaleDateString('es-PA', {
                                    day: '2-digit',
                                    month: 'short',
                                    year: 'numeric'
                                })}
                            </span>
                        </div>

                        {/* Notificaciones */}
                        <button className="relative p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition">
                            <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                            </svg>
                            <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
                        </button>

                        {/* Usuario */}
                        <div className="flex items-center gap-3 pl-4 border-l border-gray-200">
                            <div className="text-right">
                                <p className="text-sm font-medium text-gray-900">Administrador</p>
                                <p className="text-xs text-gray-500">Sistema</p>
                            </div>
                            <div className="w-10 h-10 bg-gradient-to-br from-blue-600 to-blue-500 rounded-full flex items-center justify-center shadow-md">
                                <span className="text-white text-sm font-bold">AD</span>
                            </div>
                        </div>
                    </div>
                </header>

                {/* Page Content */}
                <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
                    <Routes>
                        <Route path="/" element={<DashboardPage />} />
                        <Route path="/empleados" element={<EmpleadosPage />} />
                        <Route path="/departamentos" element={<DepartamentosPage />} />
                        <Route path="/posiciones" element={<PosicionesPage />} />
                        <Route path="/prestamos" element={<PrestamosPage />} />
                        <Route path="/deducciones" element={<DeduccionesPage />} />
                        <Route path="/anticipos" element={<AnticiposPage />} />
                        <Route path="/horas-extra" element={<HorasExtraPage />} />
                        <Route path="/ausencias" element={<AusenciasPage />} />
                        <Route path="/vacaciones" element={<VacacionesPage />} />
                        <Route path="/planillas" element={<PlanillasPage />} />
                        <Route path="/reportes" element={<ReportesPage />} />
                        <Route path="/configuracion" element={<ConfiguracionPage />} />
                    </Routes>
                </main>
            </div>
        </div>
        </ToastProvider>
    );
};

export default App;
