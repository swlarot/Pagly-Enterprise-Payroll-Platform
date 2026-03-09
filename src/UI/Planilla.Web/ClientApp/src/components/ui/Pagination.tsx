import { Button } from './Button';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount?: number;
  pageSize?: number;
  onPageChange: (page: number) => void;
}

/**
 * Componente de paginación reutilizable.
 * No renderiza nada si totalPages <= 1.
 */
export function Pagination({ page, totalPages, totalCount, pageSize, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null;

  const from = pageSize && totalCount ? (page - 1) * pageSize + 1 : null;
  const to = pageSize && totalCount ? Math.min(page * pageSize, totalCount) : null;

  return (
    <div className="flex items-center justify-between px-6 py-4 border-t border-navy-700">
      <div className="text-sm text-gray-400">
        {from !== null && to !== null && totalCount != null
          ? `Mostrando ${from} a ${to} de ${totalCount} resultados`
          : `Página ${page} de ${totalPages}`}
      </div>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(page - 1)}
          disabled={page === 1}
        >
          Anterior
        </Button>
        <span className="text-sm text-gray-400">{page} / {totalPages}</span>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(page + 1)}
          disabled={page === totalPages}
        >
          Siguiente
        </Button>
      </div>
    </div>
  );
}
