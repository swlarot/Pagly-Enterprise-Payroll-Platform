import React, { useMemo } from 'react';
import { X, AlertTriangle, AlertCircle, CheckCircle2, SkipForward, Undo2 } from 'lucide-react';
import CuadriculaMeses from './CuadriculaMeses';
import { TIPOS_PERIODO, TIPOS_CONTRATO, CLASES_RIESGO } from '../../utils/validacionImportacion';

// ============================================================
// Panel lateral de la importación: un empleado, todos sus datos, editable.
// Cada cambio se manda arriba y la fila se revalida al instante; los
// problemas se muestran junto al campo al que pertenecen.
//
// Sitio donde va: es un panel a la derecha de la tabla de revisión (a 1280)
// y ocupa la pantalla entera a 400. Mientras está abierto, la tabla de atrás
// se atenúa: lo que manda es el empleado que se está corrigiendo.
// ============================================================

const Campo = ({ id, label, error, aviso, children }) => (
  <div>
    <label htmlFor={id} className="block text-xs font-medium text-gray-400 mb-1">{label}</label>
    {children}
    {error && <p className="mt-1 text-xs text-red-400 flex items-center gap-1"><AlertCircle className="w-3 h-3" />{error}</p>}
    {!error && aviso && <p className="mt-1 text-xs text-amber-400 flex items-center gap-1"><AlertTriangle className="w-3 h-3" />{aviso}</p>}
  </div>
);

const inputCls = (conError) =>
  `w-full bg-slate-800 border rounded-lg px-3 py-1.5 text-sm text-gray-100 focus:outline-none focus:ring-1 focus:ring-primary-500 ${
    conError ? 'border-red-500' : 'border-slate-600'
  }`;

