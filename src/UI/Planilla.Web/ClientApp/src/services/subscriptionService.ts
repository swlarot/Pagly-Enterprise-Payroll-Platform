import { api } from './api';
import type { SubscriptionUsageDto } from '../types/api';

export interface BillingPortalResponse {
  url: string;
}

export const subscriptionService = {
  async getBillingPortalUrl(): Promise<string> {
    const response = await api.post<BillingPortalResponse>('/api/subscription/portal');
    return response.url;
  },

  async getUsage(): Promise<SubscriptionUsageDto> {
    return api.get<SubscriptionUsageDto>('/api/subscription/usage');
  },
};
