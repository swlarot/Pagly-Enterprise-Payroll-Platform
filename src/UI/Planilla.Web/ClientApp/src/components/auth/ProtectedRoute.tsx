import React, { ReactNode } from 'react';
import { Navigate, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { AlertTriangle, CreditCard } from 'lucide-react';
import { SubscriptionStatus } from '../../types/api';

interface ProtectedRouteProps {
  children: ReactNode;
}

export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, subscription } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen bg-gray-50">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p className="text-gray-600">Cargando...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Check subscription status
  if (subscription?.status === SubscriptionStatus.Canceled) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-white rounded-lg shadow-lg p-6">
          <div className="flex items-center justify-center w-12 h-12 bg-red-100 rounded-full mx-auto mb-4">
            <AlertTriangle className="w-6 h-6 text-red-600" />
          </div>
          <h2 className="text-xl font-bold text-gray-900 text-center mb-2">
            Suscripción Cancelada
          </h2>
          <p className="text-gray-600 text-center mb-6">
            Tu suscripción ha sido cancelada. Para continuar usando Planilla, por favor reactiva tu suscripción.
          </p>
          <Link
            to="/billing"
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 transition-colors"
          >
            <CreditCard className="w-5 h-5" />
            Ir a Facturación
          </Link>
        </div>
      </div>
    );
  }

  if (subscription?.status === SubscriptionStatus.PastDue) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-white rounded-lg shadow-lg p-6">
          <div className="flex items-center justify-center w-12 h-12 bg-yellow-100 rounded-full mx-auto mb-4">
            <AlertTriangle className="w-6 h-6 text-yellow-600" />
          </div>
          <h2 className="text-xl font-bold text-gray-900 text-center mb-2">
            Problema con el Pago
          </h2>
          <p className="text-gray-600 text-center mb-6">
            Hay un problema con tu forma de pago. Por favor actualiza tu información de pago para continuar usando el servicio.
          </p>
          <Link
            to="/billing"
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 transition-colors"
          >
            <CreditCard className="w-5 h-5" />
            Actualizar Pago
          </Link>
        </div>
      </div>
    );
  }

  // Show trial warning if ending soon (< 3 days)
  if (subscription?.status === SubscriptionStatus.Trialing && subscription.trialEndsAt) {
    const trialEnd = new Date(subscription.trialEndsAt);
    const now = new Date();
    const daysRemaining = Math.ceil((trialEnd.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));

    if (daysRemaining <= 3 && daysRemaining > 0) {
      return (
        <>
          <div className="bg-yellow-50 border-b border-yellow-200 px-4 py-3">
            <div className="max-w-7xl mx-auto flex items-center justify-between">
              <div className="flex items-center gap-3">
                <AlertTriangle className="w-5 h-5 text-yellow-600 flex-shrink-0" />
                <p className="text-sm text-yellow-800">
                  <strong>Tu periodo de prueba termina en {daysRemaining} {daysRemaining === 1 ? 'día' : 'días'}.</strong>{' '}
                  Actualiza tu plan para continuar sin interrupciones.
                </p>
              </div>
              <Link
                to="/billing"
                className="flex-shrink-0 px-3 py-1.5 bg-yellow-600 text-white text-sm font-medium rounded-lg hover:bg-yellow-700 transition-colors"
              >
                Actualizar Plan
              </Link>
            </div>
          </div>
          {children}
        </>
      );
    }
  }

  return <>{children}</>;
}
