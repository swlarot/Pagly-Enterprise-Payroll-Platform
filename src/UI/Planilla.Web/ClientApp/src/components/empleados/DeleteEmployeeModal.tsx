import { useState, useEffect } from 'react';
import { Modal } from '../ui/Modal';
import { Button } from '../ui/Button';
import {
  Loader2,
  AlertTriangle,
  XCircle,
  CheckCircle,
  DollarSign,
  Gavel,
  CreditCard,
  Clock,
  FileText,
  AlertCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import type { DeletionResult, DeletionBlocker, DeletionWarning } from '../../types/api';

interface DeleteEmployeeModalProps {
  isOpen: boolean;
  onClose: () => void;
  empleadoId: number;
  empleadoNombre: string;
  onSuccess: () => void;
}

type ValidationState = 'validating' | 'blockers' | 'warnings' | 'safe' | 'deleting';

export function DeleteEmployeeModal({
  isOpen,
  onClose,
  empleadoId,
  empleadoNombre,
  onSuccess,
}: DeleteEmployeeModalProps) {
  const [state, setState] = useState<ValidationState>('validating');
  const [deletionResult, setDeletionResult] = useState<DeletionResult | null>(null);
  const [confirmChecked, setConfirmChecked] = useState(false);

  useEffect(() => {
    if (isOpen) {
      validateDeletion();
    }
  }, [isOpen, empleadoId]);

  const validateDeletion = async () => {
    setState('validating');
    setConfirmChecked(false);

    try {
      const result = await api.delete<DeletionResult>(`/api/empleados/${empleadoId}`);
      setDeletionResult(result);

      if (!result.canDelete && result.blockers.length > 0) {
        setState('blockers');
      } else if (result.requiresConfirmation && result.warnings.length > 0) {
        setState('warnings');
      } else {
        setState('safe');
      }
    } catch (error: any) {
      toast.error(error.message || 'Error al validar eliminación');
      onClose();
    }
  };

  const handleConfirmDelete = async () => {
    setState('deleting');

    try {
      await api.post(`/api/empleados/${empleadoId}/force-delete`, { confirmed: true });
      toast.success(`Empleado ${empleadoNombre} eliminado exitosamente`);
      onSuccess();
      onClose();
    } catch (error: any) {
      toast.error(error.message || 'Error al eliminar empleado');
      setState(deletionResult?.warnings.length ? 'warnings' : 'safe');
    }
  };

  const getBlockerIcon = (entity: string) => {
    const entityLower = entity.toLowerCase();
    if (entityLower.includes('préstamo')) return DollarSign;
    if (entityLower.includes('deducción') || entityLower.includes('judicial')) return Gavel;
    if (entityLower.includes('anticipo')) return CreditCard;
    if (entityLower.includes('hora')) return Clock;
    if (entityLower.includes('planilla')) return FileText;
    return AlertCircle;
  };

  const renderContent = () => {
    const isDeleting = state === 'deleting';
    switch (state) {
      case 'validating':
        return (
          <div className="text-center py-8">
            <Loader2 className="w-12 h-12 animate-spin text-blue-600 mx-auto mb-4" />
            <p className="text-gray-600">Validando si el empleado puede ser eliminado...</p>
          </div>
        );

      case 'blockers':
        return (
          <div className="space-y-4">
            {/* Header */}
            <div className="flex items-center justify-center">
              <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center">
                <XCircle className="w-8 h-8 text-red-600" />
              </div>
            </div>

            <div className="text-center">
              <h3 className="text-lg font-semibold text-gray-900 mb-2">
                No se puede eliminar este empleado
              </h3>
              <p className="text-gray-600">
                Debes resolver los siguientes problemas antes de continuar:
              </p>
            </div>

            {/* Blockers List */}
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 space-y-3">
              {deletionResult?.blockers.map((blocker: DeletionBlocker, index: number) => {
                const Icon = getBlockerIcon(blocker.entity);
                return (
                  <div key={index} className="flex gap-3 items-start">
                    <div className="flex-shrink-0 w-10 h-10 bg-red-100 rounded-lg flex items-center justify-center">
                      <Icon className="w-5 h-5 text-red-600" />
                    </div>
                    <div className="flex-1">
                      <p className="font-medium text-red-900">{blocker.reason}</p>
                      <p className="text-sm text-red-700 mt-1">
                        {blocker.count} {blocker.entity} activo{blocker.count !== 1 ? 's' : ''}
                      </p>
                      <p className="text-sm text-red-600 mt-1 italic">
                        💡 {blocker.resolution}
                      </p>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Footer */}
            <div className="flex justify-end pt-4">
              <Button variant="outline" onClick={onClose}>
                Cerrar
              </Button>
            </div>
          </div>
        );

      case 'warnings':
        return (
          <div className="space-y-4">
            {/* Header */}
            <div className="flex items-center justify-center">
              <div className="w-16 h-16 bg-amber-100 rounded-full flex items-center justify-center">
                <AlertTriangle className="w-8 h-8 text-amber-600" />
              </div>
            </div>

            <div className="text-center">
              <h3 className="text-lg font-semibold text-gray-900 mb-2">
                ¿Estás seguro?
              </h3>
              <p className="text-gray-600">
                Se encontraron las siguientes advertencias:
              </p>
            </div>

            {/* Warnings List */}
            <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 space-y-3">
              {deletionResult?.warnings.map((warning: DeletionWarning, index: number) => (
                <div key={index} className="flex gap-3 items-start">
                  <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
                  <div className="flex-1">
                    <p className="text-amber-900">{warning.message}</p>
                    <p className="text-sm text-amber-700 mt-1">
                      {warning.count} {warning.entity} encontrado{warning.count !== 1 ? 's' : ''}
                    </p>
                  </div>
                </div>
              ))}
            </div>

            {/* Confirmation Checkbox */}
            <div className="bg-navy-800 border border-navy-700 rounded-lg p-4">
              <label className="flex items-start gap-3 cursor-pointer">
                <input
                  type="checkbox"
                  checked={confirmChecked}
                  onChange={(e) => setConfirmChecked(e.target.checked)}
                  className="mt-1 w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700">
                  Entiendo que este empleado tiene datos históricos asociados y deseo continuar con la eliminación de todas formas.
                </span>
              </label>
            </div>

            <div className="bg-red-50 border border-red-200 rounded-lg p-3">
              <p className="text-sm text-red-800">
                <strong>Advertencia:</strong> El empleado será marcado como eliminado y no aparecerá en listados,
                pero sus datos históricos se mantendrán en el sistema.
              </p>
            </div>

            {/* Footer */}
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="outline" onClick={onClose}>
                Cancelar
              </Button>
              <Button
                variant="danger"
                onClick={handleConfirmDelete}
                disabled={!confirmChecked}
                loading={isDeleting}
              >
                Eliminar de todas formas
              </Button>
            </div>
          </div>
        );

      case 'safe':
        return (
          <div className="space-y-4">
            {/* Header */}
            <div className="flex items-center justify-center">
              <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center">
                <CheckCircle className="w-8 h-8 text-blue-600" />
              </div>
            </div>

            <div className="text-center">
              <h3 className="text-lg font-semibold text-gray-900 mb-2">
                ¿Eliminar empleado?
              </h3>
              <p className="text-gray-600">
                ¿Estás seguro de eliminar a <strong>{empleadoNombre}</strong>?
              </p>
            </div>

            <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
              <div className="flex gap-3 items-start">
                <AlertCircle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
                <div className="text-sm text-amber-900">
                  <p className="font-medium mb-1">Esta acción no se puede deshacer fácilmente.</p>
                  <p>
                    El empleado será desactivado y no aparecerá en listados, pero sus datos históricos
                    se mantendrán en el sistema por razones de auditoría.
                  </p>
                </div>
              </div>
            </div>

            {/* Footer */}
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="outline" onClick={onClose}>
                Cancelar
              </Button>
              <Button
                variant="danger"
                onClick={handleConfirmDelete}
                loading={isDeleting}
              >
                Eliminar
              </Button>
            </div>
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={
        state === 'blockers'
          ? 'Error al eliminar'
          : state === 'warnings'
          ? 'Confirmar eliminación'
          : state === 'safe'
          ? 'Eliminar empleado'
          : 'Validando...'
      }
      size="md"
    >
      {renderContent()}
    </Modal>
  );
}
