import { useEffect, useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { SystemAdminLayout } from '../components/layout/SystemAdminLayout';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { systemAdminService } from '../services/systemAdminService';
import type { AdminTenantDto } from '../types/api';
import { SubscriptionPlan, SubscriptionStatus } from '../types/api';
import {
  Plus,
  Search,
  Eye,
  Loader2,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import toast from 'react-hot-toast';

export default function TenantsManagementPage() {
  const navigate = useNavigate();
  const [allTenants, setAllTenants] = useState<AdminTenantDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [planFilter, setPlanFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [activeFilter, setActiveFilter] = useState('');

  useEffect(() => {
    loadTenants();
  }, []);

  const loadTenants = async () => {
    try {
      setIsLoading(true);
      const data = await systemAdminService.getAllTenants();
      setAllTenants(data);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'Error al cargar tenants';
      toast.error(message);
    } finally {
      setIsLoading(false);
    }
  };

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
                        <Button
                          variant="ghost"
                          size="sm"
                          icon={Eye}
                          onClick={() => navigate(`/system-admin/tenants/${tenant.id}`)}
                        >
                          Ver
                        </Button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="px-6 py-4 border-t border-navy-700 flex items-center justify-between">
              <div className="text-sm text-gray-400">
                Mostrando {(page - 1) * pageSize + 1} a{' '}
                {Math.min(page * pageSize, totalCount)} de {totalCount} resultados
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  icon={ChevronLeft}
                  onClick={() => setPage(page - 1)}
                  disabled={page === 1}
                >
                  {''}
                </Button>
                <span className="text-sm text-gray-400">
                  Página {page} de {totalPages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  icon={ChevronRight}
                  onClick={() => setPage(page + 1)}
                  disabled={page === totalPages}
                >
                  {''}
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </SystemAdminLayout>
  );
}
