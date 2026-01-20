import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import toast from 'react-hot-toast';

export default function RegisterPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [ruc, setRuc] = useState('');
  const [dv, setDv] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { register } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validation
    if (!email || !password || !companyName || !ruc || !dv) {
      toast.error('Por favor completa todos los campos');
      return;
    }

    if (password.length < 8) {
      toast.error('La contraseña debe tener al menos 8 caracteres');
      return;
    }

    if (!/^\d+$/.test(ruc)) {
      toast.error('El RUC debe contener solo números');
      return;
    }

    if (!/^\d+$/.test(dv)) {
      toast.error('El DV debe contener solo números');
      return;
    }

    setIsLoading(true);

    try {
      await register({ email, password, companyName, ruc, dv });
      toast.success('Registro exitoso. Bienvenido a Planilla');
      navigate('/dashboard', { replace: true });
    } catch (error: any) {
      toast.error(error.message || 'Error al registrarse');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100 flex items-center justify-center p-4">
      <div className="max-w-md w-full">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-blue-600 rounded-xl shadow-lg mb-4">
            <svg className="w-10 h-10 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z"
              />
            </svg>
          </div>
          <h1 className="text-3xl font-bold text-gray-900">Planilla</h1>
          <p className="text-gray-600 mt-2">Sistema de Gestión de Nómina</p>
        </div>

        {/* Register Card */}
        <div className="bg-white rounded-2xl shadow-xl p-8">
          <h2 className="text-2xl font-bold text-gray-900 mb-6">Crear Cuenta</h2>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-2">
                Correo Electrónico
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
                placeholder="usuario@empresa.com"
                disabled={isLoading}
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-gray-700 mb-2">
                Contraseña
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
                placeholder="Mínimo 8 caracteres"
                disabled={isLoading}
              />
            </div>

            <div>
              <label htmlFor="companyName" className="block text-sm font-medium text-gray-700 mb-2">
                Nombre de la Empresa
              </label>
              <input
                id="companyName"
                type="text"
                value={companyName}
                onChange={(e) => setCompanyName(e.target.value)}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
                placeholder="Mi Empresa S.A."
                disabled={isLoading}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label htmlFor="ruc" className="block text-sm font-medium text-gray-700 mb-2">
                  RUC
                </label>
                <input
                  id="ruc"
                  type="text"
                  value={ruc}
                  onChange={(e) => setRuc(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
                  placeholder="12345678"
                  disabled={isLoading}
                />
              </div>

              <div>
                <label htmlFor="dv" className="block text-sm font-medium text-gray-700 mb-2">
                  DV
                </label>
                <input
                  id="dv"
                  type="text"
                  value={dv}
                  onChange={(e) => setDv(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
                  placeholder="12"
                  maxLength={2}
                  disabled={isLoading}
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full bg-blue-600 text-white py-3 rounded-lg font-medium hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
            >
              {isLoading ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>
                  Creando cuenta...
                </>
              ) : (
                'Crear Cuenta'
              )}
            </button>
          </form>

          <div className="mt-6 text-center">
            <p className="text-gray-600">
              ¿Ya tienes una cuenta?{' '}
              <Link to="/login" className="text-blue-600 hover:text-blue-700 font-medium">
                Inicia sesión
              </Link>
            </p>
          </div>
        </div>

        <p className="text-center text-gray-500 text-sm mt-8">
          © {new Date().getFullYear()} Planilla. Todos los derechos reservados.
        </p>
      </div>
    </div>
  );
}
