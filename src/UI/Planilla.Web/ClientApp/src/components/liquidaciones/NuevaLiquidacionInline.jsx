import React, { useEffect, useState } from 'react';
import { Plus, Loader2, Eye, ArrowRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { NOMBRES_MES, formatear, diasDelMes } from '../../utils/periodos';
import DatePicker from '../ui/DatePicker';
import DetalleLiquidacion from './DetalleLiquidacion';

// ============================================================
// Crear una liquidación en el mes, sin modal: empleado, fecha de salida,
// causa y los días pendientes. «Ver qué se paga» enseña las cinco
// pestañas con el devengado real ANTES de crear nada.
// ============================================================

const TIPOS = [
  [0, 'Despido injustificado'],
  [1, 'Renuncia'],
  [2, 'Mutuo acuerdo'],
  [3, 'Despido justificado'],
  [4, 'Jubilación'],
];

export default function NuevaLiquidacionInline({ anio, mes, onCreada }) {
  const [empleados, setEmpleados] = useState([]);
  const [empleadoId, setEmpleadoId] = useState('');
  const [fecha, setFecha] = useState(() => formatear(anio, mes, diasDelMes(anio, mes)));
  const [tipo, setTipo] = useState(2);
  const [diasSalario, setDiasSalario] = useState('');
  const [preaviso, setPreaviso] = useState(false);
  const [preview, setPreview] = useState(null);
  const [cargando, setCargando] = useState(false);
  const [creando, setCreando] = useState(false);

  useEffect(() => { setFecha(formatear(anio, mes, diasDelMes(anio, mes))); setPreview(null); }, [anio, mes]);

  useEffect(() => {
    (async () => {
      try {
        const data = await api.get('/api/empleados');
        const lista = Array.isArray(data) ? data : (data?.items ?? []);
        // Se liquida a quien sigue activo: el que ya salió tiene su liquidación hecha.
        setEmpleados(lista.filter(e => e.estaActivo !== false));
      } catch {
        // Sin lista de empleados la fila queda deshabilitada; no hace falta gritar.
      }
    })();
  }, []);

  const cuerpo = () => ({
    empleadoId: Number(empleadoId),
    fechaTerminacion: fecha,
    tipoTerminacion: Number(tipo),
    incluyePreaviso: preaviso,
    diasSalarioPendiente: diasSalario === '' ? null : Number(diasSalario),
  });

  const invalido = !empleadoId || !fecha;
  const mesDestino = fecha && (Number(fecha.slice(0, 4)) !== anio || Number(fecha.slice(5, 7)) !== mes)
    ? { anio: Number(fecha.slice(0, 4)), mes: Number(fecha.slice(5, 7)) }
    : null;

  const previsualizar = async () => {
    if (invalido) return;
    try {
      setCargando(true);
      setPreview(await api.post('/api/liquidaciones/previsualizar', cuerpo()));
    } catch (e) {
      toast.error(e.message || 'No se pudo calcular la liquidación');
    } finally {
      setCargando(false);
    }
  };

  const crear = async () => {
    if (invalido) return;
    try {
      setCreando(true);
      const r = await api.post('/api/liquidaciones', cuerpo());
      toast.success(`Liquidación ${r.numero} creada`);
      setPreview(null); setEmpleadoId(''); setDiasSalario('');
      onCreada?.(r, mesDestino);
    } catch (e) {
      toast.error(e.message || 'No se pudo crear la liquidación');
    } finally {
      setCreando(false);
    }
  };

  const controlCls = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 h-[34px]';

  return (
    <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 text-sm text-gray-300 mb-3">
        <Plus className="w-4 h-4 text-gray-500" />
        Liquidar a un empleado en <span className="text-white font-medium">{NOMBRES_MES[mes - 1]} {anio}</span>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-[240px]">
          <label htmlFor="nl-empleado" className="block text-xs text-gray-400 mb-1">Empleado</label>
          <select id="nl-empleado" value={empleadoId} onChange={e => { setEmpleadoId(e.target.value); setPreview(null); }} className={controlCls + ' w-full'}>
            <option value="">Elige un empleado</option>
            {empleados.map(e => (
              <option key={e.id} value={e.id}>{e.nombre} {e.apellido} — {e.numeroIdentificacion}</option>
            ))}
          </select>
        </div>
        <div className="w-[150px]">
          <label htmlFor="nl-fecha" className="block text-xs text-gray-400 mb-1">Último día</label>
          <DatePicker id="nl-fecha" value={fecha} onChange={v => { setFecha(v); setPreview(null); }} ariaLabel="Fecha de terminación" />
        </div>
        <div className="w-[190px]">
          <label htmlFor="nl-tipo" className="block text-xs text-gray-400 mb-1">Causa</label>
          <select id="nl-tipo" value={tipo} onChange={e => { setTipo(Number(e.target.value)); setPreview(null); }} className={controlCls + ' w-full'}>
            {TIPOS.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
          </select>
        </div>
        <div className="w-[130px]">
          <label htmlFor="nl-dias" className="block text-xs text-gray-400 mb-1">Días por pagar</label>
          <input
            id="nl-dias" type="number" min="0" step="0.5" value={diasSalario}
            onChange={e => { setDiasSalario(e.target.value); setPreview(null); }}
            placeholder="0"
            className={controlCls + ' w-full font-mono'}
          />
        </div>
        <label className="flex items-center gap-2 h-[34px] text-xs text-gray-300">
          <input type="checkbox" checked={preaviso} onChange={e => { setPreaviso(e.target.checked); setPreview(null); }} className="accent-primary-600" />
          Preaviso otorgado
        </label>
        <button
          onClick={previsualizar}
          disabled={invalido || cargando}
          className="h-[34px] flex items-center gap-2 px-4 bg-navy-800 hover:bg-navy-700 border border-navy-600 disabled:opacity-50 text-gray-100 rounded-lg text-sm font-medium"
        >
          {cargando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Eye className="w-4 h-4" />} Ver qué se paga
        </button>
        {preview && (
          <button
            onClick={crear}
            disabled={invalido || creando}
            className="h-[34px] flex items-center gap-2 px-4 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium"
          >
            {creando ? <Loader2 className="w-4 h-4 animate-spin" /> : (mesDestino ? <ArrowRight className="w-4 h-4" /> : <Plus className="w-4 h-4" />)}
            Crear liquidación
          </button>
        )}
      </div>

      {mesDestino && (
        <p className="text-xs text-gray-400 mt-2">
          El último día cae en {NOMBRES_MES[mesDestino.mes - 1]}{mesDestino.anio !== anio ? ` ${mesDestino.anio}` : ''}:
          la liquidación quedará en ese mes y la pantalla cambiará allí.
        </p>
      )}

      {preview && (
        <div className="mt-3 border border-navy-700 rounded-lg overflow-hidden">
          <DetalleLiquidacion
            calculo={preview.calculo}
            bases={preview.bases}
            usaDevengadoReal={preview.usaDevengadoReal}
            empleado={preview.empleado}
          />
          <p className="px-4 py-2 text-xs text-gray-500 border-t border-navy-700">
            Nada de esto está guardado todavía.
          </p>
        </div>
      )}
    </div>
  );
}
