// ============================================================
// Validación de la importación de empleados — lado cliente
//
// Espejo de ImportacionEmpleadosValidator.cs para poder corregir en tiempo
// real en la pantalla de revisión. El servidor vuelve a validar al confirmar,
// así que esta versión no necesita ser perfecta, pero sí decir lo mismo.
// ============================================================

export const TIPOS_PERIODO = ['Semanal', 'Bisemanal', 'Quincenal', 'Mensual'] as const;
export const TIPOS_CONTRATO = ['Indefinido', 'Definido', 'PorObra'] as const;
export const CLASES_RIESGO = [0.56, 0.98, 2.10, 3.64, 5.67] as const;

export interface MesImportado {
  anio: number;
  mes: number;
  monto: number | null;
}

export interface SaldosRenta {
  anio: number;
  isrRetenido: number | null;
  decimoPagado: number | null;
  partidasDecimo: number | null;
  gastoRepresentacionPagado: number | null;
  isrGastoRepresentacion: number | null;
}

export interface FilaImportacion {
  fila: number;
  cedula: string;
  nombre: string;
  apellido: string;
  email?: string | null;
  salarioBase: number | null;
  fechaContratacion: string | null;   // yyyy-MM-dd
  tipoPeriodo: string | null;
  tipoContrato?: string | null;
  departamento?: string | null;
  posicion?: string | null;
  dependientes?: number | null;
  riesgoProfesional?: number | null;
  gastoRepresentacionMensual?: number | null;
  sujetoCss?: boolean | null;
  sujetoSe?: boolean | null;
  sujetoIsr?: boolean | null;
  meses: MesImportado[];
  saldos?: SaldosRenta | null;
  yaExiste: boolean;
}

export type TipoProblema = 'Error' | 'Aviso';

export interface Problema {
  campo: string;
  tipo: TipoProblema;
  mensaje: string;
}

const CEDULA = /^(?:\d{1,2}|PE|E|N|NT|\d{1,2}AV|\d{1,2}PI)-\d{1,5}-\d{1,7}$/i;
const PASAPORTE = /^[A-Z0-9]{6,20}$/i;
const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export function normalizarCedula(c: string | null | undefined): string {
  return (c ?? '').trim().toUpperCase().replace(/\s+/g, '');
}

const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
export const nombreMes = (m: number) => MESES[m - 1] ?? String(m);

/**
 * Valida una fila. `repetidas` son las cédulas que aparecen más de una vez en
 * el archivo (se calculan una vez fuera, con `cedulasRepetidas`).
 */
