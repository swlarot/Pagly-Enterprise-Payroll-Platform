import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Users, UserPlus, TrendingUp, AlertCircle, Check, Lock, ArrowUpCircle } from 'lucide-react';
import { subscriptionService } from '../services/subscriptionService';
import { useAuth } from '../contexts/AuthContext';
import type { SubscriptionUsageDto } from '../types/api';

export function UsageDashboard() {
  const { subscription } = useAuth();
  const [usage, setUsage] = useState<SubscriptionUsageDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadUsage();
  }, []);

  const loadUsage = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await subscriptionService.getUsage();
      setUsage(data);
    } catch (err: any) {
      setError(err.message || 'Error al cargar el uso');
    } finally {
      setLoading(false);
    }
  };

  const getUsageColor = (percentage: number): string => {
    if (percentage >= 90) return 'red';
    if (percentage >= 70) return 'yellow';
    return 'green';
  };

  const getProgressBarClass = (color: string): string => {
    const classes = {
      green: 'bg-primary-500',
      yellow: 'bg-amber-500',
      red: 'bg-red-500',
    };
    return classes[color as keyof typeof classes] || classes.green;
  };

  const getTextClass = (color: string): string => {
    const classes = {
      green: 'text-green-400',
      yellow: 'text-amber-400',
      red: 'text-red-400',
    };
    return classes[color as keyof typeof classes] || classes.green;
  };

  if (loading) {
    return (
      <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
        <div className="animate-pulse">
          <div className="h-6 bg-navy-800 rounded w-1/3 mb-4"></div>
          <div className="space-y-4">
            <div className="h-20 bg-navy-800 rounded"></div>
            <div className="h-20 bg-navy-800 rounded"></div>
            <div className="h-20 bg-navy-800 rounded"></div>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-red-500/20 p-6">
        <div className="flex items-center gap-3 text-red-400 mb-2">
          <AlertCircle className="w-5 h-5" />
          <h3 className="font-semibold">Error al cargar el uso</h3>
        </div>
        <p className="text-red-400 text-sm">{error}</p>
      </div>
    );
  }

  if (!usage || !subscription) {
    return null;
  }

  const employeesPercentage = usage.employeePercentage;
  const usersPercentage = usage.userPercentage;

  const employeesColor = getUsageColor(employeesPercentage);
  const usersColor = getUsageColor(usersPercentage);

  const shouldShowUpgrade = employeesPercentage >= 70 || usersPercentage >= 70;

  return (
    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700">
      {/* Header */}
      <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-primary-500/10 rounded-lg flex items-center justify-center">
            <TrendingUp className="w-5 h-5 text-primary-400" />
          </div>
          <div>
            <h3 className="font-semibold text-gray-100">Uso del Plan</h3>
            <p className="text-sm text-gray-400">
              Plan {subscription.planName}
            </p>
          </div>
        </div>
        {shouldShowUpgrade && (
          <Link
            to="/billing"
            className="flex items-center gap-2 px-4 py-2 bg-primary-600 text-white text-sm font-medium rounded-lg hover:bg-primary-700 transition-colors"
          >
            <ArrowUpCircle className="w-4 h-4" />
            Actualizar Plan
          </Link>
        )}
      </div>

      {/* Usage Stats */}
      <div className="p-6 space-y-6">
        {/* Employees */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2">
              <Users className="w-5 h-5 text-gray-400" />
              <span className="font-medium text-gray-300">Empleados</span>
            </div>
            <span className={`text-sm font-semibold ${getTextClass(employeesColor)}`}>
              {usage.employeeCount} / {usage.employeeLimit === Number.MAX_VALUE ? '∞' : usage.employeeLimit}
              {' '}({Math.round(employeesPercentage)}%)
            </span>
          </div>
          <div className="w-full bg-navy-800 rounded-full h-2 overflow-hidden">
            <div
              className={`h-full transition-all duration-300 ${getProgressBarClass(employeesColor)}`}
              style={{ width: `${employeesPercentage}%` }}
            />
          </div>
          {employeesPercentage >= 90 && (
            <div className={`mt-2 flex items-center gap-2 text-sm ${getTextClass(employeesColor)}`}>
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              <span>Has alcanzado el {Math.round(employeesPercentage)}% del límite de empleados</span>
            </div>
          )}
        </div>

        {/* Users */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2">
              <UserPlus className="w-5 h-5 text-gray-400" />
              <span className="font-medium text-gray-300">Usuarios</span>
            </div>
            <span className={`text-sm font-semibold ${getTextClass(usersColor)}`}>
              {usage.userCount} / {usage.userLimit === Number.MAX_VALUE ? '∞' : usage.userLimit}
              {' '}({Math.round(usersPercentage)}%)
            </span>
          </div>
          <div className="w-full bg-navy-800 rounded-full h-2 overflow-hidden">
            <div
              className={`h-full transition-all duration-300 ${getProgressBarClass(usersColor)}`}
              style={{ width: `${usersPercentage}%` }}
            />
          </div>
          {usersPercentage >= 90 && (
            <div className={`mt-2 flex items-center gap-2 text-sm ${getTextClass(usersColor)}`}>
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              <span>Has alcanzado el {Math.round(usersPercentage)}% del límite de usuarios</span>
            </div>
          )}
        </div>

        {/* Features */}
        <div className="pt-4 border-t border-navy-700">
          <h4 className="font-medium text-gray-300 mb-3">Funciones Disponibles</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="flex items-center gap-2">
              {subscription.canExportExcel ? (
                <Check className="w-5 h-5 text-green-400 flex-shrink-0" />
              ) : (
                <Lock className="w-5 h-5 text-gray-600 flex-shrink-0" />
              )}
              <span className={usage.canExportExcel ? 'text-gray-300' : 'text-gray-500'}>
                Exportar a Excel
              </span>
            </div>
            <div className="flex items-center gap-2">
              {usage.canExportPdf ? (
                <Check className="w-5 h-5 text-green-400 flex-shrink-0" />
              ) : (
                <Lock className="w-5 h-5 text-gray-600 flex-shrink-0" />
              )}
              <span className={usage.canExportPdf ? 'text-gray-300' : 'text-gray-500'}>
                Exportar a PDF
              </span>
            </div>
            <div className="flex items-center gap-2">
              {usage.canUseApi ? (
                <Check className="w-5 h-5 text-green-400 flex-shrink-0" />
              ) : (
                <Lock className="w-5 h-5 text-gray-600 flex-shrink-0" />
              )}
              <span className={usage.canUseApi ? 'text-gray-300' : 'text-gray-500'}>
                Acceso API
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
