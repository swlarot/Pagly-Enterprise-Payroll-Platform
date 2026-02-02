import React from 'react';
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
    <div className="bg-white border border-gray-200 rounded-lg p-6 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-center gap-3">
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center"
            style={{ backgroundColor: role.color + '20' }}
          >
            <Shield className="w-5 h-5" style={{ color: role.color }} />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-semibold text-gray-900">{role.name}</h3>
              {role.isSystem && (
                <Badge variant="default" size="sm">
                  Sistema
                </Badge>
              )}
            </div>
            <p className="text-sm text-gray-600 mt-1">{role.description}</p>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-4 text-sm text-gray-600 mb-4">
        <div className="flex items-center gap-1">
          <Key className="w-4 h-4" />
          <span>{role.permissionCount} permisos</span>
        </div>
        <div className="flex items-center gap-1">
          <Users className="w-4 h-4" />
          <span>{role.userCount} usuarios</span>
        </div>
      </div>

      <div className="flex items-center gap-2 pt-4 border-t border-gray-200">
        {!role.isSystem ? (
          <>
            <Button
              variant="outline"
              size="sm"
              icon={Key}
              onClick={() => onEditPermissions(role)}
              className="flex-1"
            >
              Editar Permisos
            </Button>
            <Button variant="ghost" size="sm" icon={Edit} onClick={() => onEdit(role)} />
            <Button variant="ghost" size="sm" icon={Trash2} onClick={() => onDelete(role)} />
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
