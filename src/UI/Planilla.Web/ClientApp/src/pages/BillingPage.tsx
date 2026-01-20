import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { subscriptionService } from '../services/subscriptionService';
import toast from 'react-hot-toast';

export default function BillingPage() {
  const { subscription } = useAuth();
  const [isRedirecting, setIsRedirecting] = useState(false);

  const handleManageSubscription = async () => {
    setIsRedirecting(true);

    try {
      const url = await subscriptionService.getBillingPortalUrl();
      window.location.href = url;
    } catch (error: any) {
      toast.error(error.message || 'Error al redirigir al portal de facturación');
      setIsRedirecting(false);
    }
  };

  const planFeatures: Record<string, string[]> = {
    Free: [
      '5 empleados',
      '1 usuario',
      '1 empresa',
      'Reportes básicos',
      'Retención de 90 días',
    ],
    Starter: [
      '25 empleados',
      '3 usuarios',
      '1 empresa',
      'Exportar a Excel',
      'Notificaciones por email',
      'Retención de 1 año',
    ],
    Professional: [
      '100 empleados',
      '10 usuarios',
      '3 empresas',
      'Exportar a Excel y PDF',
      'Acceso API',
      'Notificaciones por email',
      'Registro de auditoría',
      'Retención de 2 años',
    ],
    Enterprise: [
      'Empleados ilimitados',
      'Usuarios ilimitados',
      'Empresas ilimitadas',
      'Exportar a Excel y PDF',
      'Acceso API completo',
      'Notificaciones por email',
      'Registro de auditoría',
      'Retención ilimitada',
      'Soporte prioritario',
      'Personalización',
    ],
  };

  const currentPlanFeatures = subscription ? planFeatures[subscription.planName] || [] : [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Facturación y Suscripción</h1>
        <p className="text-gray-600 mt-2">Administra tu plan y métodos de pago</p>
      </div>

      {/* Current Plan Card */}
      <div className="bg-gradient-to-br from-blue-500 to-blue-600 rounded-xl shadow-lg p-8 text-white">
        <div className="flex items-center justify-between mb-6">
          <div>
            <p className="text-sm opacity-90 mb-1">Plan Actual</p>
            <h2 className="text-4xl font-bold">{subscription?.planName}</h2>
          </div>
          <div className="text-right">
            <p className="text-sm opacity-90 mb-1">Precio Mensual</p>
            <p className="text-3xl font-bold">${subscription?.monthlyPrice.toFixed(2)}</p>
          </div>
        </div>

        <div className="flex items-center gap-3 mb-6">
          <span
            className={`px-3 py-1 rounded-full text-sm font-medium ${
              subscription?.status === 0
                ? 'bg-green-500/30 text-white'
                : subscription?.status === 1
                ? 'bg-blue-500/30 text-white'
                : 'bg-red-500/30 text-white'
            }`}
          >
            {subscription?.statusName}
          </span>
          {subscription?.status === 1 && subscription.trialEndsAt && (
            <p className="text-sm">
              Prueba termina: {new Date(subscription.trialEndsAt).toLocaleDateString('es-PA')}
            </p>
          )}
        </div>

        <button
          onClick={handleManageSubscription}
          disabled={isRedirecting}
          className="w-full bg-white text-blue-600 px-6 py-3 rounded-lg font-medium hover:bg-blue-50 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
        >
          {isRedirecting ? (
            <>
              <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600"></div>
              Redirigiendo...
            </>
          ) : (
            <>
              <svg
                className="w-5 h-5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"
                />
              </svg>
              Gestionar Suscripción en Stripe
            </>
          )}
        </button>
      </div>

      {/* Plan Features */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          Características de tu Plan
        </h3>
        <ul className="space-y-3">
          {currentPlanFeatures.map((feature, index) => (
            <li key={index} className="flex items-center gap-3">
              <svg
                className="w-5 h-5 text-green-500 flex-shrink-0"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M5 13l4 4L19 7"
                />
              </svg>
              <span className="text-gray-700">{feature}</span>
            </li>
          ))}
        </ul>
      </div>

      {/* Info Card */}
      <div className="bg-blue-50 rounded-xl border border-blue-200 p-6">
        <div className="flex gap-4">
          <div className="flex-shrink-0">
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
                d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
          </div>
          <div>
            <h4 className="font-semibold text-gray-900 mb-1">
              Portal de Facturación de Stripe
            </h4>
            <p className="text-sm text-gray-600">
              Al hacer clic en "Gestionar Suscripción", serás redirigido al portal seguro de
              Stripe donde podrás:
            </p>
            <ul className="text-sm text-gray-600 mt-2 space-y-1 list-disc list-inside">
              <li>Actualizar tu plan</li>
              <li>Cambiar método de pago</li>
              <li>Ver historial de facturas</li>
              <li>Cancelar tu suscripción</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
