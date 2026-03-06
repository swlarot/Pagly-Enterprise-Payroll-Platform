import { useEffect, useState } from 'react';
import { auditService } from '../services/auditService';
import type { AuditLogDto, PagedResultDto } from '../types/api';
import { useAsyncLoad } from '../hooks/useAsyncLoad';

export default function AuditLogPage() {
  const [logs, setLogs] = useState<PagedResultDto<AuditLogDto> | null>(null);
  const { isLoading, run } = useAsyncLoad();
  const [page, setPage] = useState(1);
  const pageSize = 20;

  useEffect(() => {
    loadLogs();
  }, [page]);

  const loadLogs = () => run(async () => {
    const data = await auditService.getAuditLogs({ page, pageSize });
    setLogs(data);
  }, 'Error al cargar el registro de auditoría');

  // Formatea fechas de forma robusta.
  // El backend envía 'createdAt' (DateTime UTC de .NET) serializado como ISO 8601.
  // Protege contra valores nulos, vacíos o no parseables.
  const formatDate = (dateStr: string | undefined | null) => {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return '-';
    return date.toLocaleString('es-PA', {
      dateStyle: 'short',
      timeStyle: 'short',
    });
  };

  const getActionBadge = (action: string) => {
    // Colores adaptados para dark theme
    const colors: Record<string, string> = {
      Created: 'bg-green-900/50 text-green-300 border border-green-700',
      Updated: 'bg-blue-900/50 text-blue-300 border border-blue-700',
      Deleted: 'bg-red-900/50 text-red-300 border border-red-700',
      Login: 'bg-purple-900/50 text-purple-300 border border-purple-700',
      Logout: 'bg-navy-700 text-gray-300 border border-navy-600',
      InviteSent: 'bg-yellow-900/50 text-yellow-300 border border-yellow-700',
      UserRemoved: 'bg-red-900/50 text-red-300 border border-red-700',
    };

    return (
      <span
        className={`px-2 py-1 rounded-full text-xs font-medium ${
          colors[action] || 'bg-navy-700 text-gray-300 border border-navy-600'
        }`}
      >
        {action}
      </span>
    );
  };

  return (
    <div className="space-y-6">
      {/* Encabezado */}
      <div>
        <h1 className="text-3xl font-bold text-gray-100">Registro de Auditoría</h1>
        <p className="text-gray-400 mt-2">
          Historial de actividades y cambios en el sistema
        </p>
      </div>

      {/* Tabla de auditoría */}
      <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-navy-700 bg-navy-800">
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  Fecha/Hora
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  Usuario
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  Acción
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  Entidad
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  Detalles
                </th>
                <th className="px-6 py-4 text-left text-sm font-semibold text-gray-100">
                  IP
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-700">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-400">
                    <div className="flex items-center justify-center gap-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-primary-400"></div>
                      Cargando registros...
                    </div>
                  </td>
                </tr>
              ) : !logs || logs.items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-400">
                    No hay registros de auditoría
                  </td>
                </tr>
              ) : (
                logs.items.map((log) => (
                  <tr key={log.id} className="hover:bg-navy-800 transition-colors">
                    <td className="px-6 py-4 text-sm text-gray-100 whitespace-nowrap">
                      {/* Usamos log.createdAt porque el backend serializa AuditLogDto.CreatedAt como "createdAt" */}
                      {formatDate(log.createdAt)}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-100">
                      {log.actorEmail || 'Sistema'}
                    </td>
                    <td className="px-6 py-4">{getActionBadge(log.action)}</td>
                    <td className="px-6 py-4 text-sm text-gray-300">
                      {log.entityType || '-'}
                      {log.entityId && (
                        <span className="text-gray-500"> #{log.entityId}</span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-300 max-w-xs truncate">
                      {log.metadataJson || '-'}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-300 font-mono">
                      {log.ipAddress || '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Paginación */}
        {logs && logs.totalPages > 1 && (
          <div className="flex items-center justify-between px-6 py-4 border-t border-navy-700">
            <div className="text-sm text-gray-400">
              Mostrando página {logs.page} de {logs.totalPages} ({logs.totalCount} registros
              totales)
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage(page - 1)}
                disabled={page === 1}
                className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                Anterior
              </button>
              <button
                onClick={() => setPage(page + 1)}
                disabled={page === logs.totalPages}
                className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
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
