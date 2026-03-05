import { ReactNode } from 'react';
import { Navigate, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { AlertTriangle, CreditCard } from 'lucide-react';
import { SubscriptionStatus } from '../../types/api';

interface ProtectedRouteProps {
  children: ReactNode;
}

export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, subscription, tenant, availableTenants } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen bg-navy-950">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600 mx-auto mb-4"></div>
          <p className="text-gray-400">Cargando...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Verificar si es una ruta de system-admin
  const isSystemAdminRoute = location.pathname.startsWith('/system-admin');

  // Para rutas de system-admin, permitir acceso sin tenant (system admins no tienen tenant)
  if (isSystemAdminRoute) {
    // SystemAdminRoute manejará la verificación de permisos
    return <>{children}</>;
  }

  // Para rutas normales que requieren tenant:
  // Si el usuario tiene availableTenants pero no tenant seleccionado, redirigir a selección
  if (isAuthenticated && !tenant && (availableTenants?.length ?? 0) > 0) {
    return <Navigate to="/select-tenant" replace />;
  }

  // Si no hay tenant y no hay availableTenants, puede ser que aún esté cargando
  // En este caso, permitir que la página se renderice con su propio loading state
  // en lugar de bloquear completamente el acceso
  // Las páginas individuales manejarán el caso de tenant null

  // Check subscription status
  if (subscription?.status === SubscriptionStatus.Canceled) {
    return (
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-navy-900 border border-navy-700 rounded-lg shadow-2xl shadow-black/30 p-6">
          <div className="flex items-center justify-center w-12 h-12 bg-red-500/15 rounded-full mx-auto mb-4">
            <AlertTriangle className="w-6 h-6 text-red-400" />
          </div>
          <h2 className="text-xl font-bold text-gray-100 text-center mb-2">
            Suscripción Cancelada
          </h2>
          <p className="text-gray-400 text-center mb-6">
            Tu suscripción ha sido cancelada. Para continuar usando Pagly, por favor reactiva tu suscripción.
          </p>
          <Link
            to="/billing"
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 text-white font-medium rounded-lg hover:bg-primary-700 transition-colors"
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
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-navy-900 border border-navy-700 rounded-lg shadow-2xl shadow-black/30 p-6">
          <div className="flex items-center justify-center w-12 h-12 bg-amber-500/15 rounded-full mx-auto mb-4">
            <AlertTriangle className="w-6 h-6 text-amber-400" />
          </div>
          <h2 className="text-xl font-bold text-gray-100 text-center mb-2">
            Problema con el Pago
          </h2>
          <p className="text-gray-400 text-center mb-6">
            Hay un problema con tu forma de pago. Por favor actualiza tu información de pago para continuar usando el servicio.
          </p>
          <Link
            to="/billing"
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 text-white font-medium rounded-lg hover:bg-primary-700 transition-colors"
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
