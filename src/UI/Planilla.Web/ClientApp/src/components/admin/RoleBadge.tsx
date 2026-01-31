import React from 'react';
import { Shield } from 'lucide-react';
import { TenantRole } from '../../types/api';

interface RoleBadgeProps {
  role: TenantRole;
  showIcon?: boolean;
}

export function RoleBadge({ role, showIcon = false }: RoleBadgeProps) {
  const getRoleConfig = (role: TenantRole) => {
    switch (role) {
      case TenantRole.Owner:
        return {
          color: 'bg-purple-100 text-purple-800',
          label: 'Owner',
        };
      case TenantRole.Admin:
        return {
          color: 'bg-blue-100 text-blue-800',
          label: 'Admin',
        };
      case TenantRole.Manager:
        return {
          color: 'bg-green-100 text-green-800',
          label: 'Manager',
        };
      case TenantRole.Accountant:
        return {
          color: 'bg-amber-100 text-amber-800',
          label: 'Contador',
        };
      case TenantRole.Employee:
        return {
          color: 'bg-gray-100 text-gray-800',
          label: 'Empleado',
        };
      default:
        return {
          color: 'bg-gray-100 text-gray-800',
          label: 'Desconocido',
        };
    }
  };

  const config = getRoleConfig(role);

  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${config.color}`}
    >
      {showIcon && role === TenantRole.Owner && <Shield className="w-3 h-3" />}
      {config.label}
    </span>
  );
}
