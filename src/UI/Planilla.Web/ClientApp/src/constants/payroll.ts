/** Configuración de períodos de pago — claves coinciden con el enum PayPeriodType del backend */
export const PAY_PERIOD_CONFIG: Record<number, { name: string; periodsPerYear: number; weekMultiplier: number }> = {
  0: { name: 'Semanal',   periodsPerYear: 52, weekMultiplier: 1 },
  1: { name: 'Bisemanal', periodsPerYear: 26, weekMultiplier: 2 },
  2: { name: 'Quincenal', periodsPerYear: 24, weekMultiplier: 52 / 24 },
  3: { name: 'Mensual',   periodsPerYear: 12, weekMultiplier: 52 / 12 },
};

/** Opciones de riesgo profesional CSS — 5 clases (Acuerdo N°2 de 1995, CSS Panamá) */
export const CSS_RISK_OPTIONS: { value: number; label: string }[] = [
  { value: 0.56, label: '0.56% — Clase I (Riesgo Mínimo: oficinas, administración, comercio)' },
  { value: 0.98, label: '0.98% — Clase II' },
  { value: 2.10, label: '2.10% — Clase III (Riesgo Medio: transporte, manufactura)' },
  { value: 3.64, label: '3.64% — Clase IV' },
  { value: 5.67, label: '5.67% — Clase V (Riesgo Máximo: construcción, maquinaria, minería)' },
];
