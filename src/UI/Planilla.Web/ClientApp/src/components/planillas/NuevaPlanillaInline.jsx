import React, { useEffect, useState } from 'react';
import { Plus, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatDate } from '../../utils/date';
import { PAY_PERIOD_CONFIG } from '../../constants/payroll';
import { finDePeriodo, inicioSugerido, NOMBRES_MES } from '../../utils/periodos';

// ============================================================
// Crear una planilla en el mes, sin modal. El inicio se sugiere a partir de
// la última planilla del mismo tipo en el mes; el fin se calcula solo según
// la frecuencia (y queda editable); la fecha de pago es opcional. El número
// lo pone el servidor.
// ============================================================

export default function NuevaPlanillaInline({ anio, mes, planillasDelMes, tipoPorDefecto = 2, onCreada }) {
  const [tipo, setTipo] = useState(tipoPorDefecto);
  const [inicio, setInicio] = useState(() => inicioSugerido(anio, mes, tipoPorDefecto, planillasDelMes));
  const [fin, setFin] = useState(() => finDePeriodo(inicioSugerido(anio, mes, tipoPorDefecto, planillasDelMes), tipoPorDefecto) ?? '');
  const [finTocado, setFinTocado] = useState(false);
  const [pago, setPago] = useState('');
  const [tipoPlanilla, setTipoPlanilla] = useState(0);
  const [creando, setCreando] = useState(false);

  // Cambia el mes o el tipo → se vuelve a sugerir inicio y fin.
  useEffect(() => {
    const ini = inicioSugerido(anio, mes, tipo, planillasDelMes);
    setInicio(ini);
    setFin(finDePeriodo(ini, tipo) ?? '');
    setFinTocado(false);
  }, [anio, mes, tipo, planillasDelMes]);

  const cambiarInicio = (v) => {
    setInicio(v);
    if (!finTocado) setFin(finDePeriodo(v, tipo) ?? '');
  };

  const invalido = !inicio || !fin || fin <= inicio || (pago && pago < inicio);
  const fueraDelMes = inicio && (Number(inicio.slice(0, 4)) !== anio || Number(inicio.slice(5, 7)) !== mes);

  const crear = async () => {
    if (invalido) return;
    try {
      setCreando(true);
      const r = await api.post('/api/payrollheaders', {
        payrollNumber: '',
        periodStartDate: inicio,
        periodEndDate: fin,
        payDate: pago || null,
        payPeriodType: Number(tipo),
        tipoPlanilla: Number(tipoPlanilla),
      });
      toast.success(`Planilla ${r.payrollNumber} creada`);
      setPago('');
      onCreada?.(r);
    } catch (e) {
      toast.error(e.message || 'No se pudo crear la planilla');
    } finally {
      setCreando(false);
    }
  };

  const inputCls = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500';

  return (
    <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 text-sm text-gray-300 mb-3">
        <Plus className="w-4 h-4 text-gray-500" /> Agregar planilla a <span className="text-white font-medium">{NOMBRES_MES[mes - 1]} {anio}</span>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-6 gap-3 items-end">
        <div>
          <label htmlFor="np-tipo" className="block text-xs text-gray-400 mb-1">Tipo</label>
          <select id="np-tipo" value={tipo} onChange={e => setTipo(Number(e.target.value))} className={inputCls + ' w-full'}>
            {Object.entries(PAY_PERIOD_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.name}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="np-inicio" className="block text-xs text-gray-400 mb-1">Inicio</label>
          <input id="np-inicio" type="date" value={inicio} onChange={e => cambiarInicio(e.target.value)} className={inputCls + ' w-full'} />
          {inicio && <p className="text-[11px] text-gray-500 mt-0.5">{formatDate(inicio)}</p>}
        </div>
        <div>
          <label htmlFor="np-fin" className="block text-xs text-gray-400 mb-1">Fin <span className="text-gray-600">(automático)</span></label>
          <input id="np-fin" type="date" value={fin} onChange={e => { setFin(e.target.value); setFinTocado(true); }} className={inputCls + ' w-full'} />
          {fin && <p className="text-[11px] text-gray-500 mt-0.5">{formatDate(fin)}</p>}
        </div>
        <div>
          <label htmlFor="np-pago" className="block text-xs text-gray-400 mb-1">Pago <span className="text-gray-600">(opcional)</span></label>
          <input id="np-pago" type="date" value={pago} onChange={e => setPago(e.target.value)} className={inputCls + ' w-full'} />
          <p className="text-[11px] text-gray-500 mt-0.5">{pago ? formatDate(pago) : 'Si se deja vacío, el fin del período'}</p>
        </div>
        <div>
          <label htmlFor="np-clase" className="block text-xs text-gray-400 mb-1">Planilla</label>
          <select id="np-clase" value={tipoPlanilla} onChange={e => setTipoPlanilla(Number(e.target.value))} className={inputCls + ' w-full'}>
            <option value={0}>Regular</option>
            <option value={1}>Sin deducciones legales</option>
          </select>
        </div>
        <div>
          <button
            onClick={crear}
            disabled={invalido || creando}
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-medium"
          >
            {creando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Crear
          </button>
        </div>
      </div>
      {fin && inicio && fin <= inicio && <p className="text-xs text-red-300 mt-2">El fin debe ser posterior al inicio.</p>}
      {pago && inicio && pago < inicio && <p className="text-xs text-red-300 mt-2">La fecha de pago no puede ser anterior al inicio.</p>}
      {fueraDelMes && !invalido && <p className="text-xs text-amber-300 mt-2">El inicio cae fuera de {NOMBRES_MES[mes - 1]}: la planilla aparecerá en el mes de su inicio.</p>}
    </div>
  );
}
