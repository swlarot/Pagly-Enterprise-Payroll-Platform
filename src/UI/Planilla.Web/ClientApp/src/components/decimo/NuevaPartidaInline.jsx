import React, { useEffect, useMemo, useState } from 'react';
import { Plus, Loader2, Eye, ArrowRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { NOMBRES_MES, formatear } from '../../utils/periodos';
import { cuatrimestreDe } from '../../utils/decimo';
import DatePicker from '../ui/DatePicker';
import TablaDecimo from './TablaDecimo';

// ============================================================
// Crear una partida de décimo en el mes, sin modal. El cuatrimestre se
// propone solo a partir del mes de pago (abril, agosto o diciembre) y la
// previsualización enseña todos los empleados con sus meses ANTES de
// crear: ahí mismo se escriben los meses que no tienen planilla.
// ============================================================

const PARTIDAS = { 4: 'primera partida (16 dic – 15 abr)', 8: 'segunda partida (16 abr – 15 ago)', 12: 'tercera partida (16 ago – 15 dic)' };

export default function NuevaPartidaInline({ anio, mes, onCreada }) {
  const mesPago = [4, 8, 12].includes(mes) ? mes : 4;
  const sugerido = useMemo(() => cuatrimestreDe(anio, mesPago), [anio, mesPago]);
  const [desde, setDesde] = useState(sugerido.desde);
  const [hasta, setHasta] = useState(sugerido.hasta);
  const [pago, setPago] = useState(formatear(anio, mesPago, 15));
  const [preview, setPreview] = useState(null);
  const [ajustes, setAjustes] = useState({});
  const [cargando, setCargando] = useState(false);
  const [creando, setCreando] = useState(false);

  useEffect(() => {
    setDesde(sugerido.desde);
    setHasta(sugerido.hasta);
    setPago(formatear(anio, mesPago, 15));
    setPreview(null);
    setAjustes({});
  }, [anio, mesPago, sugerido]);

  const invalido = !desde || !hasta || hasta <= desde || !pago;
  const mesDePago = pago ? Number(pago.slice(5, 7)) : mesPago;
  const fueraDelMes = pago && (Number(pago.slice(0, 4)) !== anio || mesDePago !== mes);

  const listaAjustes = (obj) => Object.entries(obj).map(([k, monto]) => {
    const [empleadoId, a, m] = k.split('-').map(Number);
    return { empleadoId, anio: a, mes: m, monto: Number(monto) || 0 };
  });

  const previsualizar = async (conAjustes = ajustes) => {
    if (invalido) return;
    try {
      setCargando(true);
      setPreview(await api.post('/api/decimo/previsualizar', {
        periodoDesde: desde, periodoHasta: hasta, fechaPago: pago, ajustes: listaAjustes(conAjustes),
      }));
    } catch (e) {
      toast.error(e.message || 'No se pudo previsualizar el décimo');
    } finally {
      setCargando(false);
    }
  };

  const cambiarMes = (empleadoId, a, m, valor) => {
    const clave = `${empleadoId}-${a}-${m}`;
    const monto = Number(valor) || 0;
    const siguiente = { ...ajustes, [clave]: monto };
    setAjustes(siguiente);
    setPreview(prev => prev && {
      ...prev,
      empleados: prev.empleados.map(e => e.empleadoId !== empleadoId ? e : {
        ...e, meses: e.meses.map(x => (x.anio === a && x.mes === m ? { ...x, monto } : x)),
      }),
    });
    clearTimeout(cambiarMes.t);
    cambiarMes.t = setTimeout(() => previsualizar(siguiente), 600);
  };

  const crear = async () => {
    if (invalido) return;
    try {
      setCreando(true);
      const p = await api.post('/api/decimo', { periodoDesde: desde, periodoHasta: hasta, fechaPago: pago });
      const r = await api.post(`/api/decimo/${p.id}/calcular`, { ajustes: listaAjustes(ajustes) });
      toast.success(`Partida ${p.numero} creada · ${r.message}`, { duration: 5000 });
      setPreview(null); setAjustes({});
      onCreada?.(p, fueraDelMes ? { anio: Number(pago.slice(0, 4)), mes: mesDePago } : null);
    } catch (e) {
      toast.error(e.message || 'No se pudo crear la partida');
    } finally {
      setCreando(false);
    }
  };

  return (
    <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 text-sm text-gray-300 mb-3">
        <Plus className="w-4 h-4 text-gray-500" />
        Agregar partida de décimo a <span className="text-white font-medium">{NOMBRES_MES[mes - 1]} {anio}</span>
        <span className="text-gray-500">· {PARTIDAS[mesDePago] ?? 'el décimo se paga en abril, agosto o diciembre'}</span>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-[140px]">
          <label htmlFor="nd-desde" className="block text-xs text-gray-400 mb-1">Período desde</label>
          <DatePicker id="nd-desde" value={desde} onChange={setDesde} ariaLabel="Inicio del cuatrimestre" />
        </div>
        <div className="w-[140px]">
          <label htmlFor="nd-hasta" className="block text-xs text-gray-400 mb-1">Hasta</label>
          <DatePicker id="nd-hasta" value={hasta} min={desde} onChange={setHasta} ariaLabel="Fin del cuatrimestre" />
        </div>
        <div className="w-[140px]">
          <label htmlFor="nd-pago" className="block text-xs text-gray-400 mb-1">Fecha de pago</label>
          <DatePicker id="nd-pago" value={pago} onChange={setPago} ariaLabel="Fecha de pago del décimo" />
        </div>
        <button
          onClick={() => previsualizar()}
          disabled={invalido || cargando}
          className="h-[34px] flex items-center gap-2 px-4 bg-navy-800 hover:bg-navy-700 border border-navy-600 disabled:opacity-50 text-gray-100 rounded-lg text-sm font-medium"
        >
          {cargando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Eye className="w-4 h-4" />} Ver qué se pagaría
        </button>
        {preview && (
          <button
            onClick={crear}
            disabled={invalido || creando}
            className="h-[34px] flex items-center gap-2 px-4 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium"
          >
            {creando ? <Loader2 className="w-4 h-4 animate-spin" /> : (fueraDelMes ? <ArrowRight className="w-4 h-4" /> : <Plus className="w-4 h-4" />)}
            Crear partida
          </button>
        )}
      </div>

      {hasta && desde && hasta <= desde && <p className="text-xs text-red-300 mt-2">El fin del período debe ser posterior al inicio.</p>}
      {![4, 8, 12].includes(mesDePago) && <p className="text-xs text-amber-300 mt-2">El décimo se paga el 15 de abril, de agosto y de diciembre: con otra fecha el servidor no dejará crearla.</p>}
      {fueraDelMes && [4, 8, 12].includes(mesDePago) && (
        <p className="text-xs text-gray-400 mt-2">Se paga en {NOMBRES_MES[mesDePago - 1]}: la partida quedará en ese mes y la pantalla cambiará allí.</p>
      )}

      {preview && (
        <div className="mt-3 border border-navy-700 rounded-lg overflow-hidden">
          <div className={cargando ? 'opacity-50 transition-opacity' : 'transition-opacity'} aria-busy={cargando || undefined}>
            <TablaDecimo preview={preview} editable onMes={cambiarMes} />
          </div>
          <p className="px-4 py-2 text-xs text-gray-500 border-t border-navy-700">
            Nada de esto está guardado todavía. Los meses que escribas quedarán como devengado del empleado al crear la partida.
          </p>
        </div>
      )}
    </div>
  );
}
