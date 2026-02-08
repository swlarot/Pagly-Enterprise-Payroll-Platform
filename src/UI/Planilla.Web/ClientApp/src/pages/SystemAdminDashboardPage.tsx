import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { systemAdminService } from '../services/systemAdminService';
import type { SystemMetricsDto } from '../types/api';
import {
  Building2,
  Users,
  UserCheck,
  TrendingUp,
  ArrowRight,
  Loader2,
} from 'lucide-react';
import toast from 'react-hot-toast';

export default function SystemAdminDashboardPage() {
  const [metrics, setMetrics] = useState<SystemMetricsDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadMetrics();
  }, []);

  const loadMetrics = async () => {
    try {
      setIsLoading(true);
      const data = await systemAdminService.getMetrics();
      setMetrics(data);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar métricas');
    } finally {
      setIsLoading(false);
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

  const statCards = [
    {
      title: 'Total Tenants',
      value: metrics?.totalTenants || 0,
      subtitle: `${metrics?.activeTenants || 0} activos`,
      icon: Building2,
      iconBg: 'bg-primary-500/15',
      iconColor: 'text-primary-400',
    },
    {
      title: 'Total Usuarios',
      value: metrics?.totalUsers || 0,
      subtitle: 'En todos los tenants',
      icon: Users,
      iconBg: 'bg-blue-500/15',
      iconColor: 'text-blue-400',
    },
    {
      title: 'Total Empleados',
      value: metrics?.totalEmployees || 0,
      subtitle: 'Gestionados en sistema',
      icon: UserCheck,
      iconBg: 'bg-purple-500/15',
      iconColor: 'text-purple-400',
    },
  ];

  const planColors = {
    free: 'bg-gray-500',
    starter: 'bg-primary-500',
    professional: 'bg-purple-500',
    enterprise: 'bg-amber-500',
  };

  const totalPlans =
    (metrics?.planDistribution.free || 0) +
    (metrics?.planDistribution.starter || 0) +
    (metrics?.planDistribution.professional || 0) +
    (metrics?.planDistribution.enterprise || 0);

  return (
    <SystemAdminLayout>
      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-100">Dashboard del Sistema</h1>
          <p className="text-gray-400 mt-2">Vista general de métricas y estadísticas del sistema</p>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          {statCards.map((stat) => (
            <div
              key={stat.title}
              className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 px-6 py-5 flex items-center gap-4"
            >
              <div className={`w-12 h-12 ${stat.iconBg} rounded-lg flex items-center justify-center`}>
                <stat.icon className={`w-6 h-6 ${stat.iconColor}`} />
              </div>
              <div className="flex-1">
                <p className="text-sm text-gray-400">{stat.title}</p>
                <p className="text-2xl font-bold text-white">{stat.value}</p>
                <p className="text-xs text-gray-400 mt-1">{stat.subtitle}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Two Column Layout */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Plan Distribution */}
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-4 border-b border-navy-700">
              <h3 className="font-semibold text-gray-100">Distribución por Plan</h3>
            </div>
            <div className="px-6 py-4">
              <div className="space-y-4">
                {/* Free */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-300">Free</span>
                    <span className="font-medium text-white">
                      {metrics?.planDistribution.free || 0} ({totalPlans > 0 ? Math.round((metrics?.planDistribution.free || 0) / totalPlans * 100) : 0}%)
                    </span>
                  </div>
                  <div className="w-full bg-navy-700 rounded-full h-2">
                    <div
                      className={`${planColors.free} h-2 rounded-full`}
                      style={{ width: `${totalPlans > 0 ? (metrics?.planDistribution.free || 0) / totalPlans * 100 : 0}%` }}
                    />
                  </div>
                </div>

                {/* Starter */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-300">Starter</span>
                    <span className="font-medium text-white">
                      {metrics?.planDistribution.starter || 0} ({totalPlans > 0 ? Math.round((metrics?.planDistribution.starter || 0) / totalPlans * 100) : 0}%)
                    </span>
                  </div>
                  <div className="w-full bg-navy-700 rounded-full h-2">
                    <div
                      className={`${planColors.starter} h-2 rounded-full`}
                      style={{ width: `${totalPlans > 0 ? (metrics?.planDistribution.starter || 0) / totalPlans * 100 : 0}%` }}
                    />
                  </div>
                </div>

                {/* Professional */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-300">Professional</span>
                    <span className="font-medium text-white">
                      {metrics?.planDistribution.professional || 0} ({totalPlans > 0 ? Math.round((metrics?.planDistribution.professional || 0) / totalPlans * 100) : 0}%)
                    </span>
                  </div>
                  <div className="w-full bg-navy-700 rounded-full h-2">
                    <div
                      className={`${planColors.professional} h-2 rounded-full`}
                      style={{ width: `${totalPlans > 0 ? (metrics?.planDistribution.professional || 0) / totalPlans * 100 : 0}%` }}
                    />
                  </div>
                </div>

                {/* Enterprise */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-300">Enterprise</span>
                    <span className="font-medium text-white">
                      {metrics?.planDistribution.enterprise || 0} ({totalPlans > 0 ? Math.round((metrics?.planDistribution.enterprise || 0) / totalPlans * 100) : 0}%)
                    </span>
                  </div>
                  <div className="w-full bg-navy-700 rounded-full h-2">
                    <div
                      className={`${planColors.enterprise} h-2 rounded-full`}
                      style={{ width: `${totalPlans > 0 ? (metrics?.planDistribution.enterprise || 0) / totalPlans * 100 : 0}%` }}
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Growth Metrics */}
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-4 border-b border-navy-700">
              <h3 className="font-semibold text-gray-100">Crecimiento Reciente</h3>
            </div>
            <div className="px-6 py-4">
              <div className="space-y-6">
                {/* Last 7 Days */}
                <div>
                  <div className="flex items-center gap-2 mb-3">
                    <TrendingUp className="w-5 h-5 text-green-400" />
                    <h4 className="font-medium text-gray-100">Últimos 7 días</h4>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="bg-navy-800 border border-navy-700 rounded-lg p-4">
                      <p className="text-sm text-gray-400 mb-1">Nuevos Tenants</p>
                      <p className="text-2xl font-bold text-white">
                        {metrics?.recentGrowth.last7Days.newTenants || 0}
                      </p>
                    </div>
                    <div className="bg-navy-800 border border-navy-700 rounded-lg p-4">
                      <p className="text-sm text-gray-400 mb-1">Nuevos Usuarios</p>
                      <p className="text-2xl font-bold text-white">
                        {metrics?.recentGrowth.last7Days.newUsers || 0}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Last 30 Days */}
                <div>
                  <div className="flex items-center gap-2 mb-3">
                    <TrendingUp className="w-5 h-5 text-purple-400" />
                    <h4 className="font-medium text-gray-100">Últimos 30 días</h4>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="bg-navy-800 border border-navy-700 rounded-lg p-4">
                      <p className="text-sm text-gray-400 mb-1">Nuevos Tenants</p>
                      <p className="text-2xl font-bold text-white">
                        {metrics?.recentGrowth.last30Days.newTenants || 0}
                      </p>
                    </div>
                    <div className="bg-navy-800 border border-navy-700 rounded-lg p-4">
                      <p className="text-sm text-gray-400 mb-1">Nuevos Usuarios</p>
                      <p className="text-2xl font-bold text-white">
                        {metrics?.recentGrowth.last30Days.newUsers || 0}
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Quick Actions */}
        <div className="mt-8">
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-4 border-b border-navy-700">
              <h3 className="font-semibold text-gray-100">Acciones Rápidas</h3>
            </div>
            <div className="px-6 py-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Link
                  to="/system-admin/tenants"
                  className="flex items-center justify-between p-4 bg-navy-800 border border-navy-700 rounded-lg hover:bg-navy-700 transition-colors group"
                >
                  <div className="flex items-center gap-3">
                    <Building2 className="w-5 h-5 text-primary-400" />
                    <div>
                      <p className="font-medium text-gray-100">Ver Todos los Tenants</p>
                      <p className="text-sm text-gray-400">Gestionar empresas del sistema</p>
                    </div>
                  </div>
                  <ArrowRight className="w-5 h-5 text-primary-400 group-hover:translate-x-1 transition-transform" />
                </Link>

                <Link
                  to="/system-admin/tenants/create"
                  className="flex items-center justify-between p-4 bg-navy-800 border border-navy-700 rounded-lg hover:bg-navy-700 transition-colors group"
                >
                  <div className="flex items-center gap-3">
                    <Building2 className="w-5 h-5 text-green-400" />
                    <div>
                      <p className="font-medium text-gray-100">Crear Nuevo Tenant</p>
                      <p className="text-sm text-gray-400">Agregar nueva empresa al sistema</p>
                    </div>
                  </div>
                  <ArrowRight className="w-5 h-5 text-green-400 group-hover:translate-x-1 transition-transform" />
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </SystemAdminLayout>
  );
}
