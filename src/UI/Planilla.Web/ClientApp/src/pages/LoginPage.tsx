import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const from = (location.state as any)?.from?.pathname || '/dashboard';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!email || !password) {
      toast.error('Por favor completa todos los campos');
      return;
    }

    setIsLoading(true);

    try {
      const result = await login(email, password);

      // Caso 1: Usuario tiene múltiples tenants - debe seleccionar
      if (result.requiresTenantSelection) {
        toast.success('Por favor selecciona tu empresa');
        navigate('/select-tenant', { replace: true });
        return;
      }

      // Caso 2 y 3: Login exitoso con un solo tenant o SystemAdmin
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
    <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
      <div className="max-w-md w-full">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="flex justify-center mb-4">
            <PaglyLogo variant="icon" theme="dark" size="lg" />
          </div>
          <h1 className="text-3xl font-bold text-gray-100 font-display">Pagly</h1>
          <p className="text-gray-400 mt-2">Planilla Inteligente</p>
        </div>

        {/* Login Card */}
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl p-8">
          <h2 className="text-2xl font-bold text-gray-100 mb-6 font-display">Iniciar Sesión</h2>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-2">
                Correo Electrónico
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500/20 focus:border-primary-500 transition placeholder-gray-500"
                placeholder="usuario@empresa.com"
                disabled={isLoading}
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-gray-300 mb-2">
                Contraseña
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500/20 focus:border-primary-500 transition placeholder-gray-500"
                placeholder="••••••••"
                disabled={isLoading}
              />
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full bg-primary-600 text-white py-3 rounded-lg font-medium hover:bg-primary-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
            >
              {isLoading ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>
                  Iniciando sesión...
                </>
              ) : (
                'Iniciar Sesión'
              )}
            </button>
          </form>

          <div className="mt-6 text-center">
            <p className="text-gray-500 text-sm">
              Si necesitas acceso, contacta a tu administrador.
            </p>
          </div>
        </div>

        <p className="text-center text-gray-500 text-sm mt-8">
          © {new Date().getFullYear()} Pagly. Todos los derechos reservados.
        </p>
      </div>
    </div>
  );
}
