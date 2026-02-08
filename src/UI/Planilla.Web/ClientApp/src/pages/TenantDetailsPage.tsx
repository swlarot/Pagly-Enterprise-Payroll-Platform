import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { Modal } from '../components/ui/Modal';
import { Select } from '../components/ui/Select';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import { InviteUserModal } from '../components/admin/InviteUserModal';
import { RoleBadge } from '../components/admin/RoleBadge';
import { StatusBadge } from '../components/admin/StatusBadge';
import { ActionBadge } from '../components/admin/ActionBadge';
import type { AdminTenantDto, AdminTenantUserDto, AuditLogDto } from '../types/api';
import { SubscriptionPlan, SubscriptionStatus } from '../types/api';
import {
  ArrowLeft,
  Building2,
  Users,
  UserCheck,
  Calendar,
  Loader2,
  Edit,
  AlertCircle,
  CheckCircle2,
  XCircle,
  UserPlus,
  FileText,
  Info,
  Trash2,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { TenantRole } from '../types/api';

type TabType = 'info' | 'users' | 'audit';

export default function TenantDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  // Main state
  const [tenant, setTenant] = useState<AdminTenantDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<TabType>('info');

  // Users tab state
  const [users, setUsers] = useState<AdminTenantUserDto[]>([]);
  const [isLoadingUsers, setIsLoadingUsers] = useState(false);
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [deletingUserId, setDeletingUserId] = useState<string | null>(null);

  // Audit log tab state
  const [auditLogs, setAuditLogs] = useState<AuditLogDto[]>([]);
  const [isLoadingAudit, setIsLoadingAudit] = useState(false);
  const [auditFilters, setAuditFilters] = useState({
    page: 1,
    pageSize: 50,
    from: '',
    to: '',
    action: '',
  });
  const [auditTotal, setAuditTotal] = useState(0);

  // Modals
  const [showChangePlanModal, setShowChangePlanModal] = useState(false);
  const [showExtendTrialModal, setShowExtendTrialModal] = useState(false);
  const [showDeactivateModal, setShowDeactivateModal] = useState(false);

  // Form states
  const [newPlan, setNewPlan] = useState<SubscriptionPlan>(SubscriptionPlan.Free);
  const [extendDays, setExtendDays] = useState(7);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (id) {
      loadTenant();
    }
  }, [id]);

  useEffect(() => {
    if (id && activeTab === 'users') {
      loadUsers();
    } else if (id && activeTab === 'audit') {
      loadAuditLogs();
    }
  }, [id, activeTab]);

  useEffect(() => {
    if (activeTab === 'audit' && id) {
      loadAuditLogs();
    }
  }, [auditFilters]);

  const loadTenant = async () => {
    try {
      setIsLoading(true);
      const data = await systemAdminService.getTenantById(parseInt(id!));
      setTenant(data);
      setNewPlan(data.subscription?.plan || SubscriptionPlan.Free);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar tenant');
      navigate('/system-admin/tenants');
    } finally {
      setIsLoading(false);
    }
  };

  const loadUsers = async () => {
    try {
      setIsLoadingUsers(true);
      const data = await systemAdminService.getTenantUsers(parseInt(id!));
      setUsers(data);
    } catch (error: any) {
      console.error('Error loading users:', error);
      toast.error('Error al cargar usuarios');
    } finally {
      setIsLoadingUsers(false);
    }
  };

  const handleRemoveUser = async (userId: string, userEmail: string, userRole: TenantRole) => {
    // Verificar si es el último Owner
    const ownersCount = users.filter(
      (u) => u.role === TenantRole.Owner && u.isActive && u.userId !== userId
    ).length;
    const isOwner = userRole === TenantRole.Owner;

    if (isOwner && ownersCount === 0) {
      toast.error('No se puede eliminar el último Owner del tenant');
      return;
    }

    if (
      !confirm(
        `¿Estás seguro de eliminar al usuario "${userEmail}" del tenant?\n\n${
          isOwner
            ? 'ADVERTENCIA: Este usuario es Owner. Asegúrate de que haya otro Owner antes de continuar.'
            : ''
        }`
      )
    ) {
      return;
    }

    try {
      setDeletingUserId(userId);
      await systemAdminService.removeTenantUser(parseInt(id!), userId);
      toast.success('Usuario eliminado del tenant exitosamente');
      await loadUsers();
      await loadTenant(); // Recargar para actualizar estadísticas
    } catch (error: any) {
      console.error('Error removing user:', error);
      if (error.statusCode === 400 && error.message?.includes('último Owner')) {
        toast.error('No se puede eliminar el último Owner del tenant');
      } else {
        toast.error(error.message || 'Error al eliminar usuario del tenant');
      }
    } finally {
      setDeletingUserId(null);
    }
  };

  const loadAuditLogs = async () => {
    try {
      setIsLoadingAudit(true);
      const data = await systemAdminService.getTenantAuditLog(parseInt(id!), auditFilters);
      setAuditLogs(data.data);
      setAuditTotal(data.total);
    } catch (error: any) {
      console.error('Error loading audit logs:', error);
      toast.error('Error al cargar logs de auditoría');
    } finally {
      setIsLoadingAudit(false);
    }
  };

  const handleChangePlan = async () => {
    try {
      setIsSubmitting(true);
      const updated = await systemAdminService.updateTenantSubscription(parseInt(id!), {
        plan: newPlan,
      });
      setTenant(updated);
      setShowChangePlanModal(false);
      toast.success('Plan actualizado exitosamente');
    } catch (error: any) {
      toast.error(error.message || 'Error al cambiar plan');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleExtendTrial = async () => {
    try {
      setIsSubmitting(true);
      const updated = await systemAdminService.updateTenantSubscription(parseInt(id!), {
        plan: tenant!.subscription?.plan || SubscriptionPlan.Free,
        extendTrialDays: extendDays,
      });
      setTenant(updated);
      setShowExtendTrialModal(false);
      toast.success(`Trial extendido ${extendDays} días`);
    } catch (error: any) {
      toast.error(error.message || 'Error al extender trial');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeactivate = async () => {
    try {
      setIsSubmitting(true);
      if (tenant?.isActive) {
        await systemAdminService.deactivateTenant(parseInt(id!));
        toast.success('Tenant desactivado');
      } else {
        await systemAdminService.reactivateTenant(parseInt(id!));
        toast.success('Tenant reactivado');
      }
      await loadTenant();
      setShowDeactivateModal(false);
    } catch (error: any) {
      toast.error(error.message || 'Error al cambiar estado del tenant');
    } finally {
      setIsSubmitting(false);
    }
  };

  const getPlanBadgeVariant = (plan: SubscriptionPlan) => {
    switch (plan) {
      case SubscriptionPlan.Free:
        return 'default';
      case SubscriptionPlan.Starter:
        return 'info';
      case SubscriptionPlan.Professional:
        return 'success';
      case SubscriptionPlan.Enterprise:
        return 'warning';
      default:
        return 'default';
    }
  };

  const getStatusBadgeVariant = (status: SubscriptionStatus) => {
    switch (status) {
      case SubscriptionStatus.Active:
        return 'success';
      case SubscriptionStatus.Trialing:
        return 'info';
      case SubscriptionStatus.PastDue:
        return 'warning';
      case SubscriptionStatus.Canceled:
        return 'danger';
      default:
        return 'default';
    }
  };

  if (isLoading || !tenant) {
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
      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="mb-8">
          <button
            onClick={() => navigate('/system-admin/tenants')}
            className="flex items-center gap-2 text-gray-400 hover:text-gray-100 mb-4 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Volver a Tenants
          </button>
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-100">{tenant.name}</h1>
              <p className="text-gray-400 mt-2">
                {tenant.ruc && tenant.dv ? `RUC: ${tenant.ruc}-${tenant.dv} • ` : ''}
                Creado el {new Date(tenant.createdAt).toLocaleDateString('es-PA')}
              </p>
            </div>
            <div className="flex items-center gap-3">
              {tenant.isActive ? (
                <Badge variant="success">Activo</Badge>
              ) : (
                <Badge variant="danger">Inactivo</Badge>
              )}
            </div>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 px-6 py-5 flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
              <UserCheck className="w-6 h-6 text-primary-400" />
            </div>
            <div>
              <p className="text-sm text-gray-400">Empleados</p>
              <p className="text-2xl font-bold text-gray-100">
                {tenant.usage.totalEmployees}/{tenant.subscription?.maxEmployees || 0}
              </p>
            </div>
          </div>

          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 px-6 py-5 flex items-center gap-4">
            <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
              <Users className="w-6 h-6 text-green-400" />
            </div>
            <div>
              <p className="text-sm text-gray-400">Usuarios</p>
              <p className="text-2xl font-bold text-gray-100">
                {tenant.usage.totalUsers}/{tenant.subscription?.maxUsers || 0}
              </p>
            </div>
          </div>

          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 px-6 py-5 flex items-center gap-4">
            <div className="w-12 h-12 bg-purple-500/15 rounded-lg flex items-center justify-center">
              <Building2 className="w-6 h-6 text-purple-400" />
            </div>
            <div>
              <p className="text-sm text-gray-400">Planillas</p>
              <p className="text-2xl font-bold text-gray-100">{tenant.usage.totalPayrolls}</p>
            </div>
          </div>
        </div>

        {/* Tabs */}
        <div className="border-b border-navy-700 mb-6">
          <div className="flex gap-4">
            <button
              onClick={() => setActiveTab('info')}
              className={`flex items-center gap-2 px-4 py-2 border-b-2 font-medium transition-colors ${
                activeTab === 'info'
                  ? 'border-primary-500 text-primary-400'
                  : 'border-transparent text-gray-400 hover:text-gray-200 hover:border-navy-600'
              }`}
            >
              <Info className="w-4 h-4" />
              Información
            </button>
            <button
              onClick={() => setActiveTab('users')}
              className={`flex items-center gap-2 px-4 py-2 border-b-2 font-medium transition-colors ${
                activeTab === 'users'
                  ? 'border-primary-500 text-primary-400'
                  : 'border-transparent text-gray-400 hover:text-gray-200 hover:border-navy-600'
              }`}
            >
              <Users className="w-4 h-4" />
              Usuarios ({users.length})
            </button>
            <button
              onClick={() => setActiveTab('audit')}
              className={`flex items-center gap-2 px-4 py-2 border-b-2 font-medium transition-colors ${
                activeTab === 'audit'
                  ? 'border-primary-500 text-primary-400'
                  : 'border-transparent text-gray-400 hover:text-gray-200 hover:border-navy-600'
              }`}
            >
              <FileText className="w-4 h-4" />
              Audit Log ({auditTotal})
            </button>
          </div>
        </div>

        {/* Tab Content */}
        {activeTab === 'info' && (
          <div className="space-y-6">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              {/* Subscription Info */}
              <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
                <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
                  <h3 className="font-semibold text-gray-100">Información de Suscripción</h3>
                  <Button variant="ghost" size="sm" icon={Edit} onClick={() => setShowChangePlanModal(true)}>
                    Cambiar
                  </Button>
                </div>
                <div className="px-6 py-4 space-y-4">
                  {tenant.subscription ? (
                    <>
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-400">Plan Actual</span>
                        <Badge variant={getPlanBadgeVariant(tenant.subscription.plan)}>
                          {tenant.subscription.planName}
                        </Badge>
                      </div>

                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-400">Estado</span>
                        <Badge variant={getStatusBadgeVariant(tenant.subscription.status)}>
                          {tenant.subscription.statusName}
                        </Badge>
                      </div>

                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-400">Precio Mensual</span>
                        <span className="font-medium text-gray-100">
                          ${tenant.subscription.monthlyPrice.toFixed(2)}
                        </span>
                      </div>

                      {tenant.subscription.trialEndsAt && (
                        <div className="flex items-center justify-between">
                          <span className="text-sm text-gray-400">Trial Expira</span>
                          <div className="flex items-center gap-2">
                            <span className="font-medium text-gray-100">
                              {new Date(tenant.subscription.trialEndsAt).toLocaleDateString('es-PA')}
                            </span>
                            <button
                              onClick={() => setShowExtendTrialModal(true)}
                              className="text-xs text-primary-400 hover:text-primary-300 transition-colors"
                            >
                              Extender
                            </button>
                          </div>
                        </div>
                      )}

                      {/* Usage Progress Bars */}
                      <div className="pt-4 border-t border-navy-700 space-y-3">
                        <div>
                          <div className="flex justify-between text-sm mb-1">
                            <span className="text-gray-400">Uso de Empleados</span>
                            <span className="font-medium text-gray-100">
                              {Math.round((tenant.usage.totalEmployees / (tenant.subscription.maxEmployees || 1)) * 100)}%
                            </span>
                          </div>
                          <div className="w-full bg-navy-700 rounded-full h-2">
                            <div
                              className="bg-primary-500 h-2 rounded-full"
                              style={{
                                width: `${Math.min((tenant.usage.totalEmployees / (tenant.subscription.maxEmployees || 1)) * 100, 100)}%`,
                              }}
                            />
                          </div>
                        </div>

                        <div>
                          <div className="flex justify-between text-sm mb-1">
                            <span className="text-gray-400">Uso de Usuarios</span>
                            <span className="font-medium text-gray-100">
                              {Math.round((tenant.usage.totalUsers / (tenant.subscription.maxUsers || 1)) * 100)}%
                            </span>
                          </div>
                          <div className="w-full bg-navy-700 rounded-full h-2">
                            <div
                              className="bg-green-500 h-2 rounded-full"
                              style={{
                                width: `${Math.min((tenant.usage.totalUsers / (tenant.subscription.maxUsers || 1)) * 100, 100)}%`,
                              }}
                            />
                          </div>
                        </div>
                      </div>
                    </>
                  ) : (
                    <div className="text-center py-4 text-gray-400">
                      Este tenant no tiene una suscripción activa
                    </div>
                  )}
                </div>
              </div>

              {/* Owner Info */}
              <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
                <div className="px-6 py-4 border-b border-navy-700">
                  <h3 className="font-semibold text-gray-100">Propietario del Tenant</h3>
                </div>
                <div className="px-6 py-4 space-y-4">
                  <div>
                    <p className="text-sm text-gray-400 mb-1">Email</p>
                    <p className="font-medium text-gray-100">{tenant.owner?.email}</p>
                  </div>

                  <div>
                    <p className="text-sm text-gray-400 mb-1">Usuario desde</p>
                    <p className="font-medium text-gray-100">
                      {tenant.owner?.joinedAt && new Date(tenant.owner.joinedAt).toLocaleDateString('es-PA')}
                    </p>
                  </div>

                  {tenant.owner?.lastLoginAt && (
                    <div>
                      <p className="text-sm text-gray-400 mb-1">Último acceso</p>
                      <p className="font-medium text-gray-100">
                        {new Date(tenant.owner.lastLoginAt).toLocaleDateString('es-PA')}
                      </p>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Actions */}
            <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
              <div className="px-6 py-4 border-b border-navy-700">
                <h3 className="font-semibold text-gray-100">Acciones Administrativas</h3>
              </div>
              <div className="px-6 py-4">
                <div className="flex gap-3">
                  <Button variant="outline" icon={Edit} onClick={() => setShowChangePlanModal(true)}>
                    Cambiar Plan
                  </Button>
                  {tenant.subscription?.trialEndsAt && (
                    <Button variant="outline" icon={Calendar} onClick={() => setShowExtendTrialModal(true)}>
                      Extender Trial
                    </Button>
                  )}
                  <Button
                    variant={tenant.isActive ? 'danger' : 'success'}
                    icon={tenant.isActive ? XCircle : CheckCircle2}
                    onClick={() => setShowDeactivateModal(true)}
                  >
                    {tenant.isActive ? 'Desactivar' : 'Reactivar'} Tenant
                  </Button>
                </div>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'users' && (
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <Users className="w-5 h-5 text-gray-400" />
                <h3 className="font-semibold text-gray-100">
                  Usuarios del Tenant ({users.length}/{tenant.usage.maxUsers})
                </h3>
              </div>
              <Button icon={UserPlus} onClick={() => setShowInviteModal(true)}>
                Invitar Usuario
              </Button>
            </div>
            <div className="px-6 py-4">
              {isLoadingUsers ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="w-6 h-6 animate-spin text-primary-400" />
                </div>
              ) : users.length === 0 ? (
                <div className="text-center py-8 text-gray-400">
                  No hay usuarios registrados en este tenant
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead className="bg-navy-800">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Usuario
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Rol
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Estado
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Desde
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Último Acceso
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                          Acciones
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-navy-700">
                      {users.map((user) => {
                        const ownersCount = users.filter(
                          (u) => u.role === TenantRole.Owner && u.isActive && u.userId !== user.userId
                        ).length;
                        const isLastOwner = user.role === TenantRole.Owner && ownersCount === 0;

                        return (
                          <tr key={user.id} className="hover:bg-navy-800 transition-colors">
                            <td className="px-4 py-4 whitespace-nowrap">
                              <div className="flex items-center gap-3">
                                <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-primary-600 rounded-full flex items-center justify-center">
                                  <span className="text-white text-xs font-bold">
                                    {user.email.substring(0, 2).toUpperCase()}
                                  </span>
                                </div>
                                <div>
                                  <p className="text-sm font-medium text-gray-100">{user.email}</p>
                                  {user.fullName && (
                                    <p className="text-xs text-gray-400">{user.fullName}</p>
                                  )}
                                </div>
                              </div>
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap">
                              {user.customRoleName ? (
                                <div className="flex items-center gap-2">
                                  <span
                                    className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium border-2"
                                    style={{
                                      backgroundColor: `${user.customRoleColor}20`,
                                      borderColor: user.customRoleColor || '#6b7280',
                                      color: user.customRoleColor || '#374151',
                                    }}
                                  >
                                    {user.customRoleName}
                                  </span>
                                  <span className="text-xs text-gray-400">
                                    ({user.roleName})
                                  </span>
                                </div>
                              ) : (
                                <RoleBadge role={user.role} showIcon />
                              )}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap">
                              <StatusBadge isActive={user.isActive} />
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {new Date(user.joinedAt).toLocaleDateString('es-PA')}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {user.lastLoginAt
                                ? new Date(user.lastLoginAt).toLocaleDateString('es-PA')
                                : '-'}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {user.isActive && (
                                <Button
                                  variant="danger"
                                  size="sm"
                                  icon={deletingUserId === user.userId ? Loader2 : Trash2}
                                  onClick={() =>
                                    handleRemoveUser(user.userId, user.email, user.role)
                                  }
                                  disabled={deletingUserId === user.userId || isLastOwner}
                                  title={
                                    isLastOwner
                                      ? 'No se puede eliminar el último Owner del tenant'
                                      : 'Eliminar usuario del tenant'
                                  }
                                >
                                  {deletingUserId === user.userId ? 'Eliminando...' : 'Eliminar'}
                                </Button>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {activeTab === 'audit' && (
          <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
            <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
              <h3 className="font-semibold text-gray-100">
                Audit Log ({auditTotal} eventos)
              </h3>
            </div>
            <div className="px-6 py-4">
              {/* Filters */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
                <Input
                  label="Desde"
                  type="date"
                  value={auditFilters.from}
                  onChange={(e) => setAuditFilters({ ...auditFilters, from: e.target.value, page: 1 })}
                />
                <Input
                  label="Hasta"
                  type="date"
                  value={auditFilters.to}
                  onChange={(e) => setAuditFilters({ ...auditFilters, to: e.target.value, page: 1 })}
                />
                <Input
                  label="Acción"
                  type="text"
                  value={auditFilters.action}
                  onChange={(e) => setAuditFilters({ ...auditFilters, action: e.target.value, page: 1 })}
                  placeholder="Ej: InviteUser, Create, Update"
                />
              </div>

              {isLoadingAudit ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="w-6 h-6 animate-spin text-primary-400" />
                </div>
              ) : auditLogs.length === 0 ? (
                <div className="text-center py-8 text-gray-400">
                  No hay eventos de auditoría registrados
                </div>
              ) : (
                <>
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead className="bg-navy-800">
                        <tr>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Fecha
                          </th>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Usuario
                          </th>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Acción
                          </th>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Entidad
                          </th>
                          <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            IP
                          </th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-navy-700">
                        {auditLogs.map((log) => (
                          <tr key={log.id} className="hover:bg-navy-800 transition-colors">
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {log.createdAt
                                ? new Date(log.createdAt).toLocaleString('es-PA')
                                : '-'}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-100">
                              {log.actorEmail}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap">
                              <ActionBadge action={log.action} />
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {log.entityType}
                              {log.entityId && <span className="text-xs text-gray-500"> (#{log.entityId})</span>}
                            </td>
                            <td className="px-4 py-4 whitespace-nowrap text-sm text-gray-400">
                              {log.ipAddress || '-'}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  {/* Pagination */}
                  {auditTotal > auditFilters.pageSize && (
                    <div className="mt-4 flex items-center justify-between">
                      <div className="text-sm text-gray-400">
                        Mostrando {(auditFilters.page - 1) * auditFilters.pageSize + 1} -{' '}
                        {Math.min(auditFilters.page * auditFilters.pageSize, auditTotal)} de {auditTotal}
                      </div>
                      <div className="flex gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={auditFilters.page === 1}
                          onClick={() => setAuditFilters({ ...auditFilters, page: auditFilters.page - 1 })}
                        >
                          Anterior
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={auditFilters.page * auditFilters.pageSize >= auditTotal}
                          onClick={() => setAuditFilters({ ...auditFilters, page: auditFilters.page + 1 })}
                        >
                          Siguiente
                        </Button>
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        )}

        {/* Modals */}
        <InviteUserModal
          tenantId={parseInt(id!)}
          isOpen={showInviteModal}
          onClose={() => setShowInviteModal(false)}
          onSuccess={() => {
            loadUsers();
            loadTenant(); // Reload to update usage stats
          }}
        />

        <Modal
          isOpen={showChangePlanModal}
          onClose={() => setShowChangePlanModal(false)}
          title="Cambiar Plan de Suscripción"
        >
          <div className="space-y-4">
            <Select
              label="Nuevo Plan"
              value={newPlan.toString()}
              onChange={(e) => setNewPlan(parseInt(e.target.value))}
              options={[
                { value: SubscriptionPlan.Free.toString(), label: 'Free (5 empleados)' },
                { value: SubscriptionPlan.Starter.toString(), label: 'Starter (25 empleados)' },
                {
                  value: SubscriptionPlan.Professional.toString(),
                  label: 'Professional (100 empleados)',
                },
                { value: SubscriptionPlan.Enterprise.toString(), label: 'Enterprise (Ilimitado)' },
              ]}
            />
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="outline" onClick={() => setShowChangePlanModal(false)}>
                Cancelar
              </Button>
              <Button onClick={handleChangePlan} loading={isSubmitting}>
                Cambiar Plan
              </Button>
            </div>
          </div>
        </Modal>

        <Modal
          isOpen={showExtendTrialModal}
          onClose={() => setShowExtendTrialModal(false)}
          title="Extender Trial"
        >
          <div className="space-y-4">
            <Input
              label="Días a Extender"
              type="number"
              value={extendDays}
              onChange={(e) => setExtendDays(parseInt(e.target.value) || 0)}
              min="1"
              max="365"
              helperText="Agregar días adicionales al trial actual"
            />
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="outline" onClick={() => setShowExtendTrialModal(false)}>
                Cancelar
              </Button>
              <Button onClick={handleExtendTrial} loading={isSubmitting}>
                Extender Trial
              </Button>
            </div>
          </div>
        </Modal>

        <Modal
          isOpen={showDeactivateModal}
          onClose={() => setShowDeactivateModal(false)}
          title={tenant.isActive ? 'Desactivar Tenant' : 'Reactivar Tenant'}
        >
          <div className="space-y-4">
            <div className="flex items-start gap-3">
              <AlertCircle className="w-6 h-6 text-amber-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-gray-100 font-medium mb-2">
                  {tenant.isActive
                    ? '¿Estás seguro de que deseas desactivar este tenant?'
                    : '¿Estás seguro de que deseas reactivar este tenant?'}
                </p>
                <p className="text-sm text-gray-400">
                  {tenant.isActive
                    ? 'Los usuarios no podrán acceder al sistema hasta que sea reactivado.'
                    : 'Los usuarios podrán acceder nuevamente al sistema.'}
                </p>
              </div>
            </div>
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="outline" onClick={() => setShowDeactivateModal(false)}>
                Cancelar
              </Button>
              <Button
                variant={tenant.isActive ? 'danger' : 'success'}
                onClick={handleDeactivate}
                loading={isSubmitting}
              >
                {tenant.isActive ? 'Desactivar' : 'Reactivar'}
              </Button>
            </div>
          </div>
        </Modal>
      </div>
    </SystemAdminLayout>
  );
}
