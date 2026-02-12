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
  // Lista de las últimas planillas para el mini chart
  const [planillasList, setPlanillasList] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const hasLoadedRef = React.useRef(false); // Ref para evitar cargar múltiples veces

  const loadDashboardData = React.useCallback(async () => {
    // Evitar cargar si ya se cargó
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
      const empleados = Array.isArray(empleadosRes)
        ? empleadosRes
        : Array.isArray(empleadosRes?.data)
          ? empleadosRes.data
          : [];
      const activos = empleados.filter((e: any) => e.estaActivo).length;

      // Cargar planillas
      const planillasRes = await api.get('/api/payrollheaders');
      const planillas = Array.isArray(planillasRes)
        ? planillasRes
        : Array.isArray(planillasRes?.data)
          ? planillasRes.data
          : [];

      // Última planilla (la más reciente)
      const ultimaPlanilla = planillas.length > 0 ? planillas[0] : null;

      // Planillas pendientes (Draft = 0)
      const pendientes = planillas.filter((p: any) => p.status === 0).length;

      // Guardar lista para mini chart (hasta 8)
      setPlanillasList(planillas.slice(0, 8));

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

    // Si el tenant está disponible, cargar datos
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

  const formatCurrencyShort = (amount: number) => {
    if (amount >= 1000) {
      return `$${(amount / 1000).toFixed(1)}k`;
    }
    return formatCurrency(amount);
  };

  const getStatusBadge = (status: number) => {
    const badges: Record<number, { text: string; color: string }> = {
      0: { text: 'BORRADOR', color: 'bg-yellow-500/15 text-yellow-400 border border-yellow-500/20' },
      1: { text: 'CALCULADO', color: 'bg-primary-500/15 text-primary-400 border border-primary-500/20' },
      2: { text: 'APROBADO', color: 'bg-green-500/15 text-green-400 border border-green-500/20' },
      3: { text: 'PAGADO', color: 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/20' },
      4: { text: 'CANCELADO', color: 'bg-red-500/15 text-red-400 border border-red-500/20' },
    };
    const badge = badges[status] || badges[0];
    return (
      <span className={`px-3 py-1 rounded-full text-xs font-semibold ${badge.color}`}>
        {badge.text}
      </span>
    );
  };

  // Determinar el número de paso del status para el progress tracker
  const getStatusStep = (status: number): number => {
    // Borrador=0, Calculado=1, Aprobado=2, Pagado=3
    const map: Record<number, number> = { 0: 0, 1: 1, 2: 2, 3: 3 };
    return map[status] ?? 0;
  };

  // Obtener saludo según la hora del día
  const getGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return 'Buenos días';
    if (hour < 18) return 'Buenas tardes';
    return 'Buenas noches';
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

  // Calcular valor máximo de planillas para el mini chart
  const chartPlanillas = [...planillasList].reverse();
  const maxNetPay = chartPlanillas.length > 0
    ? Math.max(...chartPlanillas.map((p: any) => p.totalNetPay || 0))
    : 0;

  return (
    <div className="space-y-6">

      {/* ── ROW 1: Header ── */}
      <div className="animate-fade-in-up">
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-100 font-display">
              {getGreeting()},{' '}
              <span className="text-primary-400">
                {user?.email?.split('@')[0]}
              </span>
            </h1>
            <p className="text-gray-400 mt-1 font-body">
              {tenant?.name} ·{' '}
              {new Date().toLocaleDateString('es-PA', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
              })}
            </p>
          </div>
          <div className="text-right">
            <span className="text-xs px-3 py-1.5 rounded-full bg-primary-500/15 text-primary-400 border border-primary-500/20 font-medium">
              {user?.roleName}
            </span>
          </div>
        </div>
      </div>

      {/* ── ROW 2: KPI Cards ── */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5">

        {/* Empleados Activos */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-5 border border-navy-700 hover:border-primary-500/40 transition-all group">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">Empleados Activos</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">{stats.empleadosActivos}</p>
              <div className="flex items-center gap-1 mt-2">
                <span className="text-xs text-primary-400 flex items-center gap-0.5">
                  <svg className="w-3 h-3" viewBox="0 0 12 12" fill="none">
                    <path d="M2 9l3-3 2 2 3-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                  </svg>
                  de {stats.totalEmpleados} registrados
                </span>
              </div>
            </div>
            <div className="w-13 h-13 bg-gradient-to-br from-primary-600/30 to-primary-500/10 rounded-xl flex items-center justify-center border border-primary-500/20 group-hover:border-primary-500/40 transition-colors">
              <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
          </div>
        </div>

        {/* Última Planilla */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-5 border border-navy-700 hover:border-green-500/40 transition-all group">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">Última Planilla</p>
              <p className="text-2xl font-bold text-gray-100 mt-2 font-mono">
                {stats.ultimaPlanilla ? formatCurrency(stats.ultimaPlanilla.totalNetPay) : '—'}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                {stats.ultimaPlanilla
                  ? new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')
                  : 'No hay datos'}
              </p>
            </div>
            <div className="w-13 h-13 bg-gradient-to-br from-green-600/30 to-green-500/10 rounded-xl flex items-center justify-center border border-green-500/20 group-hover:border-green-500/40 transition-colors">
              <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
          </div>
        </div>

        {/* Aportes Patronales */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-5 border border-navy-700 hover:border-amber-500/40 transition-all group">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">Aportes Patronales</p>
              <p className="text-2xl font-bold text-gray-100 mt-2 font-mono">{formatCurrency(stats.aportesCss)}</p>
              <p className="text-xs text-gray-500 mt-1">CSS + SE + Riesgo</p>
            </div>
            <div className="w-13 h-13 bg-gradient-to-br from-amber-600/30 to-amber-500/10 rounded-xl flex items-center justify-center border border-amber-500/20 group-hover:border-amber-500/40 transition-colors">
              <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
          </div>
        </div>

        {/* Planillas Pendientes */}
        <div className="bg-navy-900 rounded-2xl shadow-lg shadow-black/20 p-5 border border-navy-700 hover:border-primary-500/40 transition-all group">
          <div className="flex items-center justify-between">
            <div className="flex-1">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wider">Planillas Pendientes</p>
              <p className="text-3xl font-bold text-gray-100 mt-2">{stats.pendientes}</p>
              <div className="flex items-center gap-2 mt-2">
                {stats.pendientes > 0 ? (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-red-500/15 text-red-400 border border-red-500/20">
                    Acción requerida
                  </span>
                ) : (
                  <p className="text-xs text-gray-500">Todo al día</p>
                )}
              </div>
            </div>
            <div
              className={`w-13 h-13 rounded-xl flex items-center justify-center border transition-colors ${
                stats.pendientes > 0
                  ? 'bg-gradient-to-br from-red-600/30 to-red-500/10 border-red-500/20 group-hover:border-red-500/40'
                  : 'bg-navy-800 border-navy-600'
              }`}
            >
              <svg
                className={`w-6 h-6 ${stats.pendientes > 0 ? 'text-red-400' : 'text-gray-500'}`}
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

      {/* ── ROW 3: Mini Chart (2/3) + Desglose Costo Patronal (1/3) ── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">

        {/* Mini Chart de períodos — CSS puro, sin recharts */}
        <div className="lg:col-span-2 bg-navy-900 rounded-2xl border border-navy-700 p-5">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h3 className="text-sm font-semibold text-gray-200">Historial de Planillas</h3>
              <p className="text-xs text-gray-500 mt-0.5">Neto pagado por período</p>
            </div>
            <Link
              to="/planillas"
              className="text-xs text-primary-400 hover:text-primary-300 flex items-center gap-1 transition-colors"
            >
              Ver todas
              <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
              </svg>
            </Link>
          </div>

          {chartPlanillas.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-32 text-gray-600">
              <svg className="w-8 h-8 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              <span className="text-xs">Sin planillas registradas</span>
            </div>
          ) : (
            <>
              {/* Barras del chart */}
              <div className="flex items-end gap-1.5 h-20 mb-2">
                {chartPlanillas.map((p: any, i: number) => {
                  const pct = maxNetPay > 0 ? Math.round(((p.totalNetPay || 0) / maxNetPay) * 100) : 0;
                  const isLast = i === chartPlanillas.length - 1;
                  return (
                    <div key={i} className="flex-1 flex flex-col items-center gap-0.5 h-full justify-end">
                      <div
                        className={`w-full rounded-t-sm transition-all duration-300 cursor-default ${
                          isLast
                            ? 'bg-primary-500 hover:bg-primary-400'
                            : 'bg-primary-500/40 hover:bg-primary-500/70'
                        }`}
                        style={{ height: `${Math.max(pct, 8)}%` }}
                        title={`${p.payrollNumber ? `#${p.payrollNumber} · ` : ''}${formatCurrency(p.totalNetPay || 0)}`}
                      />
                    </div>
                  );
                })}
              </div>

              {/* Etiquetas inferiores (período abreviado) */}
              <div className="flex items-center gap-1.5">
                {chartPlanillas.map((p: any, i: number) => {
                  const date = p.periodStartDate
                    ? new Date(p.periodStartDate).toLocaleDateString('es-PA', { month: 'short', day: 'numeric' })
                    : `#${i + 1}`;
                  return (
                    <div key={i} className="flex-1 text-center">
                      <span className="text-[9px] text-gray-600 truncate block">{date}</span>
                    </div>
                  );
                })}
              </div>

              {/* Leyenda debajo */}
              <div className="flex items-center gap-4 mt-3 pt-3 border-t border-navy-800">
                <div className="flex items-center gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-sm bg-primary-500" />
                  <span className="text-[10px] text-gray-500">Más reciente</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-sm bg-primary-500/40" />
                  <span className="text-[10px] text-gray-500">Anteriores</span>
                </div>
                {stats.ultimaPlanilla && (
                  <div className="ml-auto">
                    <span className="text-[10px] text-gray-500">
                      Máx: <span className="text-gray-300 font-mono">{formatCurrency(maxNetPay)}</span>
                    </span>
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        {/* Desglose de Costo Patronal */}
        <div className="bg-navy-900 rounded-2xl border border-navy-700 p-5">
          <h3 className="text-sm font-semibold text-gray-400 mb-4">Costo Patronal Última Planilla</h3>

          {!stats.ultimaPlanilla ? (
            <div className="flex flex-col items-center justify-center h-32 text-gray-600">
              <svg className="w-8 h-8 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
              <span className="text-xs">Sin datos de planilla</span>
            </div>
          ) : (
            <div className="space-y-3">
              {[
                {
                  label: 'Salario Bruto',
                  value: stats.ultimaPlanilla?.totalGrossPay,
                  color: 'bg-primary-500',
                  textColor: 'text-primary-400',
                },
                {
                  label: 'CSS Patronal (12.25%)',
                  value: stats.ultimaPlanilla?.totalEmployerCss,
                  color: 'bg-blue-500',
                  textColor: 'text-blue-400',
                },
                {
                  label: 'SE Patronal (1.5%)',
                  value: stats.ultimaPlanilla?.totalEmployerSe,
                  color: 'bg-amber-500',
                  textColor: 'text-amber-400',
                },
                {
                  label: 'Riesgo Prof.',
                  value: stats.ultimaPlanilla?.totalRiskInsurance,
                  color: 'bg-red-500',
                  textColor: 'text-red-400',
                },
              ].map(item => (
                <div key={item.label} className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className={`w-2 h-2 rounded-full flex-shrink-0 ${item.color}`} />
                    <span className="text-[11px] text-gray-400">{item.label}</span>
                  </div>
                  <span className={`text-xs font-mono font-semibold ${item.textColor}`}>
                    {formatCurrencyShort(item.value || 0)}
                  </span>
                </div>
              ))}

              {/* Total */}
              <div className="border-t border-navy-700 pt-2.5 mt-2.5 flex items-center justify-between">
                <span className="text-xs font-semibold text-gray-300">Costo Total</span>
                <span className="text-sm font-mono font-bold text-primary-400">
                  {formatCurrency(stats.ultimaPlanilla?.totalEmployerCost || 0)}
                </span>
              </div>

              {/* Badge del período */}
              <div className="pt-1">
                <span className="text-[10px] text-gray-600">
                  Planilla #{stats.ultimaPlanilla?.payrollNumber} ·{' '}
                  {new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA', {
                    day: '2-digit', month: 'short'
                  })}
                </span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ── ROW 4: Progress Tracker ── */}
      {stats.ultimaPlanilla && (
        <div className="bg-navy-900 border border-navy-700 rounded-xl p-5">
          <div className="flex items-center gap-2 mb-4">
            <div className="w-2 h-2 rounded-full bg-primary-400 animate-pulse" />
            <p className="text-sm font-medium text-gray-300">
              Planilla #{stats.ultimaPlanilla.payrollNumber} — Estado actual
            </p>
          </div>
          <div className="flex items-center gap-0">
            {['Borrador', 'Calculado', 'Aprobado', 'Pagado'].map((step, idx) => {
              const currentStep = getStatusStep(stats.ultimaPlanilla.status);
              const isCompleted = idx < currentStep;
              const isActive = idx === currentStep;
              return (
                <React.Fragment key={step}>
                  <div className="flex flex-col items-center flex-1">
                    <div
                      className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold transition-all ${
                        isCompleted
                          ? 'bg-primary-600 text-white'
                          : isActive
                          ? 'bg-primary-500/20 text-primary-400 border-2 border-primary-500'
                          : 'bg-navy-800 text-gray-600 border border-navy-600'
                      }`}
                    >
                      {isCompleted ? '✓' : idx + 1}
                    </div>
                    <p
                      className={`text-xs mt-1.5 font-medium ${
                        isActive ? 'text-primary-400' : isCompleted ? 'text-gray-400' : 'text-gray-600'
                      }`}
                    >
                      {step}
                    </p>
                  </div>
                  {idx < 3 && (
                    <div
                      className={`flex-1 h-0.5 mb-4 ${
                        idx < currentStep ? 'bg-primary-600' : 'bg-navy-700'
                      }`}
                    />
                  )}
                </React.Fragment>
              );
            })}
          </div>
        </div>
      )}

      {/* ── ROW 5: Resumen del Período + Acciones Rápidas ── */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">

        {/* Resumen del Período (2/3) */}
        {stats.ultimaPlanilla && (
          <div className="lg:col-span-2 bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 p-6">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-semibold text-gray-100 font-display">Resumen del Período</h3>
              {getStatusBadge(stats.ultimaPlanilla.status)}
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 divide-x divide-navy-700">
              {/* Salario Bruto */}
              <div className="px-4 first:pl-0 text-center">
                <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-2">Salario Bruto</p>
                <p className="text-lg font-bold text-gray-100 font-mono">
                  {formatCurrency(stats.ultimaPlanilla.totalGrossPay)}
                </p>
                <div className="w-8 h-0.5 bg-primary-500/40 mx-auto mt-2 rounded-full" />
              </div>

              {/* Deducciones */}
              <div className="px-4 text-center">
                <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-2">Deducciones</p>
                <p className="text-lg font-bold text-red-400 font-mono">
                  -{formatCurrency(stats.ultimaPlanilla.totalDeductions)}
                </p>
                <div className="w-8 h-0.5 bg-red-500/40 mx-auto mt-2 rounded-full" />
              </div>

              {/* Neto a Pagar */}
              <div className="px-4 text-center">
                <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-2">Neto a Pagar</p>
                <p className="text-lg font-bold text-primary-400 font-mono">
                  {formatCurrency(stats.ultimaPlanilla.totalNetPay)}
                </p>
                <div className="w-8 h-0.5 bg-primary-500/60 mx-auto mt-2 rounded-full" />
              </div>

              {/* Costo Patronal */}
              <div className="px-4 last:pr-0 text-center">
                <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wider mb-2">Costo Patronal</p>
                <p className="text-lg font-bold text-amber-400 font-mono">
                  {formatCurrency(stats.ultimaPlanilla.totalEmployerCost)}
                </p>
                <div className="w-8 h-0.5 bg-amber-500/40 mx-auto mt-2 rounded-full" />
              </div>
            </div>

            {/* Footer con número y período */}
            <div className="mt-5 pt-4 border-t border-navy-700 flex items-center justify-between">
              <div className="flex items-center gap-2 text-sm text-gray-400">
                <span>Planilla #{stats.ultimaPlanilla.payrollNumber}</span>
              </div>
              <span className="text-sm text-gray-500">
                {new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')} —{' '}
                {new Date(stats.ultimaPlanilla.periodEndDate).toLocaleDateString('es-PA')}
              </span>
            </div>
          </div>
        )}

        {/* Acciones Rápidas (1/3 cuando hay planilla, full width cuando no) */}
        <div className={`${stats.ultimaPlanilla ? '' : 'lg:col-span-3'}`}>
          <div className="h-full bg-navy-900 border border-navy-700 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-400 mb-4">Acciones Rápidas</h3>
            <div className="space-y-2">

              {/* Nueva Planilla */}
              <Link
                to="/planillas"
                className="group flex items-center gap-3 p-3 bg-navy-800/50 border border-navy-700 hover:border-primary-500/50 hover:bg-primary-500/5 rounded-xl transition-all"
              >
                <div className="w-9 h-9 bg-gradient-to-br from-primary-600/30 to-primary-500/10 rounded-lg flex items-center justify-center border border-primary-500/20 flex-shrink-0">
                  <svg className="w-4 h-4 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-bold text-gray-200 group-hover:text-primary-300 transition-colors">
                    Nueva Planilla
                  </p>
                  <p className="text-[10px] text-gray-600">Crear período de pago</p>
                </div>
                <svg className="w-3.5 h-3.5 text-gray-600 group-hover:text-primary-400 group-hover:translate-x-0.5 transition-all flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </Link>

              {/* Gestionar Empleados */}
              <Link
                to="/empleados"
                className="group flex items-center gap-3 p-3 bg-navy-800/50 border border-navy-700 hover:border-green-500/50 hover:bg-green-500/5 rounded-xl transition-all"
              >
                <div className="w-9 h-9 bg-gradient-to-br from-green-600/30 to-green-500/10 rounded-lg flex items-center justify-center border border-green-500/20 flex-shrink-0">
                  <svg className="w-4 h-4 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-bold text-gray-200 group-hover:text-green-300 transition-colors">
                    Gestionar Empleados
                  </p>
                  <p className="text-[10px] text-gray-600">Ver y editar personal</p>
                </div>
                <svg className="w-3.5 h-3.5 text-gray-600 group-hover:text-green-400 group-hover:translate-x-0.5 transition-all flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </Link>

              {/* Ver Reportes */}
              <Link
                to="/reportes"
                className="group flex items-center gap-3 p-3 bg-navy-800/50 border border-navy-700 hover:border-emerald-500/50 hover:bg-emerald-500/5 rounded-xl transition-all"
              >
                <div className="w-9 h-9 bg-gradient-to-br from-emerald-600/30 to-emerald-500/10 rounded-lg flex items-center justify-center border border-emerald-500/20 flex-shrink-0">
                  <svg className="w-4 h-4 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-bold text-gray-200 group-hover:text-emerald-300 transition-colors">
                    Ver Reportes
                  </p>
                  <p className="text-[10px] text-gray-600">CSS, SE, ISR</p>
                </div>
                <svg className="w-3.5 h-3.5 text-gray-600 group-hover:text-emerald-400 group-hover:translate-x-0.5 transition-all flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </Link>

              {/* Configuración */}
              <Link
                to="/configuracion"
                className="group flex items-center gap-3 p-3 bg-navy-800/50 border border-navy-700 hover:border-purple-500/50 hover:bg-purple-500/5 rounded-xl transition-all"
              >
                <div className="w-9 h-9 bg-gradient-to-br from-purple-600/30 to-purple-500/10 rounded-lg flex items-center justify-center border border-purple-500/20 flex-shrink-0">
                  <svg className="w-4 h-4 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-bold text-gray-200 group-hover:text-purple-300 transition-colors">
                    Configuración
                  </p>
                  <p className="text-[10px] text-gray-600">Ajustes del sistema</p>
                </div>
                <svg className="w-3.5 h-3.5 text-gray-600 group-hover:text-purple-400 group-hover:translate-x-0.5 transition-all flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </Link>

            </div>
          </div>
        </div>
      </div>

    </div>
  );
}
