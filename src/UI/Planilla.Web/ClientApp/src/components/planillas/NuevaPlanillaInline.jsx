import React, { useEffect, useMemo, useState } from 'react';
import { Plus, Loader2, ArrowRight, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { PAY_PERIOD_CONFIG } from '../../constants/payroll';
import { finDePeriodo, inicioSugerido, parsear, NOMBRES_MES } from '../../utils/periodos';
import DatePicker from '../ui/DatePicker';
import SelectorPersonas from './SelectorPersonas';

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
//
// El tipo arranca en el de la última planilla del mes; si el mes está vacío,
// en la frecuencia de pago más común entre las personas activas. Si el tipo
// elegido no es el de algunas personas, se avisa: su renta se proyecta con
// la frecuencia de la planilla, y una persona de pago mensual en una
// planilla quincenal sale con un ISR muchas veces mayor. Solo avisa, no
// bloquea: hay empresas que usan a propósito dos frecuencias con la misma gente.
//
// «Extraordinaria» es para pagarle a quien se quedó fuera de la planilla
// de su período: se eligen las personas y, antes de crear, se avisa si
// alguna ya está en otra planilla de esas fechas (para no pagar dos veces).
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
  const [tipoTocado, setTipoTocado] = useState(false);
  const [inicio, setInicio] = useState(() => inicioSugerido(anio, mes, tipoPorDefecto, planillasDelMes));
  const [fin, setFin] = useState(() => finDePeriodo(inicioSugerido(anio, mes, tipoPorDefecto, planillasDelMes), tipoPorDefecto) ?? '');
  const [finTocado, setFinTocado] = useState(false);
  const [pago, setPago] = useState('');
  const [tipoPlanilla, setTipoPlanilla] = useState(0);   // 0 regular · 1 sin deducciones · 'ext' extraordinaria
  const [creando, setCreando] = useState(false);
  const [empleados, setEmpleados] = useState(null);
  const [personas, setPersonas] = useState([]);
  const [yaCobraron, setYaCobraron] = useState([]);
  const esExtra = tipoPlanilla === 'ext';

  // La lista de empleados activos: para la extraordinaria y para comparar
  // su frecuencia de pago con el tipo de la planilla. Si falla, no hay aviso.
  useEffect(() => {
    if (empleados) return;
    api.get('/api/empleados')
      .then(d => setEmpleados((Array.isArray(d) ? d : d?.items ?? []).filter(e => e.estaActivo !== false)))
      .catch(() => setEmpleados([]));
  }, [empleados]);

  // Cuántas personas activas hay de cada frecuencia, y la más común.
  const porFrecuencia = useMemo(() => {
    const cuenta = {};
    for (const e of empleados ?? []) {
      const t = e.payPeriodTypeValue ?? e.payPeriodType;
      if (PAY_PERIOD_CONFIG[t]) cuenta[t] = (cuenta[t] ?? 0) + 1;
    }
    const comun = Object.entries(cuenta).sort((a, b) => b[1] - a[1])[0];
    return { cuenta, total: (empleados ?? []).length, comun: comun ? Number(comun[0]) : null };
  }, [empleados]);

  // Mes sin planillas: el tipo arranca en la frecuencia más común (si nadie lo cambió).
  useEffect(() => {
    if (tipoTocado || planillasDelMes.length > 0 || porFrecuencia.comun == null) return;
    setTipo(porFrecuencia.comun);
  }, [tipoTocado, planillasDelMes.length, porFrecuencia.comun]);

  const otras = esExtra ? 0 : porFrecuencia.total - (porFrecuencia.cuenta[tipo] ?? 0);
  const nombreTipo = t => PAY_PERIOD_CONFIG[t]?.name?.toLowerCase();
  const frecuenciasDistintas = Object.keys(porFrecuencia.cuenta).map(Number).filter(t => t !== Number(tipo));

  // ¿Alguna ya está en otra planilla de esas fechas? Se revisa al elegir.
  useEffect(() => {
    if (!esExtra || personas.length === 0 || !inicio || !fin || fin <= inicio) { setYaCobraron([]); return undefined; }
    const t = setTimeout(() => {
      api.post('/api/payrollheaders/extraordinaria/verificar', { periodStartDate: inicio, periodEndDate: fin, empleadoIds: personas })
        .then(r => setYaCobraron(Array.isArray(r) ? r : []))
        .catch(() => setYaCobraron([]));
    }, 400);
    return () => clearTimeout(t);
  }, [esExtra, personas, inicio, fin]);

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

  const invalido = !inicio || !fin || fin <= inicio || (pago && pago < inicio) || (esExtra && personas.length === 0);
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
        tipoPlanilla: esExtra ? 0 : Number(tipoPlanilla),
        esExtraordinaria: esExtra,
        empleadoIds: esExtra ? personas : null,
      });
      const n = r.novedades ?? {};
      const partes = [`${r.horasGeneradas ?? 0} empleados con horas`];
      if (n.empleados > 0) partes.push(`${n.empleados} con novedades aprobadas`);
      toast.success(esExtra
        ? `Planilla extraordinaria ${r.payrollNumber} creada para ${personas.length} ${personas.length === 1 ? 'persona' : 'personas'}`
        : `Planilla ${r.payrollNumber} creada · ${partes.join(' · ')}`, { duration: 5000 });
      setPago('');
      if (esExtra) { setPersonas([]); setTipoPlanilla(0); }
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
          <select id="np-tipo" value={tipo} onChange={e => { setTipo(Number(e.target.value)); setTipoTocado(true); }} className={selectCls + ' w-full'}>
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
          <select id="np-clase" value={tipoPlanilla} onChange={e => setTipoPlanilla(e.target.value === 'ext' ? 'ext' : Number(e.target.value))} className={selectCls + ' w-full'}>
            <option value={0}>Regular</option>
            <option value={1}>Sin deducciones legales</option>
            <option value="ext">Extraordinaria</option>
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

      {otras > 0 && (
        <div role="status" className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-200">
          <p className="flex items-start gap-1.5 min-w-0">
            <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
            <span>
              {otras === porFrecuencia.total ? `Las ${otras} personas activas` : `${otras} de ${porFrecuencia.total} personas activas`} tienen pago {frecuenciasDistintas.map(nombreTipo).join(' o ')}.
              {' '}En una planilla {nombreTipo(tipo)} su renta se proyecta como {nombreTipo(tipo)}: revisa sus horas antes de calcular.
            </span>
          </p>
          {porFrecuencia.comun != null && porFrecuencia.comun !== Number(tipo) && (
            <button type="button" onClick={() => { setTipo(porFrecuencia.comun); setTipoTocado(true); }}
              className="text-xs font-medium px-2.5 py-1 rounded-md border border-amber-400/40 hover:bg-amber-500/20 whitespace-nowrap">
              Usar {nombreTipo(porFrecuencia.comun)}
            </button>
          )}
        </div>
      )}
      {fin && inicio && fin <= inicio && <p className="text-xs text-red-300 mt-2">El fin debe ser posterior al inicio.</p>}
      {pago && inicio && pago < inicio && <p className="text-xs text-red-300 mt-2">La fecha de pago no puede ser anterior al inicio.</p>}
      {esExtra && (
        <div className="mt-3 space-y-2">
          <p className="text-xs text-gray-400">
            Para quien se quedó fuera de la planilla de ese período. Se calcula como si hubiera estado en ella: misma renta, mismas cuotas.
          </p>
          {empleados == null
            ? <Loader2 className="w-4 h-4 text-primary-400 animate-spin" aria-label="Cargando empleados" />
            : <SelectorPersonas empleados={empleados} seleccion={personas} onCambio={setPersonas} />}
          {yaCobraron.length > 0 && (
            <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-200 space-y-1">
              <p className="flex items-center gap-1.5 font-medium"><AlertTriangle className="w-4 h-4" /> Ya están en otra planilla de esas fechas; revisa que no se les pague dos veces:</p>
              <ul className="list-disc pl-6 text-amber-100/90">
                {yaCobraron.map(y => <li key={y.empleadoId}>{y.nombre}: {y.planillas.join('; ')}</li>)}
              </ul>
            </div>
          )}
          {pIni && (pIni.y < new Date().getFullYear() || (pIni.y === new Date().getFullYear() && pIni.m < new Date().getMonth() + 1)) && (
            <p className="text-xs text-gray-400">
              Si el SIPE de {NOMBRES_MES[pIni.m - 1]} ya se presentó a la CSS, esta planilla va en una declaración complementaria; aparecerá marcada como extraordinaria en el SIPE de ese mes.
            </p>
          )}
        </div>
      )}
      {mesDestino && !mesCompleto && !invalido && (
        <p className="text-xs text-gray-400 mt-2">El inicio cae en {nombreDestino}: la planilla se creará allí y la pantalla cambiará a ese mes.</p>
      )}
    </div>
  );
}
