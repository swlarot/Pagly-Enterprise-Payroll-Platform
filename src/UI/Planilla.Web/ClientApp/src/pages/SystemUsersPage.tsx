import React, { useEffect, useState } from 'react';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Card, CardBody, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import { UserPlus, Mail, Phone, Loader2, Trash2, Building2 } from 'lucide-react';
import toast from 'react-hot-toast';

interface User {
  id: string;
  nombreCompleto: string;
  email: string;
  telefono?: string;
  isSystemAdmin: boolean;
  emailConfirmed: boolean;
  tenantAssignments?: { tenantName: string; role: string }[];
}

export default function SystemUsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formData, setFormData] = useState({
    nombre: '',
    apellido: '',
    correo: '',
    telefono: ''
  });

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = async () => {
    try {
      setIsLoading(true);
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
    } catch (error: any) {
      toast.error(error.message || 'Error cargando usuarios');
    } finally {
      setIsLoading(false);
    }
  };

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
    } catch (error: any) {
      toast.error(error.message || 'Error creando usuario');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancel = () => {
    setShowModal(false);
    setFormData({ nombre: '', apellido: '', correo: '', telefono: '' });
  };

  if (isLoading) {
    return (
      <SystemAdminLayout>
        <div className="flex items-center justify-center h-[calc(100vh-80px)]">
          <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
        </div>
      </SystemAdminLayout>
    );
  }

  return (
    <SystemAdminLayout>
      <div className="space-y-6">
        <div className="flex justify-between items-center">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Usuarios del Sistema</h1>
            <p className="text-gray-600 mt-1">Gestiona todos los usuarios de Planilla</p>
          </div>
          <Button onClick={() => setShowModal(true)} className="flex items-center gap-2">
            <UserPlus className="w-4 h-4" />
            Crear Usuario
          </Button>
        </div>

        <Card>
          <CardHeader>
            <h2 className="text-lg font-semibold">Todos los Usuarios</h2>
          </CardHeader>
          <CardBody>
            {users.length === 0 ? (
              <div className="text-center py-12">
                <UserPlus className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                <p className="text-gray-600">No hay usuarios en el sistema</p>
                <Button onClick={() => setShowModal(true)} className="mt-4">
                  Crear primer usuario
                </Button>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
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
                  <tbody className="bg-white divide-y divide-gray-200">
                    {users.map((user) => (
                      <tr key={user.id} className="hover:bg-gray-50">
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex items-center">
                            <div>
                              <div className="text-sm font-medium text-gray-900">
                                {user.nombreCompleto}
                              </div>
                              <div className="text-sm text-gray-500 flex items-center gap-1">
                                <Mail className="w-3 h-3" />
                                {user.email}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="text-sm text-gray-900 flex items-center gap-1">
                            {user.telefono ? (
                              <>
                                <Phone className="w-3 h-3 text-gray-400" />
                                {user.telefono}
                              </>
                            ) : (
                              <span className="text-gray-400">N/A</span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="flex flex-wrap gap-1">
                            {user.tenantAssignments && user.tenantAssignments.length > 0 ? (
                              user.tenantAssignments.map((ta, idx) => (
                                <span
                                  key={idx}
                                  className="inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-800"
                                >
                                  <Building2 className="w-3 h-3" />
                                  {ta.tenantName} ({ta.role})
                                </span>
                              ))
                            ) : (
                              <span className="text-xs text-gray-400">Sin asignar</span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex flex-col gap-1">
                            {user.isSystemAdmin && (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-red-100 text-red-800">
                                System Admin
                              </span>
                            )}
                            {user.emailConfirmed ? (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-green-100 text-green-800">
                                Verificado
                              </span>
                            ) : (
                              <span className="inline-flex px-2 py-1 text-xs font-semibold rounded-full bg-yellow-100 text-yellow-800">
                                Pendiente
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {!user.isSystemAdmin && (
                            <Button
                              variant="danger"
                              size="sm"
                              onClick={() => {
                                if (confirm('¿Estás seguro de eliminar este usuario?')) {
                                  toast.error('Funcionalidad no implementada');
                                }
                              }}
                            >
                              <Trash2 className="w-4 h-4" />
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardBody>
        </Card>
      </div>

      {/* Modal de creación */}
      {showModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
            <h2 className="text-xl font-bold text-gray-900 mb-4">Crear Nuevo Usuario</h2>

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

              <div className="bg-blue-50 border border-blue-200 rounded-md p-3">
                <p className="text-xs text-blue-800">
                  ℹ️ Se enviará un email con la contraseña temporal:{' '}
                  <code className="bg-white px-2 py-1 rounded text-blue-900 font-mono">
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
        </div>
      )}
    </SystemAdminLayout>
  );
}
