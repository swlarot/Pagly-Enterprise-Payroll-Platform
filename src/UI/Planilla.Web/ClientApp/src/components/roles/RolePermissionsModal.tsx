import React, { useEffect, useState } from 'react';
import { Modal } from '../ui/Modal';
import { Button } from '../ui/Button';
import { PermissionCategoryCheckboxes } from './PermissionCategoryCheckboxes';
import { roleService } from '../../services/roleService';
import toast from 'react-hot-toast';
import { Loader2, Save } from 'lucide-react';
import type { CustomTenantRoleDto, PermissionDto } from '../../types/api';

interface RolePermissionsModalProps {
  role: CustomTenantRoleDto | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function RolePermissionsModal({ role, isOpen, onClose, onSuccess }: RolePermissionsModalProps) {
  const [allPermissions, setAllPermissions] = useState<PermissionDto[]>([]);
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (isOpen && role) {
      loadData();
    }
  }, [isOpen, role]);

  const loadData = async () => {
    if (!role) return;

    try {
      setIsLoading(true);
      const [permissions, rolePermissions] = await Promise.all([
        roleService.getAvailablePermissions(),
        roleService.getRolePermissions(role.id),
      ]);

      setAllPermissions(permissions);
      setSelectedPermissions(rolePermissions);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar permisos');
    } finally {
      setIsLoading(false);
    }
  };

  const handleToggle = (permissionKey: string) => {
    setSelectedPermissions((prev) =>
      prev.includes(permissionKey)
        ? prev.filter((p) => p !== permissionKey)
        : [...prev, permissionKey]
    );
  };

  const handleSelectAll = (permissionKeys: string[]) => {
    setSelectedPermissions((prev) => {
      const newSet = new Set(prev);
      permissionKeys.forEach((key) => newSet.add(key));
      return Array.from(newSet);
    });
  };

  const handleSave = async () => {
    if (!role) return;

    try {
      setIsSaving(true);
      await roleService.updateRolePermissions(role.id, selectedPermissions);
      toast.success('Permisos actualizados exitosamente');
      onSuccess();
      onClose();
    } catch (error: any) {
      toast.error(error.message || 'Error al actualizar permisos');
    } finally {
      setIsSaving(false);
    }
  };

  // Agrupar permisos por categoría
  const groupedPermissions = allPermissions.reduce(
    (acc, permission) => {
      if (!acc[permission.category]) {
        acc[permission.category] = [];
      }
      acc[permission.category].push(permission);
      return acc;
    },
    {} as Record<string, PermissionDto[]>
  );

  const categoryDisplayNames: Record<string, string> = {
    Employees: 'Empleados',
    Structure: 'Estructura Organizacional',
    Payroll: 'Planilla',
    PayrollConcepts: 'Conceptos de Nómina',
    Reports: 'Reportes',
    Configuration: 'Configuración',
    Audit: 'Auditoría',
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={role ? `Permisos de ${role.name}` : 'Permisos'}
      size="xl"
    >
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
        </div>
      ) : (
        <>
          <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
            {Object.entries(groupedPermissions).map(([category, permissions]) => (
              <PermissionCategoryCheckboxes
                key={category}
                category={categoryDisplayNames[category] || category}
                permissions={permissions}
                selectedPermissions={selectedPermissions}
                onToggle={handleToggle}
                onSelectAll={handleSelectAll}
              />
            ))}
          </div>

          <div className="flex items-center justify-between pt-6 border-t border-gray-200 mt-6">
            <div className="text-sm text-gray-600">
              {selectedPermissions.length} de {allPermissions.length} permisos seleccionados
            </div>
            <div className="flex items-center gap-3">
              <Button variant="outline" onClick={onClose} disabled={isSaving}>
                Cancelar
              </Button>
              <Button icon={Save} onClick={handleSave} loading={isSaving}>
                Guardar Permisos
              </Button>
            </div>
          </div>
        </>
      )}
    </Modal>
  );
}
