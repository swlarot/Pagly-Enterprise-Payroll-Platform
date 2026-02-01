import React from 'react';
import type { PermissionDto } from '../../types/api';

interface PermissionCategoryCheckboxesProps {
  category: string;
  permissions: PermissionDto[];
  selectedPermissions: string[];
  onToggle: (permissionKey: string) => void;
  onSelectAll: (permissionKeys: string[]) => void;
}

export function PermissionCategoryCheckboxes({
  category,
  permissions,
  selectedPermissions,
  onToggle,
  onSelectAll,
}: PermissionCategoryCheckboxesProps) {
  const allSelected = permissions.every((p) => selectedPermissions.includes(p.key));
  const someSelected = permissions.some((p) => selectedPermissions.includes(p.key));

  const handleSelectAll = () => {
    if (allSelected) {
      // Deseleccionar todos
      permissions.forEach((p) => {
        if (selectedPermissions.includes(p.key)) {
          onToggle(p.key);
        }
      });
    } else {
      // Seleccionar todos
      onSelectAll(permissions.map((p) => p.key));
    }
  };

  return (
    <div className="border border-gray-200 rounded-lg p-4">
      <div className="flex items-center justify-between mb-3">
        <h4 className="font-semibold text-gray-900">{category}</h4>
        <button
          type="button"
          onClick={handleSelectAll}
          className="text-sm text-blue-600 hover:text-blue-700 font-medium"
        >
          {allSelected ? 'Deseleccionar todos' : 'Seleccionar todos'}
        </button>
      </div>
      <div className="space-y-2">
        {permissions.map((permission) => (
          <label
            key={permission.key}
            className="flex items-start gap-3 cursor-pointer hover:bg-gray-50 p-2 rounded transition-colors"
          >
            <input
              type="checkbox"
              checked={selectedPermissions.includes(permission.key)}
              onChange={() => onToggle(permission.key)}
              className="mt-1 w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
            />
            <div className="flex-1">
              <div className="font-medium text-sm text-gray-900">{permission.name}</div>
              <div className="text-xs text-gray-600">{permission.description}</div>
            </div>
          </label>
        ))}
      </div>
    </div>
  );
}
