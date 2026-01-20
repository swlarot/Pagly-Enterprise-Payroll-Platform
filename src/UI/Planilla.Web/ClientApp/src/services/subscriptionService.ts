import { api } from './api';

export interface BillingPortalResponse {
  url: string;
}

export const subscriptionService = {
  async getBillingPortalUrl(): Promise<string> {
    const response = await api.post<BillingPortalResponse>('/api/subscription/portal');
    return response.url;
  },
};
