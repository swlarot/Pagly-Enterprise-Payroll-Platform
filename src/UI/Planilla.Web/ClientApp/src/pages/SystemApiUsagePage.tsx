import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  Activity,
  Users,
  Key,
  AlertTriangle,
  Clock,
  TrendingUp,
  Download,
  Loader2,
  ArrowRight,
  Zap,
  ShieldAlert,
  UserX,
  KeyRound,
} from 'lucide-react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  AreaChart,
  Area,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  Legend,
} from 'recharts';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { systemApiUsageService } from '../services/systemApiUsageService';
import type {
  SystemApiUsageDto,
  TenantSignalDto,
  TenantUsageRowDto,
} from '../types/api';

// ============================================================================
// Constantes
// ============================================================================

const PERIOD_OPTIONS = [
  { label: '7 días', value: 7 },
  { label: '30 días', value: 30 },
  { label: '90 días', value: 90 },
];

const STATUS_COLORS: Record<string, string> = {
  OK: '#10b981',
  'Bad Request': '#f59e0b',
  Unauthorized: '#ef4444',
  Forbidden: '#ef4444',
  'Rate Limited': '#f97316',
  'Server Error': '#dc2626',
};

// Paleta por plan (stacked area)
const PLAN_COLORS: Record<string, string> = {
  Free: '#64748b',
  Starter: '#3b82f6',
  Professional: '#8b5cf6',
  Enterprise: '#f59e0b',
  Unknown: '#475569',
};

const CHART_FALLBACK_COLORS = ['#8b5cf6', '#06b6d4', '#10b981', '#f59e0b', '#ef4444'];

// ============================================================================
// Page
// ============================================================================

