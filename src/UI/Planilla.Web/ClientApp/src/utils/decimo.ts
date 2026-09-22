import { formatear } from './periodos';

// ============================================================
// El décimo tercer mes se paga en tres partidas (15 de abril, de agosto y
// de diciembre) y cada una cubre el cuatrimestre que termina ese día: del
// 16 del mes cuatro meses antes al 15 del mes de pago.
// ============================================================

/** Meses en los que se paga una partida de décimo. */
export const MESES_DE_PARTIDA = [4, 8, 12] as const;

/** El cuatrimestre que cierra el 15 del mes de pago. */
export function cuatrimestreDe(anio: number, mesPago: number): { desde: string; hasta: string } {
  const bruto = mesPago - 4;
  const desdeAnio = bruto <= 0 ? anio - 1 : anio;
  const desdeMes = bruto <= 0 ? bruto + 12 : bruto;
  return { desde: formatear(desdeAnio, desdeMes, 16), hasta: formatear(anio, mesPago, 15) };
}
