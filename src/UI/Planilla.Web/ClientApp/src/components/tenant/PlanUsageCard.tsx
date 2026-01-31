import { useState, useEffect } from 'react';
import {
  Users,
  UserCheck,
  Building2,
  TrendingUp,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Crown,
  Loader2
} from 'lucide-react';
import { tenantService } from '../../services/tenantService';
import { PlanUsageDto } from '../../types/api';
import toast from 'react-hot-toast';

export default function PlanUsageCard() {
  const [planUsage, setPlanUsage] = useState<PlanUsageDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadPlanUsage();
  }, []);

  const loadPlanUsage = async () => {
    try {
      setIsLoading(true);
      const data = await tenantService.getPlanUsage();
      setPlanUsage(data);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar información del plan');
      console.error('Error loading plan usage:', error);
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) {
    return (
      <div className="bg-white rounded-lg shadow p-6">
        <div className="flex items-center justify-center h-48">
          <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
        </div>
      </div>
    );
  }

  if (!planUsage) {
    return (
      <div className="bg-white rounded-lg shadow p-6">
        <div className="text-center py-8 text-gray-500">
          No se pudo cargar la información del plan
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg shadow-lg overflow-hidden">
      {/* Header */}
      <div className="bg-gradient-to-r from-blue-600 to-blue-700 text-white p-6">
        <div className="flex justify-between items-start">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <Crown className="w-6 h-6" />
              <h3 className="text-2xl font-bold">{planUsage.planDisplayName}</h3>
            </div>
            <p className="text-blue-100">
              ${planUsage.monthlyPrice.toFixed(2)}/mes
            </p>
          </div>
          {planUsage.shouldUpgrade && (
            <button
              className="bg-yellow-500 hover:bg-yellow-600 text-white px-4 py-2 rounded-lg flex items-center gap-2 transition-colors"
              onClick={() => {
                // TODO: Navegar a página de planes
                toast.success('Próximamente: Portal de actualización de planes');
              }}
            >
              <TrendingUp className="w-4 h-4" />
              Actualizar Plan
            </button>
          )}
        </div>
      </div>

      {/* Upgrade Alert */}
      {planUsage.shouldUpgrade && planUsage.upgradeMessage && (
        <div className="bg-yellow-50 border-l-4 border-yellow-500 p-4">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-5 h-5 text-yellow-600 flex-shrink-0" />
            <p className="text-yellow-800 font-medium">{planUsage.upgradeMessage}</p>
          </div>
        </div>
      )}

      {/* Usage Stats */}
      <div className="p-6 space-y-6">
        {/* Empleados */}
        <UsageBar
          label="Empleados"
          icon={<Users className="w-5 h-5" />}
          current={planUsage.usage.activeEmployees}
          max={planUsage.limits.maxEmployees}
          percentage={planUsage.remaining.employeesPercentage}
          color={getColorClass(planUsage.remaining.employeesPercentage)}
        />

        {/* Usuarios */}
        <UsageBar
          label="Usuarios"
          icon={<UserCheck className="w-5 h-5" />}
          current={planUsage.usage.activeUsers + planUsage.usage.pendingInvitations}
          max={planUsage.limits.maxUsers}
          percentage={planUsage.remaining.usersPercentage}
          color={getColorClass(planUsage.remaining.usersPercentage)}
          subtitle={`${planUsage.usage.activeUsers} activos, ${planUsage.usage.pendingInvitations} pendientes`}
        />

        {/* Compañías */}
        <UsageBar
          label="Compañías"
          icon={<Building2 className="w-5 h-5" />}
          current={planUsage.usage.activeCompanies}
          max={planUsage.limits.maxCompanies}
          percentage={planUsage.remaining.companiesPercentage}
          color={getColorClass(planUsage.remaining.companiesPercentage)}
        />
      </div>

      {/* Features */}
      <div className="border-t border-gray-200 p-6">
        <h4 className="font-semibold text-gray-900 mb-4">Características del Plan</h4>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          {planUsage.features.map((feature) => (
            <div
              key={feature.featureName}
              className="flex items-start gap-2"
              title={feature.description}
            >
              {feature.isAvailable ? (
                <CheckCircle className="w-5 h-5 text-green-600 flex-shrink-0 mt-0.5" />
              ) : (
                <XCircle className="w-5 h-5 text-gray-400 flex-shrink-0 mt-0.5" />
              )}
              <span className={feature.isAvailable ? 'text-gray-900' : 'text-gray-500 line-through'}>
                {feature.featureName}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

interface UsageBarProps {
  label: string;
  icon: React.ReactNode;
  current: number;
  max: number;
  percentage: number;
  color: string;
  subtitle?: string;
}

function UsageBar({ label, icon, current, max, percentage, color, subtitle }: UsageBarProps) {
  const isUnlimited = max === 2147483647; // int.MaxValue
  const displayMax = isUnlimited ? '∞' : max.toLocaleString();
  const displayPercentage = isUnlimited ? 0 : percentage;

  return (
    <div>
      <div className="flex justify-between items-center mb-2">
        <div className="flex items-center gap-2">
          <span className="text-gray-700">{icon}</span>
          <span className="font-medium text-gray-900">{label}</span>
        </div>
        <div className="text-sm font-semibold text-gray-900">
          {current.toLocaleString()} / {displayMax}
        </div>
      </div>

      {subtitle && (
        <div className="text-xs text-gray-500 mb-1">{subtitle}</div>
      )}

      <div className="w-full bg-gray-200 rounded-full h-2.5 overflow-hidden">
        <div
          className={`h-full rounded-full transition-all duration-300 ${color}`}
          style={{ width: `${Math.min(displayPercentage, 100)}%` }}
        />
      </div>

      <div className="flex justify-between items-center mt-1">
        <div className="text-xs text-gray-500">
          {isUnlimited ? 'Ilimitado' : `${(max - current).toLocaleString()} disponibles`}
        </div>
        <div className={`text-xs font-medium ${getTextColor(displayPercentage)}`}>
          {displayPercentage.toFixed(0)}%
        </div>
      </div>
    </div>
  );
}

function getColorClass(percentage: number): string {
  if (percentage >= 90) return 'bg-red-600';
  if (percentage >= 75) return 'bg-yellow-500';
  if (percentage >= 50) return 'bg-blue-500';
  return 'bg-green-500';
}

function getTextColor(percentage: number): string {
  if (percentage >= 90) return 'text-red-600';
  if (percentage >= 75) return 'text-yellow-600';
  if (percentage >= 50) return 'text-blue-600';
  return 'text-green-600';
}
