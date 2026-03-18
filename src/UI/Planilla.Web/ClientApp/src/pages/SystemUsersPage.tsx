import React, { useEffect, useState } from 'react';
import { useAsyncLoad } from '../hooks/useAsyncLoad';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Modal } from '../components/ui/Modal';
import { DataTable } from '../components/ui/DataTable';
import type { Column } from '../components/ui/DataTable';
import { systemAdminService } from '../services/systemAdminService';
import { UserPlus, Mail, Phone, Loader2, Trash2, Building2, Pencil } from 'lucide-react';
import toast from 'react-hot-toast';
import type { SystemUserViewModel } from '../types/api';

export default function SystemUsersPage() {
  const [users, setUsers] = useState<SystemUserViewModel[]>([]);
  const { isLoading, run } = useAsyncLoad();
  const [showModal, setShowModal] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [deletingUserId, setDeletingUserId] = useState<string | null>(null);
  const [formData, setFormData] = useState({
    nombre: '',
    apellido: '',
    correo: '',
    telefono: ''
  });
  const [editingUser, setEditingUser] = useState<SystemUserViewModel | null>(null);
  const [editFormData, setEditFormData] = useState({
    nombreCompleto: '',
    email: '',
    telefono: ''
  });

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = () => run(async () => {
    const response = await systemAdminService.getAllSystemUsers();

    // Mapear SystemUserDto a interfaz local User
    const mappedUsers = (response.data || []).map((u: any) => ({
      id: u.userId,
      nombreCompleto: u.fullName,
      email: u.email,
      telefono: u.telefono || undefined,
      isSystemAdmin: u.isSystemAdmin,
      emailConfirmed: true, // Por ahora asumimos true
      tenantAssignments: (u.tenants || []).map((t: any) => ({
        tenantName: t.tenantName,
        role: t.role
      }))
    }));

    setUsers(mappedUsers);
  }, 'Error cargando usuarios');

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.nombre || !formData.apellido || !formData.correo) {
      toast.error('Por favor completa todos los campos requeridos');
      return;
    }

    try {
      setIsSubmitting(true);
      await systemAdminService.createUser({
        nombre: formData.nombre,
        apellido: formData.apellido,
        correo: formData.correo,
        telefono: formData.telefono || undefined
      });

      toast.success('Usuario creado exitosamente. Email de invitación enviado.');
      setShowModal(false);
      setFormData({ nombre: '', apellido: '', correo: '', telefono: '' });
      loadUsers();
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Error creando usuario';
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancel = () => {
    setShowModal(false);
    setFormData({ nombre: '', apellido: '', correo: '', telefono: '' });
  };

  const handleOpenEdit = (user: SystemUserViewModel) => {
    setEditingUser(user);
    setEditFormData({
      nombreCompleto: user.nombreCompleto,
      email: user.email,
      telefono: user.telefono || ''
    });
  };

  const handleEditCancel = () => {
    setEditingUser(null);
    setEditFormData({ nombreCompleto: '', email: '', telefono: '' });
  };

  const handleEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;

    if (!editFormData.nombreCompleto.trim()) {
      toast.error('El nombre completo es requerido');
      return;
    }

    try {
      setIsSubmitting(true);
      await systemAdminService.updateUser(editingUser.id, {
        nombreCompleto: editFormData.nombreCompleto.trim(),
        email: editFormData.email.trim() || undefined,
        telefono: editFormData.telefono.trim() || undefined
      });
      toast.success('Usuario actualizado exitosamente');
      handleEditCancel();
      loadUsers();
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Error actualizando usuario';
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteUser = async (userId: string, userName: string) => {
    if (!confirm(`¿Estás seguro de eliminar permanentemente al usuario "${userName}"?\n\nEsta acción no se puede deshacer.`)) {
      return;
    }

    try {
      setDeletingUserId(userId);
      await systemAdminService.deleteUser(userId);
      toast.success('Usuario eliminado permanentemente');
      loadUsers();
    } catch (error: unknown) {
      // Manejar errores específicos del backend
      const errObj = error as { statusCode?: number; message?: string; details?: { activeTenants?: { tenantName: string; role: string }[] } };
      if (errObj.statusCode === 400 && errObj.details?.activeTenants) {
        const tenantsList = errObj.details.activeTenants
          .map((t) => `${t.tenantName} (${t.role})`)
          .join(', ');
        toast.error(
          `No se puede eliminar el usuario porque tiene membresías activas en: ${tenantsList}. Debes removerlo primero de cada tenant.`,
          { duration: 8000 }
        );
      } else {
        const message = error instanceof Error ? error.message : 'Error al eliminar usuario';
        toast.error(message);
      }
    } finally {
      setDeletingUserId(null);
    }
  };

  const userColumns: Column<SystemUserViewModel>[] = [
    { key: 'nombreCompleto', header: 'Usuario', render: (u) => (
      <div>
        <div className="font-medium text-gray-100">{u.nombreCompleto}</div>
        <div className="text-gray-400 flex items-center gap-1"><Mail className="w-3 h-3" />{u.email}</div>
      </div>
    )},
    { key: 'telefono', header: 'Teléfono', render: (u) =>
      u.telefono ? <span className="text-gray-300 flex items-center gap-1"><Phone className="w-3 h-3 text-gray-500" />{u.telefono}</span> : <span className="text-gray-500">N/A</span>
    },
    { key: 'tenants', header: 'Tenants Asignados', render: (u) => (
      <div className="flex flex-wrap gap-1">
        {u.tenantAssignments && u.tenantAssignments.length > 0
          ? u.tenantAssignments.map((ta, i) => (
              <span key={i} className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                <Building2 className="w-3 h-3" />{ta.tenantName} ({ta.role})
              </span>
            ))
          : <span className="text-xs text-gray-500">Sin asignar</span>}
      </div>
    )},
    { key: 'estado', header: 'Estado', render: (u) => (
      <div className="flex flex-col gap-1">
        {u.isSystemAdmin && <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-red-500/15 text-red-400">System Admin</span>}
        {u.emailConfirmed
          ? <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-green-500/15 text-green-400">Verificado</span>
          : <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-amber-500/15 text-amber-400">Pendiente</span>}
      </div>
    )},
    { key: 'acciones', header: 'Acciones', render: (u) => !u.isSystemAdmin ? (
      <div className="flex items-center gap-2">
        <button
          onClick={() => handleOpenEdit(u)}
          className="inline-flex items-center justify-center p-2 rounded-lg bg-blue-500/15 hover:bg-blue-500/25 text-blue-400 transition-colors"
          title="Editar usuario"
        >
          <Pencil className="w-4 h-4" />
        </button>
        <button
          onClick={() => handleDeleteUser(u.id, u.nombreCompleto)}
          disabled={deletingUserId === u.id}
          className="inline-flex items-center justify-center p-2 rounded-lg bg-red-500/15 hover:bg-red-500/25 text-red-400 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          title="Eliminar usuario"
        >
          {deletingUserId === u.id ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />}
        </button>
      </div>
    ) : null},
  ];

  return (
    <SystemAdminLayout>
      <div className="max-w-7xl mx-auto px-6 py-8 space-y-6">
        <div className="flex justify-between items-center">
          <div>
            <h1 className="text-3xl font-bold text-gray-100">Usuarios del Sistema</h1>
            <p className="text-gray-400 mt-1">Gestiona todos los usuarios del sistema</p>
          </div>
          <Button onClick={() => setShowModal(true)} className="flex items-center gap-2">
            <UserPlus className="w-4 h-4" />
            Crear Usuario
          </Button>
        </div>

        <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
          <div className="px-6 py-4 border-b border-navy-700 bg-navy-800 rounded-t-xl">
            <h2 className="text-lg font-semibold text-gray-100">Todos los Usuarios</h2>
          </div>
          <DataTable<SystemUserViewModel>
            columns={userColumns}
            data={users}
            isLoading={isLoading}
            emptyMessage="No hay usuarios en el sistema"
            keyExtractor={(u) => u.id}
          />
        </div>
      </div>

      {/* Modal de edición */}
      <Modal isOpen={!!editingUser} onClose={handleEditCancel} title="Editar Usuario" size="sm">
            <form onSubmit={handleEdit} className="space-y-4">
              <Input
                label="Nombre Completo"
                value={editFormData.nombreCompleto}
                onChange={(e) => setEditFormData({ ...editFormData, nombreCompleto: e.target.value })}
                required
                placeholder="Juan Pérez"
              />

              <Input
                label="Correo"
                type="email"
                value={editFormData.email}
                onChange={(e) => setEditFormData({ ...editFormData, email: e.target.value })}
                placeholder="juan.perez@empresa.com"
              />

              <Input
                label="Teléfono (opcional)"
                value={editFormData.telefono}
                onChange={(e) => setEditFormData({ ...editFormData, telefono: e.target.value })}
                placeholder="+507 6000-0000"
              />

              <div className="flex gap-3 pt-4">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={handleEditCancel}
                  disabled={isSubmitting}
                  className="flex-1"
                >
                  Cancelar
                </Button>
                <Button
                  type="submit"
                  disabled={isSubmitting}
                  className="flex-1"
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                      Guardando...
                    </>
                  ) : (
                    'Guardar Cambios'
                  )}
                </Button>
              </div>
            </form>
      </Modal>

      {/* Modal de creación */}
      <Modal isOpen={showModal} onClose={handleCancel} title="Crear Nuevo Usuario" size="sm">
            <form onSubmit={handleCreate} className="space-y-4">
              <Input
                label="Nombre"
                value={formData.nombre}
                onChange={(e) => setFormData({ ...formData, nombre: e.target.value })}
                required
                placeholder="Juan"
              />

              <Input
                label="Apellido"
                value={formData.apellido}
                onChange={(e) => setFormData({ ...formData, apellido: e.target.value })}
                required
                placeholder="Pérez"
              />

              <Input
                label="Correo"
                type="email"
                value={formData.correo}
                onChange={(e) => setFormData({ ...formData, correo: e.target.value })}
                required
                placeholder="juan.perez@empresa.com"
              />

              <Input
                label="Teléfono (opcional)"
                value={formData.telefono}
                onChange={(e) => setFormData({ ...formData, telefono: e.target.value })}
                placeholder="+507 6000-0000"
              />

              <div className="bg-primary-500/15 border border-primary-500/25 rounded-lg p-3">
                <p className="text-xs text-primary-300">
                  Se enviará un email con la contraseña temporal:{' '}
                  <code className="bg-navy-800 px-2 py-1 rounded text-primary-200 font-mono">
                    Planilla2024!Temp
                  </code>
                </p>
              </div>

              <div className="flex gap-3 pt-4">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={handleCancel}
                  disabled={isSubmitting}
                  className="flex-1"
                >
                  Cancelar
                </Button>
                <Button
                  type="submit"
                  disabled={isSubmitting}
                  className="flex-1"
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                      Creando...
                    </>
                  ) : (
                    'Crear Usuario'
                  )}
                </Button>
              </div>
            </form>
      </Modal>
    </SystemAdminLayout>
  );
}