export default function SystemApiUsagePage() {
  const [data, setData] = useState<SystemApiUsageDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isExporting, setIsExporting] = useState(false);
  const [period, setPeriod] = useState(30);

  useEffect(() => {
    loadData();
  }, [period]);

  const loadData = async () => {
    try {
      setIsLoading(true);
      const result = await systemApiUsageService.getGlobalUsage(period, 20);
      setData(result);
    } catch (error: unknown) {
      const message =
        error instanceof Error ? error.message : 'Error al cargar analytics del sistema';
      toast.error(message);
    } finally {
      setIsLoading(false);
    }
  };

  const handleExportCsv = async () => {
    try {
      setIsExporting(true);
      await systemApiUsageService.exportCsv(period);
      toast.success('CSV descargado');
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Error al exportar CSV';
      toast.error(message);
    } finally {
      setIsExporting(false);
    }
  };

  if (isLoading) {
    return (
      <SystemAdminLayout>
        <div className="flex items-center justify-center h-[calc(100vh-80px)]">
          <Loader2 className="w-8 h-8 animate-spin text-primary-400" />
        </div>
      </SystemAdminLayout>
    );
  }

  if (!data) {
    return (
      <SystemAdminLayout>
        <div className="max-w-7xl mx-auto px-6 py-8">
          <div className="text-center text-gray-400 py-16">
            No se pudieron cargar las métricas. Intenta de nuevo.
          </div>
        </div>
      </SystemAdminLayout>
    );
  }

  const { summary, tenantRanking, dailyUsage, statusBreakdown, planDistribution, signals } = data;

  return (
    <SystemAdminLayout>
      <div className="max-w-7xl mx-auto px-6 py-8 space-y-8">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-3xl font-bold text-gray-100 flex items-center gap-3">
              <Activity className="w-8 h-8 text-violet-400" />
              Uso del API Platform
            </h1>
            <p className="mt-2 text-sm text-gray-400">
              Visión global del consumo del API entre todos los tenants.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <div className="flex gap-1 bg-navy-800 rounded-lg p-1">
              {PERIOD_OPTIONS.map((opt) => (
                <button
                  key={opt.value}
                  onClick={() => setPeriod(opt.value)}
                  className={`px-3 py-1.5 text-sm rounded-md transition-all ${
                    period === opt.value
                      ? 'bg-violet-600 text-white font-medium'
                      : 'text-gray-400 hover:text-gray-200'
                  }`}
                >
                  {opt.label}
                </button>
              ))}
            </div>
            <button
              onClick={handleExportCsv}
              disabled={isExporting}
              className="flex items-center gap-2 px-3 py-1.5 text-sm rounded-lg bg-navy-800 hover:bg-navy-700 text-gray-200 border border-navy-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isExporting ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <Download className="w-4 h-4" />
              )}
              Exportar CSV
            </button>
          </div>
        </div>

        {/* Stat cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <GlobalStatCard
            icon={<TrendingUp className="w-5 h-5" />}
            label="Total Requests"
            value={summary.totalRequests.toLocaleString('es-PA')}
            color="violet"
          />
          <GlobalStatCard
            icon={<Users className="w-5 h-5" />}
            label="Tenants Activos"
            value={summary.activeTenants.toLocaleString('es-PA')}
            color="emerald"
          />
          <GlobalStatCard
            icon={<Key className="w-5 h-5" />}
            label="Keys en Uso"
            value={summary.activeKeys.toLocaleString('es-PA')}
            color="cyan"
          />
          <GlobalStatCard
            icon={<AlertTriangle className="w-5 h-5" />}
            label="Error Rate"
            value={`${summary.errorRatePercent.toFixed(1)}%`}
            color={summary.errorRatePercent >= 15 ? 'red' : 'amber'}
          />
          <GlobalStatCard
            icon={<Clock className="w-5 h-5" />}
            label="Latencia Promedio"
            value={`${summary.avgResponseTimeMs}ms`}
            color="blue"
          />
          <GlobalStatCard
            icon={<Clock className="w-5 h-5" />}
            label="Latencia P95"
            value={`${summary.p95ResponseTimeMs}ms`}
            color="indigo"
          />
          <GlobalStatCard
            icon={<Zap className="w-5 h-5" />}
            label="Pico Req/Min"
            value={summary.peakRequestsPerMinute.toLocaleString('es-PA')}
            color="pink"
          />
          <GlobalStatCard
            icon={<TrendingUp className="w-5 h-5" />}
            label="5xx Errores"
            value={summary.serverErrors.toLocaleString('es-PA')}
            color={summary.serverErrors > 0 ? 'red' : 'slate'}
          />
        </div>

        {/* Charts row */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Line chart — daily usage */}
          <ChartCard title="Requests por día" className="lg:col-span-2">
            {dailyUsage.length === 0 ? (
              <EmptyChart message="Sin datos en este período." />
            ) : (
              <div className="h-72 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart
                    data={dailyUsage.map((d) => ({
                      date: formatDateShort(d.date),
                      total: d.count,
                      exitosos: d.successCount,
                      errores: d.errorCount,
                    }))}
                    margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" vertical={false} />
                    <XAxis
                      dataKey="date"
                      tick={{ fill: '#94a3b8', fontSize: 11 }}
                      axisLine={{ stroke: '#334155' }}
                      tickLine={false}
                    />
                    <YAxis
                      tick={{ fill: '#94a3b8', fontSize: 11 }}
                      axisLine={{ stroke: '#334155' }}
                      tickLine={false}
                    />
                    <RechartsTooltip contentStyle={tooltipStyle} labelStyle={{ color: '#94a3b8' }} />
                    <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
                    <Line type="monotone" dataKey="exitosos" stroke="#10b981" strokeWidth={2} dot={false} name="Exitosos" />
                    <Line type="monotone" dataKey="errores" stroke="#ef4444" strokeWidth={2} dot={false} name="Errores" />
                    <Line
                      type="monotone"
                      dataKey="total"
                      stroke="#8b5cf6"
                      strokeWidth={2}
                      dot={false}
                      name="Total"
                      strokeDasharray="5 5"
                    />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}
          </ChartCard>

          {/* Pie chart — status codes */}
          <ChartCard title="Status Codes">
            {statusBreakdown.length === 0 ? (
              <EmptyChart message="Sin datos." />
            ) : (
              <div className="h-72 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={statusBreakdown}
                      dataKey="count"
                      nameKey="label"
                      cx="50%"
                      cy="50%"
                      innerRadius={50}
                      outerRadius={90}
                      paddingAngle={2}
                      label={({ label, percent }) => `${label} ${(percent * 100).toFixed(0)}%`}
                      labelLine={false}
                    >
                      {statusBreakdown.map((entry, index) => (
                        <Cell
                          key={entry.statusCode}
                          fill={
                            STATUS_COLORS[entry.label] ||
                            CHART_FALLBACK_COLORS[index % CHART_FALLBACK_COLORS.length]
                          }
                        />
                      ))}
                    </Pie>
                    <RechartsTooltip contentStyle={tooltipStyle} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            )}
          </ChartCard>
        </div>

        {/* Plan distribution stacked area */}
        {planDistribution.length > 0 && (
          <ChartCard title="Distribución por plan">
            <div className="h-64 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart
                  data={planDistribution.map((p) => ({
                    plan: p.planName,
                    requests: p.totalRequests,
                    tenants: p.tenantCount,
                  }))}
                  margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" vertical={false} />
                  <XAxis
                    dataKey="plan"
                    tick={{ fill: '#94a3b8', fontSize: 11 }}
                    axisLine={{ stroke: '#334155' }}
                    tickLine={false}
                  />
                  <YAxis
                    tick={{ fill: '#94a3b8', fontSize: 11 }}
                    axisLine={{ stroke: '#334155' }}
                    tickLine={false}
                  />
                  <RechartsTooltip contentStyle={tooltipStyle} />
                  <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
                  <Area
                    type="monotone"
                    dataKey="requests"
                    stroke="#8b5cf6"
                    fill="#8b5cf6"
                    fillOpacity={0.3}
                    name="Requests"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </ChartCard>
        )}

        {/* Signals / alerts */}
        <SignalsSection signals={signals} />

        {/* Tenant ranking */}
        <TenantRankingTable rows={tenantRanking} />

        {/* Empty state */}
        {summary.totalRequests === 0 && (
          <div className="bg-navy-900 border border-navy-700 rounded-xl p-12 text-center">
            <Activity className="w-12 h-12 mx-auto mb-4 text-gray-600" />
            <p className="text-lg font-medium text-gray-300 mb-2">
              Sin actividad en el API Platform en este período
            </p>
            <p className="text-sm text-gray-500">
              Cuando los tenants empiecen a consumir <span className="font-mono">/v1/payroll/calculate</span>,
              verás aquí las métricas globales de uso, latencia, errores y señales operacionales.
            </p>
          </div>
        )}
      </div>
    </SystemAdminLayout>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

const STAT_COLOR_MAP: Record<string, string> = {
  violet: 'from-violet-600 to-violet-800 border-violet-500/30',
  emerald: 'from-emerald-600 to-emerald-800 border-emerald-500/30',
  cyan: 'from-cyan-600 to-cyan-800 border-cyan-500/30',
  amber: 'from-amber-600 to-amber-800 border-amber-500/30',
  red: 'from-red-600 to-red-800 border-red-500/30',
  blue: 'from-blue-600 to-blue-800 border-blue-500/30',
  indigo: 'from-indigo-600 to-indigo-800 border-indigo-500/30',
  pink: 'from-pink-600 to-pink-800 border-pink-500/30',
  slate: 'from-slate-600 to-slate-800 border-slate-500/30',
};

function GlobalStatCard({
  icon,
  label,
  value,
  color,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div
      className={`rounded-xl border bg-gradient-to-br p-5 ${STAT_COLOR_MAP[color] || STAT_COLOR_MAP.violet}`}
    >
      <div className="flex items-center gap-2 mb-3 text-white/70">
        {icon}
        <span className="text-xs font-medium uppercase tracking-wider">{label}</span>
      </div>
      <p className="text-2xl font-bold text-white tabular-nums">{value}</p>
    </div>
  );
}

function ChartCard({
  title,
  className,
  children,
}: {
  title: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={`bg-navy-900 border border-navy-700 rounded-xl overflow-hidden ${className ?? ''}`}>
      <div className="px-6 py-4 border-b border-navy-700">
        <h2 className="text-lg font-semibold text-gray-100">{title}</h2>
      </div>
      <div className="p-6">{children}</div>
    </div>
  );
}

function EmptyChart({ message }: { message: string }) {
  return (
    <div className="flex items-center justify-center h-64 text-gray-500 text-sm">{message}</div>
  );
}

// ============================================================================
// Signals section
// ============================================================================

function SignalsSection({ signals }: { signals: SystemApiUsageDto['signals'] }) {
  const hasAnySignal =
    signals.highErrorRate.length > 0 ||
    signals.trafficSpikes.length > 0 ||
    signals.possibleChurn.length > 0 ||
    signals.noActiveKeys.length > 0;

  if (!hasAnySignal) {
    return (
      <div className="bg-navy-900 border border-navy-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-100 mb-2">Señales operacionales</h2>
        <p className="text-sm text-gray-400">
          Todo en orden. No hay tenants con error-rate alto, spikes de tráfico, posible churn,
          ni onboarding incompleto en este período.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-gray-100">Señales operacionales</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <SignalCard
          icon={<ShieldAlert className="w-5 h-5" />}
          title="Error-rate alto"
          description="Tenants con ≥ 15% de requests fallidos"
          tenants={signals.highErrorRate}
          tone="red"
        />
        <SignalCard
          icon={<Zap className="w-5 h-5" />}
          title="Spikes de tráfico"
          description="Tenants con volumen ≥ 3x el promedio"
          tenants={signals.trafficSpikes}
          tone="orange"
        />
        <SignalCard
          icon={<UserX className="w-5 h-5" />}
          title="Posible churn"
          description="Tenants con keys pero sin actividad reciente"
          tenants={signals.possibleChurn}
          tone="amber"
        />
        <SignalCard
          icon={<KeyRound className="w-5 h-5" />}
          title="Onboarding incompleto"
          description="Plan permite API pero no tienen keys"
          tenants={signals.noActiveKeys}
          tone="blue"
        />
      </div>
    </div>
  );
}

const TONE_STYLES: Record<string, { bg: string; border: string; icon: string; text: string }> = {
  red: {
    bg: 'bg-red-500/5',
    border: 'border-red-500/30',
    icon: 'text-red-400',
    text: 'text-red-200',
  },
  orange: {
    bg: 'bg-orange-500/5',
    border: 'border-orange-500/30',
    icon: 'text-orange-400',
    text: 'text-orange-200',
  },
  amber: {
    bg: 'bg-amber-500/5',
    border: 'border-amber-500/30',
    icon: 'text-amber-400',
    text: 'text-amber-200',
  },
  blue: {
    bg: 'bg-blue-500/5',
    border: 'border-blue-500/30',
    icon: 'text-blue-400',
    text: 'text-blue-200',
  },
};

function SignalCard({
  icon,
  title,
  description,
  tenants,
  tone,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  tenants: TenantSignalDto[];
  tone: string;
}) {
  const styles = TONE_STYLES[tone] ?? TONE_STYLES.blue;

  return (
    <div className={`rounded-xl border p-5 ${styles.bg} ${styles.border}`}>
      <div className="flex items-start gap-3 mb-3">
        <div className={styles.icon}>{icon}</div>
        <div className="flex-1">
          <h3 className={`font-semibold ${styles.text}`}>{title}</h3>
          <p className="text-xs text-gray-400 mt-0.5">{description}</p>
        </div>
        <span className="text-xs font-medium px-2 py-1 rounded-md bg-navy-800 text-gray-300">
          {tenants.length}
        </span>
      </div>
      {tenants.length === 0 ? (
        <p className="text-sm text-gray-500 italic">Ninguno.</p>
      ) : (
        <ul className="space-y-2">
          {tenants.slice(0, 5).map((t) => (
            <li key={`${title}-${t.tenantId}`}>
              <Link
                to={`/system-admin/tenants/${t.tenantId}`}
                className="flex items-center justify-between text-sm hover:bg-navy-800 rounded-md px-2 py-1.5 -mx-2 transition-colors group"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-gray-200 truncate">{t.tenantName}</p>
                  <p className="text-xs text-gray-500 truncate">
                    {t.planName} · {t.metric}
                  </p>
                </div>
                <ArrowRight className="w-4 h-4 text-gray-500 group-hover:text-gray-300 flex-shrink-0 ml-2" />
              </Link>
            </li>
          ))}
          {tenants.length > 5 && (
            <li className="text-xs text-gray-500 pt-1">
              + {tenants.length - 5} tenant{tenants.length - 5 === 1 ? '' : 's'} más
            </li>
          )}
        </ul>
      )}
    </div>
  );
}

// ============================================================================
// Ranking table
// ============================================================================

function TenantRankingTable({ rows }: { rows: TenantUsageRowDto[] }) {
  const maxRequests = useMemo(
    () => (rows.length > 0 ? Math.max(...rows.map((r) => r.totalRequests)) : 0),
    [rows],
  );

  if (rows.length === 0) {
    return null;
  }

  return (
    <div className="bg-navy-900 border border-navy-700 rounded-xl overflow-hidden">
      <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-gray-100">Ranking de tenants</h2>
          <p className="text-xs text-gray-500 mt-0.5">
            Top {rows.length} por volumen de requests. Click en un tenant para ver su ficha.
          </p>
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-navy-950 text-xs uppercase text-gray-400">
            <tr>
              <th className="px-6 py-3 text-left font-medium">#</th>
              <th className="px-6 py-3 text-left font-medium">Tenant</th>
              <th className="px-6 py-3 text-left font-medium">Plan</th>
              <th className="px-6 py-3 text-right font-medium">Requests</th>
              <th className="px-6 py-3 text-right font-medium">Error %</th>
              <th className="px-6 py-3 text-right font-medium">Latencia P95</th>
              <th className="px-6 py-3 text-right font-medium">Keys</th>
              <th className="px-6 py-3 text-left font-medium">Señales</th>
              <th className="px-6 py-3"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-navy-800">
            {rows.map((row, idx) => {
              const planColor = PLAN_COLORS[row.planName] ?? PLAN_COLORS.Unknown;
              const barWidth = maxRequests > 0 ? (row.totalRequests / maxRequests) * 100 : 0;
              return (
                <tr key={row.tenantId} className="hover:bg-navy-800/50 transition-colors">
                  <td className="px-6 py-4 text-gray-500 tabular-nums">{idx + 1}</td>
                  <td className="px-6 py-4">
                    <div className="text-gray-100 font-medium">{row.tenantName}</div>
                    <div className="text-xs text-gray-500">{row.subdomain || '—'}</div>
                  </td>
                  <td className="px-6 py-4">
                    <span
                      className="inline-flex items-center gap-2 px-2 py-1 rounded-md text-xs font-medium"
                      style={{
                        background: `${planColor}22`,
                        color: planColor,
                        border: `1px solid ${planColor}55`,
                      }}
                    >
                      {row.planName}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3 justify-end">
                      <span className="text-gray-200 tabular-nums">
                        {row.totalRequests.toLocaleString('es-PA')}
                      </span>
                      <div className="w-16 h-1.5 bg-navy-800 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-violet-500"
                          style={{ width: `${barWidth}%` }}
                        />
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-right tabular-nums">
                    <span
                      className={
                        row.errorRatePercent >= 15
                          ? 'text-red-400'
                          : row.errorRatePercent >= 5
                            ? 'text-amber-400'
                            : 'text-emerald-400'
                      }
                    >
                      {row.errorRatePercent.toFixed(1)}%
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right tabular-nums text-gray-300">
                    {row.p95ResponseTimeMs}ms
                  </td>
                  <td className="px-6 py-4 text-right tabular-nums text-gray-400">
                    {row.activeKeysCount}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex flex-wrap gap-1">
                      {row.signals.map((s) => (
                        <SignalBadge key={s} signal={s} />
                      ))}
                    </div>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <Link
                      to={`/system-admin/tenants/${row.tenantId}`}
                      className="inline-flex items-center gap-1 text-xs font-medium text-violet-400 hover:text-violet-300"
                    >
                      Ver
                      <ArrowRight className="w-3 h-3" />
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function SignalBadge({ signal }: { signal: string }) {
  const map: Record<string, { label: string; cls: string }> = {
    'high-error-rate': {
      label: 'Error rate ↑',
      cls: 'bg-red-500/10 text-red-300 border-red-500/30',
    },
    'traffic-spike': {
      label: 'Spike',
      cls: 'bg-orange-500/10 text-orange-300 border-orange-500/30',
    },
  };
  const meta = map[signal] ?? { label: signal, cls: 'bg-navy-800 text-gray-400 border-navy-700' };
  return (
    <span
      className={`inline-flex items-center text-[10px] font-medium px-1.5 py-0.5 rounded border ${meta.cls}`}
    >
      {meta.label}
    </span>
  );
}

// ============================================================================
// Helpers
// ============================================================================

const tooltipStyle = {
  background: '#0f172a',
  border: '1px solid #334155',
  borderRadius: 8,
  fontSize: 13,
};

function formatDateShort(date: string | Date): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  return d.toLocaleDateString('es-PA', { month: 'short', day: 'numeric' });
}
