import { Shield, Users, Key, Edit, Trash2 } from 'lucide-react';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';
import type { CustomTenantRoleDto } from '../../types/api';

interface RoleCardProps {
  role: CustomTenantRoleDto;
  onEditPermissions: (role: CustomTenantRoleDto) => void;
  onEdit: (role: CustomTenantRoleDto) => void;
  onDelete: (role: CustomTenantRoleDto) => void;
}

export function RoleCard({ role, onEditPermissions, onEdit, onDelete }: RoleCardProps) {
  return (
    <div className="bg-navy-800 border border-navy-700 rounded-lg p-6 hover:shadow-lg hover:shadow-black/20 transition-shadow">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center"
            style={{ backgroundColor: (role.color || '#6B7280') + '20' }}
          >
            <Shield className="w-5 h-5" style={{ color: role.color || '#6B7280' }} />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-semibold text-gray-100">{role.name}</h3>
              {role.isSystem && (
                <Badge variant="default">
                  Sistema
                </Badge>
              )}
            </div>
            <p className="text-sm text-gray-400 mt-1">{role.description}</p>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-4 text-sm text-gray-500 mb-4">
        <div className="flex items-center gap-1">
          <Key className="w-4 h-4" />
          <span>{role.permissionCount ?? role.permissions?.length ?? 0} permisos</span>
        </div>
        <div className="flex items-center gap-1">
          <Users className="w-4 h-4" />
          <span>{role.userCount} usuarios</span>
        </div>
      </div>

      <div className="flex items-center gap-2 pt-4 border-t border-navy-700">
        {!role.isSystem ? (
          <>
            <Button
              variant="outline"
              size="sm"
              icon={Key}
              onClick={() => onEditPermissions(role)}
              className="flex-1 bg-navy-700 border-navy-600 text-gray-300 hover:bg-navy-600"
            >
              Editar Permisos
            </Button>
            <Button variant="ghost" size="sm" icon={Edit} onClick={() => onEdit(role)} aria-label="Editar rol">
              {''}
            </Button>
            <Button variant="ghost" size="sm" icon={Trash2} onClick={() => onDelete(role)} aria-label="Eliminar rol">
              {''}
            </Button>
          </>
        ) : (
          <div className="flex-1 text-center text-sm text-gray-500 py-2">
            Permisos predefinidos del sistema
          </div>
        )}
      </div>
    </div>
  );
}