export function validarFila(f: FilaImportacion, repetidas: Set<string>, hoy: Date): Problema[] {
  const p: Problema[] = [];
  const error = (campo: string, mensaje: string) => p.push({ campo, tipo: 'Error', mensaje });
  const aviso = (campo: string, mensaje: string) => p.push({ campo, tipo: 'Aviso', mensaje });

  const cedula = normalizarCedula(f.cedula);
  if (!cedula) error('cedula', 'Falta la cédula.');
  else if (!CEDULA.test(cedula) && !PASAPORTE.test(cedula)) error('cedula', 'La cédula no tiene un formato válido (ej. 8-123-4567, PE-12-345 o un pasaporte).');
  else if (repetidas.has(cedula)) error('cedula', 'Esta cédula aparece más de una vez en el archivo.');
  else if (f.yaExiste) aviso('cedula', 'Ya existe en la empresa: se actualizarán sus datos, no se creará otro.');

  const nombre = (f.nombre ?? '').trim();
  const apellido = (f.apellido ?? '').trim();
  if (!nombre) error('nombre', 'Falta el nombre.');
  if (!apellido) error('apellido', 'Falta el apellido.');
  if (nombre.length > 100) error('nombre', 'El nombre no puede pasar de 100 caracteres.');
  if (apellido.length > 100) error('apellido', 'El apellido no puede pasar de 100 caracteres.');

  if (f.email && f.email.trim() && !EMAIL.test(f.email.trim())) error('email', 'El correo no tiene un formato válido.');

  if (f.salarioBase == null || Number.isNaN(f.salarioBase)) error('salarioBase', 'Falta el salario base mensual.');
  else if (f.salarioBase <= 0) error('salarioBase', 'El salario base debe ser mayor que cero.');
  else if (f.salarioBase > 100000) aviso('salarioBase', 'Salario mensual muy alto; revisa que no sea anual.');

  const contratacion = f.fechaContratacion ? new Date(f.fechaContratacion + 'T00:00:00') : null;
  if (!contratacion || Number.isNaN(contratacion.getTime())) error('fechaContratacion', 'Falta la fecha de contratación.');
  else if (contratacion > hoy) error('fechaContratacion', 'La fecha de contratación no puede ser futura.');
  else if (contratacion.getFullYear() < 1950) error('fechaContratacion', 'La fecha de contratación no es válida.');

  if (!f.tipoPeriodo) error('tipoPeriodo', 'Falta el tipo de período de pago.');
  else if (!TIPOS_PERIODO.some(t => t.toLowerCase() === f.tipoPeriodo!.trim().toLowerCase()))
    error('tipoPeriodo', `El tipo de período debe ser uno de: ${TIPOS_PERIODO.join(', ')}.`);

  if (f.tipoContrato && !TIPOS_CONTRATO.some(t => t.toLowerCase() === f.tipoContrato!.replace(/\s/g, '').toLowerCase()))
    error('tipoContrato', `El tipo de contrato debe ser uno de: ${TIPOS_CONTRATO.join(', ')}.`);

  if (f.dependientes != null && (f.dependientes < 0 || f.dependientes > 20)) error('dependientes', 'Los dependientes deben estar entre 0 y 20.');

  if (f.riesgoProfesional != null && !CLASES_RIESGO.some(c => Math.abs(c - f.riesgoProfesional!) < 0.001))
    error('riesgoProfesional', 'El riesgo profesional debe ser una de las cinco clases: 0.56, 0.98, 2.10, 3.64 o 5.67.');

  if (f.gastoRepresentacionMensual != null) {
    if (f.gastoRepresentacionMensual < 0) error('gastoRepresentacionMensual', 'El gasto de representación no puede ser negativo.');
    else if (f.salarioBase && f.gastoRepresentacionMensual > f.salarioBase) error('gastoRepresentacionMensual', 'El gasto de representación no puede ser mayor que el salario.');
  }

  const conMonto = f.meses.filter(m => m.monto != null && !Number.isNaN(m.monto));
  for (const m of conMonto) {
    if (m.mes < 1 || m.mes > 12 || m.anio < 1990) error('meses', `El mes ${m.anio}-${String(m.mes).padStart(2, '0')} no es válido.`);
    else if ((m.monto as number) < 0) error('meses', `El salario de ${nombreMes(m.mes)} ${m.anio} es negativo.`);
  }

  if (contratacion && !Number.isNaN(contratacion.getTime())) {
    const inicio = new Date(contratacion.getFullYear(), contratacion.getMonth(), 1);
    const antes = conMonto.filter(m => (m.monto as number) > 0 && new Date(m.anio, m.mes - 1, 1) < inicio);
    if (antes.length > 0) aviso('meses', `Hay ${antes.length} mes(es) con salario antes de la fecha de contratación; revisa la fecha o esos meses.`);

    const masAntiguo = f.meses.length > 0
      ? f.meses.reduce((a, m) => { const d = new Date(m.anio, m.mes - 1, 1); return d < a ? d : a; }, new Date(9999, 0, 1))
      : inicio;
    const desde = masAntiguo > inicio ? masAntiguo : inicio;
    const hasta = new Date(hoy.getFullYear(), hoy.getMonth() - 1, 1);
    let faltantes = 0;
    for (let c = new Date(desde); c <= hasta; c = new Date(c.getFullYear(), c.getMonth() + 1, 1)) {
      if (!conMonto.some(m => m.anio === c.getFullYear() && m.mes === c.getMonth() + 1)) faltantes++;
    }
    if (faltantes > 0 && conMonto.length > 0)
      aviso('meses', `Faltan ${faltantes} mes(es) de salario desde la contratación. La prima de antigüedad, la indemnización y el décimo se calcularán con los meses que haya.`);
    else if (conMonto.length === 0 && inicio < hasta)
      aviso('meses', 'No trae salarios históricos. Hasta que haya planillas en Pagly, la liquidación y el décimo no tendrán con qué calcularse.');
  }

  const s = f.saldos;
  if (s) {
    const neg = [s.isrRetenido, s.decimoPagado, s.gastoRepresentacionPagado, s.isrGastoRepresentacion].some(v => v != null && v < 0);
    if (neg) error('saldos', 'Los saldos de renta no pueden ser negativos.');
    if (s.partidasDecimo != null && (s.partidasDecimo < 0 || s.partidasDecimo > 3)) error('saldos', 'Las partidas de décimo pagadas deben estar entre 0 y 3.');
    if ((s.decimoPagado ?? 0) > 0 && (s.partidasDecimo ?? 0) === 0)
      aviso('saldos', 'Hay décimo pagado pero 0 partidas: indica cuántas partidas se pagaron (la columna PERIODOS de la ficha depende de eso).');
    if ((s.isrGastoRepresentacion ?? 0) > 0 && (s.gastoRepresentacionPagado ?? 0) === 0)
      aviso('saldos', 'Hay ISR sobre gastos de representación pero no gastos pagados.');
  }

  return p;
}

export function cedulasRepetidas(filas: FilaImportacion[]): Set<string> {
  const vistas = new Map<string, number>();
  for (const f of filas) {
    const c = normalizarCedula(f.cedula);
    if (!c) continue;
    vistas.set(c, (vistas.get(c) ?? 0) + 1);
  }
  return new Set([...vistas.entries()].filter(([, n]) => n > 1).map(([c]) => c));
}

export const tieneErrores = (p: Problema[]) => p.some(x => x.tipo === 'Error');
export const tieneAvisos = (p: Problema[]) => p.some(x => x.tipo === 'Aviso');
