import React, { useEffect, useState } from 'react';
import { CheckCheck, Banknote, ChevronDown, ChevronUp, Trash2, Ban, Loader2, AlertCircle, FileDown } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatCurrency } from '../../utils/currency';
import { formatDate } from '../../utils/date';
import DetalleLiquidacion from './DetalleLiquidacion';

// ============================================================
// Una liquidación del mes: cabecera con el empleado, su estado y el neto,
// sus acciones según el estado y, desplegada, las cinco pestañas de la
// hoja del contador.
//
// El detalle se vuelve a calcular contra el servidor al abrirla, así que
// enseña también de dónde sale cada partida (los 60 meses, los cortes);
// los montos guardados mandan y se avisa si ya no coinciden.
// ============================================================

const ESTADO = {
  0: { label: 'Borrador', cls: 'bg-slate-700 text-gray-200' },
  1: { label: 'Calculada', cls: 'bg-blue-900/70 text-blue-200' },
  2: { label: 'Aprobada', cls: 'bg-primary-900/70 text-primary-200' },
  3: { label: 'Pagada', cls: 'bg-emerald-900/70 text-emerald-200' },
  4: { label: 'Anulada', cls: 'bg-red-900/60 text-red-200 line-through' },
};

// eslint-disable-next-line no-unused-vars -- Icon se usa como componente en el JSX
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

export default function FilaLiquidacion({ liquidacion, abierta, ocupada, atenuada, error, onAbrir, onAccion }) {
  const [detalle, setDetalle] = useState(null);
  const [cargando, setCargando] = useState(false);
  const st = ESTADO[liquidacion.estado] ?? ESTADO[1];

  useEffect(() => {
    if (!abierta || detalle) return;
    let vivo = true;
    (async () => {
      try {
        setCargando(true);
        const r = await api.post('/api/liquidaciones/previsualizar', {
          empleadoId: liquidacion.empleadoId,
          fechaTerminacion: liquidacion.fechaTerminacion,
          tipoTerminacion: liquidacion.tipoTerminacion,
          incluyePreaviso: liquidacion.incluyePreaviso,
          diasSalarioPendiente: liquidacion.diasSalarioPendiente,
        });
        if (vivo) setDetalle(r);
      } catch (e) {
        if (vivo) toast.error(e.message || 'No se pudo cargar el detalle de la liquidación');
      } finally {
        if (vivo) setCargando(false);
      }
    })();
    return () => { vivo = false; };
  }, [abierta]); // eslint-disable-line react-hooks/exhaustive-deps

  // Lo guardado manda; si el cálculo de hoy difiere, se dice.
  const recalculado = detalle?.calculo?.totalNeto;
  const difiere = recalculado != null && Math.abs(recalculado - (liquidacion.totalNeto || 0)) > 0.01;

  const descargarPdf = async () => {
    try {
      await api.download(`/api/liquidaciones/${liquidacion.id}/pdf`, `Liquidacion_${liquidacion.numero}.pdf`);
    } catch (e) {
      toast.error(e.message || 'No se pudo descargar el PDF');
    }
  };

  return (
    <div
      aria-busy={ocupada || undefined}
      className={`bg-navy-900 border rounded-xl overflow-hidden transition-opacity ${abierta ? 'border-navy-600' : 'border-navy-700'} ${
        ocupada ? 'ring-1 ring-primary-500' : ''} ${atenuada ? 'opacity-40 pointer-events-none' : ''}`}
    >
      <div
        role="button"
        tabIndex={0}
        onClick={onAbrir}
        onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onAbrir(); } }}
        className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 cursor-pointer hover:bg-navy-800/60"
      >
        <div className="flex items-center gap-2 min-w-0">
          {ocupada ? <Loader2 className="w-4 h-4 text-primary-400 animate-spin shrink-0" /> : (abierta ? <ChevronUp className="w-4 h-4 text-gray-500 shrink-0" /> : <ChevronDown className="w-4 h-4 text-gray-500 shrink-0" />)}
          <span className="font-mono font-semibold text-gray-100">{liquidacion.numero}</span>
        </div>
        <span className="text-sm text-gray-100">{liquidacion.empleadoNombre}</span>
        <span className="text-sm text-gray-400">
          {liquidacion.tipoTerminacionNombre} · sale el {formatDate(liquidacion.fechaTerminacion)}
          <span className="text-gray-500 ml-2 text-xs">{Number(liquidacion.anosServicio ?? 0).toFixed(2)} años</span>
        </span>
        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${st.cls}`}>{st.label}</span>
        <span className="text-sm text-gray-300 ml-auto whitespace-nowrap">
          Bruto <span className="font-mono text-gray-100">{formatCurrency(liquidacion.totalBruto)}</span>{' '}
          <span className="text-gray-500">·</span> Neto <span className="font-mono text-emerald-400">{formatCurrency(liquidacion.totalNeto)}</span>
        </span>
      </div>

      {error && <p className="px-4 pb-2 text-xs text-red-300 flex items-center gap-1.5"><AlertCircle className="w-3.5 h-3.5" /> {error}</p>}

      {liquidacion.estado !== 4 && (
        <div className="flex flex-wrap items-center gap-2 px-4 pb-3">
          {liquidacion.estado === 1 && <Btn onClick={() => onAccion('aprobar')} icon={CheckCheck} primario disabled={ocupada}>Aprobar</Btn>}
          {liquidacion.estado === 2 && <Btn onClick={() => onAccion('pagar')} icon={Banknote} primario disabled={ocupada}>Marcar pagada</Btn>}
          <Btn onClick={descargarPdf} icon={FileDown}>PDF</Btn>
          <span className="ml-auto flex items-center gap-2">
            {liquidacion.estado === 1 && <Btn onClick={() => onAccion('eliminar')} icon={Trash2} peligro disabled={ocupada} title="Solo mientras está calculada">Eliminar</Btn>}
            {liquidacion.estado >= 1 && <Btn onClick={() => onAccion('anular')} icon={Ban} peligro disabled={ocupada}>Anular</Btn>}
          </span>
        </div>
      )}

      {abierta && (
        <div className="border-t border-navy-700">
          {cargando ? (
            <div className="flex items-center justify-center py-10"><span className="w-6 h-6 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" /></div>
          ) : detalle ? (
            <>
              {difiere && (
                <p className="px-4 py-2 text-xs text-amber-300 border-b border-navy-700">
                  Lo guardado ({formatCurrency(liquidacion.totalNeto)}) ya no coincide con el cálculo de hoy
                  ({formatCurrency(recalculado)}): cambiaron los meses devengados del empleado desde que se creó.
                </p>
              )}
              <DetalleLiquidacion
                calculo={detalle.calculo}
                bases={detalle.bases}
                usaDevengadoReal={detalle.usaDevengadoReal}
                empleado={detalle.empleado}
              />
            </>
          ) : null}
        </div>
      )}
    </div>
  );
}
