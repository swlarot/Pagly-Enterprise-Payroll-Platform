import React, { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { formatCurrency } from '../../utils/currency';
import { formatDate } from '../../utils/date';

// ============================================================
// El detalle de una liquidación con las mismas cinco vistas de la hoja del
// contador: Resumen · Salarios acumulados (los 60 meses) · Vacaciones ·
// Décimo · Indemnización. Cada pestaña dice de dónde sale su número, para
// que el contador pueda cotejarlo contra su Excel sin preguntar.
// ============================================================

const NOMBRE_MES = ['', 'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
const fmt = (n) => formatCurrency(n);
const n2 = (n) => Number(n ?? 0).toFixed(2);

const PESTANAS = [
  ['resumen', 'Resumen'],
  ['salarios', 'Salarios acumulados'],
  ['vacaciones', 'Vacaciones'],
  ['decimo', 'Décimo'],
  ['indemnizacion', 'Indemnización'],
];

function Linea({ label, valor, nota, fuerte, color }) {
  return (
    <div className="flex items-baseline justify-between gap-4 py-1">
      <span className="text-gray-400">
        {label}
        {nota && <span className="block text-[11px] text-gray-600">{nota}</span>}
      </span>
      <span className={`font-mono whitespace-nowrap ${fuerte ? 'font-semibold' : ''} ${color ?? 'text-gray-200'}`}>{valor}</span>
    </div>
  );
}

export default function DetalleLiquidacion({ calculo, bases, usaDevengadoReal, empleado }) {
  const [pestana, setPestana] = useState('resumen');
  const c = calculo ?? {};
  const b = bases ?? null;

  return (
    <div>
      <div className="flex items-center gap-1 px-4 pt-2 border-b border-navy-700 overflow-x-auto">
        {PESTANAS.map(([k, label]) => (
          <button
            key={k}
            onClick={() => setPestana(k)}
            className={`px-3 py-2 text-sm border-b-2 -mb-px whitespace-nowrap ${
              pestana === k ? 'border-primary-400 text-white' : 'border-transparent text-gray-400 hover:text-gray-200'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {!usaDevengadoReal && (
        <p className="flex items-start gap-2 px-4 py-2.5 text-xs text-amber-200 bg-amber-950/30 border-b border-amber-900/50">
          <AlertTriangle className="w-4 h-4 shrink-0 mt-px" />
          Este empleado no tiene meses devengados en Pagly: la liquidación se calculó con su salario base.
          Carga su historial (importación o ficha del empleado) y vuelve a previsualizar para liquidar como la hoja.
        </p>
      )}

      <div className="px-4 py-4 text-sm">
        {pestana === 'resumen' && (
          <div className="grid gap-x-8 gap-y-1 md:grid-cols-2">
            <div>
              <h4 className="font-semibold text-emerald-400 uppercase tracking-wider text-xs mb-1.5">Lo que se paga</h4>
              <Linea label="Prima de antigüedad" nota="Art. 224 · devengado de 60 meses ÷ 260 semanas × años" valor={fmt(c.primaAntiguedad)} />
              <Linea label={`Indemnización (${n2(c.indemnizacionSemanas)} semanas)`} nota="Art. 225 · salario semanal más favorable" valor={fmt(c.indemnizacion)} />
              {c.recargoArt219 > 0 && <Linea label="Recargo Art. 219" valor={fmt(c.recargoArt219)} />}
              {c.preaviso > 0 && <Linea label="Preaviso" nota="Art. 211" valor={fmt(c.preaviso)} />}
              <Linea label="Vacaciones" nota="Art. 54 · devengado del período ÷ 11" valor={fmt(c.vacacionesProporcionales)} />
              <Linea label="Décimo proporcional" nota="Art. 142 · (devengado + vacaciones) ÷ 12" valor={fmt(c.decimoTercerMesProporcional)} />
              {c.cesantia > 0 && <Linea label="Cesantía" nota="Decreto 60/1995" valor={fmt(c.cesantia)} />}
              {c.salarioPendiente > 0 && <Linea label={`Salarios vencidos (${n2(c.diasSalarioPendiente)} días)`} nota="Días trabajados: cotizan" valor={fmt(c.salarioPendiente)} />}
              <div className="border-t border-navy-700 mt-1 pt-1">
                <Linea label="Total bruto" valor={fmt(c.totalBruto)} fuerte />
              </div>
            </div>
            <div>
              <h4 className="font-semibold text-amber-400 uppercase tracking-wider text-xs mb-1.5">Lo que se descuenta</h4>
              <Linea label="Seguro Social" nota="9.75 % vacaciones y salarios · 7.25 % décimo" valor={fmt(c.cssEmpleado)} color="text-red-400" />
              <Linea label="Seguro Educativo" nota="1.25 %" valor={fmt(c.seEmpleado)} color="text-red-400" />
              {c.isr > 0 && <Linea label="ISR" valor={fmt(c.isr)} color="text-red-400" />}
              <div className="border-t border-navy-700 mt-1 pt-1">
                <Linea label="Total deducciones" valor={fmt(c.totalDeducciones)} fuerte color="text-red-400" />
                <Linea label="Neto a pagar" valor={fmt(c.totalNeto)} fuerte color="text-emerald-400" />
              </div>
              <div className="mt-3 text-[11px] text-gray-500">
                Prima, indemnización, recargo, preaviso y cesantía no cotizan (Ley 51/2005 Art. 92).
                Años de servicio: {n2(c.anosServicio)} · salario diario {fmt(c.salarioDiario)}.
              </div>
            </div>
          </div>
        )}

        {pestana === 'salarios' && (
          b ? (
            <div>
              <p className="text-xs text-gray-400 mb-2">
                Los meses que la ley manda mirar (Art. 224: 260 semanas). Suman <span className="font-mono text-gray-200">{fmt(b.devengado60Meses)}</span>
                {' '}en <span className="text-gray-200">{b.mesesConDatos}</span> {b.mesesConDatos === 1 ? 'mes' : 'meses'} con datos
                ({n2(b.semanas)} semanas) · promedio de los últimos 6 <span className="font-mono text-gray-200">{fmt(b.promedio6Meses)}</span>
                {' '}· último mes <span className="font-mono text-gray-200">{fmt(b.ultimoMesDevengado)}</span>.
              </p>
              <div className="overflow-x-auto">
                <table className="w-full text-xs min-w-[520px]">
                  <thead className="bg-navy-950 border-b border-navy-700">
                    <tr>
                      <th className="text-left py-2 px-3 text-[11px] font-semibold text-gray-400 uppercase">Mes</th>
                      <th className="text-right py-2 px-3 text-[11px] font-semibold text-gray-400 uppercase">Devengado</th>
                      <th className="text-left py-2 px-3 text-[11px] font-semibold text-gray-400 uppercase">Origen</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-navy-700/50">
                    {(b.meses ?? []).map(m => (
                      <tr key={`${m.anio}-${m.mes}`} className={m.monto > 0 ? '' : 'text-gray-600'}>
                        <td className="py-1.5 px-3">{NOMBRE_MES[m.mes]} {m.anio}</td>
                        <td className="py-1.5 px-3 text-right font-mono">{n2(m.monto)}</td>
                        <td className="py-1.5 px-3 text-gray-500">{m.origen === 'SinDatos' ? 'sin datos' : m.origen.toLowerCase()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : <p className="text-gray-400">Sin meses devengados: se liquidó con el salario base.</p>
        )}

        {pestana === 'vacaciones' && (
          <div className="max-w-xl">
            <Linea label="Devengado del período de referencia" nota={b?.vacacionesDesde ? `Desde ${formatDate(b.vacacionesDesde)} (última vacación tomada o contratación)` : undefined} valor={fmt(b?.devengadoDesdeUltimaVacacion)} />
            <Linea label="÷ 11 (Art. 54)" valor={fmt(c.vacacionesProporcionales)} fuerte color="text-emerald-400" />
            <Linea label="Equivalen a" valor={`${n2(c.diasVacacionesProporcionales)} días`} />
            <p className="text-[11px] text-gray-500 mt-2">Cotizan CSS 9.75 % y SE 1.25 %.</p>
          </div>
        )}

        {pestana === 'decimo' && (
          <div className="max-w-xl">
            <Linea label="Devengado desde la última partida pagada" nota={b?.decimoDesde ? `Desde ${formatDate(b.decimoDesde)}` : undefined} valor={fmt(b?.devengadoDesdeUltimaPartidaDecimo)} />
            <Linea label="+ vacaciones proporcionales" valor={fmt(c.vacacionesProporcionales)} />
            <Linea label="÷ 12 (Art. 142)" valor={fmt(c.decimoTercerMesProporcional)} fuerte color="text-emerald-400" />
            <p className="text-[11px] text-gray-500 mt-2">Cotiza CSS reducida 7.25 % (Ley 51/2005 Art. 96).</p>
          </div>
        )}

        {pestana === 'indemnizacion' && (
          <div className="max-w-xl">
            <Linea label="Promedio de los últimos 6 meses" nota="Art. 149" valor={fmt(b?.promedio6Meses)} />
            <Linea label="Último mes devengado" valor={fmt(b?.ultimoMesDevengado)} />
            <Linea label="Salario semanal (el más favorable ÷ 4.3333)" valor={fmt(c.salarioSemanal)} fuerte />
            <Linea label="Semanas (3.4 los primeros 10 años + 1 por año)" valor={n2(c.indemnizacionSemanas)} />
            <Linea label="Indemnización (Art. 225)" valor={fmt(c.indemnizacion)} fuerte color="text-emerald-400" />
            <div className="border-t border-navy-700 mt-2 pt-2">
              <Linea label="Prima de antigüedad (Art. 224)" nota={b ? `(${fmt(b.devengado60Meses)} + vacaciones) ÷ ${n2(b.semanas)} semanas × ${n2(c.anosServicio)} años` : undefined} valor={fmt(c.primaAntiguedad)} fuerte color="text-emerald-400" />
            </div>
            <p className="text-[11px] text-gray-500 mt-2">Ninguna de las dos cotiza (Ley 51/2005 Art. 92).</p>
          </div>
        )}
      </div>

      {empleado && (
        <p className="px-4 pb-3 text-[11px] text-gray-600">
          {empleado.nombre} {empleado.apellido} · {empleado.numeroIdentificacion} · contratado el {formatDate(empleado.fechaContratacion)}
        </p>
      )}
    </div>
  );
}
