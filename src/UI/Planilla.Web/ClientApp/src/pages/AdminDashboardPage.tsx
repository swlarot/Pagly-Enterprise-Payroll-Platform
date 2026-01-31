import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { UsageDashboard } from '../components/UsageDashboard';
import PlanUsageCard from '../components/tenant/PlanUsageCard';
import { TenantRole } from '../types/api';

export default function AdminDashboardPage() {
  const { user, tenant, hasRole } = useAuth();

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

      {/* Main Content Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column: Usage Dashboard and Quick Actions */}
        <div className="lg:col-span-2 space-y-6">
          {/* Usage Dashboard - New Component */}
          <UsageDashboard />

          {/* Quick Actions */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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

        {/* Right Column: Plan Usage Card */}
        <div className="lg:col-span-1">
          <PlanUsageCard />
        </div>
      </div>
    </div>
  );
}
