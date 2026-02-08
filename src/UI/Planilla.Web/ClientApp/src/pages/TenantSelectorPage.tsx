import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';
import { Building2, ChevronRight, Shield, User, Clock } from 'lucide-react';
import { TenantRole } from '../types/api';

const TENANT_SELECTION_TIMEOUT_MS = 5 * 60 * 1000; // 5 minutos
const LAST_TENANT_KEY = 'pagly_last_tenant_id';

export default function TenantSelectorPage() {
  const { availableTenants, selectTenant, user, logout } = useAuth();
  const navigate = useNavigate();
  const [isSelecting, setIsSelecting] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [timeRemaining, setTimeRemaining] = useState(TENANT_SELECTION_TIMEOUT_MS);

  // Timeout de 5 minutos - redirigir a login si no se selecciona tenant
  useEffect(() => {
    const timer = setInterval(() => {
      setTimeRemaining((prev) => {
        const newTime = prev - 1000;
        if (newTime <= 0) {
          clearInterval(timer);
          toast.error('Tiempo de selección expirado. Por favor, inicia sesión nuevamente.');
          logout();
          navigate('/login', { replace: true });
          return 0;
        }
        return newTime;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [logout, navigate]);

  // Cargar último tenant seleccionado de localStorage
  useEffect(() => {
    const lastTenantId = localStorage.getItem(LAST_TENANT_KEY);
    if (lastTenantId) {
      const tenantIdNum = parseInt(lastTenantId, 10);
      const tenantExists = availableTenants.some(t => t.id === tenantIdNum);
      if (tenantExists) {
        setSelectedId(tenantIdNum);
      }
    }
  }, [availableTenants]);

  const handleSelectTenant = async (tenantId: number) => {
    setIsSelecting(true);
    setSelectedId(tenantId);

    try {
      await selectTenant(tenantId);
      // Guardar último tenant en localStorage
      localStorage.setItem(LAST_TENANT_KEY, tenantId.toString());
      toast.success('Empresa seleccionada exitosamente');
      navigate('/dashboard', { replace: true });
    } catch (error: any) {
      toast.error(error.message || 'Error al seleccionar empresa');
      setIsSelecting(false);
      setSelectedId(null);
    }
  };

  const formatTimeRemaining = () => {
    const minutes = Math.floor(timeRemaining / 60000);
    const seconds = Math.floor((timeRemaining % 60000) / 1000);
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  };

  const getRoleIcon = (role: TenantRole) => {
    switch (role) {
      case TenantRole.Owner:
        return <Shield className="w-5 h-5 text-primary-400" />;
      case TenantRole.User:
        return <User className="w-5 h-5 text-blue-400" />;
      default:
        return <User className="w-5 h-5 text-gray-400" />;
    }
  };

  const getRoleBadgeColor = (role: TenantRole) => {
    switch (role) {
      case TenantRole.Owner:
        return 'bg-primary-500/15 text-primary-400 border-primary-500/30';
      case TenantRole.User:
        return 'bg-blue-500/15 text-blue-400 border-blue-500/30';
      default:
        return 'bg-navy-700 text-gray-300 border-navy-600';
    }
  };

  if (!user || availableTenants.length === 0) {
    return (
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl shadow-black/30 p-8 max-w-md w-full text-center">
          <div className="w-16 h-16 bg-red-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
            <Building2 className="w-8 h-8 text-red-400" />
          </div>
          <h2 className="text-2xl font-bold text-gray-100 mb-2">Sin Empresas Disponibles</h2>
          <p className="text-gray-400 mb-6">
            No tienes acceso a ninguna empresa. Contacta a tu administrador.
          </p>
          <button
            onClick={() => navigate('/login', { replace: true })}
            className="w-full px-4 py-2 bg-primary-600 text-white font-medium rounded-lg hover:bg-primary-700 transition-colors"
          >
            Volver al Login
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
      <div className="max-w-2xl w-full">
        {/* Logo y Header */}
        <div className="text-center mb-8">
          <div className="flex justify-center mb-4">
            <PaglyLogo variant="icon" theme="dark" size="lg" />
          </div>
          <h1 className="text-3xl font-bold text-gray-100 font-display">Pagly</h1>
          <p className="text-gray-400 mt-2">Selecciona una empresa</p>
        </div>

        {/* User Info */}
        <div className="bg-navy-900 border border-navy-700 rounded-xl shadow-lg shadow-black/20 p-4 mb-6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-primary-500/15 rounded-full flex items-center justify-center">
              <User className="w-5 h-5 text-primary-400" />
            </div>
            <div>
              <p className="text-sm text-gray-500">Sesión iniciada como</p>
              <p className="font-medium text-gray-100">{user.email}</p>
            </div>
          </div>
        </div>

        {/* Tenants List */}
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl shadow-black/30 p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-bold text-gray-100">Empresas Disponibles</h2>
            {timeRemaining <= 60000 && (
              <div className="flex items-center gap-2 text-red-400">
                <Clock className="w-4 h-4" />
                <span className="text-sm font-medium">{formatTimeRemaining()}</span>
              </div>
            )}
          </div>

          <div className="space-y-3">
            {availableTenants.map((tenant) => (
              <button
                key={tenant.id}
                onClick={() => handleSelectTenant(tenant.id)}
                disabled={isSelecting}
                className={`w-full p-4 border-2 rounded-lg text-left transition-all ${
                  selectedId === tenant.id
                    ? 'border-primary-500 bg-primary-500/10 ring-2 ring-primary-500/20'
                    : 'border-navy-700 hover:border-primary-500/50 hover:bg-navy-800'
                } disabled:opacity-50 disabled:cursor-not-allowed`}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-4 flex-1">
                    <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center flex-shrink-0">
                      <Building2 className="w-6 h-6 text-primary-400" />
                    </div>

                    <div className="flex-1 min-w-0">
                      <h3 className="text-lg font-semibold text-gray-100 truncate">
                        {tenant.name}
                      </h3>
                      <div className="flex items-center gap-2 mt-1">
                        {getRoleIcon(tenant.role)}
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${getRoleBadgeColor(
                            tenant.role
                          )}`}
                        >
                          {tenant.roleName}
                        </span>
                      </div>
                    </div>
                  </div>

                  {isSelecting && selectedId === tenant.id ? (
                    <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary-500" />
                  ) : (
                    <ChevronRight className="w-5 h-5 text-gray-500" />
                  )}
                </div>
              </button>
            ))}
          </div>

          {/* Logout Button */}
          <div className="mt-6 pt-6 border-t border-navy-700">
            <button
              onClick={() => navigate('/login', { replace: true })}
              className="w-full px-4 py-2 text-gray-400 hover:text-gray-200 font-medium transition-colors"
              disabled={isSelecting}
            >
              Cancelar y Cerrar Sesión
            </button>
          </div>
        </div>

        <p className="text-center text-gray-500 text-sm mt-8">
          © {new Date().getFullYear()} Pagly. Todos los derechos reservados.
        </p>
      </div>
    </div>
  );
}
