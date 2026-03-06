import { useEffect, useState } from 'react';
import { auditService } from '../services/auditService';
import type { AuditLogDto, PagedResultDto } from '../types/api';
import { useAsyncLoad } from '../hooks/useAsyncLoad';
import { formatDateTime } from '../utils/date';
import { Pagination } from '../components/ui/Pagination';
import { DataTable } from '../components/ui/DataTable';
import type { Column } from '../components/ui/DataTable';

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

  const auditColumns: Column<AuditLogDto>[] = [
    { key: 'createdAt', header: 'Fecha/Hora', className: 'whitespace-nowrap', render: (log) => formatDateTime(log.createdAt) },
    { key: 'actorEmail', header: 'Usuario', render: (log) => log.actorEmail || 'Sistema' },
    { key: 'action', header: 'Acción', render: (log) => getActionBadge(log.action) },
    { key: 'entityType', header: 'Entidad', className: 'text-gray-300', render: (log) => (
      <>{log.entityType || '-'}{log.entityId && <span className="text-gray-500"> #{log.entityId}</span>}</>
    )},
    { key: 'metadataJson', header: 'Detalles', className: 'text-gray-300 max-w-xs truncate', render: (log) => log.metadataJson || '-' },
    { key: 'ipAddress', header: 'IP', className: 'text-gray-300 font-mono', render: (log) => log.ipAddress || '-' },
  ];

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
        <DataTable<AuditLogDto>
          columns={auditColumns}
          data={logs?.items}
          isLoading={isLoading}
          emptyMessage="No hay registros de auditoría"
          keyExtractor={(log) => log.id}
        />

        {/* Paginación */}
        {logs && (
          <Pagination
            page={page}
            totalPages={logs.totalPages}
            totalCount={logs.totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
          />
        )}
      </div>
    </div>
  );
}
