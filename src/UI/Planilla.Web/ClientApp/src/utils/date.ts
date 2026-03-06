/**
 * Formatea una fecha (con hora) en formato legible es-PA.
 * Maneja valores nulos, vacíos o no parseables devolviendo '-'.
 */
export function formatDateTime(dateStr: string | undefined | null): string {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return '-';
  return date.toLocaleString('es-PA', { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * Formatea solo la fecha (sin hora) en formato legible es-PA.
 * Maneja valores nulos, vacíos o no parseables devolviendo '-'.
 */
export function formatDate(dateStr: string | undefined | null): string {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return '-';
  return date.toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' });
}
