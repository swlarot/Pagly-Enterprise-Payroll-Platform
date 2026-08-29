/**
 * Utilidades de fecha para Panamá (UTC-5).
 *
 * PROBLEMA QUE RESUELVEN: el backend guarda las fechas como
 * `timestamp with time zone` y las serializa en UTC ("2026-01-01T00:00:00Z").
 * Al hacer `new Date(...)` en Panamá (UTC-5) eso equivale al 31 de diciembre
 * a las 19:00, así que `toLocaleDateString` imprime el DÍA ANTERIOR.
 *
 * Para fechas de CALENDARIO (contratación, período de planilla, vigencia) el
 * instante no importa — importa el día. Por eso se parsea la parte YYYY-MM-DD
 * del string y se construye la fecha en horario local, sin conversión de zona.
 *
 * Para marcas de tiempo reales (creado/actualizado) sí se respeta la zona.
 */

/** Extrae la fecha de calendario de un ISO string, ignorando la zona horaria. */
function parseCalendarDate(dateStr: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(dateStr);
  if (match) {
    // Construida en local: el día se conserva tal cual lo envió el backend.
    return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  }
  const fallback = new Date(dateStr);
  return isNaN(fallback.getTime()) ? null : fallback;
}

/**
 * Formatea una marca de tiempo (con hora) en es-PA.
 * Aquí SÍ se convierte a la zona local, porque el instante es lo relevante.
 * Devuelve '-' para valores nulos, vacíos o no parseables.
 */
export function formatDateTime(dateStr: string | undefined | null): string {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return '-';
  return date.toLocaleString('es-PA', { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * Formatea una fecha de calendario en es-PA (ej. "01 ene 2026"),
 * sin desplazarla por zona horaria.
 * Devuelve '-' para valores nulos, vacíos o no parseables.
 */
export function formatDate(dateStr: string | undefined | null): string {
  if (!dateStr) return '-';
  const date = parseCalendarDate(dateStr);
  if (!date) return '-';
  return date.toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' });
}

/**
 * Formatea una fecha de calendario como dd/MM/yyyy — el formato de Panamá.
 * Devuelve '-' para valores nulos, vacíos o no parseables.
 */
export function formatDateShort(dateStr: string | undefined | null): string {
  if (!dateStr) return '-';
  const date = parseCalendarDate(dateStr);
  if (!date) return '-';
  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}/${date.getFullYear()}`;
}

/**
 * Convierte una fecha del backend al valor que espera un `<input type="date">`
 * (YYYY-MM-DD), sin desplazarla por zona horaria.
 *
 * Reemplaza al patrón `new Date(x).toISOString().split('T')[0]`, que en Panamá
 * retrocede un día cada vez que se abre y vuelve a guardar un registro.
 */
export function toDateInputValue(dateStr: string | undefined | null): string {
  if (!dateStr) return '';
  const match = /^(\d{4}-\d{2}-\d{2})/.exec(dateStr);
  if (match) return match[1];
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return '';
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

/**
 * Fecha de hoy como YYYY-MM-DD en hora LOCAL, para valores por defecto de formularios.
 * No usar `new Date().toISOString()`: convierte a UTC y en Panamá, después de las
 * 19:00, devuelve el día siguiente.
 */
export function todayInputValue(): string {
  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}
