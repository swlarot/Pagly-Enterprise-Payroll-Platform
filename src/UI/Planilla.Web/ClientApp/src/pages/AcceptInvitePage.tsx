import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { authService } from '../services/authService';
import type { ValidateInviteResponseDto } from '../types/api';
import { PaglyLogo } from '../components/ui/PaglyLogo';
import toast from 'react-hot-toast';

export default function AcceptInvitePage() {
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isValidating, setIsValidating] = useState(true);
  const [inviteInfo, setInviteInfo] = useState<ValidateInviteResponseDto | null>(null);
  const [searchParams] = useSearchParams();
  const { acceptInvite, isAuthenticated } = useAuth();
  const navigate = useNavigate();

  const token = searchParams.get('token');

  useEffect(() => {
    // If already authenticated, redirect to dashboard
    if (isAuthenticated) {
      navigate('/dashboard', { replace: true });
      return;
    }

    if (!token) {
      toast.error('Token de invitación inválido');
      navigate('/login');
      return;
    }

    validateInvitation();
  }, [token, isAuthenticated, navigate]);

  const validateInvitation = async () => {
    if (!token) return;

    setIsValidating(true);

    try {
      const validation = await authService.validateInvite(token);

      if (!validation.isValid) {
        toast.error(validation.message || 'Esta invitación no es válida o ha expirado');
        setTimeout(() => navigate('/login'), 2000);
        return;
      }

      setInviteInfo(validation);
    } catch (error: any) {
      toast.error(error.message || 'Error al validar la invitación');
      setTimeout(() => navigate('/login'), 2000);
    } finally {
      setIsValidating(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!password || !confirmPassword) {
      toast.error('Por favor completa todos los campos');
      return;
    }

    if (password.length < 8) {
      toast.error('La contraseña debe tener al menos 8 caracteres');
      return;
    }

    if (password !== confirmPassword) {
      toast.error('Las contraseñas no coinciden');
      return;
    }

    if (!token) {
      toast.error('Token de invitación inválido');
      return;
    }

    setIsLoading(true);

    try {
      await acceptInvite(token, password, confirmPassword);
      toast.success('Invitación aceptada. Bienvenido a Pagly');
      navigate('/dashboard', { replace: true });
    } catch (error: any) {
      toast.error(error.message || 'Error al aceptar la invitación');
    } finally {
      setIsLoading(false);
    }
  };

  if (isValidating) {
    return (
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl shadow-black/30 p-8 max-w-md w-full text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500 mx-auto mb-4"></div>
          <p className="text-gray-400">Validando invitación...</p>
        </div>
      </div>
    );
  }

  if (!inviteInfo || !inviteInfo.isValid) {
    return (
      <div className="min-h-screen bg-navy-950 flex items-center justify-center p-4">
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl shadow-black/30 p-8 max-w-md w-full text-center">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-red-500/15 rounded-full mb-4">
            <svg
              className="w-8 h-8 text-red-400"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-100 mb-2">Invitación No Válida</h2>
          <p className="text-gray-400 mb-6">
            Esta invitación no es válida, ha expirado o ya ha sido utilizada.
          </p>
          <button
            onClick={() => navigate('/login')}
            className="px-6 py-3 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors"
          >
            Ir a Inicio de Sesión
          </button>
        </div>
      </div>
    );
  }

  const getRoleBadgeColor = (roleName: string) => {
    const colors: Record<string, string> = {
      Owner: 'bg-purple-500/15 text-purple-400',
      User: 'bg-blue-500/15 text-blue-400',
    };
    return colors[roleName] || 'bg-navy-700 text-gray-300';
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

        {/* Accept Invite Card */}
        <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl shadow-black/30 p-8">
          <div className="text-center mb-6">
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
                  d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                />
              </svg>
            </div>
            <h2 className="text-2xl font-bold text-gray-100 mb-2">Aceptar Invitación</h2>
            <p className="text-gray-400 mb-4">
              Has sido invitado a unirte a <strong className="text-gray-200">{inviteInfo.tenantName}</strong>
            </p>

            {/* Invitation Info Card */}
            <div className="bg-primary-500/10 border border-primary-500/20 rounded-lg p-4 mb-4">
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400">Email:</span>
                <span className="text-sm font-medium text-gray-100">{inviteInfo.email}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-gray-400">Rol:</span>
                <span className={`px-2 py-1 rounded-full text-xs font-medium ${getRoleBadgeColor(inviteInfo.roleName)}`}>
                  {inviteInfo.roleName}
                </span>
              </div>
            </div>

            <p className="text-sm text-gray-400">
              Crea tu contraseña para completar el registro
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
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
                placeholder="Mínimo 8 caracteres"
                disabled={isLoading}
              />
              {password && password.length < 8 && (
                <p className="text-xs text-red-400 mt-1">
                  La contraseña debe tener al menos 8 caracteres
                </p>
              )}
            </div>

            <div>
              <label
                htmlFor="confirmPassword"
                className="block text-sm font-medium text-gray-300 mb-2"
              >
                Confirmar Contraseña
              </label>
              <input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full px-4 py-3 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500/20 focus:border-primary-500 transition placeholder-gray-500"
                placeholder="Repite tu contraseña"
                disabled={isLoading}
              />
              {confirmPassword && password !== confirmPassword && (
                <p className="text-xs text-red-400 mt-1">
                  Las contraseñas no coinciden
                </p>
              )}
            </div>

            <button
              type="submit"
              disabled={isLoading || password.length < 8 || password !== confirmPassword}
              className="w-full bg-primary-600 text-white py-3 rounded-lg font-medium hover:bg-primary-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
            >
              {isLoading ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>
                  Aceptando invitación...
                </>
              ) : (
                'Aceptar Invitación'
              )}
            </button>
          </form>
        </div>

        {/* Footer */}
        <p className="text-center text-gray-500 text-sm mt-8">
          © {new Date().getFullYear()} Pagly. Todos los derechos reservados.
        </p>
      </div>
    </div>
  );
}
