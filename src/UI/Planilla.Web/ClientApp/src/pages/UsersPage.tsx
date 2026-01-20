import React, { useEffect, useState } from 'react';
import { tenantService } from '../services/tenantService';
import type { TenantUserDto, TenantRole } from '../types/api';
import toast from 'react-hot-toast';

export default function UsersPage() {
  const [users, setUsers] = useState<TenantUserDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState<TenantRole>(2); // Manager by default
  const [isInviting, setIsInviting] = useState(false);

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = async () => {
    try {
      const data = await tenantService.getUsers();
      setUsers(data);
    } catch (error: any) {
      toast.error('Error al cargar usuarios');
    } finally {
      setIsLoading(false);
    }
  };

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!inviteEmail) {
      toast.error('Por favor ingresa un correo electrónico');
      return;
    }

    setIsInviting(true);

    try {
      await tenantService.inviteUser({ email: inviteEmail, role: inviteRole });
      toast.success(`Invitación enviada a ${inviteEmail}`);
      setInviteEmail('');
      setInviteRole(2);
      setIsInviteModalOpen(false);
      loadUsers();
    } catch (error: any) {
      toast.error(error.message || 'Error al enviar invitación');
    } finally {
      setIsInviting(false);
    }
  };

  const handleRemoveUser = async (userId: number, email: string) => {
    if (!confirm(`¿Estás seguro de que deseas remover a ${email}?`)) {
      return;
    }

    try {
      await tenantService.removeUser(userId);
      toast.success('Usuario removido exitosamente');
      loadUsers();
    } catch (error: any) {
      toast.error(error.message || 'Error al remover usuario');
    }
  };

  const getRoleBadge = (roleName: string) => {
    const colors: Record<string, string> = {
      Owner: 'bg-purple-100 text-purple-800',
      Admin: 'bg-blue-100 text-blue-800',
      Manager: 'bg-green-100 text-green-800',
      Accountant: 'bg-yellow-100 text-yellow-800',
      Employee: 'bg-gray-100 text-gray-800',
    };

    return (
      <span className={`px-2 py-1 rounded-full text-xs font-medium ${colors[roleName] || 'bg-gray-100 text-gray-800'}`}>
        {roleName}
      </span>
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Gestión de Usuarios</h1>
          <p className="text-gray-600 mt-2">Administra los usuarios de tu equipo</p>
        </div>
        <button
          onClick={() => setIsInviteModalOpen(true)}
          className="bg-blue-600 text-white px-6 py-3 rounded-lg font-medium hover:bg-blue-700 transition-colors flex items-center gap-2"
        >
          <svg
            className="w-5 h-5"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"
            />
          </svg>
          Invitar Usuario
        </button>
      </div>

      {/* Users Table */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-gray-200 bg-gray-50">
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Email
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Rol
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Estado
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Fecha de Ingreso
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Último Acceso
                </th>
                <th className="px-6 py-4 text-right text-sm font-semibold text-gray-900">
                  Acciones
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    <div className="flex items-center justify-center gap-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600"></div>
                      Cargando usuarios...
                    </div>
                  </td>
                </tr>
              ) : users.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    No hay usuarios
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr key={user.id} className="hover:bg-gray-50">
                    <td className="px-6 py-4 text-sm text-gray-900">{user.email}</td>
                    <td className="px-6 py-4">{getRoleBadge(user.roleName)}</td>
                    <td className="px-6 py-4">
                      {user.isActive ? (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">
                          Activo
                        </span>
                      ) : (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                          Inactivo
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-600">
                      {new Date(user.joinedAt).toLocaleDateString('es-PA')}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-600">
                      {user.lastLoginAt
                        ? new Date(user.lastLoginAt).toLocaleDateString('es-PA')
                        : 'Nunca'}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <button
                        onClick={() => handleRemoveUser(user.id, user.email)}
                        className="text-red-600 hover:text-red-700 font-medium text-sm"
                      >
                        Remover
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Invite Modal */}
      {isInviteModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
              <h3 className="text-lg font-semibold text-gray-900">Invitar Usuario</h3>
              <button
                onClick={() => setIsInviteModalOpen(false)}
                className="text-gray-400 hover:text-gray-600"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>

            <form onSubmit={handleInvite} className="p-6 space-y-4">
              <div>
                <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-2">
                  Correo Electrónico
                </label>
                <input
                  id="email"
                  type="email"
                  value={inviteEmail}
                  onChange={(e) => setInviteEmail(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="usuario@empresa.com"
                  disabled={isInviting}
                />
              </div>

              <div>
                <label htmlFor="role" className="block text-sm font-medium text-gray-700 mb-2">
                  Rol
                </label>
                <select
                  id="role"
                  value={inviteRole}
                  onChange={(e) => setInviteRole(Number(e.target.value) as TenantRole)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  disabled={isInviting}
                >
                  <option value={1}>Admin</option>
                  <option value={2}>Manager</option>
                  <option value={3}>Accountant</option>
                  <option value={4}>Employee</option>
                </select>
              </div>

              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={() => setIsInviteModalOpen(false)}
                  className="flex-1 px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
                  disabled={isInviting}
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                  disabled={isInviting}
                >
                  {isInviting ? (
                    <>
                      <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                      Enviando...
                    </>
                  ) : (
                    'Enviar Invitación'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
