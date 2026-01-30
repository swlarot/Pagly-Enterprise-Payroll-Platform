/**
 * Configuración centralizada de la aplicación
 *
 * Este archivo centraliza todas las variables de entorno y configuraciones
 * para evitar valores hardcoded en el código.
 *
 * Las variables se leen desde import.meta.env (Vite)
 * y tienen valores por defecto seguros para desarrollo.
 */

export interface AppConfig {
  apiUrl: string;
  stripePublicKey: string;
  environment: string;
  features: {
    enableStripe: boolean;
    enableAnalytics: boolean;
  };
  apiTimeout: number;
  refreshTokenBuffer: number;
}

const config: AppConfig = {
  // URL base del API backend
  apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:5039',

  // Clave pública de Stripe (solo para producción)
  stripePublicKey: import.meta.env.VITE_STRIPE_PUBLIC_KEY || '',

  // Entorno actual (development, staging, production)
  environment: import.meta.env.VITE_APP_ENV || 'development',

  // Feature flags
  features: {
    // Habilitar integración de Stripe (solo si hay clave pública configurada)
    enableStripe: import.meta.env.VITE_ENABLE_STRIPE === 'true' && !!import.meta.env.VITE_STRIPE_PUBLIC_KEY,

    // Habilitar analytics (Google Analytics, Mixpanel, etc.)
    enableAnalytics: import.meta.env.VITE_ENABLE_ANALYTICS === 'true',
  },

  // Timeout para requests API en milisegundos (30 segundos)
  apiTimeout: Number(import.meta.env.VITE_API_TIMEOUT) || 30000,

  // Buffer antes de expiración del token para refresh (2 minutos)
  refreshTokenBuffer: Number(import.meta.env.VITE_REFRESH_TOKEN_BUFFER) || 120000,
} as const;

/**
 * Valida que la configuración sea correcta al iniciar la aplicación
 */
export function validateConfig(): void {
  if (!config.apiUrl) {
    throw new Error('VITE_API_URL no está configurado');
  }

  // En producción, verificar que Stripe esté configurado si está habilitado
  if (config.environment === 'production' && config.features.enableStripe && !config.stripePublicKey) {
    console.warn('⚠ Stripe está habilitado pero VITE_STRIPE_PUBLIC_KEY no está configurada');
  }
}

// Validar configuración al cargar el módulo
if (import.meta.env.MODE !== 'test') {
  validateConfig();
}

export default config;
