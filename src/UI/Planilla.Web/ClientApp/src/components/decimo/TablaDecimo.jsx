import React from 'react';
import { AlertTriangle } from 'lucide-react';
import { formatCurrency } from '../../utils/currency';

// ============================================================
// Los empleados de una partida de décimo: una fila cada uno, los meses del
// cuatrimestre en columnas y, a la derecha, décimo, cuotas y neto.
//
// Un mes que viene de una planilla de Pagly se enseña y no se toca (dice de
// qué planilla sale). Un mes importado, escrito a mano o sin datos es
// editable mientras la partida no esté pagada: es el «agregar el mes a
// mano» de los meses en que no hubo planilla.
// ============================================================

const NOMBRE_MES = ['', 'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
const fmt = (n) => formatCurrency(n);

const ORIGEN = {
  Planilla: { etiqueta: 'de planilla', cls: 'text-gray-100' },
  Importado: { etiqueta: 'importado', cls: 'text-sky-200' },
  Manual: { etiqueta: 'a mano', cls: 'text-amber-200' },
  SinDatos: { etiqueta: 'sin datos', cls: 'text-gray-500' },
};

export default function TablaDecimo({ preview, editable, onMes }) {
  const empleados = preview?.empleados ?? [];
  if (empleados.length === 0) {
    return <div className="px-4 py-10 text-center text-sm text-gray-400">Sin empleados activos para este período.</div>;
  }

  // Las columnas de meses salen del primer empleado: el período es el mismo para todos.
  const columnas = empleados[0].meses ?? [];
  const suma = (k) => empleados.reduce((s, e) => s + (e[k] || 0), 0);
  const sumaMes = (i) => empleados.reduce((s, e) => s + (e.meses?.[i]?.monto || 0), 0);

  return (
    <div>
      {preview.empleadosConMesesSinDatos > 0 && (
        <p className="flex items-start gap-2 px-4 py-2.5 text-xs text-amber-200 bg-amber-950/30 border-b border-amber-900/50">
          <AlertTriangle className="w-4 h-4 shrink-0 mt-px" />
          {preview.empleadosConMesesSinDatos} {preview.empleadosConMesesSinDatos === 1 ? 'empleado tiene un mes' : 'empleados tienen meses'} sin datos:
          escribe el monto en la celda o impórtalo en la ficha del empleado. Lo que quede en 0 no suma al décimo.
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-sm min-w-[980px]">
          <thead className="bg-navy-950 border-b border-navy-700">
            <tr>
              <th className="text-left py-2.5 px-3 text-[11px] font-semibold text-gray-400 uppercase">Empleado</th>
              {columnas.map(m => (
                <th key={`${m.anio}-${m.mes}`} className="text-center py-2.5 px-2 text-[11px] font-semibold text-gray-400 uppercase">
                  {NOMBRE_MES[m.mes]} {String(m.anio).slice(2)}
                  {m.fraccion < 1 && <span className="block font-normal text-[10px] text-gray-500 normal-case">{Math.round(m.fraccion * 100)}% del mes</span>}
                </th>
              ))}
              <th className="text-right py-2.5 px-3 text-[11px] font-semibold text-gray-400 uppercase">Devengado</th>
              <th className="text-right py-2.5 px-3 text-[11px] font-semibold text-primary-300 uppercase" title="Devengado ÷ 12">Décimo</th>
              <th className="text-right py-2.5 px-2 text-[11px] font-semibold text-gray-400 uppercase" title="7.25 % sobre el décimo">CSS</th>
              <th className="text-right py-2.5 px-2 text-[11px] font-semibold text-gray-400 uppercase">SE</th>
              <th className="text-right py-2.5 px-2 text-[11px] font-semibold text-gray-400 uppercase">ISR</th>
              <th className="text-right py-2.5 px-3 text-[11px] font-semibold text-gray-400 uppercase">Neto</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-navy-700/50">
            {empleados.map(e => (
              <tr key={e.empleadoId} className="hover:bg-navy-800/50">
                <td className="py-2 px-3 whitespace-nowrap">
                  <span className="text-gray-100">{e.nombreCompleto}</span>
                  <span className="ml-2 text-[11px] text-gray-500 font-mono">{e.numeroIdentificacion}</span>
                </td>
                {(e.meses ?? []).map(m => {
                  const o = ORIGEN[m.origen] ?? ORIGEN.SinDatos;
                  const titulo = m.origen === 'Planilla'
                    ? `Viene de ${m.planillas?.length === 1 ? 'la planilla' : 'las planillas'} ${(m.planillas ?? []).join(', ')}`
                    : `Mes ${o.etiqueta}${m.fraccion < 1 ? ` · solo ${Math.round(m.fraccion * 100)} % del mes cae en el período` : ''}`;
                  return (
                    <td key={`${m.anio}-${m.mes}`} className="py-1.5 px-1.5 text-center" title={titulo}>
                      {editable && m.editable ? (
                        <input
                          type="number" min="0" step="0.01"
                          aria-label={`${NOMBRE_MES[m.mes]} ${m.anio} de ${e.nombreCompleto}`}
                          value={m.monto}
                          onChange={ev => onMes?.(e.empleadoId, m.anio, m.mes, ev.target.value)}
                          onFocus={ev => ev.target.select()}
                          className={`w-[84px] px-2 py-1.5 bg-navy-800 border rounded-md text-sm text-center font-mono focus:outline-none focus:ring-2 focus:ring-primary-500 ${
                            m.origen === 'SinDatos' ? 'border-amber-500/50 text-amber-200' : 'border-navy-600 text-gray-100'
                          }`}
                        />
                      ) : (
                        <span className={`font-mono ${o.cls}`}>{Number(m.monto ?? 0).toFixed(2)}</span>
                      )}
                    </td>
                  );
                })}
                <td className="py-2 px-3 text-right font-mono text-gray-200">{fmt(e.totalDevengado)}</td>
                <td className="py-2 px-3 text-right font-mono font-semibold text-primary-300">{fmt(e.montoDecimo)}</td>
                <td className="py-2 px-2 text-right font-mono text-gray-400">{fmt(e.cssEmpleado)}</td>
                <td className="py-2 px-2 text-right font-mono text-gray-400">{fmt(e.seEmpleado)}</td>
                <td className="py-2 px-2 text-right font-mono text-gray-400">{fmt(e.isr)}</td>
                <td className="py-2 px-3 text-right font-mono font-semibold text-emerald-400">{fmt(e.netoPago)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot className="bg-navy-950 border-t-2 border-navy-600">
            <tr>
              <td className="py-2.5 px-3 text-sm font-bold text-gray-100">TOTALES</td>
              {columnas.map((m, i) => (
                <td key={`t-${m.anio}-${m.mes}`} className="py-2.5 px-2 text-right font-mono text-xs font-bold text-gray-300">{sumaMes(i).toFixed(2)}</td>
              ))}
              <td className="py-2.5 px-3 text-right font-mono font-bold text-gray-100">{fmt(suma('totalDevengado'))}</td>
              <td className="py-2.5 px-3 text-right font-mono font-bold text-primary-300">{fmt(suma('montoDecimo'))}</td>
              <td className="py-2.5 px-2 text-right font-mono font-bold text-gray-300">{fmt(suma('cssEmpleado'))}</td>
              <td className="py-2.5 px-2 text-right font-mono font-bold text-gray-300">{fmt(suma('seEmpleado'))}</td>
              <td className="py-2.5 px-2 text-right font-mono font-bold text-gray-300">{fmt(suma('isr'))}</td>
              <td className="py-2.5 px-3 text-right font-mono font-bold text-emerald-400">{fmt(suma('netoPago'))}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
