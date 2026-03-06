import React from 'react';
import { Loader2 } from 'lucide-react';

export interface Column<T> {
  header: string;
  key: string;
  className?: string;
  render: (row: T) => React.ReactNode;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[] | undefined | null;
  isLoading?: boolean;
  emptyMessage?: string;
  keyExtractor: (row: T) => string | number;
}

export function DataTable<T>({
  columns,
  data,
  isLoading = false,
  emptyMessage = 'No hay datos disponibles',
  keyExtractor,
}: DataTableProps<T>) {
  const colSpan = columns.length;

  return (
    <div className="overflow-x-auto">
      <table className="w-full">
        <thead>
          <tr className="border-b border-navy-700 bg-navy-800">
            {columns.map((col) => (
              <th
                key={col.key}
                className={`px-6 py-4 text-left text-sm font-semibold text-gray-100 ${col.className ?? ''}`}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-navy-700">
          {isLoading ? (
            <tr>
              <td colSpan={colSpan} className="px-6 py-12 text-center text-gray-400">
                <div className="flex items-center justify-center gap-2">
                  <Loader2 className="w-5 h-5 animate-spin text-primary-400" />
                  Cargando...
                </div>
              </td>
            </tr>
          ) : !data || data.length === 0 ? (
            <tr>
              <td colSpan={colSpan} className="px-6 py-12 text-center text-gray-400">
                {emptyMessage}
              </td>
            </tr>
          ) : (
            data.map((row) => (
              <tr key={keyExtractor(row)} className="hover:bg-navy-800 transition-colors">
                {columns.map((col) => (
                  <td key={col.key} className={`px-6 py-4 text-sm text-gray-100 ${col.className ?? ''}`}>
                    {col.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
