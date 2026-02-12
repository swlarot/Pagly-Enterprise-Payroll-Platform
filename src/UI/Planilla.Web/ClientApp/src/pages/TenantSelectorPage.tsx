import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';
import { Building2, Shield, User, Clock, ArrowRight, LogOut, Crown, Briefcase, BookOpen } from 'lucide-react';
import { TenantRole } from '../types/api';

const TENANT_SELECTION_TIMEOUT_MS = 5 * 60 * 1000;
const LAST_TENANT_KEY = 'pagly_last_tenant_id';

// Colores e iconos por rol
const roleConfig: Record<number, { label: string; icon: typeof Shield; color: string; bg: string; border: string }> = {
  [TenantRole.Owner]: {
    label: 'Propietario',
    icon: Crown,
    color: '#fbbf24',
    bg: 'rgba(251,191,36,0.08)',
    border: 'rgba(251,191,36,0.2)',
  },
  [TenantRole.Admin]: {
    label: 'Administrador',
    icon: Shield,
    color: '#10b981',
    bg: 'rgba(16,185,129,0.08)',
    border: 'rgba(16,185,129,0.2)',
  },
  [TenantRole.Manager]: {
    label: 'Gerente',
    icon: Briefcase,
    color: '#60a5fa',
    bg: 'rgba(96,165,250,0.08)',
    border: 'rgba(96,165,250,0.2)',
  },
  [TenantRole.Accountant]: {
    label: 'Contador',
    icon: BookOpen,
    color: '#a78bfa',
    bg: 'rgba(167,139,250,0.08)',
    border: 'rgba(167,139,250,0.2)',
  },
};

const defaultRole = {
  label: 'Usuario',
  icon: User,
  color: '#627d98',
  bg: 'rgba(98,125,152,0.08)',
  border: 'rgba(98,125,152,0.2)',
};

// Genera un color sólido a partir del nombre del tenant (para el avatar)
function tenantColor(name: string): string {
  const palette = ['#10b981', '#0d9488', '#059669', '#0284c7', '#7c3aed', '#db2777', '#ea580c'];
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return palette[Math.abs(hash) % palette.length];
}

