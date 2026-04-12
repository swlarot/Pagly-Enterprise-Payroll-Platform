import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import {
  Activity,
  Clock,
  CheckCircle,
  AlertTriangle,
  Loader2,
  TrendingUp,
} from 'lucide-react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip as RechartsTooltip,
  Legend,
} from 'recharts';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { apiKeysService } from '../services/apiKeysService';
import type { ApiUsageStatsDto } from '../types/api';

const PERIOD_OPTIONS = [
  { label: '7 días', value: 7 },
  { label: '30 días', value: 30 },
  { label: '90 días', value: 90 },
];

const STATUS_COLORS: Record<string, string> = {
  OK: '#10b981',
  'Bad Request': '#f59e0b',
  Unauthorized: '#ef4444',
  'Rate Limited': '#f97316',
  'Server Error': '#dc2626',
};

const CHART_COLORS = ['#8b5cf6', '#06b6d4', '#10b981', '#f59e0b', '#ef4444'];

export default function ApiUsageDashboardPage() {
  const [stats, setStats] = useState<ApiUsageStatsDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [period, setPeriod] = useState(30);

  useEffect(() => {
    loadStats();
  }, [period]);

  const loadStats = async () => {
    try {
      setIsLoading(true);
      const data = await apiKeysService.getUsageStats(period);
      setStats(data);
    } catch (error: any) {
      toast.error(error?.message || 'Error al cargar estadísticas de uso');
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin text-violet-400" />
      </div>
    );
  }

  if (!stats) {
    return (
      <div className="text-center text-gray-400 py-16">
        No se pudieron cargar las estadísticas. Intenta de nuevo.
      </div>
    );
  }

  const { summary, dailyUsage, statusBreakdown, topKeys } = stats;
  const successRate = summary.totalRequests > 0
    ? Math.round((summary.successfulRequests / summary.totalRequests) * 100)
    : 0;

  const dailyChartData = dailyUsage.map((d) => ({
    date: new Date(d.date).toLocaleDateString('es-PA', { month: 'short', day: 'numeric' }),
    total: d.count,
    exitosos: d.successCount,
    errores: d.errorCount,
  }));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-100 flex items-center gap-3">
            <Activity className="w-8 h-8 text-violet-400" />
            Uso del API
          </h1>
          <p className="mt-2 text-sm text-gray-400">
            Métricas de uso de <span className="font-mono text-gray-300">POST /v1/payroll/calculate</span> por tus clientes.
          </p>
        </div>
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
      </div>

      {/* Stat Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          icon={<TrendingUp className="w-5 h-5" />}
          label="Total Requests"
          value={summary.totalRequests.toLocaleString('es-PA')}
          color="violet"
        />
        <StatCard
          icon={<CheckCircle className="w-5 h-5" />}
          label="Tasa de Éxito"
          value={`${successRate}%`}
          color="emerald"
        />
        <StatCard
          icon={<Clock className="w-5 h-5" />}
          label="Latencia Promedio"
          value={`${summary.avgResponseTimeMs}ms`}
          color="cyan"
        />
        <StatCard
          icon={<AlertTriangle className="w-5 h-5" />}
          label="Latencia P95"
          value={`${summary.p95ResponseTimeMs}ms`}
          color="amber"
        />
      </div>

      {/* Charts Row 1: Line + Pie */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Line Chart — Requests por día */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <h2 className="text-lg font-semibold text-gray-100">Requests por día</h2>
          </CardHeader>
          <CardBody>
            {dailyChartData.length === 0 ? (
              <EmptyState message="Sin datos de uso en este período." />
            ) : (
              <div className="h-72 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={dailyChartData} margin={{ top: 8, right: 16, bottom: 8, left: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" vertical={false} />
                    <XAxis dataKey="date" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={{ stroke: '#334155' }} tickLine={false} />
                    <YAxis tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={{ stroke: '#334155' }} tickLine={false} />
                    <RechartsTooltip
                      contentStyle={{ background: '#0f172a', border: '1px solid #334155', borderRadius: 8, fontSize: 13 }}
                      labelStyle={{ color: '#94a3b8' }}
                    />
                    <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
                    <Line type="monotone" dataKey="exitosos" stroke="#10b981" strokeWidth={2} dot={false} name="Exitosos" />
                    <Line type="monotone" dataKey="errores" stroke="#ef4444" strokeWidth={2} dot={false} name="Errores" />
                    <Line type="monotone" dataKey="total" stroke="#8b5cf6" strokeWidth={2} dot={false} name="Total" strokeDasharray="5 5" />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}
          </CardBody>
        </Card>

        {/* Pie Chart — Status Codes */}
        <Card>
          <CardHeader>
            <h2 className="text-lg font-semibold text-gray-100">Status Codes</h2>
          </CardHeader>
          <CardBody>
            {statusBreakdown.length === 0 ? (
              <EmptyState message="Sin datos." />
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
                      label={({ label, percent }) =>
                        `${label} ${(percent * 100).toFixed(0)}%`
                      }
                      labelLine={false}
                    >
                      {statusBreakdown.map((entry, index) => (
                        <Cell
                          key={entry.statusCode}
                          fill={STATUS_COLORS[entry.label] || CHART_COLORS[index % CHART_COLORS.length]}
                        />
                      ))}
                    </Pie>
                    <RechartsTooltip
                      contentStyle={{ background: '#0f172a', border: '1px solid #334155', borderRadius: 8, fontSize: 13 }}
                    />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            )}
          </CardBody>
        </Card>
      </div>

      {/* Charts Row 2: Top Keys bar chart */}
      {topKeys.length > 0 && (
        <Card>
          <CardHeader>
            <h2 className="text-lg font-semibold text-gray-100">Top Keys por Uso</h2>
          </CardHeader>
          <CardBody>
            <div className="h-64 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={topKeys.map((k) => ({
                    name: k.keyName.length > 16 ? k.keyName.slice(0, 15) + '…' : k.keyName,
                    requests: k.totalRequests,
                    latencia: k.avgResponseTimeMs,
                    prefix: k.keyPrefix,
                  }))}
                  margin={{ top: 8, right: 16, bottom: 8, left: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" vertical={false} />
                  <XAxis dataKey="name" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={{ stroke: '#334155' }} tickLine={false} />
                  <YAxis yAxisId="left" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={{ stroke: '#334155' }} tickLine={false} />
                  <YAxis yAxisId="right" orientation="right" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={{ stroke: '#334155' }} tickLine={false} />
                  <RechartsTooltip
                    contentStyle={{ background: '#0f172a', border: '1px solid #334155', borderRadius: 8, fontSize: 13 }}
                  />
                  <Legend wrapperStyle={{ fontSize: 12, color: '#94a3b8' }} />
                  <Bar yAxisId="left" dataKey="requests" fill="#8b5cf6" radius={[6, 6, 0, 0]} name="Requests" />
                  <Bar yAxisId="right" dataKey="latencia" fill="#06b6d4" radius={[6, 6, 0, 0]} name="Latencia (ms)" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </CardBody>
        </Card>
      )}

      {/* Empty overall state */}
      {summary.totalRequests === 0 && (
        <Card>
          <CardBody>
            <div className="text-center py-12 text-gray-400">
              <Activity className="w-12 h-12 mx-auto mb-4 text-gray-600" />
              <p className="text-lg font-medium text-gray-300 mb-2">Sin actividad en este período</p>
              <p className="text-sm">
                Cuando tus clientes empiecen a usar el API, verás aquí las métricas de uso,
                latencia, errores y más.
              </p>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}

// ============================================================================
// Sub-componentes
// ============================================================================

const COLOR_MAP: Record<string, string> = {
  violet: 'from-violet-600 to-violet-800 border-violet-500/30',
  emerald: 'from-emerald-600 to-emerald-800 border-emerald-500/30',
  cyan: 'from-cyan-600 to-cyan-800 border-cyan-500/30',
  amber: 'from-amber-600 to-amber-800 border-amber-500/30',
};

function StatCard({
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
    <div className={`rounded-xl border bg-gradient-to-br p-5 ${COLOR_MAP[color] || COLOR_MAP.violet}`}>
      <div className="flex items-center gap-2 mb-3 text-white/70">
        {icon}
        <span className="text-xs font-medium uppercase tracking-wider">{label}</span>
      </div>
      <p className="text-2xl font-bold text-white tabular-nums">{value}</p>
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex items-center justify-center h-64 text-gray-500 text-sm">
      {message}
    </div>
  );
}
