import React, { useEffect, useState } from 'react';
import { useAsyncLoad } from '../hooks/useAsyncLoad';
import { createPortal } from 'react-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import { UserPlus, Mail, Phone, Loader2, Trash2, Building2 } from 'lucide-react';
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

  if (isLoading) {
    return (
      <SystemAdminLayout>
        <div className="flex items-center justify-center h-[calc(100vh-80px)]">
          <Loader2 className="w-8 h-8 animate-spin text-primary-400" />
        </div>
      </SystemAdminLayout>
    );
  }

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
          <div className="px-6 py-4">
            {users.length === 0 ? (
              <div className="text-center py-12">
                <UserPlus className="w-12 h-12 text-gray-500 mx-auto mb-4" />
                <p className="text-gray-400">No hay usuarios en el sistema</p>
                <Button onClick={() => setShowModal(true)} className="mt-4">
                  Crear primer usuario
                </Button>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-navy-700">
                  <thead className="bg-navy-800 rounded-lg">
                    <tr>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Usuario
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Teléfono
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Tenants Asignados
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Estado
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Acciones
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-navy-700">
                    {users.map((user) => (
                      <tr key={user.id} className="hover:bg-navy-800 transition-colors">
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex items-center">
                            <div>
                              <div className="text-sm font-medium text-gray-100">
                                {user.nombreCompleto}
                              </div>
                              <div className="text-sm text-gray-400 flex items-center gap-1">
                                <Mail className="w-3 h-3" />
                                {user.email}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="text-sm text-gray-300 flex items-center gap-1">
                            {user.telefono ? (
                              <>
                                <Phone className="w-3 h-3 text-gray-500" />
                                {user.telefono}
                              </>
                            ) : (
                              <span className="text-gray-500">N/A</span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="flex flex-wrap gap-1">
                            {user.tenantAssignments && user.tenantAssignments.length > 0 ? (
                              user.tenantAssignments.map((ta, idx) => (
                                <span
                                  key={idx}
                                  className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400"
                                >
                                  <Building2 className="w-3 h-3" />
                                  {ta.tenantName} ({ta.role})
                                </span>
                              ))
                            ) : (
                              <span className="text-xs text-gray-500">Sin asignar</span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex flex-col gap-1">
                            {user.isSystemAdmin && (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-red-500/15 text-red-400">
                                System Admin
                              </span>
                            )}
                            {user.emailConfirmed ? (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-green-500/15 text-green-400">
                                Verificado
                              </span>
                            ) : (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-amber-500/15 text-amber-400">
                                Pendiente
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-400">
                          {!user.isSystemAdmin && (
                            <button
                              onClick={() => handleDeleteUser(user.id, user.nombreCompleto)}
                              disabled={deletingUserId === user.id}
                              className="inline-flex items-center justify-center p-2 rounded-lg bg-red-500/15 hover:bg-red-500/25 text-red-400 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                              title="Eliminar usuario"
                            >
                              {deletingUserId === user.id ? (
                                <Loader2 className="w-4 h-4 animate-spin" />
                              ) : (
                                <Trash2 className="w-4 h-4" />
                              )}
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Modal de creación */}
      {showModal && createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-xl shadow-black/25 p-6 w-full max-w-md">
            <h2 className="text-xl font-bold text-gray-100 mb-4">Crear Nuevo Usuario</h2>

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
          </div>
        </div>,
        document.body
      )}
    </SystemAdminLayout>
  );
}