export default function TenantSelectorPage() {
  const { availableTenants, selectTenant, user, logout } = useAuth();
  const navigate = useNavigate();
  const [isSelecting, setIsSelecting] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [timeRemaining, setTimeRemaining] = useState(TENANT_SELECTION_TIMEOUT_MS);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setMounted(true), 60);
    return () => clearTimeout(t);
  }, []);

  // Timeout de 5 minutos
  useEffect(() => {
    const timer = setInterval(() => {
      setTimeRemaining((prev) => {
        const next = prev - 1000;
        if (next <= 0) {
          clearInterval(timer);
          toast.error('Tiempo de selección expirado. Por favor, inicia sesión nuevamente.');
          logout();
          navigate('/login', { replace: true });
          return 0;
        }
        return next;
      });
    }, 1000);
    return () => clearInterval(timer);
  }, [logout, navigate]);

  // Último tenant usado
  useEffect(() => {
    const lastId = localStorage.getItem(LAST_TENANT_KEY);
    if (lastId) {
      const num = parseInt(lastId, 10);
      if (availableTenants.some(t => t.id === num)) setSelectedId(num);
    }
  }, [availableTenants]);

  const handleSelectTenant = async (tenantId: number) => {
    setIsSelecting(true);
    setSelectedId(tenantId);
    try {
      await selectTenant(tenantId);
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
    const m = Math.floor(timeRemaining / 60000);
    const s = Math.floor((timeRemaining % 60000) / 1000);
    return `${m}:${s.toString().padStart(2, '0')}`;
  };

  // Estado: sin empresas
  if (!user || availableTenants.length === 0) {
    return (
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-6">
        <div
          className="max-w-sm w-full text-center p-10 rounded-2xl"
          style={{
            background: 'rgba(16,42,67,0.5)',
            border: '1px solid #334e68',
            backdropFilter: 'blur(12px)',
          }}
        >
          <div
            className="w-16 h-16 rounded-full flex items-center justify-center mx-auto mb-5"
            style={{ background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.2)' }}
          >
            <Building2 className="w-8 h-8" style={{ color: '#ef4444' }} />
          </div>
          <h2 className="text-xl font-bold text-gray-100 font-display mb-2">Sin Empresas</h2>
          <p className="text-navy-400 text-sm mb-6 leading-relaxed">
            No tienes acceso a ninguna empresa. Contacta a tu administrador.
          </p>
          <button
            onClick={() => navigate('/login', { replace: true })}
            className="w-full py-2.5 rounded-xl text-sm font-semibold text-white transition-all duration-200"
            style={{ background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)' }}
          >
            Volver al Login
          </button>
        </div>
      </div>
    );
  }

  const useTwoColumns = availableTenants.length > 2;

  return (
    <div
      className="min-h-screen flex items-center justify-center p-6 relative overflow-hidden"
      style={{ background: '#0a1929' }}
    >
      {/* Grid de fondo */}
      <svg className="absolute inset-0 w-full h-full opacity-[0.04] pointer-events-none" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <pattern id="tsgrid" width="56" height="56" patternUnits="userSpaceOnUse">
            <path d="M 56 0 L 0 0 0 56" fill="none" stroke="#10b981" strokeWidth="0.8" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#tsgrid)" />
      </svg>

      {/* Orbes */}
      <div
        className="absolute top-[-150px] right-[-100px] w-[500px] h-[500px] rounded-full pointer-events-none"
        style={{
          background: 'radial-gradient(circle, rgba(16,185,129,0.08) 0%, transparent 70%)',
          animation: 'driftA 16s ease-in-out infinite',
        }}
      />
      <div
        className="absolute bottom-[-80px] left-[-80px] w-[380px] h-[380px] rounded-full pointer-events-none"
        style={{
          background: 'radial-gradient(circle, rgba(13,148,136,0.07) 0%, transparent 70%)',
          animation: 'driftB 20s ease-in-out infinite',
        }}
      />

      <div
        className="relative z-10 w-full"
        style={{ maxWidth: useTwoColumns ? '680px' : '460px' }}
      >
        {/* Header */}
        <div
          className="text-center mb-8"
          style={{
            opacity: mounted ? 1 : 0,
            transform: mounted ? 'translateY(0)' : 'translateY(-16px)',
            transition: 'opacity 0.5s ease, transform 0.5s ease',
          }}
        >
          <div className="flex justify-center mb-4">
            <PaglyLogo variant="icon" theme="dark" size="lg" />
          </div>
          <h1 className="text-2xl font-bold text-gray-100 font-display tracking-tight">
            Selecciona tu empresa
          </h1>
          <p className="text-navy-400 text-sm mt-1">
            Hola, <span className="text-gray-300">{user.email}</span>
          </p>
        </div>

        {/* Grid de tenants */}
        <div
          className={`grid gap-3 ${useTwoColumns ? 'grid-cols-2' : 'grid-cols-1'}`}
        >
          {availableTenants.map((tenant, i) => {
            const role = roleConfig[tenant.role as number] ?? defaultRole;
            const RoleIcon = role.icon;
            const avatarColor = tenantColor(tenant.name);
            const initials = tenant.name
              .split(' ')
              .slice(0, 2)
              .map((w: string) => w[0])
              .join('')
              .toUpperCase();
            const isThis = selectedId === tenant.id;
            const isLoadingThis = isSelecting && isThis;

            return (
              <button
                key={tenant.id}
                onClick={() => !isSelecting && handleSelectTenant(tenant.id)}
                disabled={isSelecting}
                className="group text-left rounded-2xl p-5 transition-all duration-200 disabled:cursor-not-allowed relative overflow-hidden"
                style={{
                  background: isThis
                    ? 'rgba(16,185,129,0.06)'
                    : 'rgba(16,42,67,0.45)',
                  border: isThis
                    ? '1.5px solid rgba(16,185,129,0.4)'
                    : '1.5px solid rgba(51,78,104,0.6)',
                  backdropFilter: 'blur(10px)',
                  boxShadow: isThis
                    ? '0 0 0 4px rgba(16,185,129,0.06), 0 8px 24px rgba(0,0,0,0.3)'
                    : '0 4px 16px rgba(0,0,0,0.2)',
                  opacity: mounted ? 1 : 0,
                  transform: mounted ? 'translateY(0)' : 'translateY(16px)',
                  transition: `opacity 0.5s ease ${0.1 + i * 0.07}s, transform 0.5s ease ${0.1 + i * 0.07}s, background 0.2s, border 0.2s, box-shadow 0.2s`,
                }}
                onMouseEnter={(e) => {
                  if (!isSelecting && !isThis) {
                    const el = e.currentTarget as HTMLElement;
                    el.style.background = 'rgba(16,185,129,0.04)';
                    el.style.border = '1.5px solid rgba(16,185,129,0.25)';
                    el.style.boxShadow = '0 0 0 3px rgba(16,185,129,0.04), 0 8px 24px rgba(0,0,0,0.25)';
                    el.style.transform = 'translateY(-2px)';
                  }
                }}
                onMouseLeave={(e) => {
                  if (!isThis) {
                    const el = e.currentTarget as HTMLElement;
                    el.style.background = 'rgba(16,42,67,0.45)';
                    el.style.border = '1.5px solid rgba(51,78,104,0.6)';
                    el.style.boxShadow = '0 4px 16px rgba(0,0,0,0.2)';
                    el.style.transform = 'translateY(0)';
                  }
                }}
              >
                {/* Highlight glow seleccionado */}
                {isThis && (
                  <div
                    className="absolute inset-0 pointer-events-none"
                    style={{
                      background: 'radial-gradient(ellipse at top left, rgba(16,185,129,0.06) 0%, transparent 60%)',
                    }}
                  />
                )}

                <div className="flex items-start gap-4 relative">
                  {/* Avatar inicial */}
                  <div
                    className="w-12 h-12 rounded-xl flex items-center justify-center flex-shrink-0 text-white font-bold text-sm font-display"
                    style={{
                      background: `linear-gradient(135deg, ${avatarColor}cc 0%, ${avatarColor}88 100%)`,
                      border: `1px solid ${avatarColor}33`,
                    }}
                  >
                    {isLoadingThis ? (
                      <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    ) : (
                      initials
                    )}
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <h3 className="text-gray-100 font-semibold text-sm leading-tight truncate pr-6">
                      {tenant.name}
                    </h3>
                    <div className="flex items-center gap-1.5 mt-1.5">
                      <RoleIcon className="w-3.5 h-3.5 flex-shrink-0" style={{ color: role.color }} />
                      <span
                        className="text-xs font-medium px-2 py-0.5 rounded-full"
                        style={{
                          color: role.color,
                          background: role.bg,
                          border: `1px solid ${role.border}`,
                        }}
                      >
                        {tenant.roleName || role.label}
                      </span>
                    </div>
                  </div>

                  {/* Flecha */}
                  <div
                    className="absolute right-0 top-1/2 -translate-y-1/2 transition-all duration-200"
                    style={{
                      opacity: isThis ? 1 : 0.3,
                      transform: `translateY(-50%) translateX(${isThis ? '0' : '-2px'})`,
                    }}
                  >
                    <ArrowRight
                      className="w-4 h-4 transition-transform duration-200 group-hover:translate-x-0.5"
                      style={{ color: isThis ? '#10b981' : '#627d98' }}
                    />
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        {/* Footer */}
        <div
          className="mt-6 flex items-center justify-between"
          style={{
            opacity: mounted ? 1 : 0,
            transition: 'opacity 0.5s ease 0.5s',
          }}
        >
          {/* Timer — solo visible cuando queda <60s */}
          {timeRemaining <= 60000 ? (
            <div
              className="flex items-center gap-1.5 text-xs font-medium"
              style={{ color: '#ef4444' }}
            >
              <Clock className="w-3.5 h-3.5" />
              <span>Expira en {formatTimeRemaining()}</span>
            </div>
          ) : (
            <div />
          )}

          <button
            onClick={() => { logout(); navigate('/login', { replace: true }); }}
            disabled={isSelecting}
            className="flex items-center gap-1.5 text-xs text-navy-500 hover:text-navy-300 transition-colors disabled:opacity-40"
          >
            <LogOut className="w-3.5 h-3.5" />
            Cerrar sesión
          </button>
        </div>

        <div className="flex items-center justify-between mt-6">
          <p className="text-navy-600 text-xs">
            © {new Date().getFullYear()} Pagly · Todos los derechos reservados
          </p>
          <a
            href="https://vorluno.dev"
            target="_blank"
            rel="noopener noreferrer"
            className="text-navy-600 text-xs hover:text-navy-400 transition-colors"
          >
            Desarrollado por vorluno.dev
          </a>
        </div>
      </div>

      <style>{`
        @keyframes driftA {
          0%, 100% { transform: translate(0, 0) scale(1); }
          33%       { transform: translate(30px, -20px) scale(1.05); }
          66%       { transform: translate(-20px, 15px) scale(0.97); }
        }
        @keyframes driftB {
          0%, 100% { transform: translate(0, 0) scale(1); }
          40%       { transform: translate(-25px, 20px) scale(1.04); }
          70%       { transform: translate(15px, -10px) scale(0.98); }
        }
      `}</style>
    </div>
  );
}
