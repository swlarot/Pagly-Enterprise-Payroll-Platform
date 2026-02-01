import React, { useEffect, useState } from 'react';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { RoleCard } from '../components/roles/RoleCard';
import { RoleFormModal } from '../components/roles/RoleFormModal';
import { RolePermissionsModal } from '../components/roles/RolePermissionsModal';
import ConfirmModal from '../components/ConfirmModal';
import { roleService } from '../services/roleService';
import toast from 'react-hot-toast';
import { Plus, Loader2, Shield, AlertCircle } from 'lucide-react';
import type { CustomTenantRoleDto } from '../types/api';

export default function RolesPage() {
  const [roles, setRoles] = useState<CustomTenantRoleDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editingRole, setEditingRole] = useState<CustomTenantRoleDto | null>(null);
  const [permissionsRole, setPermissionsRole] = useState<CustomTenantRoleDto | null>(null);
  const [deletingRole, setDeletingRole] = useState<CustomTenantRoleDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    loadRoles();
  }, []);

  const loadRoles = async () => {
    try {
      setIsLoading(true);
      const data = await roleService.getRoles();
      setRoles(data);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar roles');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!deletingRole) return;

    if (deletingRole.userCount > 0) {
      toast.error('No puedes eliminar un rol que tiene usuarios asignados');
      setDeletingRole(null);
      return;
    }

    try {
      setIsDeleting(true);
      await roleService.deleteRole(deletingRole.id);
      toast.success('Rol eliminado exitosamente');
      await loadRoles();
      setDeletingRole(null);
    } catch (error: any) {
      toast.error(error.message || 'Error al eliminar rol');
    } finally {
      setIsDeleting(false);
    }
  };

  // Separar roles del sistema y personalizados
  const systemRoles = roles.filter((r) => r.isSystem);
  const customRoles = roles.filter((r) => !r.isSystem);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 flex items-center gap-3">
            <Shield className="w-8 h-8 text-blue-600" />
            Roles y Permisos
          </h1>
          <p className="text-gray-600 mt-2">
            Gestiona los roles y permisos de tu organización
          </p>
        </div>
        <Button icon={Plus} onClick={() => setShowCreateModal(true)}>
          Crear Rol Personalizado
        </Button>
      </div>

      {/* Info Banner */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 flex items-start gap-3">
        <AlertCircle className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
        <div className="flex-1">
          <h3 className="font-medium text-blue-900 mb-1">Sobre los roles</h3>
          <p className="text-sm text-blue-800">
            Los roles del sistema no pueden ser editados o eliminados. Puedes crear roles personalizados
            con combinaciones específicas de permisos para adaptarse a tu estructura organizacional.
          </p>
        </div>
      </div>

      {/* Roles del Sistema */}
      <Card>
        <CardHeader>
          <h2 className="text-lg font-semibold text-gray-900">Roles del Sistema</h2>
        </CardHeader>
        <CardBody>
          {systemRoles.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No hay roles del sistema</div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {systemRoles.map((role) => (
                <RoleCard
                  key={role.id}
                  role={role}
                  onEditPermissions={setPermissionsRole}
                  onEdit={setEditingRole}
                  onDelete={setDeletingRole}
                />
              ))}
            </div>
          )}
        </CardBody>
      </Card>

      {/* Roles Personalizados */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-gray-900">Roles Personalizados</h2>
            <span className="text-sm text-gray-600">{customRoles.length} roles</span>
          </div>
        </CardHeader>
        <CardBody>
          {customRoles.length === 0 ? (
            <div className="text-center py-12">
              <Shield className="w-12 h-12 text-gray-300 mx-auto mb-4" />
              <h3 className="text-lg font-medium text-gray-900 mb-2">
                No hay roles personalizados
              </h3>
              <p className="text-gray-600 mb-4">
                Crea roles personalizados con permisos específicos para tu organización
              </p>
              <Button icon={Plus} onClick={() => setShowCreateModal(true)}>
                Crear Primer Rol
              </Button>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {customRoles.map((role) => (
                <RoleCard
                  key={role.id}
                  role={role}
                  onEditPermissions={setPermissionsRole}
                  onEdit={setEditingRole}
                  onDelete={setDeletingRole}
                />
              ))}
            </div>
          )}
        </CardBody>
      </Card>

      {/* Modals */}
      <RoleFormModal
        role={null}
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSuccess={() => {
          loadRoles();
          setShowCreateModal(false);
        }}
      />

      <RoleFormModal
        role={editingRole}
        isOpen={!!editingRole}
        onClose={() => setEditingRole(null)}
        onSuccess={() => {
          loadRoles();
          setEditingRole(null);
        }}
      />

      <RolePermissionsModal
        role={permissionsRole}
        isOpen={!!permissionsRole}
        onClose={() => setPermissionsRole(null)}
        onSuccess={() => {
          loadRoles();
          setPermissionsRole(null);
        }}
      />

      <ConfirmModal
        isOpen={!!deletingRole}
        onClose={() => setDeletingRole(null)}
        onConfirm={handleDelete}
        title="Eliminar Rol"
        message={
          deletingRole
            ? `¿Estás seguro de que deseas eliminar el rol "${deletingRole.name}"? Esta acción no se puede deshacer.`
            : ''
        }
        confirmText="Eliminar"
        isLoading={isDeleting}
      />
    </div>
  );
}
