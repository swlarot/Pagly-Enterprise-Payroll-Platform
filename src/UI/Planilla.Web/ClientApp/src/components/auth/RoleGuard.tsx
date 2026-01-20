import React, { ReactNode } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import type { TenantRole } from '../../types/api';

interface RoleGuardProps {
  allowedRoles: TenantRole[];
  children: ReactNode;
}

export function RoleGuard({ allowedRoles, children }: RoleGuardProps) {
  const { hasRole } = useAuth();

  if (!hasRole(...allowedRoles)) {
    return (
      <div className="flex items-center justify-center h-screen bg-gray-50">
        <div className="max-w-md p-8 bg-white rounded-xl shadow-lg text-center">
          <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg
              className="w-8 h-8 text-red-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Acceso Denegado</h2>
          <p className="text-gray-600 mb-6">
            No tienes los permisos necesarios para acceder a este recurso.
          </p>
          <a
            href="/"
            className="inline-block px-6 py-3 bg-blue-600 text-white font-medium rounded-lg hover:bg-blue-700 transition-colors"
          >
            Volver al Dashboard
          </a>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
