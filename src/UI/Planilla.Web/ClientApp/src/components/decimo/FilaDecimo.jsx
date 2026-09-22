import React, { useCallback, useEffect, useState } from 'react';
import { Calculator, Banknote, ChevronDown, ChevronUp, Trash2, RefreshCw, RotateCcw, Loader2, AlertCircle, FileDown, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatCurrency } from '../../utils/currency';
import { formatDate } from '../../utils/date';
import TablaDecimo from './TablaDecimo';

// ============================================================
// Una partida de décimo del mes: cabecera con estado y totales, sus
// acciones según el estado y, desplegada, la tabla por empleado con los
// meses del cuatrimestre.
//
// Mientras no esté pagada, los meses sin planilla se pueden escribir a
// mano: la tabla se recalcula en el servidor sin guardar nada (lo mismo que
// verá al pulsar Calcular). Borrar es permanente y pide el número.
// ============================================================

const ESTADO = {
  Borrador: { label: 'Borrador', cls: 'bg-slate-700 text-gray-200' },
  Calculada: { label: 'Calculada', cls: 'bg-blue-900/70 text-blue-200' },
  Pagada: { label: 'Pagada', cls: 'bg-emerald-900/70 text-emerald-200' },
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

export default function FilaDecimo({ partida, abierta, ocupada, atenuada, error, onAbrir, onAccion, onBorrar }) {
  const [preview, setPreview] = useState(null);
  const [cargando, setCargando] = useState(false);
  const [ajustes, setAjustes] = useState({}); // "empId-anio-mes" → monto
  const editable = partida.estado !== 'Pagada';
  const st = ESTADO[partida.estado] ?? ESTADO.Borrador;

  const cargarPreview = useCallback(async (conAjustes) => {
    try {
      setCargando(true);
      const r = await api.post('/api/decimo/previsualizar', {
        periodoDesde: partida.periodoDesde,
        periodoHasta: partida.periodoHasta,
        fechaPago: partida.fechaPago,
        ajustes: Object.entries(conAjustes ?? {}).map(([k, monto]) => {
          const [empleadoId, anio, mes] = k.split('-').map(Number);
          return { empleadoId, anio, mes, monto: Number(monto) || 0 };
        }),
      });
      setPreview(r);
    } catch (e) {
      toast.error(e.message || 'No se pudo calcular la previsualización');
    } finally {
      setCargando(false);
    }
  }, [partida.periodoDesde, partida.periodoHasta, partida.fechaPago]);

  useEffect(() => { if (abierta && !preview) cargarPreview(ajustes); }, [abierta]); // eslint-disable-line react-hooks/exhaustive-deps

  // Escribir un mes: se refleja al instante y el servidor recalcula tras la pausa.
  const cambiarMes = (empleadoId, anio, mes, valor) => {
    const clave = `${empleadoId}-${anio}-${mes}`;
    const monto = Number(valor) || 0;
    setAjustes(prev => {
      const siguiente = { ...prev, [clave]: monto };
      clearTimeout(cambiarMes.t);
      cambiarMes.t = setTimeout(() => cargarPreview(siguiente), 600);
      return siguiente;
    });
    setPreview(prev => prev && {
      ...prev,
      empleados: prev.empleados.map(e => e.empleadoId !== empleadoId ? e : {
        ...e,
        meses: e.meses.map(m => (m.anio === anio && m.mes === mes ? { ...m, monto } : m)),
      }),
    });
  };

  const hayAjustes = Object.keys(ajustes).length > 0;
  const descargar = async (formato) => {
    const ext = formato === 'pdf' ? 'pdf' : 'excel';
    try {
      await api.download(`/api/reportes/desglose-decimo/${partida.id}/${ext}`, `Decimo_${partida.numero}.${formato === 'pdf' ? 'pdf' : 'xlsx'}`);
    } catch (e) {
      toast.error(e.message || 'No se pudo descargar el desglose');
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
          <span className="font-mono font-semibold text-gray-100">{partida.numero}</span>
        </div>
        <span className="text-sm text-gray-300">
          {formatDate(partida.periodoDesde)} – {formatDate(partida.periodoHasta)}
          <span className="text-gray-500 ml-2 text-xs">pago {formatDate(partida.fechaPago)}</span>
        </span>
        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${st.cls}`}>{st.label}</span>
        {partida.estado !== 'Borrador' && (
          <span className="text-sm text-gray-300 ml-auto whitespace-nowrap">
            <span className="text-gray-500">{partida.numEmpleados} {partida.numEmpleados === 1 ? 'empleado' : 'empleados'} ·</span>{' '}
            Décimo <span className="font-mono text-gray-100">{formatCurrency(partida.totalDecimo)}</span>{' '}
            <span className="text-gray-500">·</span> Neto <span className="font-mono text-emerald-400">{formatCurrency(partida.totalNetoPago)}</span>
          </span>
        )}
      </div>

      {error && <p className="px-4 pb-2 text-xs text-red-300 flex items-center gap-1.5"><AlertCircle className="w-3.5 h-3.5" /> {error}</p>}

      <div className="flex flex-wrap items-center gap-2 px-4 pb-3">
        {partida.estado === 'Borrador' && <Btn onClick={() => onAccion('calcular', ajustes)} icon={Calculator} primario disabled={ocupada}>Calcular</Btn>}
        {partida.estado === 'Calculada' && (
          <>
            <Btn onClick={() => onAccion('calcular', ajustes)} icon={hayAjustes ? Save : RefreshCw} primario={hayAjustes} disabled={ocupada}>
              {hayAjustes ? 'Guardar cambios' : 'Recalcular'}
            </Btn>
            <Btn onClick={() => onAccion('pagar')} icon={Banknote} primario={!hayAjustes} disabled={ocupada}>Marcar pagada</Btn>
          </>
        )}
        {partida.estado === 'Pagada' && <Btn onClick={() => onAccion('reabrir')} icon={RotateCcw} disabled={ocupada} title="Vuelve a Calculada para corregirla">Reabrir</Btn>}
        {partida.estado !== 'Borrador' && (
          <>
            <Btn onClick={() => descargar('excel')} icon={FileDown}>Excel</Btn>
            <Btn onClick={() => descargar('pdf')} icon={FileDown}>PDF</Btn>
          </>
        )}
        <span className="ml-auto">
          <Btn onClick={onBorrar} icon={Trash2} peligro disabled={ocupada} title="Borrado permanente: pide el número de la partida">Borrar</Btn>
        </span>
      </div>

      {abierta && (
        <div className="border-t border-navy-700">
          {cargando && !preview ? (
            <div className="flex items-center justify-center py-10"><span className="w-6 h-6 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" /></div>
          ) : preview ? (
            <div className={cargando ? 'opacity-50 transition-opacity' : 'transition-opacity'} aria-busy={cargando || undefined}>
              <TablaDecimo preview={preview} editable={editable} onMes={cambiarMes} />
              {editable && (
                <p className="px-4 py-2 text-xs text-gray-500 border-t border-navy-700">
                  Los meses con planilla en Pagly no se editan. Lo que escribas queda guardado como devengado del empleado al calcular.
                </p>
              )}
            </div>
          ) : null}
        </div>
      )}
    </div>
  );
}
