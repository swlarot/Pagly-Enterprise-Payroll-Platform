import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { Modal } from '../components/ui/Modal';
import { Input } from '../components/ui/Input';
import { systemAdminService } from '../services/systemAdminService';
import type { AdminTenantDto, UpdateAdminTenantDto } from '../types/api';
import { SubscriptionPlan, SubscriptionStatus } from '../types/api';
import {
  Plus,
  Search,
  Eye,
  Loader2,
  Pencil,
} from 'lucide-react';
import { useAsyncLoad } from '../hooks/useAsyncLoad';
import { Pagination } from '../components/ui/Pagination';
import toast from 'react-hot-toast';

export default function TenantsManagementPage() {
  const navigate = useNavigate();
  const [allTenants, setAllTenants] = useState<AdminTenantDto[]>([]);
  const { isLoading, run } = useAsyncLoad();
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);

  // Edit tenant state
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState<AdminTenantDto | null>(null);
  const [editForm, setEditForm] = useState({
    name: '',
    ruc: '',
    dv: '',
    address: '',
    phone: '',
    email: '',
    isActive: true,
  });
  const [editErrors, setEditErrors] = useState<Record<string, string>>({});
  const [isUpdatingTenant, setIsUpdatingTenant] = useState(false);

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [planFilter, setPlanFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState('');

  useEffect(() => {
    loadTenants();
  }, []);

  const loadTenants = () => run(async () => {
    const data = await systemAdminService.getAllTenants();
    setAllTenants(data);
  }, 'Error al cargar tenants');

  // Client-side filtering and pagination
  const filteredTenants = useMemo(() => {
    let filtered = [...allTenants];

    // Search filter
    if (searchTerm) {
      const search = searchTerm.toLowerCase();
      filtered = filtered.filter(
        (t) =>
          t.name.toLowerCase().includes(search) ||
          t.ruc?.toLowerCase().includes(search) ||
          t.email?.toLowerCase().includes(search)
      );
    }

    // Plan filter
    if (planFilter) {
      filtered = filtered.filter(
        (t) => t.subscription?.plan === parseInt(planFilter)
      );
    }

    // Status filter
    if (statusFilter) {
      filtered = filtered.filter(
        (t) => t.subscription?.status === parseInt(statusFilter)
      );
    }

    // Active filter
    if (activeFilter) {
      filtered = filtered.filter((t) => t.isActive === (activeFilter === 'true'));
    }

    return filtered;
  }, [allTenants, searchTerm, planFilter, statusFilter, activeFilter]);

  // Paginated tenants
  const { paginatedTenants, totalPages, totalCount } = useMemo(() => {
    const startIndex = (page - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    return {
      paginatedTenants: filteredTenants.slice(startIndex, endIndex),
      totalPages: Math.ceil(filteredTenants.length / pageSize),
      totalCount: filteredTenants.length,
    };
  }, [filteredTenants, page, pageSize]);

  // Reset page when filters change
  useEffect(() => {
    setPage(1);
  }, [searchTerm, planFilter, statusFilter, activeFilter]);

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

  const openEditModal = (tenant: AdminTenantDto) => {
    setSelectedTenant(tenant);
    setEditForm({
      name: tenant.name ?? '',
      ruc: tenant.ruc ?? '',
      dv: tenant.dv ?? '',
      address: tenant.address ?? '',
      phone: tenant.phone ?? '',
      email: tenant.email ?? '',
      isActive: tenant.isActive,
    });
    setEditErrors({});
    setIsEditModalOpen(true);
  };

  const closeEditModal = () => {
    if (isUpdatingTenant) return;
    setIsEditModalOpen(false);
    setSelectedTenant(null);
    setEditErrors({});
  };

  const handleEditInputChange = (field: keyof typeof editForm, value: string | boolean) => {
    setEditForm((prev) => ({
      ...prev,
      [field]: value,
    }));

    if (editErrors[field]) {
      setEditErrors((prev) => ({
        ...prev,
        [field]: '',
      }));
    }
  };

  const validateEditForm = () => {
    const newErrors: Record<string, string> = {};

    if (!editForm.name.trim()) {
      newErrors.name = 'El nombre de la empresa es requerido';
    }

    if (!editForm.ruc.trim()) {
      newErrors.ruc = 'El RUC es requerido';
    }

    if (!editForm.dv.trim()) {
      newErrors.dv = 'El DV es requerido';
    }

    if (editForm.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(editForm.email)) {
      newErrors.email = 'El email no es válido';
    }

    setEditErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleUpdateTenant = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!selectedTenant) return;

    if (!validateEditForm()) {
      return;
    }

    try {
      setIsUpdatingTenant(true);

      const payload: UpdateAdminTenantDto = {
        nombre: editForm.name.trim(),
        ruc: editForm.ruc.trim(),
        dv: editForm.dv.trim(),
        direccion: editForm.address.trim() || undefined,
        telefono: editForm.phone.trim() || undefined,
        correo: editForm.email.trim() || undefined,
        isActive: editForm.isActive,
      };

      const updatedTenant = await systemAdminService.updateTenant(selectedTenant.id, payload);

      setAllTenants((prev) =>
        prev.map((tenant) => (tenant.id === updatedTenant.id ? updatedTenant : tenant)),
      );

      toast.success('Empresa actualizada exitosamente');
      setIsEditModalOpen(false);
      setSelectedTenant(null);
    } catch (error: unknown) {
      const message =
        error instanceof Error ? error.message : 'Error al actualizar la empresa';
      toast.error(message);
    } finally {
      setIsUpdatingTenant(false);
    }
  };

  return (
    <SystemAdminLayout>
      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-100">Gestión de Tenants</h1>
            <p className="text-gray-400 mt-2">
              {totalCount} {totalCount === 1 ? 'empresa registrada' : 'empresas registradas'}
            </p>
          </div>
          <Button
            icon={Plus}
            onClick={() => navigate('/system-admin/tenants/create')}
          >
            Crear Tenant
          </Button>
        </div>

        {/* Filters */}
        <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 mb-6 px-6 py-4">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <input
              type="text"
              placeholder="Buscar por nombre, RUC..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            />
            <select
              value={planFilter}
              onChange={(e) => {
                setPlanFilter(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            >
              <option value="">Todos los planes</option>
              <option value={SubscriptionPlan.Free.toString()}>Free</option>
              <option value={SubscriptionPlan.Starter.toString()}>Starter</option>
              <option value={SubscriptionPlan.Professional.toString()}>Professional</option>
              <option value={SubscriptionPlan.Enterprise.toString()}>Enterprise</option>
            </select>
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            >
              <option value="">Todos los estados</option>
              <option value={SubscriptionStatus.Active.toString()}>Activo</option>
              <option value={SubscriptionStatus.Trialing.toString()}>Trial</option>
              <option value={SubscriptionStatus.PastDue.toString()}>Vencido</option>
              <option value={SubscriptionStatus.Canceled.toString()}>Cancelado</option>
            </select>
            <select
              value={activeFilter}
              onChange={(e) => {
                setActiveFilter(e.target.value);
                setPage(1);
              }}
              className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            >
              <option value="">Todos</option>
              <option value="true">Activos</option>
              <option value="false">Inactivos</option>
            </select>
          </div>
        </div>

        {/* Tenants Table */}
        <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-navy-800 border-b border-navy-700">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Empresa
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    RUC
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Plan
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Estado
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Empleados
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Usuarios
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Fecha Creación
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Acciones
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-navy-700">
                {isLoading ? (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center">
                      <Loader2 className="w-8 h-8 animate-spin text-primary-400 mx-auto" />
                    </td>
                  </tr>
                ) : paginatedTenants.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center">
                      <Search className="w-12 h-12 text-gray-500 mx-auto mb-3" />
                      <p className="text-gray-400">No se encontraron tenants</p>
                    </td>
                  </tr>
                ) : (
                  paginatedTenants.map((tenant) => (
                    <tr key={tenant.id} className="hover:bg-navy-800 transition-colors">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div>
                          <div className="font-medium text-gray-100">{tenant.name}</div>
                          <div className="text-sm text-gray-400">{tenant.owner?.email || 'Sin propietario'}</div>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                        {tenant.ruc && tenant.dv ? `${tenant.ruc}-${tenant.dv}` : 'N/A'}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {tenant.subscription ? (
                          <Badge variant={getPlanBadgeVariant(tenant.subscription.plan)}>
                            {tenant.subscription.planName}
                          </Badge>
                        ) : (
                          <Badge variant="default">Sin plan</Badge>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {tenant.subscription ? (
                          <Badge variant={getStatusBadgeVariant(tenant.subscription.status)}>
                            {tenant.subscription.statusName}
                          </Badge>
                        ) : (
                          <Badge variant="default">Inactivo</Badge>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                        {tenant.usage.totalEmployees}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                        {tenant.usage.totalUsers}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-400">
                        {new Date(tenant.createdAt).toLocaleDateString('es-PA')}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            icon={Eye}
                            onClick={() =>
                              navigate(`/system-admin/tenants/${tenant.id}`)
                            }
                          >
                            Ver
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            icon={Pencil}
                            onClick={() => openEditModal(tenant)}
                          >
                            Editar
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <Pagination
            page={page}
            totalPages={totalPages}
            totalCount={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
          />
        </div>
      </div>

      {/* Edit Tenant Modal */}
      <Modal
        isOpen={isEditModalOpen && !!selectedTenant}
        onClose={closeEditModal}
        title="Editar Empresa"
        size="md"
      >
        <form onSubmit={handleUpdateTenant} className="space-y-6">
          <div className="space-y-4">
            <Input
              label="Nombre de la Empresa"
              value={editForm.name}
              onChange={(e) => handleEditInputChange('name', e.target.value)}
              error={editErrors.name}
              required
              placeholder="Empresa ABC S.A."
            />

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Input
                label="RUC"
                value={editForm.ruc}
                onChange={(e) => handleEditInputChange('ruc', e.target.value)}
                error={editErrors.ruc}
                required
                placeholder="123456789"
              />
              <Input
                label="DV"
                value={editForm.dv}
                onChange={(e) => handleEditInputChange('dv', e.target.value)}
                error={editErrors.dv}
                required
                maxLength={2}
                placeholder="12"
              />
            </div>

            <Input
              label="Dirección"
              value={editForm.address}
              onChange={(e) => handleEditInputChange('address', e.target.value)}
              placeholder="Calle 50, Edificio XYZ, Piso 5"
            />

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Input
                label="Teléfono"
                value={editForm.phone}
                onChange={(e) => handleEditInputChange('phone', e.target.value)}
                placeholder="+507 6000-0000"
              />
              <Input
                label="Correo Electrónico"
                type="email"
                value={editForm.email}
                onChange={(e) => handleEditInputChange('email', e.target.value)}
                error={editErrors.email}
                placeholder="contacto@empresa.com"
              />
            </div>

            <div className="flex items-center gap-3">
              <label className="text-sm font-medium text-gray-300">
                Empresa activa
              </label>
              <button
                type="button"
                onClick={() =>
                  handleEditInputChange('isActive', !editForm.isActive)
                }
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                  editForm.isActive ? 'bg-green-500' : 'bg-navy-700'
                }`}
              >
                <span
                  className={`inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform ${
                    editForm.isActive ? 'translate-x-5' : 'translate-x-1'
                  }`}
                />
              </button>
              <span className="text-sm text-gray-300">
                {editForm.isActive ? 'Activa' : 'Inactiva'}
              </span>
            </div>
          </div>

          <div className="flex gap-3 pt-4">
            <Button
              type="button"
              variant="outline"
              onClick={closeEditModal}
              disabled={isUpdatingTenant}
              className="flex-1"
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              disabled={isUpdatingTenant}
              className="flex-1"
            >
              {isUpdatingTenant ? 'Guardando...' : 'Guardar Cambios'}
            </Button>
          </div>
        </form>
      </Modal>
    </SystemAdminLayout>
  );
}
