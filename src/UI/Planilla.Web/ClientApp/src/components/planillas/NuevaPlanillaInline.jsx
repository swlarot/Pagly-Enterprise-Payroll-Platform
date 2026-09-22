import React, { useEffect, useState } from 'react';
import { Plus, Loader2, ArrowRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { PAY_PERIOD_CONFIG } from '../../constants/payroll';
import { finDePeriodo, inicioSugerido, parsear, NOMBRES_MES } from '../../utils/periodos';
import DatePicker from '../ui/DatePicker';

// ============================================================
// Crear una planilla en el mes, sin modal: una sola fila de controles.
// El inicio se sugiere a partir de la última planilla del mismo tipo en el
// mes; el fin se calcula solo según la frecuencia (y queda editable); la
// fecha de pago es opcional. El número lo pone el servidor.
//
// Si el mes ya está completo (la siguiente planilla empieza en otro mes), la
// fila lo dice y el botón crea en ese mes y lleva allí. Al crear, el servidor
// genera las horas de todos los empleados activos y trae las novedades
// aprobadas; la pantalla abre la planilla nueva en Horas.
// ============================================================

const selectCls = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 h-[34px]';

// Fuera del componente: si se definiera dentro, cada render lo remontaría y
// el calendario perdería el foco al escribir.
function Campo({ id, label, nota, ancho, children }) {
  return (
    <div className={ancho}>
      <label htmlFor={id} className="block text-xs text-gray-400 mb-1 whitespace-nowrap">
        {label}{nota && <span className="text-gray-600"> {nota}</span>}
      </label>
      {children}
    </div>
  );
}

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
  const pIni = parsear(inicio);
  const mesDestino = pIni && (pIni.y !== anio || pIni.m !== mes) ? { anio: pIni.y, mes: pIni.m } : null;
  const nombreDestino = mesDestino ? `${NOMBRES_MES[mesDestino.mes - 1]}${mesDestino.anio !== anio ? ' ' + mesDestino.anio : ''}` : null;
  const mesCompleto = mesDestino != null && inicio === inicioSugerido(anio, mes, tipo, planillasDelMes);

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
      const n = r.novedades ?? {};
      const partes = [`${r.horasGeneradas ?? 0} empleados con horas`];
      if (n.empleados > 0) partes.push(`${n.empleados} con novedades aprobadas`);
      toast.success(`Planilla ${r.payrollNumber} creada · ${partes.join(' · ')}`, { duration: 5000 });
      setPago('');
      onCreada?.(r, mesDestino);
    } catch (e) {
      toast.error(e.message || 'No se pudo crear la planilla');
    } finally {
      setCreando(false);
    }
  };

  return (
    <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 text-sm text-gray-300 mb-3">
        <Plus className="w-4 h-4 text-gray-500" />
        {mesCompleto ? (
          <span>
            <span className="text-white font-medium">{NOMBRES_MES[mes - 1]}</span> ya tiene sus planillas {PAY_PERIOD_CONFIG[tipo]?.name?.toLowerCase()}es · la siguiente empieza en <span className="text-white font-medium">{nombreDestino}</span>
          </span>
        ) : (
          <span>Agregar planilla a <span className="text-white font-medium">{NOMBRES_MES[mes - 1]} {anio}</span></span>
        )}
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <Campo id="np-tipo" label="Tipo" ancho="w-[130px]">
          <select id="np-tipo" value={tipo} onChange={e => setTipo(Number(e.target.value))} className={selectCls + ' w-full'}>
            {Object.entries(PAY_PERIOD_CONFIG).map(([k, v]) => <option key={k} value={k}>{v.name}</option>)}
          </select>
        </Campo>
        <Campo id="np-inicio" label="Inicio" ancho="w-[140px]">
          <DatePicker id="np-inicio" value={inicio} onChange={cambiarInicio} ariaLabel="Inicio del período" />
        </Campo>
        <Campo id="np-fin" label="Fin" nota="(automático)" ancho="w-[140px]">
          <DatePicker id="np-fin" value={fin} min={inicio} onChange={v => { setFin(v); setFinTocado(true); }} ariaLabel="Fin del período" />
        </Campo>
        <Campo id="np-pago" label="Pago" nota="(opcional)" ancho="w-[140px]">
          <DatePicker id="np-pago" value={pago} min={inicio} onChange={setPago} limpiable placeholder="fin del período" ariaLabel="Fecha de pago" />
        </Campo>
        <Campo id="np-clase" label="Planilla" ancho="w-[190px]">
          <select id="np-clase" value={tipoPlanilla} onChange={e => setTipoPlanilla(Number(e.target.value))} className={selectCls + ' w-full'}>
            <option value={0}>Regular</option>
            <option value={1}>Sin deducciones legales</option>
          </select>
        </Campo>
        <button
          onClick={crear}
          disabled={invalido || creando}
          className="h-[34px] flex items-center gap-2 px-4 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg text-sm font-medium whitespace-nowrap"
        >
          {creando ? <Loader2 className="w-4 h-4 animate-spin" /> : (mesDestino ? <ArrowRight className="w-4 h-4" /> : <Plus className="w-4 h-4" />)}
          {mesDestino ? `Crear en ${nombreDestino}` : 'Crear'}
        </button>
      </div>

      {fin && inicio && fin <= inicio && <p className="text-xs text-red-300 mt-2">El fin debe ser posterior al inicio.</p>}
      {pago && inicio && pago < inicio && <p className="text-xs text-red-300 mt-2">La fecha de pago no puede ser anterior al inicio.</p>}
      {mesDestino && !mesCompleto && !invalido && (
        <p className="text-xs text-gray-400 mt-2">El inicio cae en {nombreDestino}: la planilla se creará allí y la pantalla cambiará a ese mes.</p>
      )}
    </div>
  );
}
