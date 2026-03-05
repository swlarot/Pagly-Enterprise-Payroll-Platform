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
    <div className="border border-navy-700 rounded-lg p-4">
      <div className="flex items-center justify-between mb-3">
        <h4 className="font-semibold text-gray-100">{category}</h4>
        <button
          type="button"
          onClick={handleSelectAll}
          className="text-sm text-primary-400 hover:text-primary-300 font-medium"
        >
          {allSelected ? 'Deseleccionar todos' : 'Seleccionar todos'}
        </button>
      </div>
      <div className="space-y-2">
        {permissions.map((permission) => (
          <label
            key={permission.key}
            className="flex items-start gap-3 cursor-pointer hover:bg-navy-800 p-2 rounded transition-colors"
          >
            <input
              type="checkbox"
              checked={selectedPermissions.includes(permission.key)}
              onChange={() => onToggle(permission.key)}
              className="mt-1 w-4 h-4 text-primary-400 border-navy-600 rounded focus:ring-primary-500"
            />
            <div className="flex-1">
              <div className="font-medium text-sm text-gray-100">{permission.name}</div>
              <div className="text-xs text-gray-400">{permission.description}</div>
            </div>
          </label>
        ))}
      </div>
    </div>
  );
}
