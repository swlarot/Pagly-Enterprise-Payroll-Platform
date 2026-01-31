import React, { useState, useEffect } from 'react';
import { X, Search, UserPlus, Users, Loader2 } from 'lucide-react';
import { Input } from '../ui/Input';
import { Select } from '../ui/Select';
import { Button } from '../ui/Button';
import { TenantRole, SystemUserDto } from '../../types/api';
import { systemAdminService } from '../../services/systemAdminService';
import toast from 'react-hot-toast';

interface InviteUserModalProps {
  tenantId: number;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

type Mode = 'select' | 'create';

export function InviteUserModal({ tenantId, isOpen, onClose, onSuccess }: InviteUserModalProps) {
  // Estado de modo
  const [mode, setMode] = useState<Mode>('select');

  // Estado de búsqueda de usuarios
  const [searchTerm, setSearchTerm] = useState('');
  const [systemUsers, setSystemUsers] = useState<SystemUserDto[]>([]);
  const [isLoadingUsers, setIsLoadingUsers] = useState(false);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  // Estado de usuario seleccionado
  const [selectedUser, setSelectedUser] = useState<SystemUserDto | null>(null);

  // Estado del formulario
  const [formData, setFormData] = useState({
    email: '',
    fullName: '',
    password: '',
    role: TenantRole.Manager,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Cargar usuarios del sistema cuando se abre el modal en modo selección
  useEffect(() => {
    if (isOpen && mode === 'select') {
      loadSystemUsers();
    }
  }, [isOpen, mode, searchTerm, page]);

  // Resetear estado cuando se cierra el modal
  useEffect(() => {
    if (!isOpen) {
      resetState();
    }
  }, [isOpen]);

  const loadSystemUsers = async () => {
    try {
      setIsLoadingUsers(true);
      const result = await systemAdminService.getAllSystemUsers({
        search: searchTerm,
        page,
        pageSize: 10,
      });
      setSystemUsers(result.data);
      setTotal(result.total);
      setTotalPages(result.totalPages);
    } catch (error: any) {
      toast.error('Error al cargar usuarios del sistema');
      console.error('Error loading system users:', error);
    } finally {
      setIsLoadingUsers(false);
    }
  };

  const handleSelectUser = (user: SystemUserDto) => {
    setSelectedUser(user);
    setFormData({
      ...formData,
      email: user.email,
      fullName: user.fullName,
    });

    // Verificar si el usuario ya está en este tenant
    const alreadyInTenant = user.tenants.some((t) => t.tenantId === tenantId);
    if (alreadyInTenant) {
      toast.error(
        `El usuario ${user.email} ya está en este tenant. Si fue removido, puede re-invitarlo.`
      );
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validaciones
    if (mode === 'select' && !selectedUser) {
      toast.error('Debe seleccionar un usuario');
      return;
    }

    if (mode === 'create') {
      if (!formData.email || !formData.fullName || !formData.password) {
        toast.error('Todos los campos son requeridos');
        return;
      }

      if (formData.password.length < 6) {
        toast.error('La contraseña debe tener al menos 6 caracteres');
        return;
      }
    } else {
      // En modo selección, no se requiere password
      if (!formData.email || !formData.fullName) {
        toast.error('Email y nombre completo son requeridos');
        return;
      }
    }

    try {
      setIsSubmitting(true);
      await systemAdminService.inviteUserToTenant(tenantId, formData);
      toast.success('Usuario invitado exitosamente');
      onSuccess();
      onClose();
    } catch (error: any) {
      toast.error(error.message || 'Error al invitar usuario');
    } finally {
      setIsSubmitting(false);
    }
  };

  const resetState = () => {
    setMode('select');
    setSearchTerm('');
    setSystemUsers([]);
    setPage(1);
    setTotal(0);
    setSelectedUser(null);
    setFormData({
      email: '',
      fullName: '',
      password: '',
      role: TenantRole.Manager,
    });
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-3xl max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="p-6 border-b border-gray-200 flex justify-between items-center">
          <h3 className="text-xl font-semibold text-gray-900">Invitar Usuario al Tenant</h3>
          <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Selector de Modo */}
        <div className="p-6 border-b border-gray-200">
          <div className="flex gap-4">
            <button
              type="button"
              onClick={() => setMode('select')}
              className={`flex-1 px-4 py-3 rounded-lg flex items-center justify-center gap-2 transition-colors ${
                mode === 'select'
                  ? 'bg-blue-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              <Users className="w-5 h-5" />
              Seleccionar Usuario Existente
            </button>
            <button
              type="button"
              onClick={() => setMode('create')}
              className={`flex-1 px-4 py-3 rounded-lg flex items-center justify-center gap-2 transition-colors ${
                mode === 'create'
                  ? 'bg-blue-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              <UserPlus className="w-5 h-5" />
              Crear Nuevo Usuario
            </button>
          </div>
        </div>

        <div className="p-6">
          {/* Modo Seleccionar Usuario Existente */}
          {mode === 'select' ? (
            <div>
              {/* Búsqueda */}
              <div className="mb-4">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
                  <input
                    type="text"
                    placeholder="Buscar por email o nombre..."
                    value={searchTerm}
                    onChange={(e) => {
                      setSearchTerm(e.target.value);
                      setPage(1); // Resetear a página 1 en búsqueda
                    }}
                    className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                </div>
              </div>

              {/* Lista de Usuarios */}
              <div className="space-y-2 mb-4 max-h-96 overflow-y-auto">
                {isLoadingUsers ? (
                  <div className="flex items-center justify-center py-12">
                    <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
                  </div>
                ) : systemUsers.length === 0 ? (
                  <div className="text-center py-12 text-gray-500">
                    No se encontraron usuarios
                  </div>
                ) : (
                  systemUsers.map((user) => (
                    <div
                      key={user.userId}
                      onClick={() => handleSelectUser(user)}
                      className={`p-4 border rounded-lg cursor-pointer transition-colors ${
                        selectedUser?.userId === user.userId
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-200 hover:bg-blue-50 hover:border-blue-300'
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div className="flex-1">
                          <div className="font-semibold text-gray-900">{user.email}</div>
                          <div className="text-sm text-gray-600">{user.fullName}</div>
                          {user.tenants.length > 0 && (
                            <div className="mt-2">
                              <div className="text-xs text-gray-500 mb-1">Ya está en:</div>
                              <div className="flex flex-wrap gap-1">
                                {user.tenants.map((t) => (
                                  <span
                                    key={t.tenantId}
                                    className={`px-2 py-1 text-xs rounded ${
                                      t.tenantId === tenantId
                                        ? 'bg-red-100 text-red-700'
                                        : 'bg-gray-100 text-gray-700'
                                    }`}
                                  >
                                    {t.tenantName} ({t.role})
                                  </span>
                                ))}
                              </div>
                            </div>
                          )}
                        </div>
                        {selectedUser?.userId === user.userId && (
                          <div className="text-blue-600 font-medium ml-2">✓ Seleccionado</div>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>

              {/* Paginación */}
              {total > 10 && (
                <div className="flex justify-between items-center mb-4 pt-4 border-t border-gray-200">
                  <div className="text-sm text-gray-600">
                    Mostrando {(page - 1) * 10 + 1} - {Math.min(page * 10, total)} de {total}
                  </div>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1}
                      className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Anterior
                    </button>
                    <span className="px-3 py-1 text-sm text-gray-600">
                      Página {page} de {totalPages}
                    </span>
                    <button
                      type="button"
                      onClick={() => setPage((p) => p + 1)}
                      disabled={page >= totalPages}
                      className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Siguiente
                    </button>
                  </div>
                </div>
              )}

              {/* Form de Rol (si hay usuario seleccionado) */}
              {selectedUser && (
                <div className="mt-6 p-4 bg-blue-50 rounded-lg border border-blue-200">
                  <Select
                    label="Rol en este Tenant"
                    value={formData.role.toString()}
                    onChange={(e) =>
                      setFormData({ ...formData, role: parseInt(e.target.value) })
                    }
                    options={[
                      {
                        value: TenantRole.Admin.toString(),
                        label: 'Admin - Gestión completa excepto billing',
                      },
                      {
                        value: TenantRole.Manager.toString(),
                        label: 'Manager - Planillas, empleados, reportes',
                      },
                      {
                        value: TenantRole.Accountant.toString(),
                        label: 'Contador - Solo reportes y consultas',
                      },
                      {
                        value: TenantRole.Employee.toString(),
                        label: 'Empleado - Solo ver su información',
                      },
                    ]}
                  />
                </div>
              )}
            </div>
          ) : (
            /* Modo Crear Nuevo Usuario */
            <form onSubmit={handleSubmit} className="space-y-4">
              <Input
                label="Email"
                type="email"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                placeholder="usuario@empresa.com"
                required
              />

              <Input
                label="Nombre Completo"
                type="text"
                value={formData.fullName}
                onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                placeholder="Juan Pérez"
                required
              />

              <Input
                label="Contraseña Temporal"
                type="password"
                value={formData.password}
                onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                placeholder="Mínimo 6 caracteres"
                required
                helperText="El usuario podrá cambiarla después del primer login"
              />

              <Select
                label="Rol"
                value={formData.role.toString()}
                onChange={(e) => setFormData({ ...formData, role: parseInt(e.target.value) })}
                options={[
                  {
                    value: TenantRole.Admin.toString(),
                    label: 'Admin - Gestión completa excepto billing',
                  },
                  {
                    value: TenantRole.Manager.toString(),
                    label: 'Manager - Planillas, empleados, reportes',
                  },
                  {
                    value: TenantRole.Accountant.toString(),
                    label: 'Contador - Solo reportes y consultas',
                  },
                  {
                    value: TenantRole.Employee.toString(),
                    label: 'Empleado - Solo ver su información',
                  },
                ]}
              />
            </form>
          )}

          {/* Botones de Acción */}
          <div className="mt-6 flex justify-end gap-3 pt-4 border-t border-gray-200">
            <Button type="button" variant="outline" onClick={handleClose} disabled={isSubmitting}>
              Cancelar
            </Button>
            <Button
              type="button"
              onClick={handleSubmit}
              disabled={
                isSubmitting ||
                (mode === 'select' && !selectedUser) ||
                (mode === 'create' &&
                  (!formData.email || !formData.fullName || !formData.password))
              }
              loading={isSubmitting}
            >
              Invitar Usuario
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