export default function PanelEmpleadoImportado({ fila, problemas, omitida, onChange, onOmitir, onCerrar }) {
  const errores = useMemo(() => Object.fromEntries(problemas.filter(p => p.tipo === 'Error').map(p => [p.campo, p.mensaje])), [problemas]);
  const avisos = useMemo(() => Object.fromEntries(problemas.filter(p => p.tipo === 'Aviso').map(p => [p.campo, p.mensaje])), [problemas]);

  const set = (campo, valor) => onChange({ ...fila, [campo]: valor });
  const setSaldo = (campo, valor) => onChange({
    ...fila,
    saldos: { anio: fila.saldos?.anio ?? new Date().getFullYear(), ...(fila.saldos ?? {}), [campo]: valor },
  });
  const setMes = (anio, mes, monto) => onChange({
    ...fila,
    meses: fila.meses.map(m => (m.anio === anio && m.mes === mes) ? { ...m, monto } : m),
  });

  const num = (v) => (v === '' ? null : Number(v));
  const estado = errores && Object.keys(errores).length > 0 ? 'error' : Object.keys(avisos).length > 0 ? 'aviso' : 'ok';

  return (
    <aside
      className="fixed inset-y-0 right-0 z-40 w-full lg:w-[720px] bg-slate-900 border-l border-slate-700 shadow-2xl flex flex-col"
      role="dialog"
      aria-labelledby="panel-titulo"
    >
      {/* Cabecera */}
      <div className="flex items-start justify-between gap-3 px-5 py-4 border-b border-slate-700">
        <div className="min-w-0">
          <h2 id="panel-titulo" className="text-lg font-semibold text-white truncate">
            {fila.nombre || fila.apellido ? `${fila.nombre} ${fila.apellido}`.trim() : 'Empleado sin nombre'}
          </h2>
          <p className="text-xs text-gray-400 mt-0.5 flex items-center gap-2">
            Fila {fila.fila} del archivo
            {fila.yaExiste && <span className="px-1.5 py-0.5 rounded bg-blue-900/60 text-blue-200">ya existe · se actualiza</span>}
            {omitida && <span className="px-1.5 py-0.5 rounded bg-slate-700 text-gray-300">omitido</span>}
            {!omitida && estado === 'ok' && <span className="text-primary-400 flex items-center gap-1"><CheckCircle2 className="w-3.5 h-3.5" />listo</span>}
            {!omitida && estado === 'aviso' && <span className="text-amber-400 flex items-center gap-1"><AlertTriangle className="w-3.5 h-3.5" />con avisos</span>}
            {!omitida && estado === 'error' && <span className="text-red-400 flex items-center gap-1"><AlertCircle className="w-3.5 h-3.5" />con errores</span>}
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <button
            onClick={onOmitir}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-slate-700 hover:bg-slate-600 text-gray-200"
          >
            {omitida ? <><Undo2 className="w-3.5 h-3.5" /> Volver a incluir</> : <><SkipForward className="w-3.5 h-3.5" /> Omitir este empleado</>}
          </button>
          <button onClick={onCerrar} aria-label="Cerrar" className="p-1.5 rounded-lg hover:bg-slate-700 text-gray-300">
            <X className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Cuerpo */}
      <div className={`flex-1 overflow-y-auto px-5 py-4 space-y-6 ${omitida ? 'opacity-50 pointer-events-none' : ''}`}>
        {/* Datos del empleado */}
        <section>
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-3">Datos del empleado</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <Campo id="imp-cedula" label="Cédula *" error={errores.cedula} aviso={avisos.cedula}>
              <input id="imp-cedula" value={fila.cedula} onChange={e => set('cedula', e.target.value)} className={inputCls(!!errores.cedula)} />
            </Campo>
            <Campo id="imp-email" label="Correo" error={errores.email}>
              <input id="imp-email" type="email" value={fila.email ?? ''} onChange={e => set('email', e.target.value || null)} className={inputCls(!!errores.email)} />
            </Campo>
            <Campo id="imp-nombre" label="Nombre *" error={errores.nombre}>
              <input id="imp-nombre" value={fila.nombre} onChange={e => set('nombre', e.target.value)} className={inputCls(!!errores.nombre)} />
            </Campo>
            <Campo id="imp-apellido" label="Apellido *" error={errores.apellido}>
              <input id="imp-apellido" value={fila.apellido} onChange={e => set('apellido', e.target.value)} className={inputCls(!!errores.apellido)} />
            </Campo>
            <Campo id="imp-salario" label="Salario base mensual *" error={errores.salarioBase} aviso={avisos.salarioBase}>
              <input id="imp-salario" type="number" step="0.01" min="0" value={fila.salarioBase ?? ''} onChange={e => set('salarioBase', num(e.target.value))} className={inputCls(!!errores.salarioBase) + ' font-mono'} />
            </Campo>
            <Campo id="imp-fecha" label="Fecha de contratación *" error={errores.fechaContratacion}>
              <input id="imp-fecha" type="date" value={fila.fechaContratacion ?? ''} onChange={e => set('fechaContratacion', e.target.value || null)} className={inputCls(!!errores.fechaContratacion)} />
            </Campo>
            <Campo id="imp-periodo" label="Tipo de período *" error={errores.tipoPeriodo}>
              <select id="imp-periodo" value={fila.tipoPeriodo ?? ''} onChange={e => set('tipoPeriodo', e.target.value || null)} className={inputCls(!!errores.tipoPeriodo)}>
                <option value="">Elegir…</option>
                {TIPOS_PERIODO.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </Campo>
            <Campo id="imp-contrato" label="Tipo de contrato" error={errores.tipoContrato}>
              <select id="imp-contrato" value={fila.tipoContrato ?? 'Indefinido'} onChange={e => set('tipoContrato', e.target.value)} className={inputCls(!!errores.tipoContrato)}>
                {TIPOS_CONTRATO.map(t => <option key={t} value={t}>{t === 'PorObra' ? 'Por obra' : t}</option>)}
              </select>
            </Campo>
            <Campo id="imp-depto" label="Departamento">
              <input id="imp-depto" value={fila.departamento ?? ''} onChange={e => set('departamento', e.target.value || null)} className={inputCls(false)} placeholder="Se crea si no existe" />
            </Campo>
            <Campo id="imp-posicion" label="Posición">
              <input id="imp-posicion" value={fila.posicion ?? ''} onChange={e => set('posicion', e.target.value || null)} className={inputCls(false)} placeholder="Se crea si no existe" />
            </Campo>
            <Campo id="imp-dep" label="Dependientes" error={errores.dependientes}>
              <input id="imp-dep" type="number" min="0" max="20" value={fila.dependientes ?? ''} onChange={e => set('dependientes', num(e.target.value))} className={inputCls(!!errores.dependientes)} />
            </Campo>
            <Campo id="imp-riesgo" label="Riesgo profesional (clase)" error={errores.riesgoProfesional}>
              <select id="imp-riesgo" value={fila.riesgoProfesional ?? ''} onChange={e => set('riesgoProfesional', num(e.target.value))} className={inputCls(!!errores.riesgoProfesional)}>
                <option value="">Sin cambiar (0.56 por defecto)</option>
                {CLASES_RIESGO.map(c => <option key={c} value={c}>{c.toFixed(2)} %</option>)}
              </select>
            </Campo>
            <Campo id="imp-gr" label="Gasto de representación mensual" error={errores.gastoRepresentacionMensual}>
              <input id="imp-gr" type="number" step="0.01" min="0" value={fila.gastoRepresentacionMensual ?? ''} onChange={e => set('gastoRepresentacionMensual', num(e.target.value))} className={inputCls(!!errores.gastoRepresentacionMensual) + ' font-mono'} />
            </Campo>
            <div className="sm:col-span-2 flex flex-wrap gap-4 pt-1">
              {[['sujetoCss', 'Cotiza CSS'], ['sujetoSe', 'Cotiza SE'], ['sujetoIsr', 'Retiene ISR']].map(([k, label]) => (
                <label key={k} className="flex items-center gap-2 text-sm text-gray-300">
                  <input type="checkbox" checked={fila[k] ?? true} onChange={e => set(k, e.target.checked)} className="rounded" />
                  {label}
                </label>
              ))}
            </div>
          </div>
        </section>

        {/* Salarios históricos */}
        <section>
          <div className="flex items-baseline justify-between mb-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400">Salarios devengados por mes</h3>
            <span className="text-xs text-gray-500">Salario + extras + comisiones + vacaciones pagadas</span>
          </div>
          {errores.meses && <p className="mb-2 text-xs text-red-400 flex items-center gap-1"><AlertCircle className="w-3 h-3" />{errores.meses}</p>}
          {!errores.meses && avisos.meses && <p className="mb-2 text-xs text-amber-400 flex items-center gap-1"><AlertTriangle className="w-3 h-3" />{avisos.meses}</p>}
          <CuadriculaMeses meses={fila.meses} onChange={setMes} />
        </section>

        {/* Saldos de renta */}
        <section>
          <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-1">Saldos de renta {fila.saldos?.anio ?? new Date().getFullYear()}</h3>
          <p className="text-xs text-gray-500 mb-3">Solo si la empresa migra a mitad de año. El ingreso acumulado sale de los meses de arriba; aquí va lo ya retenido.</p>
          {errores.saldos && <p className="mb-2 text-xs text-red-400 flex items-center gap-1"><AlertCircle className="w-3 h-3" />{errores.saldos}</p>}
          {!errores.saldos && avisos.saldos && <p className="mb-2 text-xs text-amber-400 flex items-center gap-1"><AlertTriangle className="w-3 h-3" />{avisos.saldos}</p>}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <Campo id="imp-isr" label="ISR retenido en el año">
              <input id="imp-isr" type="number" step="0.01" min="0" value={fila.saldos?.isrRetenido ?? ''} onChange={e => setSaldo('isrRetenido', num(e.target.value))} className={inputCls(false) + ' font-mono'} />
            </Campo>
            <Campo id="imp-decimo" label="Décimo pagado en el año">
              <input id="imp-decimo" type="number" step="0.01" min="0" value={fila.saldos?.decimoPagado ?? ''} onChange={e => setSaldo('decimoPagado', num(e.target.value))} className={inputCls(false) + ' font-mono'} />
            </Campo>
            <Campo id="imp-partidas" label="Partidas de décimo pagadas">
              <select id="imp-partidas" value={fila.saldos?.partidasDecimo ?? 0} onChange={e => setSaldo('partidasDecimo', Number(e.target.value))} className={inputCls(false)}>
                <option value={0}>Ninguna</option>
                <option value={1}>1 (abril)</option>
                <option value={2}>2 (abril y agosto)</option>
                <option value={3}>3 (abril, agosto y diciembre)</option>
              </select>
            </Campo>
            <Campo id="imp-grp" label="Gastos de representación pagados">
              <input id="imp-grp" type="number" step="0.01" min="0" value={fila.saldos?.gastoRepresentacionPagado ?? ''} onChange={e => setSaldo('gastoRepresentacionPagado', num(e.target.value))} className={inputCls(false) + ' font-mono'} />
            </Campo>
            <Campo id="imp-isrgr" label="ISR retenido sobre esos gastos">
              <input id="imp-isrgr" type="number" step="0.01" min="0" value={fila.saldos?.isrGastoRepresentacion ?? ''} onChange={e => setSaldo('isrGastoRepresentacion', num(e.target.value))} className={inputCls(false) + ' font-mono'} />
            </Campo>
          </div>
        </section>
      </div>

      <div className="px-5 py-3 border-t border-slate-700 flex justify-end">
        <button onClick={onCerrar} className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg text-sm font-medium">
          Listo
        </button>
      </div>
    </aside>
  );
}
