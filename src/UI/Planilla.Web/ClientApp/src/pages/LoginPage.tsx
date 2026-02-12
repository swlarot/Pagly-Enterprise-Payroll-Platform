import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';
import { Mail, Lock, ArrowRight, CheckCircle2, TrendingUp, Globe } from 'lucide-react';

const features = [
  {
    icon: CheckCircle2,
    title: 'Cumplimiento CSS Total',
    desc: 'Ley 462, SE e ISR calculados automáticamente',
  },
  {
    icon: TrendingUp,
    title: 'Multi-empresa',
    desc: 'Gestiona varias empresas desde una sola cuenta',
  },
  {
    icon: Globe,
    title: 'Siempre actualizado',
    desc: 'Tablas fiscales y regulaciones en tiempo real',
  },
];

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [emailFocused, setEmailFocused] = useState(false);
  const [passwordFocused, setPasswordFocused] = useState(false);
  const [mounted, setMounted] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const from = (location.state as any)?.from?.pathname || '/dashboard';

  useEffect(() => {
    const t = setTimeout(() => setMounted(true), 50);
    return () => clearTimeout(t);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!email || !password) {
      toast.error('Por favor completa todos los campos');
      return;
    }

    setIsLoading(true);

    try {
      const result = await login(email, password);

      if (result.requiresTenantSelection) {
        toast.success('Por favor selecciona tu empresa');
        navigate('/select-tenant', { replace: true });
        return;
      }

      toast.success('Inicio de sesión exitoso');

      let shouldRedirectToAdmin = false;

      await new Promise(resolve => setTimeout(resolve, 50));

      const token = localStorage.getItem('auth_token');
      if (token) {
        try {
          const { parseJwt } = await import('../utils/jwt');
          const payload = parseJwt(token);
          const adminClaim = payload?.is_system_admin;
          shouldRedirectToAdmin =
            adminClaim === 'true' ||
            adminClaim === 'True' ||
            adminClaim === true ||
            adminClaim === '1';

          const tenantId = payload?.tenant_id;
          if (tenantId && tenantId !== '0' && tenantId !== 0 && tenantId !== 'null') {
            shouldRedirectToAdmin = false;
          }
        } catch (error) {
          console.error('[LoginPage] Error parsing token:', error);
          shouldRedirectToAdmin = false;
        }
      }

      if (!shouldRedirectToAdmin) {
        await new Promise(resolve => setTimeout(resolve, 200));
      }

      if (shouldRedirectToAdmin) {
        navigate('/system-admin/dashboard', { replace: true });
      } else {
        const targetPath = from.startsWith('/system-admin') ? '/dashboard' : from;
        navigate(targetPath, { replace: true });
      }
    } catch (error: any) {
      toast.error(error.message || 'Error al iniciar sesión');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-navy-950 flex overflow-hidden">

      {/* ── Panel izquierdo: branding ─────────────────────────────── */}
      <div
        className="hidden lg:flex lg:w-[55%] flex-col justify-between p-12 relative overflow-hidden"
        style={{ background: 'linear-gradient(145deg, #0d2137 0%, #0a1929 60%, #061520 100%)' }}
      >
        {/* Grid geométrico de fondo */}
        <svg
          className="absolute inset-0 w-full h-full opacity-[0.06] pointer-events-none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <pattern id="grid" width="48" height="48" patternUnits="userSpaceOnUse">
              <path d="M 48 0 L 0 0 0 48" fill="none" stroke="#10b981" strokeWidth="0.8" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" />
        </svg>

        {/* Orbes de gradiente animados */}
        <div
          className="absolute top-[-120px] left-[-80px] w-[480px] h-[480px] rounded-full pointer-events-none"
          style={{
            background: 'radial-gradient(circle, rgba(16,185,129,0.12) 0%, transparent 70%)',
            animation: 'driftA 14s ease-in-out infinite',
          }}
        />
        <div
          className="absolute bottom-[-100px] right-[-60px] w-[360px] h-[360px] rounded-full pointer-events-none"
          style={{
            background: 'radial-gradient(circle, rgba(13,148,136,0.10) 0%, transparent 70%)',
            animation: 'driftB 18s ease-in-out infinite',
          }}
        />

        {/* Contenido */}
        <div
          className="relative z-10"
          style={{
            opacity: mounted ? 1 : 0,
            transform: mounted ? 'translateY(0)' : 'translateY(12px)',
            transition: 'opacity 0.6s ease, transform 0.6s ease',
          }}
        >
          <PaglyLogo variant="full" theme="dark" size="lg" />
        </div>

        <div
          className="relative z-10 space-y-4"
          style={{
            opacity: mounted ? 1 : 0,
            transform: mounted ? 'translateY(0)' : 'translateY(20px)',
            transition: 'opacity 0.7s ease 0.1s, transform 0.7s ease 0.1s',
          }}
        >
          <h2
            className="text-5xl font-bold leading-[1.1] tracking-tight font-display"
            style={{ color: '#f0f4f8' }}
          >
            Planilla<br />
            <span style={{ color: '#10b981' }}>Inteligente.</span>
          </h2>
          <p className="text-navy-300 text-lg max-w-xs leading-relaxed">
            Nómina panameña con cumplimiento total de la CSS, SE e ISR — sin complicaciones.
          </p>
        </div>

        <div className="relative z-10 space-y-5">
          {features.map((f, i) => {
            const Icon = f.icon;
            return (
              <div
                key={i}
                className="flex items-start gap-4"
                style={{
                  opacity: mounted ? 1 : 0,
                  transform: mounted ? 'translateX(0)' : 'translateX(-16px)',
                  transition: `opacity 0.6s ease ${0.25 + i * 0.1}s, transform 0.6s ease ${0.25 + i * 0.1}s`,
                }}
              >
                <div
                  className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                  style={{ background: 'rgba(16,185,129,0.12)', border: '1px solid rgba(16,185,129,0.2)' }}
                >
                  <Icon className="w-5 h-5" style={{ color: '#10b981' }} />
                </div>
                <div>
                  <p className="text-gray-200 font-medium text-sm">{f.title}</p>
                  <p className="text-navy-400 text-xs mt-0.5">{f.desc}</p>
                </div>
              </div>
            );
          })}
        </div>

        <div className="relative z-10 flex items-center justify-between">
          <p className="text-navy-500 text-xs">
            © {new Date().getFullYear()} Pagly · Todos los derechos reservados
          </p>
          <a
            href="https://vorluno.dev"
            target="_blank"
            rel="noopener noreferrer"
            className="text-navy-500 text-xs hover:text-navy-300 transition-colors"
          >
            Desarrollado por vorluno.dev
          </a>
        </div>

        {/* Línea decorativa derecha */}
        <div
          className="absolute right-0 top-0 bottom-0 w-px"
          style={{ background: 'linear-gradient(to bottom, transparent, rgba(16,185,129,0.2) 40%, rgba(16,185,129,0.2) 60%, transparent)' }}
        />
      </div>

      {/* ── Panel derecho: formulario ─────────────────────────────── */}
      <div className="flex-1 flex items-center justify-center p-6 lg:p-16 bg-navy-950">
        <div
          className="w-full max-w-[400px]"
          style={{
            opacity: mounted ? 1 : 0,
            transform: mounted ? 'translateY(0)' : 'translateY(24px)',
            transition: 'opacity 0.65s ease 0.15s, transform 0.65s ease 0.15s',
          }}
        >
          {/* Logo móvil */}
          <div className="lg:hidden flex justify-center mb-8">
            <PaglyLogo variant="icon" theme="dark" size="lg" />
          </div>

          <div className="mb-8">
            <h1 className="text-3xl font-bold text-gray-100 font-display tracking-tight">
              Bienvenido de vuelta
            </h1>
            <p className="text-navy-400 mt-1.5 text-sm">
              Ingresa tus credenciales para continuar
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Campo Email */}
            <div>
              <label className="block text-xs font-semibold uppercase tracking-widest text-navy-400 mb-2">
                Correo Electrónico
              </label>
              <div className="relative">
                <Mail
                  className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 transition-colors duration-200"
                  style={{ color: emailFocused ? '#10b981' : '#627d98' }}
                />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  onFocus={() => setEmailFocused(true)}
                  onBlur={() => setEmailFocused(false)}
                  placeholder="usuario@empresa.com"
                  disabled={isLoading}
                  className="w-full pl-11 pr-4 py-3.5 text-gray-100 text-sm rounded-xl outline-none transition-all duration-200 placeholder-navy-500 disabled:opacity-50"
                  style={{
                    background: 'rgba(16,42,67,0.6)',
                    border: emailFocused ? '1.5px solid #10b981' : '1.5px solid #334e68',
                    boxShadow: emailFocused ? '0 0 0 3px rgba(16,185,129,0.08)' : 'none',
                  }}
                />
              </div>
            </div>

            {/* Campo Contraseña */}
            <div>
              <label className="block text-xs font-semibold uppercase tracking-widest text-navy-400 mb-2">
                Contraseña
              </label>
              <div className="relative">
                <Lock
                  className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 transition-colors duration-200"
                  style={{ color: passwordFocused ? '#10b981' : '#627d98' }}
                />
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onFocus={() => setPasswordFocused(true)}
                  onBlur={() => setPasswordFocused(false)}
                  placeholder="••••••••"
                  disabled={isLoading}
                  className="w-full pl-11 pr-4 py-3.5 text-gray-100 text-sm rounded-xl outline-none transition-all duration-200 placeholder-navy-500 disabled:opacity-50"
                  style={{
                    background: 'rgba(16,42,67,0.6)',
                    border: passwordFocused ? '1.5px solid #10b981' : '1.5px solid #334e68',
                    boxShadow: passwordFocused ? '0 0 0 3px rgba(16,185,129,0.08)' : 'none',
                  }}
                />
              </div>
            </div>

            {/* Botón Submit */}
            <button
              type="submit"
              disabled={isLoading}
              className="group w-full flex items-center justify-center gap-3 py-3.5 rounded-xl font-semibold text-sm transition-all duration-200 disabled:opacity-60 disabled:cursor-not-allowed relative overflow-hidden"
              style={{
                background: isLoading
                  ? '#047857'
                  : 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                color: 'white',
                boxShadow: '0 0 0 0 rgba(16,185,129,0)',
              }}
              onMouseEnter={(e) => {
                if (!isLoading) {
                  (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 4px 20px rgba(16,185,129,0.3)';
                  (e.currentTarget as HTMLButtonElement).style.transform = 'translateY(-1px)';
                }
              }}
              onMouseLeave={(e) => {
                (e.currentTarget as HTMLButtonElement).style.boxShadow = '0 0 0 0 rgba(16,185,129,0)';
                (e.currentTarget as HTMLButtonElement).style.transform = 'translateY(0)';
              }}
            >
              {isLoading ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  <span>Iniciando sesión...</span>
                </>
              ) : (
                <>
                  <span>Iniciar Sesión</span>
                  <ArrowRight className="w-4 h-4 transition-transform duration-200 group-hover:translate-x-1" />
                </>
              )}
            </button>
          </form>

          {/* Separador */}
          <div className="mt-8 pt-6 border-t border-navy-800">
            <p className="text-center text-navy-500 text-xs leading-relaxed">
              ¿Necesitas acceso?{' '}
              <span className="text-navy-300">Contacta a tu administrador.</span>
            </p>
          </div>

          {/* Footer móvil */}
          <div className="lg:hidden flex items-center justify-between mt-8">
            <p className="text-navy-600 text-xs">
              © {new Date().getFullYear()} Pagly
            </p>
            <a
              href="https://vorluno.dev"
              target="_blank"
              rel="noopener noreferrer"
              className="text-navy-600 text-xs hover:text-navy-400 transition-colors"
            >
              vorluno.dev
            </a>
          </div>
        </div>
      </div>

      {/* Keyframes globales via style tag */}
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
