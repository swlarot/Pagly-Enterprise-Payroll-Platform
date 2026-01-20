import React, { useEffect, useState } from 'react';
import { auditService } from '../services/auditService';
import type { AuditLogDto, PagedResultDto } from '../types/api';
import toast from 'react-hot-toast';

export default function AuditLogPage() {
  const [logs, setLogs] = useState<PagedResultDto<AuditLogDto> | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  useEffect(() => {
    loadLogs();
  }, [page]);

  const loadLogs = async () => {
    setIsLoading(true);
    try {
      const data = await auditService.getAuditLogs({ page, pageSize });
      setLogs(data);
    } catch (error: any) {
      toast.error('Error al cargar el registro de auditoría');
    } finally {
      setIsLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('es-PA', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const getActionBadge = (action: string) => {
    const colors: Record<string, string> = {
      Created: 'bg-green-100 text-green-800',
      Updated: 'bg-blue-100 text-blue-800',
      Deleted: 'bg-red-100 text-red-800',
      Login: 'bg-purple-100 text-purple-800',
      Logout: 'bg-gray-100 text-gray-800',
      InviteSent: 'bg-yellow-100 text-yellow-800',
      UserRemoved: 'bg-red-100 text-red-800',
    };

    return (
      <span className={`px-2 py-1 rounded-full text-xs font-medium ${colors[action] || 'bg-gray-100 text-gray-800'}`}>
        {action}
      </span>
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Registro de Auditoría</h1>
        <p className="text-gray-600 mt-2">
          Historial de actividades y cambios en el sistema
        </p>
      </div>

      {/* Audit Log Table */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-gray-200 bg-gray-50">
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Fecha/Hora
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Usuario
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Acción
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Entidad
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  Detalles
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-900">
                  IP
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    <div className="flex items-center justify-center gap-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600"></div>
                      Cargando registros...
                    </div>
                  </td>
                </tr>
              ) : !logs || logs.items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    No hay registros de auditoría
                  </td>
                </tr>
              ) : (
                logs.items.map((log) => (
                  <tr key={log.id} className="hover:bg-gray-50">
                    <td className="px-6 py-4 text-sm text-gray-900 whitespace-nowrap">
                      {formatDate(log.timestamp)}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-900">
                      {log.actorEmail || 'Sistema'}
                    </td>
                    <td className="px-6 py-4">{getActionBadge(log.action)}</td>
                    <td className="px-6 py-4 text-sm text-gray-600">
                      {log.entityType || '-'}
                      {log.entityId && (
                        <span className="text-gray-400"> #{log.entityId}</span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-600 max-w-xs truncate">
                      {log.details || '-'}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-600 font-mono">
                      {log.ipAddress || '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {logs && logs.totalPages > 1 && (
          <div className="flex items-center justify-between px-6 py-4 border-t border-gray-200">
            <div className="text-sm text-gray-600">
              Mostrando página {logs.page} de {logs.totalPages} ({logs.totalCount} registros
              totales)
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage(page - 1)}
                disabled={page === 1}
                className="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                Anterior
              </button>
              <button
                onClick={() => setPage(page + 1)}
                disabled={page === logs.totalPages}
                className="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                Siguiente
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
