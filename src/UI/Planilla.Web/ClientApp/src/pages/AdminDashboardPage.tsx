import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

interface DashboardStats {
  totalEmpleados: number;
  empleadosActivos: number;
  ultimaPlanilla: any | null;
  aportesCss: number;
  pendientes: number;
}

export default function AdminDashboardPage() {
  const { user, tenant, isLoading: authLoading } = useAuth();
  const [stats, setStats] = useState<DashboardStats>({
    totalEmpleados: 0,
    empleadosActivos: 0,
    ultimaPlanilla: null,
    aportesCss: 0,
    pendientes: 0,
  });
  const [isLoading, setIsLoading] = useState(true);
  const hasLoadedRef = React.useRef(false); // Ref para evitar cargar múltiples veces (no bloquea primera carga)

  const loadDashboardData = React.useCallback(async () => {
    // Evitar cargar si ya se cargó (solo hasLoaded; no comprobar isLoading para permitir la primera carga)
    if (hasLoadedRef.current) {
      return;
    }
    hasLoadedRef.current = true;

    try {
      setIsLoading(true);
      console.log('[AdminDashboardPage] Starting to load dashboard data...');

      // Cargar empleados
      console.log('[AdminDashboardPage] Fetching empleados...');
      const empleadosRes = await api.get('/api/empleados');
      // La respuesta puede ser un array directo o un objeto con propiedad data
      const empleados = Array.isArray(empleadosRes)
        ? empleadosRes
        : Array.isArray(empleadosRes?.data)
          ? empleadosRes.data
          : [];
      const activos = empleados.filter((e: any) => e.estaActivo).length;

      // Cargar planillas
      const planillasRes = await api.get('/api/payrollheaders');
      // La respuesta puede ser un array directo o un objeto con propiedad data
      const planillas = Array.isArray(planillasRes)
        ? planillasRes
        : Array.isArray(planillasRes?.data)
          ? planillasRes.data
          : [];

      // Última planilla (la más reciente)
      const ultimaPlanilla = planillas.length > 0 ? planillas[0] : null;

      // Planillas pendientes (Draft = 0)
      const pendientes = planillas.filter((p: any) => p.status === 0).length;

      setStats({
        totalEmpleados: empleados.length,
        empleadosActivos: activos,
        ultimaPlanilla: ultimaPlanilla,
        aportesCss: ultimaPlanilla ? ultimaPlanilla.totalEmployerCost : 0,
        pendientes: pendientes,
      });

      console.log('[AdminDashboardPage] Dashboard data loaded successfully:', {
        totalEmpleados: empleados.length,
        empleadosActivos: activos,
        planillas: planillas.length,
        pendientes: pendientes
      });
    } catch (error: any) {
      console.error('[AdminDashboardPage] Error loading dashboard data:', error);
      toast.error(error.message || 'Error al cargar datos del dashboard');
      hasLoadedRef.current = false; // Permitir reintento en caso de error
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    // Si aún está cargando el contexto, esperar
    if (authLoading) {
      return;
    }

    // Si el tenant está disponible, cargar datos (loadDashboardData evita duplicados con ref)
    if (tenant) {
      console.log('[AdminDashboardPage] Tenant available, loading dashboard data for:', tenant.name);
      loadDashboardData();
      return;
    }

    // Si no hay tenant, esperar un poco antes de mostrar estado "sin tenant"
    const timeoutId = setTimeout(() => {
      setIsLoading(false);
    }, 1000);
    return () => clearTimeout(timeoutId);
  }, [authLoading, tenant, loadDashboardData]);


  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('es-PA', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
    }).format(amount || 0);
  };

  const getStatusBadge = (status: number) => {
    const badges: Record<number, { text: string; color: string }> = {
      0: { text: 'BORRADOR', color: 'bg-yellow-500/15 text-yellow-400' },
      1: { text: 'CALCULADO', color: 'bg-primary-500/15 text-primary-400' },
      2: { text: 'APROBADO', color: 'bg-green-500/15 text-green-400' },
      3: { text: 'PAGADO', color: 'bg-emerald-500/15 text-emerald-400' },
      4: { text: 'CANCELADO', color: 'bg-red-500/15 text-red-400' },
    };
    const badge = badges[status] || badges[0];
    return (
      <span className={`px-3 py-1 rounded-full text-xs font-semibold ${badge.color}`}>
        {badge.text}
      </span>
    );
  };

  // Mostrar loading mientras se carga el contexto o el tenant
  if (authLoading || isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-12 h-12 text-primary-400 animate-spin" />
        <p className="ml-4 text-gray-400">
          {authLoading ? 'Cargando información del usuario...' : 'Cargando datos del dashboard...'}
        </p>
      </div>
    );
  }

  // Si no hay tenant después de cargar, mostrar mensaje apropiado
  if (!tenant) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <p className="text-gray-400 mb-4">No se pudo cargar la información de la empresa.</p>
          <p className="text-sm text-gray-500">Por favor, intenta recargar la página o contacta al administrador.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-100">Dashboard</h1>
        <p className="text-gray-400 mt-2">
          Bienvenido, {user?.email} · {user?.roleName}
        </p>
      </div>

      {/* Tenant Info Card */}
      {tenant && (
        <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
          <h2 className="text-xl font-bold text-gray-100 mb-2">{tenant.name}</h2>
          <div className="space-y-1 text-sm text-gray-400">
            <p>
              <strong className="text-gray-300">RUC:</strong> {tenant.ruc}-{tenant.dv}
            </p>
            <p>
              <strong className="text-gray-300">Subdominio:</strong> {tenant.subdomain}.planilla.cloud
            </p>
          </div>
        </div>
      )}

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {/* Empleados Activos */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-6 border border-navy-700 hover:border-primary-500/50 transition-all">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-400">Empleados Activos</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">{stats.empleadosActivos}</p>
              <p className="text-xs text-gray-500 mt-1">de {stats.totalEmpleados} totales</p>
            </div>
            <div className="w-16 h-16 bg-primary-500/15 rounded-full flex items-center justify-center">
              <svg className="w-8 h-8 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
          </div>
        </div>

        {/* Última Planilla */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-6 border border-navy-700 hover:border-primary-500/50 transition-all">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-400">Última Planilla</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">
                {stats.ultimaPlanilla
                  ? formatCurrency(stats.ultimaPlanilla.totalNetPay)
                  : 'Sin planillas'}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                {stats.ultimaPlanilla
                  ? new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')
                  : 'No hay datos'}
              </p>
            </div>
            <div className="w-16 h-16 bg-green-500/15 rounded-full flex items-center justify-center">
              <svg className="w-8 h-8 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
          </div>
        </div>

        {/* Aportes Patronales */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-6 border border-navy-700 hover:border-primary-500/50 transition-all">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-400">Aportes Patronales</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">{formatCurrency(stats.aportesCss)}</p>
              <p className="text-xs text-gray-500 mt-1">CSS + SE + Riesgo</p>
            </div>
            <div className="w-16 h-16 bg-amber-500/15 rounded-full flex items-center justify-center">
              <svg className="w-8 h-8 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
          </div>
        </div>

        {/* Planillas Pendientes */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-6 border border-navy-700 hover:border-primary-500/50 transition-all">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-400">Planillas Pendientes</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">{stats.pendientes}</p>
              <p className="text-xs text-gray-500 mt-1">
                {stats.pendientes > 0 ? 'Requieren cálculo' : 'Todo al día'}
              </p>
            </div>
            <div
              className={`w-16 h-16 rounded-full flex items-center justify-center ${
                stats.pendientes > 0 ? 'bg-red-500/15' : 'bg-navy-800'
              }`}
            >
              <svg
                className={`w-8 h-8 ${stats.pendientes > 0 ? 'text-red-400' : 'text-gray-500'}`}
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
          </div>
        </div>
      </div>

      {/* Resumen del Período */}
      {stats.ultimaPlanilla && (
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-6 border border-navy-700">
          <h3 className="text-lg font-semibold text-gray-100 mb-6">Resumen del Período</h3>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div className="text-center p-6 bg-primary-500/10 border border-primary-500/20 rounded-xl">
              <div className="flex justify-center mb-3">
                <div className="w-12 h-12 bg-primary-500/20 rounded-full flex items-center justify-center">
                  <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
              </div>
              <p className="text-xs font-medium text-primary-400 uppercase mb-2">Total Salarios Brutos</p>
              <p className="text-2xl font-bold text-gray-100">
                {formatCurrency(stats.ultimaPlanilla.totalGrossPay)}
              </p>
            </div>

            <div className="text-center p-6 bg-red-500/10 border border-red-500/20 rounded-xl">
              <div className="flex justify-center mb-3">
                <div className="w-12 h-12 bg-red-500/20 rounded-full flex items-center justify-center">
                  <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 12H4" />
                  </svg>
                </div>
              </div>
              <p className="text-xs font-medium text-red-400 uppercase mb-2">Total Deducciones</p>
              <p className="text-2xl font-bold text-gray-100">
                - {formatCurrency(stats.ultimaPlanilla.totalDeductions)}
              </p>
            </div>

            <div className="text-center p-6 bg-green-500/10 border border-green-500/20 rounded-xl">
              <div className="flex justify-center mb-3">
                <div className="w-12 h-12 bg-green-500/20 rounded-full flex items-center justify-center">
                  <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                  </svg>
                </div>
              </div>
              <p className="text-xs font-medium text-green-400 uppercase mb-2">Total Neto</p>
              <p className="text-2xl font-bold text-gray-100">
                {formatCurrency(stats.ultimaPlanilla.totalNetPay)}
              </p>
            </div>

            <div className="text-center p-6 bg-amber-500/10 border border-amber-500/20 rounded-xl">
              <div className="flex justify-center mb-3">
                <div className="w-12 h-12 bg-amber-500/20 rounded-full flex items-center justify-center">
                  <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                  </svg>
                </div>
              </div>
              <p className="text-xs font-medium text-amber-400 uppercase mb-2">Costo Patronal</p>
              <p className="text-2xl font-bold text-gray-100">
                {formatCurrency(stats.ultimaPlanilla.totalEmployerCost)}
              </p>
            </div>
          </div>

          <div className="mt-6 flex items-center justify-between p-4 bg-navy-800 rounded-lg border border-navy-700">
            <div className="flex items-center gap-3">
              <span className="text-sm text-gray-400">Planilla #{stats.ultimaPlanilla.payrollNumber}</span>
              <span className="text-navy-600">•</span>
              {getStatusBadge(stats.ultimaPlanilla.status)}
            </div>
            <span className="text-sm text-gray-500">
              {new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')} -{' '}
              {new Date(stats.ultimaPlanilla.periodEndDate).toLocaleDateString('es-PA')}
            </span>
          </div>
        </div>
      )}

      {/* Acciones Rápidas */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Link
          to="/planillas"
          className="flex items-center justify-between p-6 bg-navy-800 border border-navy-700 hover:border-primary-500/50 text-gray-100 rounded-2xl shadow-lg shadow-black/20 hover:shadow-xl hover:shadow-black/30 transition-all"
        >
          <div className="text-left">
            <p className="text-lg font-bold text-gray-100">Nueva Planilla</p>
            <p className="text-sm text-gray-400">Crear período de pago</p>
          </div>
          <div className="w-12 h-12 bg-primary-500/15 rounded-xl flex items-center justify-center">
            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
          </div>
        </Link>

        <Link
          to="/empleados"
          className="flex items-center justify-between p-6 bg-navy-800 border border-navy-700 hover:border-green-500/50 text-gray-100 rounded-2xl shadow-lg shadow-black/20 hover:shadow-xl hover:shadow-black/30 transition-all"
        >
          <div className="text-left">
            <p className="text-lg font-bold text-gray-100">Gestionar Empleados</p>
            <p className="text-sm text-gray-400">Ver y editar personal</p>
          </div>
          <div className="w-12 h-12 bg-green-500/15 rounded-xl flex items-center justify-center">
            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
          </div>
        </Link>

        <Link
          to="/configuracion"
          className="flex items-center justify-between p-6 bg-navy-800 border border-navy-700 hover:border-purple-500/50 text-gray-100 rounded-2xl shadow-lg shadow-black/20 hover:shadow-xl hover:shadow-black/30 transition-all"
        >
          <div className="text-left">
            <p className="text-lg font-bold text-gray-100">Configuración</p>
            <p className="text-sm text-gray-400">Ajustes del sistema</p>
          </div>
          <div className="w-12 h-12 bg-purple-500/15 rounded-xl flex items-center justify-center">
            <svg className="w-6 h-6 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
        </Link>
      </div>
    </div>
  );
}
