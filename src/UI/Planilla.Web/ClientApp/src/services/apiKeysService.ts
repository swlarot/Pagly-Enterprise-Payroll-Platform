import { api } from './api';
import type {
  ApiKeyDto,
  CreateApiKeyRequest,
  CreateApiKeyResponse,
  ApiUsageStatsDto,
} from '../types/api';

/**
 * Cliente HTTP para los endpoints /api/api-keys del dashboard.
 * Los usa exclusivamente el Owner del tenant — el backend enforza role=Owner.
 *
 * El endpoint de consumo del API Platform (/v1/payroll/calculate)
 * NO pasa por aquí — eso lo hace el cliente final directamente con su API key
 * en el header X-Api-Key, no con el JWT del dashboard.
 */
export const apiKeysService = {
  /**
   * Lista todas las API keys del tenant actual.
   * El backend filtra automáticamente vía global query filter.
   */
  async list(): Promise<ApiKeyDto[]> {
    return api.get<ApiKeyDto[]>('/api/api-keys');
  },

  /**
   * Crea una nueva API key. El response incluye el plaintext UNA sola vez —
   * el caller debe mostrarlo al usuario de inmediato y nunca volver a
   * recuperarlo desde el servidor.
   */
  async create(request: CreateApiKeyRequest): Promise<CreateApiKeyResponse> {
    return api.post<CreateApiKeyResponse>('/api/api-keys', request);
  },

  /**
   * Revoca una key por id. Idempotente en el backend —
   * revocar una key ya revocada retorna 204 sin efecto.
   */
  async revoke(keyId: number): Promise<void> {
    return api.delete<void>(`/api/api-keys/${keyId}`);
  },

  /**
   * Datos agregados de uso del API Platform: requests/día, breakdown por status,
   * top keys, latencia promedio y p95. Alimenta el dashboard de analytics.
   */
  async getUsageStats(days: number = 30): Promise<ApiUsageStatsDto> {
    const since = new Date();
    since.setDate(since.getDate() - days);
    const sinceStr = since.toISOString();
    return api.get<ApiUsageStatsDto>(`/api/api-keys/usage?since=${sinceStr}`);
  },
};
