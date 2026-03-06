import { useState, useCallback } from 'react';
import toast from 'react-hot-toast';

/**
 * Encapsula el patrón isLoading / try-catch / finally / toast.error
 * común en las páginas de la aplicación.
 *
 * Uso:
 *   const { isLoading, run } = useAsyncLoad();
 *   const loadData = () => run(async () => {
 *     const data = await api.get('/api/...');
 *     setData(data);
 *   }, 'Error al cargar datos');
 */
export function useAsyncLoad() {
  const [isLoading, setIsLoading] = useState(false);

  const run = useCallback(async (fn: () => Promise<void>, errorMessage?: string) => {
    setIsLoading(true);
    try {
      await fn();
    } catch (error: unknown) {
      const msg = error instanceof Error ? error.message : (errorMessage ?? 'Error al cargar datos');
      toast.error(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { isLoading, run };
}
