import React, { useState } from 'react';
import { Calculator, CheckCheck, Banknote, Clock, ChevronDown, ChevronUp, Trash2, Ban, RefreshCw, Loader2, AlertCircle } from 'lucide-react';
import { formatCurrency } from '../../utils/currency';
import { formatDayMonth, formatDate } from '../../utils/date';
import { PAY_PERIOD_CONFIG } from '../../constants/payroll';
import TablaPlanilla from './TablaPlanilla';
import PanelHoras from './PanelHoras';

// ============================================================
// Una planilla del mes: cabecera con estado y totales, sus acciones según
// el estado, y desplegada, dos pestañas: Planilla (empleados y desglose) y
// Horas (captura). Las acciones viven aquí, no arriba de la página.
//
// Estados: 0 Borrador · 1 Calculada · 2 Aprobada · 3 Pagada · 4 Anulada
// Mientras esta fila corre una acción, se marca con el verde y aria-busy;
// la página atenúa las demás.
// ============================================================

const ESTADO = {
  0: { label: 'Borrador', cls: 'bg-slate-700 text-gray-200' },
  1: { label: 'Calculada', cls: 'bg-blue-900/70 text-blue-200' },
  2: { label: 'Aprobada', cls: 'bg-primary-900/70 text-primary-200' },
  3: { label: 'Pagada', cls: 'bg-emerald-900/70 text-emerald-200' },
  4: { label: 'Anulada', cls: 'bg-red-900/60 text-red-200 line-through' },
};

const Btn = ({ onClick, icon: Icon, children, primario, peligro, disabled, title }) => (
  <button
    onClick={(e) => { e.stopPropagation(); onClick(); }}
    disabled={disabled}
    title={title}
    className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${
      primario ? 'bg-primary-600 hover:bg-primary-700 text-white'
      : peligro ? 'bg-navy-800 hover:bg-red-900/40 border border-navy-600 text-gray-300 hover:text-red-200'
      : 'bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200'
    }`}
  >
    <Icon className="w-3.5 h-3.5" /> {children}
  </button>
);

export default function FilaPlanilla({ planilla, abierta, ocupada, atenuada, error, onAbrir, onAccion, onRecargar }) {
  const [pestana, setPestana] = useState('planilla');
  const st = ESTADO[planilla.status] ?? ESTADO[0];
  const tipo = PAY_PERIOD_CONFIG[planilla.payPeriodType]?.name ?? '';
  const empleados = (planilla.details ?? []).filter(d => (d.grossPay || 0) > 0).length;
  const calculada = planilla.status >= 1 && planilla.status !== 4;
  const editable = planilla.status === 0 || planilla.status === 1;

  return (
    <div
      aria-busy={ocupada || undefined}
      className={`bg-navy-900 border rounded-xl overflow-hidden transition-opacity ${
        abierta ? 'border-navy-600' : 'border-navy-700'
      } ${ocupada ? 'ring-1 ring-primary-500' : ''} ${atenuada ? 'opacity-40 pointer-events-none' : ''}`}
    >
      {/* Cabecera */}
      <div
        role="button"
        tabIndex={0}
        onClick={onAbrir}
        onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onAbrir(); } }}
        className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 cursor-pointer hover:bg-navy-800/60"
      >
        <div className="flex items-center gap-2 min-w-0">
          {ocupada ? <Loader2 className="w-4 h-4 text-primary-400 animate-spin shrink-0" /> : (abierta ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" /> : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />)}
          <span className="font-mono font-semibold text-gray-100">{planilla.payrollNumber}</span>
          <span className="text-xs px-1.5 py-0.5 rounded bg-navy-800 border border-navy-600 text-gray-300">{tipo}</span>
          {planilla.tipoPlanilla === 1 && <span className="text-xs px-1.5 py-0.5 rounded bg-amber-900/40 text-amber-200">Sin deducciones</span>}
        </div>
        <span className="text-sm text-gray-300">
          {formatDayMonth(planilla.periodStartDate)} – {formatDayMonth(planilla.periodEndDate)}
          <span className="text-gray-500 ml-2 text-xs" title="Fecha de pago">pago {formatDate(planilla.payDate)}</span>
        </span>
        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${st.cls}`}>{st.label}</span>
        {calculada && (
          <span className="text-sm text-gray-300 ml-auto whitespace-nowrap">
            <span className="text-gray-500">{empleados} {empleados === 1 ? 'empleado' : 'empleados'} ·</span>{' '}
            Bruto <span className="font-mono text-gray-100">{formatCurrency(planilla.totalGrossPay)}</span>{' '}
            <span className="text-gray-500">·</span> Neto <span className="font-mono text-emerald-400">{formatCurrency(planilla.totalNetPay)}</span>
          </span>
        )}
      </div>

      {error && (
        <p className="px-4 pb-2 text-xs text-red-300 flex items-center gap-1.5"><AlertCircle className="w-3.5 h-3.5" /> {error}</p>
      )}

      {/* Acciones por estado */}
      {planilla.status !== 4 && (
        <div className="flex flex-wrap items-center gap-2 px-4 pb-3">
          {editable && (
            <Btn onClick={() => { onAbrir(true); setPestana('horas'); }} icon={Clock}>Horas</Btn>
          )}
          {planilla.status === 0 && <Btn onClick={() => onAccion('calcular')} icon={Calculator} primario disabled={ocupada}>Calcular</Btn>}
          {planilla.status === 1 && (
            <>
              <Btn onClick={() => onAccion('calcular')} icon={RefreshCw} disabled={ocupada}>Recalcular</Btn>
              <Btn onClick={() => onAccion('aprobar')} icon={CheckCheck} primario disabled={ocupada}>Aprobar</Btn>
            </>
          )}
          {planilla.status === 2 && <Btn onClick={() => onAccion('pagar')} icon={Banknote} primario disabled={ocupada}>Marcar pagada</Btn>}
          <span className="ml-auto flex items-center gap-2">
            {planilla.status === 0 && <Btn onClick={() => onAccion('eliminar')} icon={Trash2} peligro disabled={ocupada} title="Solo borradores">Eliminar</Btn>}
            {(planilla.status === 1 || planilla.status === 2) && <Btn onClick={() => onAccion('anular')} icon={Ban} peligro disabled={ocupada}>Anular</Btn>}
          </span>
        </div>
      )}

      {/* Desplegado */}
      {abierta && (
        <div className="border-t border-navy-700">
          <div className="flex items-center gap-1 px-4 pt-2 border-b border-navy-700">
            {[['planilla', 'Planilla'], ['horas', 'Horas']].map(([k, label]) => (
              <button
                key={k}
                onClick={() => setPestana(k)}
                className={`px-3 py-2 text-sm border-b-2 -mb-px ${pestana === k ? 'border-primary-400 text-white' : 'border-transparent text-gray-400 hover:text-gray-200'}`}
              >
                {label}
              </button>
            ))}
          </div>
          {pestana === 'planilla'
            ? <TablaPlanilla planilla={planilla} />
            : <PanelHoras planillaId={planilla.id} editable={editable} onCambio={onRecargar} />}
        </div>
      )}
    </div>
  );
}
