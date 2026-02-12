import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';

/* ── Animated counter ─────────────────────────── */
function useCountUp(target: number, duration: number, active: boolean) {
  const [val, setVal] = useState(0);
  useEffect(() => {
    if (!active) return;
    let start: number | null = null;
    const tick = (ts: number) => {
      if (!start) start = ts;
      const p = Math.min((ts - start) / duration, 1);
      const e = 1 - Math.pow(1 - p, 3);
      setVal(Math.round(e * target));
      if (p < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }, [target, duration, active]);
  return val;
}

export default function LoginPage() {
  const [email, setEmail]       = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw]     = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [mounted, setMounted]   = useState(false);
  const [emailFocused, setEmailFocused]       = useState(false);
  const [passwordFocused, setPasswordFocused] = useState(false);

  const { login } = useAuth();
  const navigate  = useNavigate();
  const location  = useLocation();
  const from = (location.state as any)?.from?.pathname || '/dashboard';

  const employees = useCountUp(1284, 1800, mounted);
  const amount    = useCountUp(4700000, 2400, mounted);

  useEffect(() => {
    const t = setTimeout(() => setMounted(true), 80);
    return () => clearTimeout(t);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { toast.error('Por favor completa todos los campos'); return; }
    setIsLoading(true);
    try {
      const result = await login(email, password);
      if (result.requiresTenantSelection) {
        toast.success('Por favor selecciona tu empresa');
        navigate('/select-tenant', { replace: true });
        return;
      }
      toast.success('Bienvenido a Pagly');
      let shouldRedirectToAdmin = false;
      await new Promise(r => setTimeout(r, 50));
      const token = localStorage.getItem('auth_token');
      if (token) {
        try {
          const { parseJwt } = await import('../utils/jwt');
          const payload = parseJwt(token);
          const adminClaim = payload?.is_system_admin;
          shouldRedirectToAdmin = adminClaim === 'true' || adminClaim === 'True' || adminClaim === true || adminClaim === '1';
          const tenantId = payload?.tenant_id;
          if (tenantId && tenantId !== '0' && tenantId !== 0 && tenantId !== 'null') shouldRedirectToAdmin = false;
        } catch { shouldRedirectToAdmin = false; }
      }
      if (!shouldRedirectToAdmin) await new Promise(r => setTimeout(r, 200));
      if (shouldRedirectToAdmin) {
        navigate('/system-admin/dashboard', { replace: true });
      } else {
        navigate(from.startsWith('/system-admin') ? '/dashboard' : from, { replace: true });
      }
    } catch (error: any) {
      toast.error(error.message || 'Error al iniciar sesión');
    } finally {
      setIsLoading(false);
    }
  };

  /* helpers */
  const fmt = (n: number) =>
    n >= 1_000_000
      ? `${(n / 1_000_000).toFixed(1)}M`
      : n.toLocaleString('es-PA');

  return (
    <div className="min-h-screen flex overflow-hidden" style={{ background: '#050d1a', fontFamily: "'Sora', sans-serif" }}>

      {/* ── LEFT PANEL ─────────────────────────────────────────────── */}
      <div
        className="hidden lg:flex lg:w-[52%] flex-col relative overflow-hidden"
        style={{ background: 'linear-gradient(160deg, #061422 0%, #08192e 50%, #050d1a 100%)' }}
      >
        {/* Noise grain overlay */}
        <div className="absolute inset-0 pointer-events-none" style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' opacity='1'/%3E%3C/svg%3E")`,
          opacity: 0.03,
        }} />

        {/* Orbes de luz */}
        <div className="absolute pointer-events-none" style={{
          top: '-80px', left: '-100px', width: '520px', height: '520px', borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(16,185,129,0.10) 0%, transparent 65%)',
          animation: 'floatA 16s ease-in-out infinite',
        }} />
        <div className="absolute pointer-events-none" style={{
          bottom: '-120px', right: '-80px', width: '440px', height: '440px', borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(6,182,212,0.08) 0%, transparent 65%)',
          animation: 'floatB 20s ease-in-out infinite',
        }} />
        <div className="absolute pointer-events-none" style={{
          top: '40%', right: '10%', width: '260px', height: '260px', borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(16,185,129,0.05) 0%, transparent 65%)',
          animation: 'floatC 12s ease-in-out infinite',
        }} />

        {/* Watermark Pagly */}
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none select-none overflow-hidden">
          <span
            className="font-black tracking-tighter"
            style={{ fontSize: 'clamp(120px, 18vw, 200px)', color: 'rgba(16,185,129,0.03)', letterSpacing: '-0.05em', lineHeight: 1 }}
          >
            PAGLY
          </span>
        </div>

        {/* Línea decorativa derecha */}
        <div className="absolute right-0 top-0 bottom-0 w-px" style={{
          background: 'linear-gradient(to bottom, transparent 0%, rgba(16,185,129,0.15) 30%, rgba(16,185,129,0.15) 70%, transparent 100%)',
        }} />

        {/* Contenido */}
        <div className="relative z-10 flex flex-col justify-between h-full p-12">

          {/* Logo */}
          <div style={{ opacity: mounted ? 1 : 0, transform: mounted ? 'none' : 'translateY(10px)', transition: 'all 0.6s ease' }}>
            <PaglyLogo variant="full" theme="dark" size="lg" />
          </div>

          {/* Hero text */}
          <div className="space-y-6" style={{ opacity: mounted ? 1 : 0, transform: mounted ? 'none' : 'translateY(24px)', transition: 'all 0.7s ease 0.1s' }}>

            <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full" style={{
              background: 'rgba(16,185,129,0.08)', border: '1px solid rgba(16,185,129,0.18)',
            }}>
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
              <span className="text-[11px] font-semibold tracking-widest uppercase text-emerald-400">Sistema Operativo</span>
            </div>

            <h2 className="font-black leading-[1.0] tracking-tight" style={{ fontSize: 'clamp(42px, 5vw, 60px)', color: '#f0f6ff' }}>
              Nómina<br />
              <span style={{
                background: 'linear-gradient(90deg, #10b981 0%, #06b6d4 100%)',
                WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
              }}>
                Inteligente.
              </span>
            </h2>

            <p className="text-[15px] max-w-[340px] leading-relaxed" style={{ color: '#64889c' }}>
              CSS, SE e ISR panameños calculados al instante.
              Cumplimiento total de la Ley 462.
            </p>
          </div>

          {/* Stats vivos */}
          <div
            className="grid grid-cols-2 gap-3"
            style={{ opacity: mounted ? 1 : 0, transform: mounted ? 'none' : 'translateY(20px)', transition: 'all 0.7s ease 0.2s' }}
          >
            {/* Stat 1 — Empleados */}
            <div
              className="rounded-2xl p-4"
              style={{ background: 'rgba(16,185,129,0.06)', border: '1px solid rgba(16,185,129,0.12)' }}
            >
              <p className="text-[11px] uppercase tracking-widest font-semibold mb-2" style={{ color: '#4a7c6a' }}>Empleados</p>
              <p className="font-black text-[30px] leading-none tracking-tight text-white">
                {fmt(employees)}<span className="text-emerald-400 text-[18px]">+</span>
              </p>
              <p className="text-[11px] mt-1.5" style={{ color: '#4a7c6a' }}>gestionados hoy</p>
            </div>

            {/* Stat 2 — Monto */}
            <div
              className="rounded-2xl p-4"
              style={{ background: 'rgba(6,182,212,0.06)', border: '1px solid rgba(6,182,212,0.12)' }}
            >
              <p className="text-[11px] uppercase tracking-widest font-semibold mb-2" style={{ color: '#2d6a7a' }}>Procesado PAB</p>
              <p className="font-black text-[30px] leading-none tracking-tight text-white">
                <span className="text-cyan-400 text-[18px]">B/. </span>{fmt(amount)}
              </p>
              <p className="text-[11px] mt-1.5" style={{ color: '#2d6a7a' }}>este mes</p>
            </div>

            {/* Stat 3 — Uptime */}
            <div
              className="col-span-2 rounded-2xl p-4 flex items-center justify-between"
              style={{ background: 'rgba(255,255,255,0.02)', border: '1px solid rgba(255,255,255,0.05)' }}
            >
              <div>
                <p className="text-[11px] uppercase tracking-widest font-semibold mb-1" style={{ color: '#4a5568' }}>Disponibilidad del sistema</p>
                <p className="font-black text-[24px] leading-none tracking-tight" style={{ color: '#a78bfa' }}>99.9%</p>
              </div>
              {/* Mini bar chart */}
              <div className="flex items-end gap-[3px] h-8">
                {[70, 85, 60, 90, 75, 100, 88, 95, 80, 100, 92, 98].map((h, i) => (
                  <div
                    key={i}
                    className="w-1.5 rounded-sm"
                    style={{
                      height: `${h * 0.01 * 32}px`,
                      background: h === 100 ? 'rgba(167,139,250,0.7)' : 'rgba(167,139,250,0.25)',
                      transition: `height 0.4s ease ${i * 0.05}s`,
                    }}
                  />
                ))}
              </div>
            </div>
          </div>

          {/* Footer */}
          <div
            className="flex items-center justify-between"
            style={{ opacity: mounted ? 1 : 0, transition: 'opacity 0.6s ease 0.4s' }}
          >
            <p className="text-[11px]" style={{ color: '#2a4a5e' }}>
              © {new Date().getFullYear()} Pagly · Todos los derechos reservados
            </p>
            <a href="https://vorluno.dev" target="_blank" rel="noopener noreferrer"
              className="text-[11px] transition-colors hover:text-gray-400" style={{ color: '#2a4a5e' }}>
              vorluno.dev
            </a>
          </div>
        </div>
      </div>

      {/* ── RIGHT PANEL ────────────────────────────────────────────── */}
      <div className="flex-1 flex items-center justify-center p-6 lg:p-14 relative" style={{ background: '#050d1a' }}>

        {/* Sutil orbe detrás del form */}
        <div className="absolute pointer-events-none" style={{
          top: '20%', right: '-10%', width: '400px', height: '400px', borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(16,185,129,0.04) 0%, transparent 70%)',
        }} />

        <div
          className="w-full max-w-[380px] relative z-10"
          style={{ opacity: mounted ? 1 : 0, transform: mounted ? 'none' : 'translateY(28px)', transition: 'all 0.7s ease 0.15s' }}
        >
          {/* Logo — solo móvil */}
          <div className="lg:hidden flex justify-center mb-10">
            <PaglyLogo variant="full" theme="dark" size="md" />
          </div>

          {/* Cabecera */}
          <div className="mb-10">
            <p className="text-[11px] uppercase tracking-[0.2em] font-bold mb-3" style={{ color: '#10b981' }}>
              Acceso al sistema
            </p>
            <h1 className="text-[32px] font-black leading-tight tracking-tight" style={{ color: '#eef2ff' }}>
              Bienvenido<br />de vuelta
            </h1>
            <p className="text-[13px] mt-2 leading-relaxed" style={{ color: '#3d5a70' }}>
              Ingresa tus credenciales para continuar
            </p>
          </div>

          {/* Formulario */}
          <form onSubmit={handleSubmit} className="space-y-7">

            {/* Email */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-[0.15em] mb-3" style={{ color: emailFocused ? '#10b981' : '#3d5a70' }}>
                Correo electrónico
              </label>
              <div className="relative">
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  onFocus={() => setEmailFocused(true)}
                  onBlur={() => setEmailFocused(false)}
                  placeholder="usuario@empresa.com"
                  disabled={isLoading}
                  autoComplete="email"
                  className="w-full py-3 pr-4 text-[14px] font-medium outline-none transition-all duration-200 placeholder-[#1e3a4a] disabled:opacity-50"
                  style={{
                    background: 'transparent',
                    color: '#e2eaf2',
                    borderBottom: emailFocused ? '2px solid #10b981' : '2px solid rgba(255,255,255,0.07)',
                    paddingLeft: '10px',
                    borderRadius: 0,
                    outline: 'none',
                    boxShadow: 'none',
                    WebkitAppearance: 'none',
                  }}
                />
                {/* Glow bajo el input */}
                <div style={{
                  position: 'absolute', bottom: 0, left: 0, right: 0, height: '1px',
                  background: emailFocused ? 'linear-gradient(90deg, #10b981, rgba(16,185,129,0))' : 'transparent',
                  filter: 'blur(3px)',
                  transition: 'opacity 0.3s',
                  opacity: emailFocused ? 1 : 0,
                }} />
              </div>
            </div>

            {/* Contraseña */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-[0.15em] mb-3" style={{ color: passwordFocused ? '#10b981' : '#3d5a70' }}>
                Contraseña
              </label>
              <div className="relative">
                <input
                  type={showPw ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onFocus={() => setPasswordFocused(true)}
                  onBlur={() => setPasswordFocused(false)}
                  placeholder="••••••••••"
                  disabled={isLoading}
                  autoComplete="current-password"
                  className="w-full py-3 pr-10 text-[14px] font-medium outline-none transition-all duration-200 placeholder-[#1e3a4a] disabled:opacity-50"
                  style={{
                    background: 'transparent',
                    color: '#e2eaf2',
                    borderBottom: passwordFocused ? '2px solid #10b981' : '2px solid rgba(255,255,255,0.07)',
                    paddingLeft: '10px',
                    borderRadius: 0,
                    outline: 'none',
                    boxShadow: 'none',
                    WebkitAppearance: 'none',
                  }}
                />
                <button
                  type="button"
                  onClick={() => setShowPw(v => !v)}
                  className="absolute right-0 top-1/2 -translate-y-1/2 p-1 transition-colors"
                  style={{ color: passwordFocused ? '#10b981' : '#2d4a5e' }}
                  tabIndex={-1}
                >
                  {showPw ? (
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                        d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                    </svg>
                  ) : (
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                        d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75}
                        d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                  )}
                </button>
                <div style={{
                  position: 'absolute', bottom: 0, left: 0, right: 0, height: '1px',
                  background: passwordFocused ? 'linear-gradient(90deg, #10b981, rgba(16,185,129,0))' : 'transparent',
                  filter: 'blur(3px)',
                  transition: 'opacity 0.3s',
                  opacity: passwordFocused ? 1 : 0,
                }} />
              </div>
            </div>

            {/* Botón submit */}
            <div className="pt-2">
              <button
                type="submit"
                disabled={isLoading}
                className="group w-full relative overflow-hidden rounded-2xl py-4 font-bold text-[14px] tracking-wide transition-all duration-300 disabled:opacity-60 disabled:cursor-not-allowed"
                style={{
                  background: isLoading
                    ? 'rgba(16,185,129,0.6)'
                    : 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                  color: 'white',
                  boxShadow: '0 0 40px rgba(16,185,129,0.15)',
                }}
                onMouseEnter={e => {
                  if (!isLoading) {
                    (e.currentTarget as HTMLElement).style.transform = 'translateY(-2px)';
                    (e.currentTarget as HTMLElement).style.boxShadow = '0 8px 32px rgba(16,185,129,0.35)';
                  }
                }}
                onMouseLeave={e => {
                  (e.currentTarget as HTMLElement).style.transform = 'translateY(0)';
                  (e.currentTarget as HTMLElement).style.boxShadow = '0 0 40px rgba(16,185,129,0.15)';
                }}
              >
                {/* Shimmer on hover */}
                <div className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-300" style={{
                  background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.08), transparent)',
                  transform: 'skewX(-20deg)',
                }} />
                {isLoading ? (
                  <span className="flex items-center justify-center gap-3">
                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    Verificando...
                  </span>
                ) : (
                  <span className="flex items-center justify-center gap-2.5">
                    Iniciar sesión
                    <svg className="w-4 h-4 transition-transform duration-200 group-hover:translate-x-1" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M17 8l4 4m0 0l-4 4m4-4H3" />
                    </svg>
                  </span>
                )}
              </button>
            </div>
          </form>

          {/* Divider + nota */}
          <div className="mt-10 pt-6" style={{ borderTop: '1px solid rgba(255,255,255,0.04)' }}>
            <p className="text-center text-[12px] leading-relaxed" style={{ color: '#1e3a4a' }}>
              ¿Sin acceso?{' '}
              <span style={{ color: '#2d6a7a' }}>Contacta a tu administrador.</span>
            </p>
          </div>

          {/* Footer móvil */}
          <div className="lg:hidden flex items-center justify-between mt-8">
            <p className="text-[11px]" style={{ color: '#1e3a4a' }}>© {new Date().getFullYear()} Pagly</p>
            <a href="https://vorluno.dev" target="_blank" rel="noopener noreferrer"
              className="text-[11px] transition-colors" style={{ color: '#1e3a4a' }}>
              vorluno.dev
            </a>
          </div>
        </div>
      </div>

      {/* Keyframes + Sora font */}
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Sora:wght@400;500;600;700;800;900&display=swap');

        /* Eliminar fondo blanco del autocompletado del navegador */
        input:-webkit-autofill,
        input:-webkit-autofill:hover,
        input:-webkit-autofill:focus,
        input:-webkit-autofill:active {
          -webkit-box-shadow: 0 0 0px 1000px #050d1a inset !important;
          -webkit-text-fill-color: #e2eaf2 !important;
          transition: background-color 5000s ease-in-out 0s;
          caret-color: #e2eaf2;
        }

        @keyframes floatA {
          0%, 100% { transform: translate(0, 0) scale(1); }
          33%       { transform: translate(28px, -18px) scale(1.06); }
          66%       { transform: translate(-18px, 14px) scale(0.96); }
        }
        @keyframes floatB {
          0%, 100% { transform: translate(0, 0) scale(1); }
          40%       { transform: translate(-22px, 18px) scale(1.04); }
          70%       { transform: translate(14px, -10px) scale(0.97); }
        }
        @keyframes floatC {
          0%, 100% { transform: translate(0, 0); }
          50%       { transform: translate(10px, -12px); }
        }
      `}</style>
    </div>
  );
}
