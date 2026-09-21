import React, { useMemo } from 'react';

// ============================================================
// Cuadrícula de salarios por mes: 12 filas (meses) × N columnas (años).
// Es la misma tabla "Detalle de Salarios de los 5 últimos años" que lleva
// el contador. Se usa en la importación (todo editable) y en la ficha del
// empleado (solo editables los meses que no vienen de planillas de Pagly).
//
// Sitio donde va: manda la tabla; a 400 px de ancho cabe con 5 columnas
// de año porque cada celda es un número corto — se contiene con
// overflow-x-auto en su propio marco, nunca en la página.
// ============================================================

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

const fmt = (n) => (n == null || Number.isNaN(n))
  ? ''
  : Number(n).toLocaleString('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * @param {Object} props
 * @param {Array<{anio:number, mes:number, monto:number|null, origen?:string, editable?:boolean}>} props.meses
 * @param {(anio:number, mes:number, monto:number|null) => void} [props.onChange]
 * @param {boolean} [props.soloLectura]
 * @param {Array<number>} [props.anios] años a mostrar; si no, se infieren de `meses`
 * @param {string} [props.celdaConProblema] clave "anio-mes" a resaltar
 */
export default function CuadriculaMeses({ meses, onChange, soloLectura = false, anios: aniosProp, resaltar = new Set() }) {
  const porClave = useMemo(() => {
    const m = new Map();
    for (const x of meses) m.set(`${x.anio}-${x.mes}`, x);
    return m;
  }, [meses]);

  const anios = useMemo(() => {
    if (aniosProp?.length) return aniosProp;
    const set = new Set(meses.map(m => m.anio));
    return [...set].sort((a, b) => a - b);
  }, [meses, aniosProp]);

  const totalPorAnio = (anio) => meses
    .filter(m => m.anio === anio && m.monto != null)
    .reduce((s, m) => s + Number(m.monto), 0);

  const totalGeneral = meses.filter(m => m.monto != null).reduce((s, m) => s + Number(m.monto), 0);
  const mesesConDato = meses.filter(m => m.monto != null).length;

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-700">
      <table className="w-full text-[12.5px] border-collapse min-w-[520px]">
        <thead>
          <tr className="bg-slate-700/50 text-gray-300">
            <th className="px-2 py-2 text-left font-semibold w-28">Mes</th>
            {anios.map(a => <th key={a} className="px-2 py-2 text-right font-semibold">{a}</th>)}
          </tr>
        </thead>
        <tbody>
          {MESES.map((nombre, i) => {
            const mes = i + 1;
            return (
              <tr key={mes} className="border-t border-slate-700/70 hover:bg-slate-700/20">
                <td className="px-2 py-1 text-gray-300">{nombre}</td>
                {anios.map(anio => {
                  const clave = `${anio}-${mes}`;
                  const celda = porClave.get(clave);
                  const existeEnRango = !!celda;
                  const esPlanilla = celda?.origen === 'Planilla';
                  const editable = !soloLectura && existeEnRango && (celda.editable ?? !esPlanilla);
                  const conProblema = resaltar.has(clave);
                  const titulo = !existeEnRango ? 'Fuera del rango'
                    : esPlanilla ? 'Calculado por Pagly a partir de las planillas del mes'
                    : celda.origen === 'Importado' ? 'Importado desde la plantilla'
                    : celda.origen === 'Manual' ? 'Escrito a mano'
                    : celda.monto == null ? 'Sin datos' : '';

                  if (!existeEnRango) {
                    return <td key={clave} className="px-2 py-1 bg-slate-900/40" title={titulo} />;
                  }

                  if (!editable) {
                    return (
                      <td
                        key={clave}
                        title={titulo}
                        className={`px-2 py-1 text-right font-mono ${
                          esPlanilla ? 'text-primary-300' : celda.monto == null ? 'text-gray-600' : 'text-gray-200'
                        }`}
                      >
                        {celda.monto == null ? '—' : fmt(celda.monto)}
                        {esPlanilla && <span className="ml-1 text-[9px] text-primary-400 align-top">P</span>}
                      </td>
                    );
                  }

                  return (
                    <td key={clave} className="px-1 py-0.5" title={titulo}>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        inputMode="decimal"
                        aria-label={`${nombre} ${anio}`}
                        value={celda.monto ?? ''}
                        placeholder="—"
                        onChange={e => {
                          const v = e.target.value;
                          onChange?.(anio, mes, v === '' ? null : Number(v));
                        }}
                        className={`w-full min-w-[84px] bg-slate-800 border rounded px-2 py-1 text-right font-mono text-gray-100
                          focus:outline-none focus:ring-1 focus:ring-primary-500 ${
                          conProblema ? 'border-red-500' : celda.monto == null ? 'border-slate-600 border-dashed' : 'border-slate-600'
                        }`}
                      />
                    </td>
                  );
                })}
              </tr>
            );
          })}
        </tbody>
        <tfoot>
          <tr className="border-t border-slate-600 bg-slate-900/50 text-gray-100 font-semibold">
            <td className="px-2 py-2">Totales</td>
            {anios.map(a => <td key={a} className="px-2 py-2 text-right font-mono">{fmt(totalPorAnio(a))}</td>)}
          </tr>
        </tfoot>
      </table>
      <div className="flex items-center justify-between px-3 py-2 text-xs text-gray-400 border-t border-slate-700">
        <span>{mesesConDato} de {meses.length} meses con dato</span>
        <span>Total <span className="font-mono text-gray-200">{fmt(totalGeneral)}</span></span>
      </div>
    </div>
  );
}
