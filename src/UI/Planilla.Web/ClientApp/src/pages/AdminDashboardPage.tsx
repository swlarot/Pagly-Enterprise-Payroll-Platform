import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { tenantService } from '../services/tenantService';
import type { TenantUsageDto } from '../types/api';
import { TenantRole } from '../types/api';
import toast from 'react-hot-toast';

export default function AdminDashboardPage() {
  const { user, tenant, subscription, hasRole } = useAuth();
  const [usage, setUsage] = useState<TenantUsageDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadUsage();
  }, []);

  const loadUsage = async () => {
    try {
      const data = await tenantService.getUsage();
      setUsage(data);
    } catch (error: any) {
      toast.error('Error al cargar estadísticas');
    } finally {
      setIsLoading(false);
    }
  };

  const getStatusBadge = () => {
    if (!subscription) return null;

    const colors = {
      Active: 'bg-green-100 text-green-800',
      Trialing: 'bg-blue-100 text-blue-800',
      PastDue: 'bg-red-100 text-red-800',
      Canceled: 'bg-gray-100 text-gray-800',
      Incomplete: 'bg-yellow-100 text-yellow-800',
    };

    return (
      <span className={`px-3 py-1 rounded-full text-sm font-medium ${colors[subscription.statusName as keyof typeof colors]}`}>
        {subscription.statusName}
      </span>
    );
  };

  const getUsagePercentage = (current: number, max: number) => {
    if (max === 2147483647) return 0; // Ilimitado (int.MaxValue)
    return Math.round((current / max) * 100);
  };

  const getUsageColor = (percentage: number) => {
    if (percentage >= 90) return 'bg-red-500';
    if (percentage >= 75) return 'bg-yellow-500';
    return 'bg-blue-500';
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-gray-600 mt-2">
          Bienvenido, {user?.email} · {user?.roleName}
        </p>
      </div>

      {/* Tenant Info Card */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <h2 className="text-xl font-bold text-gray-900 mb-2">{tenant?.name}</h2>
            <div className="space-y-1 text-sm text-gray-600">
              <p>
                <strong>RUC:</strong> {tenant?.ruc}-{tenant?.dv}
              </p>
              <p>
                <strong>Subdominio:</strong> {tenant?.subdomain}.planilla.cloud
              </p>
            </div>
          </div>
          <div>{getStatusBadge()}</div>
        </div>
      </div>

      {/* Subscription Card */}
      <div className="bg-gradient-to-br from-blue-500 to-blue-600 rounded-xl shadow-lg p-6 text-white">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-lg font-semibold mb-1">Plan Actual</h3>
            <p className="text-3xl font-bold">{subscription?.planName}</p>
          </div>
          <div className="text-right">
            <p className="text-sm opacity-90">Precio Mensual</p>
            <p className="text-2xl font-bold">
              ${subscription?.monthlyPrice.toFixed(2)}
            </p>
          </div>
        </div>

        {subscription?.status === 1 && subscription.trialEndsAt && (
          <div className="bg-white/20 rounded-lg p-3 mb-4">
            <p className="text-sm font-medium">
              Período de prueba termina:{' '}
              {new Date(subscription.trialEndsAt).toLocaleDateString('es-PA')}
            </p>
          </div>
        )}

        <div className="grid grid-cols-3 gap-4 text-sm">
          <div>
            <p className="opacity-80">Exportar Excel</p>
            <p className="font-medium">
              {subscription?.canExportExcel ? 'Sí' : 'No'}
            </p>
          </div>
          <div>
            <p className="opacity-80">Exportar PDF</p>
            <p className="font-medium">
              {subscription?.canExportPdf ? 'Sí' : 'No'}
            </p>
          </div>
          <div>
            <p className="opacity-80">Acceso API</p>
            <p className="font-medium">
              {subscription?.canUseApi ? 'Sí' : 'No'}
            </p>
          </div>
        </div>

        {hasRole(TenantRole.Owner) && (
          <Link
            to="/billing"
            className="mt-4 block text-center bg-white text-blue-600 px-4 py-2 rounded-lg font-medium hover:bg-blue-50 transition-colors"
          >
            Gestionar Suscripción
          </Link>
        )}
      </div>

      {/* Usage Statistics */}
      {usage && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Employees */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-semibold text-gray-900">Empleados</h3>
              <svg
                className="w-8 h-8 text-blue-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"
                />
              </svg>
            </div>
            <div className="text-3xl font-bold text-gray-900 mb-2">
              {usage.employeesCount}
              <span className="text-gray-400 text-lg">
                {' '}
                / {usage.maxEmployees === 2147483647 ? '∞' : usage.maxEmployees}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className={`h-2 rounded-full ${getUsageColor(getUsagePercentage(usage.employeesCount, usage.maxEmployees))}`}
                style={{
                  width: `${Math.min(100, getUsagePercentage(usage.employeesCount, usage.maxEmployees))}%`,
                }}
              ></div>
            </div>
          </div>

          {/* Users */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-semibold text-gray-900">Usuarios</h3>
              <svg
                className="w-8 h-8 text-green-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"
                />
              </svg>
            </div>
            <div className="text-3xl font-bold text-gray-900 mb-2">
              {usage.usersCount}
              <span className="text-gray-400 text-lg">
                {' '}
                / {usage.maxUsers === 2147483647 ? '∞' : usage.maxUsers}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className={`h-2 rounded-full ${getUsageColor(getUsagePercentage(usage.usersCount, usage.maxUsers))}`}
                style={{
                  width: `${Math.min(100, getUsagePercentage(usage.usersCount, usage.maxUsers))}%`,
                }}
              ></div>
            </div>
          </div>

          {/* Companies */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-semibold text-gray-900">Empresas</h3>
              <svg
                className="w-8 h-8 text-purple-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
                />
              </svg>
            </div>
            <div className="text-3xl font-bold text-gray-900 mb-2">
              {usage.companiesCount}
              <span className="text-gray-400 text-lg">
                {' '}
                / {usage.maxCompanies === 2147483647 ? '∞' : usage.maxCompanies}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className={`h-2 rounded-full ${getUsageColor(getUsagePercentage(usage.companiesCount, usage.maxCompanies))}`}
                style={{
                  width: `${Math.min(100, getUsagePercentage(usage.companiesCount, usage.maxCompanies))}%`,
                }}
              ></div>
            </div>
          </div>
        </div>
      )}

      {/* Quick Actions */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {hasRole(TenantRole.Owner, TenantRole.Admin) && (
          <Link
            to="/users"
            className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow"
          >
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                <svg
                  className="w-6 h-6 text-blue-600"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"
                  />
                </svg>
              </div>
              <div>
                <h3 className="font-semibold text-gray-900">Gestionar Usuarios</h3>
                <p className="text-sm text-gray-600">Invitar y administrar</p>
              </div>
            </div>
          </Link>
        )}

        {hasRole(TenantRole.Owner, TenantRole.Admin, TenantRole.Manager, TenantRole.Accountant) && (
          <Link
            to="/audit"
            className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow"
          >
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center">
                <svg
                  className="w-6 h-6 text-purple-600"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
                  />
                </svg>
              </div>
              <div>
                <h3 className="font-semibold text-gray-900">Audit Log</h3>
                <p className="text-sm text-gray-600">Ver registro de actividades</p>
              </div>
            </div>
          </Link>
        )}

        <Link
          to="/empleados"
          className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow"
        >
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
              <svg
                className="w-6 h-6 text-green-600"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"
                />
              </svg>
            </div>
            <div>
              <h3 className="font-semibold text-gray-900">Empleados</h3>
              <p className="text-sm text-gray-600">Gestionar empleados</p>
            </div>
          </div>
        </Link>
      </div>
    </div>
  );
}
