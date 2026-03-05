import { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Search, Loader2, UserCheck, AlertCircle } from 'lucide-react';
import { Button } from '../ui/Button';
import toast from 'react-hot-toast';
import { api } from '../../services/api';

interface SugerenciaUsuario {
  userId: string;
  email: string;
  nombreCompleto: string;
  telefono?: string;
  rol: string;
  esSugerencia: boolean;
}

interface LinkUserModalProps {
  empleadoId: number;
  empleadoNombre: string;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function LinkUserModal({
  empleadoId,
  empleadoNombre,
  isOpen,
  onClose,
  onSuccess,
}: LinkUserModalProps) {
  const [usuarios, setUsuarios] = useState<SugerenciaUsuario[]>([]);
  const [isLoadingUsers, setIsLoadingUsers] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    if (isOpen) {
      loadSugerencias();
    } else {
      resetState();
    }
  }, [isOpen, empleadoId]);

  const loadSugerencias = async () => {
    try {
      setIsLoadingUsers(true);
      const data = await api.get<SugerenciaUsuario[]>(
        `/api/empleados/${empleadoId}/sugerencias-usuarios`
      );
      setUsuarios(data);

      // Auto-seleccionar sugerencia si existe
      const sugerido = data.find((u: SugerenciaUsuario) => u.esSugerencia);
      if (sugerido) {
        setSelectedUserId(sugerido.userId);
      }
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar usuarios disponibles');
      console.error('Error loading user suggestions:', error);
    } finally {
      setIsLoadingUsers(false);
    }
  };

  const handleVincular = async () => {
    if (!selectedUserId) {
      toast.error('Debe seleccionar un usuario');
      return;
    }

    try {
      setIsSubmitting(true);
      await api.post(`/api/empleados/${empleadoId}/vincular-usuario`, {
        userId: selectedUserId,
      });

      toast.success('Usuario vinculado exitosamente');
      onSuccess();
      onClose();
    } catch (error: any) {
      const msg = error?.message || error?.error || 'Error al vincular usuario';
      toast.error(msg);
      console.error('Error vinculando usuario:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const resetState = () => {
    setUsuarios([]);
    setSelectedUserId(null);
    setSearchTerm('');
  };

  const usuariosFiltrados = usuarios.filter((u) => {
    if (!searchTerm) return true;
    const search = searchTerm.toLowerCase();
    return (
      u.email.toLowerCase().includes(search) ||
      u.nombreCompleto.toLowerCase().includes(search)
    );
  });

  if (!isOpen) return null;

  const content = (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-navy-900 border border-navy-700 rounded-lg shadow-2xl shadow-black/30 w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="p-6 border-b border-navy-700 flex justify-between items-center">
          <div>
            <h3 className="text-xl font-semibold text-gray-100">Vincular Usuario</h3>
            <p className="text-sm text-gray-400 mt-1">
              Selecciona un usuario para vincular a <strong className="text-gray-200">{empleadoNombre}</strong>
            </p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300">
            <X className="w-6 h-6" />
          </button>
        </div>

        <div className="p-6">
          {/* Búsqueda */}
          <div className="mb-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
              <input
                type="text"
                placeholder="Buscar por email o nombre..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full pl-10 pr-4 py-2 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500/20 focus:border-primary-500 placeholder-gray-500"
              />
            </div>
          </div>

          {/* Lista de Usuarios */}
          <div className="space-y-2 mb-6 max-h-96 overflow-y-auto">
            {isLoadingUsers ? (
              <div className="flex items-center justify-center py-12">
                <Loader2 className="w-8 h-8 animate-spin text-primary-400" />
              </div>
            ) : usuarios.length === 0 ? (
              <div className="text-center py-12">
                <AlertCircle className="w-12 h-12 text-gray-400 mx-auto mb-3" />
                <p className="text-gray-600">No hay usuarios disponibles para vincular</p>
                <p className="text-sm text-gray-500 mt-2">
                  Todos los usuarios del tenant ya están vinculados a empleados
                </p>
              </div>
            ) : usuariosFiltrados.length === 0 ? (
              <div className="text-center py-8 text-gray-500">
                No se encontraron usuarios con ese criterio
              </div>
            ) : (
              usuariosFiltrados.map((user) => (
                <div
                  key={user.userId}
                  onClick={() => setSelectedUserId(user.userId)}
                  className={`p-4 border rounded-lg cursor-pointer transition-all ${
                    selectedUserId === user.userId
                      ? 'border-primary-500 bg-primary-500/10 ring-2 ring-primary-500/20'
                      : 'border-navy-700 hover:bg-navy-800 hover:border-navy-500'
                  }`}
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <div className="font-semibold text-gray-100">{user.nombreCompleto}</div>
                        {user.esSugerencia && (
                          <span className="px-2 py-0.5 bg-amber-500/15 text-amber-400 text-xs font-medium rounded">
                            ⭐ Sugerido
                          </span>
                        )}
                      </div>
                      <div className="text-sm text-gray-400 mt-1">{user.email}</div>
                      <div className="flex items-center gap-3 mt-2">
                        {user.telefono && (
                          <span className="text-xs text-gray-500">📞 {user.telefono}</span>
                        )}
                        <span className="text-xs text-gray-500">
                          Rol: <span className="font-medium">{user.rol}</span>
                        </span>
                      </div>
                    </div>
                    {selectedUserId === user.userId && (
                      <div className="flex items-center gap-2 text-primary-400 font-medium ml-4">
                        <UserCheck className="w-5 h-5" />
                        <span className="text-sm">Seleccionado</span>
                      </div>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>

          {/* Info sobre sugerencias */}
          {usuarios.some((u) => u.esSugerencia) && (
            <div className="mb-6 p-3 bg-amber-500/15 border border-amber-500/25 rounded-lg">
              <p className="text-sm text-amber-300">
                💡 <strong>Sugerencia automática:</strong> Se sugiere vincular usuarios cuyo email
                coincide con el email del empleado.
              </p>
            </div>
          )}

          {/* Botones de Acción */}
          <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
            <Button type="button" variant="outline" onClick={onClose} disabled={isSubmitting}>
              Cancelar
            </Button>
            <Button
              type="button"
              onClick={handleVincular}
              disabled={isSubmitting || !selectedUserId || usuarios.length === 0}
              loading={isSubmitting}
            >
              Vincular Usuario
            </Button>
          </div>
        </div>
      </div>
    </div>
  );

  return createPortal(content, document.body);
}
