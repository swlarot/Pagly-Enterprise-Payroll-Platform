import React, { useEffect, useState } from 'react';
import { tenantService } from '../../services/tenantService';
import { roleService } from '../../services/roleService';
import { useAuth } from '../../contexts/AuthContext';
import type { TenantUserDto, InvitationDto, TenantRole, UpdateTenantUserDto, CustomTenantRoleDto } from '../../types/api';
import toast from 'react-hot-toast';
import ConfirmModal from '../ConfirmModal.tsx';

export function UsersManagementTab() {
  const { subscription } = useAuth();
  const [users, setUsers] = useState<TenantUserDto[]>([]);
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  const [usage, setUsage] = useState<{ usersCount: number; maxUsers: number } | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [customRoles, setCustomRoles] = useState<CustomTenantRoleDto[]>([]);
  const [isLoadingRoles, setIsLoadingRoles] = useState(false);

  // Invite modal state
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState<TenantRole>(1); // User: sin permisos hasta que el Owner asigne un rol
  const [isInviting, setIsInviting] = useState(false);
  const [generatedInviteUrl, setGeneratedInviteUrl] = useState('');

  // Role change modal state: 0 = Usuario (sin rol), >0 = custom role id
  const [isRoleModalOpen, setIsRoleModalOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<TenantUserDto | null>(null);
  const [newRole, setNewRole] = useState<number>(0);
  const [isUpdatingRole, setIsUpdatingRole] = useState(false);

  // Confirm modal state
  const [confirmModal, setConfirmModal] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    onConfirm: () => void;
    variant?: 'danger' | 'warning' | 'info';
  }>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: () => {},
  });

  useEffect(() => {
    loadData();
    loadCustomRoles();
  }, []);

  const loadData = async () => {
    try {
      setIsLoading(true);
      const [usersData, invitationsData, usageData] = await Promise.all([
        tenantService.getUsers(),
        tenantService.getInvitations(),
        tenantService.getUsage(),
      ]);

      setUsers(usersData);
      setInvitations(invitationsData);
      setUsage({ usersCount: usageData.usersCount, maxUsers: usageData.maxUsers });
    } catch (error: any) {
      toast.error('Error al cargar datos de usuarios');
      console.error(error);
    } finally {
      setIsLoading(false);
    }
  };

  const loadCustomRoles = async () => {
    try {
      setIsLoadingRoles(true);
      const data = await roleService.getRoles();
      // Solo roles personalizados (no sistema) para invitar/asignar
      setCustomRoles(Array.isArray(data) ? data.filter((r: CustomTenantRoleDto) => !r.isSystem) : []);
    } catch (error: any) {
      console.error('Error loading custom roles:', error);
      setCustomRoles([]);
    } finally {
      setIsLoadingRoles(false);
    }
  };

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!inviteEmail) {
      toast.error('Por favor ingresa un correo electrónico');
      return;
    }

    // Validate email format
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(inviteEmail)) {
      toast.error('Por favor ingresa un correo electrónico válido');
      return;
    }

    // Check plan limits
    if (usage && usage.usersCount >= usage.maxUsers) {
      toast.error(`Has alcanzado el límite de ${usage.maxUsers} usuarios. Actualiza tu plan para agregar más.`);
      return;
    }

    setIsInviting(true);

    try {
      const invitation = await tenantService.inviteUser({ email: inviteEmail, role: inviteRole });
      toast.success(`Invitación enviada a ${inviteEmail}`);

      // Show invite URL
      const inviteUrl = `${window.location.origin}/accept-invite?token=${invitation.token}`;
      setGeneratedInviteUrl(inviteUrl);

      // Reload data
      await loadData();

      // Reset form but keep modal open to show URL
      setInviteEmail('');
      setInviteRole(1);
    } catch (error: any) {
      toast.error(error.message || 'Error al enviar invitación');
    } finally {
      setIsInviting(false);
    }
  };

  const handleCopyInviteUrl = () => {
    navigator.clipboard.writeText(generatedInviteUrl);
    toast.success('Link de invitación copiado al portapapeles');
  };

  const handleCloseInviteModal = () => {
    setIsInviteModalOpen(false);
    setGeneratedInviteUrl('');
    setInviteEmail('');
    setInviteRole(1);
  };

  const handleOpenRoleModal = (user: TenantUserDto) => {
    setSelectedUser(user);
    setNewRole(0); // Por defecto "Usuario (sin rol)"; si el backend devolviera customRoleId podríamos setearlo
    setIsRoleModalOpen(true);
  };

  const handleChangeRole = async () => {
    if (!selectedUser) return;

    setIsUpdatingRole(true);

    try {
      if (newRole === 0) {
        await roleService.assignRoleToUser(selectedUser.userId, { systemRole: 1 });
      } else {
        await roleService.assignRoleToUser(selectedUser.userId, { customRoleId: newRole });
      }
      toast.success(`Rol de ${selectedUser.email} actualizado exitosamente`);
      setIsRoleModalOpen(false);
      setSelectedUser(null);
      await loadData();
    } catch (error: any) {
      toast.error(error.message || 'Error al cambiar rol');
    } finally {
      setIsUpdatingRole(false);
    }
  };

  const handleToggleActive = (user: TenantUserDto) => {
    const action = user.isActive ? 'desactivar' : 'activar';

    setConfirmModal({
      isOpen: true,
      title: `${action.charAt(0).toUpperCase() + action.slice(1)} Usuario`,
      message: `¿Estás seguro de que deseas ${action} a ${user.email}?`,
      variant: user.isActive ? 'warning' : 'info',
      onConfirm: async () => {
        try {
          const dto: UpdateTenantUserDto = { isActive: !user.isActive };
          await tenantService.updateUser(user.id, dto);
          toast.success(`Usuario ${action === 'activar' ? 'activado' : 'desactivado'} exitosamente`);
          await loadData();
        } catch (error: any) {
          toast.error(error.message || `Error al ${action} usuario`);
        }
      },
    });
  };

  const handleRemoveUser = (user: TenantUserDto) => {
    setConfirmModal({
      isOpen: true,
      title: 'Remover Usuario',
      message: `¿Estás seguro de que deseas remover a ${user.email}? Esta acción no se puede deshacer.`,
      variant: 'danger',
      onConfirm: async () => {
        try {
          await tenantService.removeUser(user.id);
          toast.success('Usuario removido exitosamente');
          await loadData();
        } catch (error: any) {
          toast.error(error.message || 'Error al remover usuario');
        }
      },
    });
  };

  const handleRevokeInvitation = (invitation: InvitationDto) => {
    setConfirmModal({
      isOpen: true,
      title: 'Revocar Invitación',
      message: `¿Estás seguro de que deseas revocar la invitación enviada a ${invitation.email}?`,
      variant: 'warning',
      onConfirm: async () => {
        try {
          await tenantService.revokeInvitation(invitation.id);
          toast.success('Invitación revocada exitosamente');
          await loadData();
        } catch (error: any) {
          toast.error(error.message || 'Error al revocar invitación');
        }
      },
    });
  };

  const getRoleBadge = (roleName: string) => {
    const colors: Record<string, string> = {
      Owner: 'bg-amber-500/15 text-amber-400',
      Admin: 'bg-blue-500/15 text-blue-400',
      Manager: 'bg-green-500/15 text-green-400',
      Accountant: 'bg-amber-500/15 text-amber-400',
      Employee: 'bg-blue-500/15 text-blue-400',
    };

    return (
      <span className={`px-2 py-1 rounded-full text-xs font-medium ${colors[roleName] || 'bg-navy-700 text-gray-300'}`}>
        {roleName}
      </span>
    );
  };

  const getRoleDescription = (role: TenantRole) => {
    const descriptions: Record<number, string> = {
      0: 'Propietario - acceso total (solo uno por tenant)',
      1: 'Usuario - sin permisos hasta que el Owner le asigne un rol desde Roles',
    };
    return descriptions[role] ?? 'Usuario';
  };

  return (
    <div className="space-y-6">
      {/* Header Section - sin título principal porque ya está en la página padre */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-gray-400">Administra los usuarios de tu equipo y sus roles</p>
        </div>
        <button
          onClick={() => setIsInviteModalOpen(true)}
          className="bg-primary-600 text-white px-6 py-3 rounded-lg font-medium hover:bg-primary-700 transition-colors flex items-center gap-2"
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

      {/* Usage Card */}
      {usage && (
        <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-gray-400">Usuarios Activos</p>
              <p className="text-2xl font-bold text-gray-100">
                {usage.usersCount} <span className="text-lg text-gray-500">/ {usage.maxUsers}</span>
              </p>
            </div>
            <div className="text-right">
              <p className="text-sm text-gray-400">Plan Actual</p>
              <p className="text-lg font-semibold text-primary-400">{subscription?.planName}</p>
            </div>
          </div>

          {/* Usage bar */}
          <div className="mt-3">
            <div className="w-full bg-navy-800 rounded-full h-2">
              <div
                className={`h-2 rounded-full transition-all ${
                  usage.usersCount >= usage.maxUsers
                    ? 'bg-red-500'
                    : usage.usersCount >= usage.maxUsers * 0.8
                    ? 'bg-amber-500'
                    : 'bg-primary-500'
                }`}
                style={{ width: `${Math.min((usage.usersCount / usage.maxUsers) * 100, 100)}%` }}
              />
            </div>
          </div>
        </div>
      )}

      {/* Pending Invitations */}
      {invitations.length > 0 && (
        <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700">
          <div className="px-6 py-4 border-b border-navy-700">
            <h2 className="text-lg font-semibold text-gray-100">Invitaciones Pendientes</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-navy-700 bg-navy-950">
                  <th className="px-6 py-3 text-left text-sm font-semibold text-gray-300">Email</th>
                  <th className="px-6 py-3 text-left text-sm font-semibold text-gray-300">Rol</th>
                  <th className="px-6 py-3 text-left text-sm font-semibold text-gray-300">Fecha de Envío</th>
                  <th className="px-6 py-3 text-left text-sm font-semibold text-gray-300">Expira</th>
                  <th className="px-6 py-3 text-right text-sm font-semibold text-gray-300">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-navy-800">
                {invitations.map((invitation) => (
                  <tr key={invitation.id} className="hover:bg-navy-800">
                    <td className="px-6 py-4 text-sm text-gray-100">{invitation.email}</td>
                    <td className="px-6 py-4">{getRoleBadge(invitation.roleName)}</td>
                    <td className="px-6 py-4 text-sm text-gray-400">
                      {new Date(invitation.createdAt).toLocaleDateString('es-PA')}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-400">
                      {new Date(invitation.expiresAt).toLocaleDateString('es-PA')}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <button
                        onClick={() => handleRevokeInvitation(invitation)}
                        className="text-red-400 hover:text-red-300 font-medium text-sm"
                      >
                        Revocar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Users Table */}
      <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700">
        <div className="px-6 py-4 border-b border-navy-700">
          <h2 className="text-lg font-semibold text-gray-100">Usuarios del Equipo</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-navy-700 bg-navy-950">
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-300">
                  Email
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-300">
                  Rol
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-300">
                  Estado
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-300">
                  Fecha de Ingreso
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-300">
                  Último Acceso
                </th>
                <th className="px-6 py-4 text-right text-sm font-semibold text-gray-300">
                  Acciones
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-800">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    <div className="flex items-center justify-center gap-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary-500"></div>
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
                  <tr key={user.id} className="hover:bg-navy-800">
                    <td className="px-6 py-4 text-sm text-gray-100">{user.email}</td>
                    <td className="px-6 py-4">{getRoleBadge(user.customRoleName ?? user.roleName)}</td>
                    <td className="px-6 py-4">
                      {user.isActive ? (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-green-500/15 text-green-400">
                          Activo
                        </span>
                      ) : (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-navy-700 text-gray-400">
                          Inactivo
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-400">
                      {new Date(user.joinedAt).toLocaleDateString('es-PA')}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-400">
                      {user.lastLoginAt
                        ? new Date(user.lastLoginAt).toLocaleDateString('es-PA')
                        : 'Nunca'}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={() => handleOpenRoleModal(user)}
                          className="text-primary-400 hover:text-primary-300 font-medium text-sm"
                          title="Cambiar rol"
                        >
                          Cambiar Rol
                        </button>
                        <button
                          onClick={() => handleToggleActive(user)}
                          className={`font-medium text-sm ${
                            user.isActive
                              ? 'text-primary-400 hover:text-primary-300'
                              : 'text-green-400 hover:text-green-300'
                          }`}
                          title={user.isActive ? 'Desactivar' : 'Activar'}
                        >
                          {user.isActive ? 'Desactivar' : 'Activar'}
                        </button>
                        {user.roleName !== 'Owner' && (
                          <button
                            onClick={() => handleRemoveUser(user)}
                            className="text-red-400 hover:text-red-300 font-medium text-sm"
                            title="Remover usuario"
                          >
                            Remover
                          </button>
                        )}
                      </div>
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
          <div className="bg-navy-900 rounded-xl shadow-xl border border-navy-700 max-w-md w-full mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-navy-700">
              <h3 className="text-lg font-semibold text-gray-100">Invitar Usuario</h3>
              <button
                onClick={handleCloseInviteModal}
                className="text-gray-500 hover:text-gray-300"
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
              {/* Usage info */}
              {usage && (
                <div className={`p-3 rounded-lg ${
                  usage.usersCount >= usage.maxUsers
                    ? 'bg-red-500/10 border border-red-500/20'
                    : usage.usersCount >= usage.maxUsers * 0.8
                    ? 'bg-amber-500/10 border border-amber-500/20'
                    : 'bg-blue-500/10 border border-blue-500/20'
                }`}>
                  <p className={`text-sm font-medium ${
                    usage.usersCount >= usage.maxUsers
                      ? 'text-red-400'
                      : usage.usersCount >= usage.maxUsers * 0.8
                      ? 'text-amber-400'
                      : 'text-blue-300'
                  }`}>
                    Usuarios: {usage.usersCount} / {usage.maxUsers}
                  </p>
                </div>
              )}

              {!generatedInviteUrl ? (
                <>
                  <div>
                    <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-2">
                      Correo Electrónico
                    </label>
                    <input
                      id="email"
                      type="email"
                      value={inviteEmail}
                      onChange={(e) => setInviteEmail(e.target.value)}
                      className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent placeholder-gray-600"
                      placeholder="usuario@empresa.com"
                      disabled={isInviting}
                      required
                    />
                  </div>

                  <div>
                    <label htmlFor="role" className="block text-sm font-medium text-gray-300 mb-2">
                      Rol
                    </label>
                    <select
                      id="role"
                      value={inviteRole}
                      onChange={(e) => setInviteRole(Number(e.target.value) as TenantRole)}
                      className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                      disabled={isInviting || isLoadingRoles}
                    >
                      <option value={1}>Usuario (sin permisos hasta asignar rol)</option>
                    </select>
                    <p className="mt-2 text-xs text-gray-500">
                      {getRoleDescription(inviteRole)}
                    </p>
                  </div>

                  <div className="flex gap-3 pt-4">
                    <button
                      type="button"
                      onClick={handleCloseInviteModal}
                      className="flex-1 px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                      disabled={isInviting}
                    >
                      Cancelar
                    </button>
                    <button
                      type="submit"
                      className="flex-1 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                      disabled={isInviting || (usage ? usage.usersCount >= usage.maxUsers : false)}
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
                </>
              ) : (
                <>
                  <div className="text-center py-4">
                    <div className="inline-flex items-center justify-center w-12 h-12 bg-green-500/15 rounded-full mb-4">
                      <svg
                        className="w-6 h-6 text-green-400"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M5 13l4 4L19 7"
                        />
                      </svg>
                    </div>
                    <h4 className="text-lg font-semibold text-gray-100 mb-2">
                      Invitación Enviada
                    </h4>
                    <p className="text-sm text-gray-400 mb-4">
                      Comparte este link con el usuario invitado
                    </p>
                  </div>

                  <div className="bg-navy-800 p-4 rounded-lg border border-navy-700">
                    <p className="text-xs text-gray-400 mb-2 font-medium">Link de Invitación:</p>
                    <div className="flex gap-2">
                      <input
                        type="text"
                        value={generatedInviteUrl}
                        readOnly
                        className="flex-1 px-3 py-2 bg-navy-900 border border-navy-600 text-gray-300 rounded-lg text-sm"
                      />
                      <button
                        type="button"
                        onClick={handleCopyInviteUrl}
                        className="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors flex items-center gap-2"
                      >
                        <svg
                          className="w-4 h-4"
                          fill="none"
                          viewBox="0 0 24 24"
                          stroke="currentColor"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
                          />
                        </svg>
                        Copiar
                      </button>
                    </div>
                  </div>

                  <div className="flex gap-3 pt-4">
                    <button
                      type="button"
                      onClick={handleCloseInviteModal}
                      className="flex-1 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
                    >
                      Cerrar
                    </button>
                  </div>
                </>
              )}
            </form>
          </div>
        </div>
      )}

      {/* Role Change Modal */}
      {isRoleModalOpen && selectedUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="bg-navy-900 rounded-xl shadow-xl border border-navy-700 max-w-md w-full mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-navy-700">
              <h3 className="text-lg font-semibold text-gray-100">Cambiar Rol de Usuario</h3>
              <button
                onClick={() => setIsRoleModalOpen(false)}
                className="text-gray-500 hover:text-gray-300"
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

            <div className="p-6 space-y-4">
              <div>
                <p className="text-sm text-gray-400 mb-1">Usuario:</p>
                <p className="font-medium text-gray-100">{selectedUser.email}</p>
              </div>

              <div>
                <label htmlFor="newRole" className="block text-sm font-medium text-gray-300 mb-2">
                  Asignar rol
                </label>
                <select
                  id="newRole"
                  value={newRole}
                  onChange={(e) => setNewRole(Number(e.target.value))}
                  className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                  disabled={isUpdatingRole || isLoadingRoles}
                >
                  <option value={0}>Usuario (sin rol asignado)</option>
                  {customRoles.map((role) => (
                    <option key={role.id} value={role.id}>
                      {role.name}
                    </option>
                  ))}
                </select>
                <p className="mt-2 text-xs text-gray-500">
                  {newRole === 0
                    ? 'El usuario no tendrá permisos hasta que le asignes un rol con permisos.'
                    : customRoles.find((r) => r.id === newRole)?.description || ''}
                </p>
              </div>

              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={() => setIsRoleModalOpen(false)}
                  className="flex-1 px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                  disabled={isUpdatingRole}
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  onClick={handleChangeRole}
                  className="flex-1 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
                  disabled={isUpdatingRole}
                >
                  {isUpdatingRole ? (
                    <>
                      <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                      Actualizando...
                    </>
                  ) : (
                    'Cambiar Rol'
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Confirm Modal */}
      <ConfirmModal
        isOpen={confirmModal.isOpen}
        onClose={() => setConfirmModal({ ...confirmModal, isOpen: false })}
        onConfirm={confirmModal.onConfirm}
        title={confirmModal.title}
        message={confirmModal.message}
        variant={confirmModal.variant}
      />
    </div>
  );
}
