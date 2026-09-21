// ============================================================
// Períodos de pago: fin de período a partir del inicio y la frecuencia,
// y el inicio del siguiente. Todo con fechas de calendario (yyyy-MM-dd),
// sin zonas horarias: en Panamá "el 15" es el 15.
//
//   Quincenal: día 1–15 → 15; día 16+ → último día del mes
//   Semanal:   inicio + 6 días
//   Bisemanal: inicio + 13 días
//   Mensual:   último día del mes de inicio
// ============================================================

/** Coincide con PayPeriodType del backend. */
export const TIPO_PERIODO = { Semanal: 0, Bisemanal: 1, Quincenal: 2, Mensual: 3 } as const;
export type TipoPeriodo = (typeof TIPO_PERIODO)[keyof typeof TIPO_PERIODO];

const pad = (n: number) => String(n).padStart(2, '0');

export function parsear(iso: string): { y: number; m: number; d: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '');
  if (!m) return null;
  const y = Number(m[1]), mo = Number(m[2]), d = Number(m[3]);
  if (mo < 1 || mo > 12 || d < 1 || d > diasDelMes(y, mo)) return null;
  return { y, m: mo, d };
}

export const formatear = (y: number, m: number, d: number) => `${y}-${pad(m)}-${pad(d)}`;

export function diasDelMes(y: number, m: number): number {
  return new Date(Date.UTC(y, m, 0)).getUTCDate();
}

function sumarDias(y: number, m: number, d: number, n: number) {
  const t = new Date(Date.UTC(y, m - 1, d + n));
  return { y: t.getUTCFullYear(), m: t.getUTCMonth() + 1, d: t.getUTCDate() };
}

/** Fecha de fin del período que empieza en `inicio`, según la frecuencia. */
export function finDePeriodo(inicio: string, tipo: TipoPeriodo | number): string | null {
  const p = parsear(inicio);
  if (!p) return null;
  switch (Number(tipo)) {
    case TIPO_PERIODO.Quincenal:
      return p.d <= 15 ? formatear(p.y, p.m, 15) : formatear(p.y, p.m, diasDelMes(p.y, p.m));
    case TIPO_PERIODO.Semanal: {
      const f = sumarDias(p.y, p.m, p.d, 6);
      return formatear(f.y, f.m, f.d);
    }
    case TIPO_PERIODO.Bisemanal: {
      const f = sumarDias(p.y, p.m, p.d, 13);
      return formatear(f.y, f.m, f.d);
    }
    case TIPO_PERIODO.Mensual:
      return formatear(p.y, p.m, diasDelMes(p.y, p.m));
    default:
      return null;
  }
}

/** El día siguiente al fin de un período: inicio natural del próximo. */
export function siguienteInicio(finAnterior: string): string | null {
  const p = parsear(finAnterior);
  if (!p) return null;
  const s = sumarDias(p.y, p.m, p.d, 1);
  return formatear(s.y, s.m, s.d);
}

/** Primer día del mes (yyyy-MM-dd) para un año y mes dados. */
export const primerDia = (y: number, m: number) => formatear(y, m, 1);

/**
 * Inicio sugerido para una planilla nueva en un mes: el día después de la
 * última planilla de ese tipo en el mes, o el día 1 si no hay ninguna.
 */
export function inicioSugerido(
  y: number,
  m: number,
  tipo: TipoPeriodo | number,
  planillasDelMes: Array<{ periodEndDate: string; payPeriodType: number }>,
): string {
  const delTipo = planillasDelMes
    .filter(p => Number(p.payPeriodType) === Number(tipo))
    .map(p => String(p.periodEndDate).slice(0, 10))
    .sort();
  if (delTipo.length === 0) return primerDia(y, m);
  const sig = siguienteInicio(delTipo[delTipo.length - 1]);
  // Si el siguiente ya cae en otro mes, se sugiere el 1 de ese mes.
  return sig ?? primerDia(y, m);
}

export const NOMBRES_MES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
