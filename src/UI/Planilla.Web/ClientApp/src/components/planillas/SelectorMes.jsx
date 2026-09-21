import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { NOMBRES_MES } from '../../utils/periodos';

// ============================================================
// Selector de mes: ◀ Septiembre 2026 ▶ y un selector de año.
// Es el filtro único de la pantalla de planillas.
// ============================================================

export default function SelectorMes({ anio, mes, onChange }) {
  const anterior = () => (mes === 1 ? onChange(anio - 1, 12) : onChange(anio, mes - 1));
  const siguiente = () => (mes === 12 ? onChange(anio + 1, 1) : onChange(anio, mes + 1));
  const hoy = new Date();
  const esActual = anio === hoy.getFullYear() && mes === hoy.getMonth() + 1;
  const anios = [hoy.getFullYear() - 2, hoy.getFullYear() - 1, hoy.getFullYear(), hoy.getFullYear() + 1];
  if (!anios.includes(anio)) anios.push(anio);

  return (
    <div className="flex items-center gap-2">
      <button
        onClick={anterior}
        aria-label="Mes anterior"
        className="p-2 rounded-lg bg-slate-700 hover:bg-slate-600 text-gray-200"
      >
        <ChevronLeft className="w-4 h-4" />
      </button>
      <div className="min-w-[180px] text-center">
        <span className="text-lg font-semibold text-white">{NOMBRES_MES[mes - 1]}</span>
        <span className="ml-2 text-lg text-gray-400">{anio}</span>
      </div>
      <button
        onClick={siguiente}
        aria-label="Mes siguiente"
        className="p-2 rounded-lg bg-slate-700 hover:bg-slate-600 text-gray-200"
      >
        <ChevronRight className="w-4 h-4" />
      </button>
      <label htmlFor="planillas-anio" className="sr-only">Año</label>
      <select
        id="planillas-anio"
        value={anio}
        onChange={e => onChange(Number(e.target.value), mes)}
        className="ml-1 bg-slate-700 border border-slate-600 text-white rounded-lg px-2 py-1.5 text-sm"
      >
        {anios.sort((a, b) => a - b).map(a => <option key={a} value={a}>{a}</option>)}
      </select>
      {!esActual && (
        <button
          onClick={() => onChange(hoy.getFullYear(), hoy.getMonth() + 1)}
          className="ml-1 text-xs text-gray-400 hover:text-gray-200 underline-offset-2 hover:underline"
        >
          Hoy
        </button>
      )}
    </div>
  );
}
