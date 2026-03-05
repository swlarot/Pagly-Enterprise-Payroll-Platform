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
      case TenantRole.User:
        return {
          color: 'bg-blue-100 text-blue-800',
          label: 'User',
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
