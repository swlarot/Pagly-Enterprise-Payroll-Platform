import { api } from './api';
import type { SystemApiUsageDto } from '../types/api';

/**
 * Cliente HTTP para el panel global del System Admin.
 * Los endpoints están restringidos por policy RequireSystemAdmin — solo
 * usuarios con IsSystemAdmin = true pueden acceder.
 *
 * A diferencia de apiKeysService.getUsageStats() (que es per-tenant), este
 * servicio consulta data AGREGADA across ALL tenants. Su razón de existir
 * es darle al operador de la plataforma una visión unificada del uso.
 */
export const systemApiUsageService = {
  /**
   * Analytics global: summary + ranking de tenants + series de tiempo + signals.
   * @param days Período en días hacia atrás (default 30). Máx recomendado: 90.
   * @param topN Cantidad de tenants en el ranking (default 20, máx 100).
   */
  async getGlobalUsage(days: number = 30, topN: number = 20): Promise<SystemApiUsageDto> {
    const since = new Date();
    since.setDate(since.getDate() - days);
    const sinceStr = since.toISOString();
    return api.get<SystemApiUsageDto>(
      `/api/system-admin/api-usage/global?since=${sinceStr}&topN=${topN}`
    );
  },

  /**
   * Exporta el ranking completo de tenants como CSV. Dispara un download
   * en el navegador. No paginado — incluye TODOS los tenants con actividad.
   */
  async exportCsv(days: number = 30): Promise<void> {
    const since = new Date();
    since.setDate(since.getDate() - days);
    const sinceStr = since.toISOString();
    const url = `/api/system-admin/api-usage/export.csv?since=${sinceStr}`;

    // Reutilizamos la instancia de axios subyacente de api.ts para que el
    // interceptor del JWT funcione (los plain fetch no incluyen Authorization).
    const response = await fetch(url, {
      headers: {
        Authorization: `Bearer ${localStorage.getItem('auth_token') ?? ''}`,
      },
    });

    if (!response.ok) {
      throw new Error(`CSV export falló: ${response.status} ${response.statusText}`);
    }

    const blob = await response.blob();
    const blobUrl = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = blobUrl;
    a.download = extractFilename(response.headers.get('content-disposition')) ?? 'api-usage.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(blobUrl);
  },
};

/** Extrae el filename del header Content-Disposition (si viene). */
function extractFilename(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDisposition);
  return match ? decodeURIComponent(match[1]) : null;
}
