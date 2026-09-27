import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatCurrency } from '../utils/currency';
import { NOMBRES_MES } from '../utils/periodos';
import SelectorMes from '../components/planillas/SelectorMes';
import FilaLiquidacion from '../components/liquidaciones/FilaLiquidacion';
import NuevaLiquidacionInline from '../components/liquidaciones/NuevaLiquidacionInline';
import ConfirmModal from '../components/ConfirmModal';
import { Modal } from '../components/ui/Modal';

// ============================================================
// Liquidaciones, con la misma UI por mes que Planillas y Décimo.
//
// Sitio donde va: manda la liquidación desplegada del mes. Arriba, solo el
// selector de mes y una línea de resumen; cada liquidación lleva sus
// acciones. Crear es una fila más al final, con las cinco pestañas de la
// hoja del contador antes de guardar nada.
//
// El mes de una liquidación es el de su ÚLTIMO DÍA de trabajo, que es el
// que se declara al SIPE.
// ============================================================

const CLAVE_MES = 'pagly.liquidaciones.mes';

function mesInicial() {
  try {
    const g = JSON.parse(localStorage.getItem(CLAVE_MES) || 'null');
    if (g?.anio && g?.mes) return g;
  } catch { /* sin memoria del mes: se usa el actual */ }
  const h = new Date();
  return { anio: h.getFullYear(), mes: h.getMonth() + 1 };
}

export default function LiquidacionesPage() {
  const { canWrite } = useAuth();
  const [{ anio, mes }, setPeriodo] = useState(mesInicial);
  const [liquidaciones, setLiquidaciones] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [abierta, setAbierta] = useState(null);
  const [ocupada, setOcupada] = useState(null);
  const [errores, setErrores] = useState({});
  const [confirmacion, setConfirmacion] = useState(null); // { tipo, liquidacion }
  const [motivo, setMotivo] = useState('');

  const cambiarMes = (a, m) => {
    setPeriodo({ anio: a, mes: m });
    setAbierta(null);
    try { localStorage.setItem(CLAVE_MES, JSON.stringify({ anio: a, mes: m })); } catch { /* sin memoria */ }
  };

  const cargar = useCallback(async (silencioso = false) => {
    try {
      if (!silencioso) setCargando(true);
      const data = await api.get(`/api/liquidaciones?anio=${anio}&mes=${mes}`);
      setLiquidaciones(Array.isArray(data) ? data : []);
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar las liquidaciones del mes');
    } finally {
      setCargando(false);
    }
  }, [anio, mes]);

  useEffect(() => { cargar(); }, [cargar]);

  const resumen = useMemo(() => {
    const activas = liquidaciones.filter(l => l.estado !== 4);
    return {
      total: activas.length,
      bruto: activas.reduce((s, l) => s + (l.totalBruto || 0), 0),
      neto: activas.reduce((s, l) => s + (l.totalNeto || 0), 0),
      porAprobar: activas.filter(l => l.estado === 1).length,
      porPagar: activas.filter(l => l.estado === 2).length,
    };
  }, [liquidaciones]);

  const ejecutar = (liquidacion, tipo) => {
    if (tipo === 'eliminar' || tipo === 'anular') {
      setConfirmacion({ tipo, liquidacion });
      setMotivo('');
      return;
    }
    correr(liquidacion, tipo);
  };

  const correr = async (liquidacion, tipo) => {
    const id = liquidacion.id;
    setOcupada(id);
    setErrores(prev => ({ ...prev, [id]: null }));
    try {
      if (tipo === 'aprobar') {
        await api.post(`/api/liquidaciones/${id}/aprobar`);
        toast.success(`${liquidacion.numero} aprobada`);
      } else if (tipo === 'pagar') {
        await api.post(`/api/liquidaciones/${id}/pagar`);
        toast.success(`${liquidacion.numero} marcada como pagada`);
      } else if (tipo === 'anular') {
        await api.post(`/api/liquidaciones/${id}/anular`, { motivo: motivo || null });
        toast.success(`${liquidacion.numero} anulada`);
      } else if (tipo === 'eliminar') {
        await api.delete(`/api/liquidaciones/${id}`);
        toast.success(`${liquidacion.numero} eliminada`);
        if (abierta === id) setAbierta(null);
      }
      await cargar(true);
    } catch (e) {
      const frase = {
        aprobar: 'No se pudo aprobar',
        pagar: 'No se pudo marcar como pagada',
        anular: 'No se pudo anular',
        eliminar: 'No se pudo eliminar',
      }[tipo];
      setErrores(prev => ({ ...prev, [id]: `${frase}: ${e.message}` }));
    } finally {
      setOcupada(null);
      setConfirmacion(null);
      setMotivo('');
    }
  };

  const titulo = `${NOMBRES_MES[mes - 1]} ${anio}`;
  const conf = confirmacion;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white">Liquidaciones</h1>
          <p className="text-gray-400 mt-1">Cada liquidación con su cálculo, sus meses devengados y sus acciones</p>
        </div>
        <SelectorMes anio={anio} mes={mes} onChange={cambiarMes} />
      </div>

      {!cargando && (
        <p className="text-sm text-gray-400">
          {resumen.total === 0 ? (
            <>Nadie salió en {titulo}.</>
          ) : (
            <>
              <span className="text-gray-200">{resumen.total} {resumen.total === 1 ? 'liquidación' : 'liquidaciones'}</span>
              {' · '}Bruto <span className="font-mono text-gray-200">{formatCurrency(resumen.bruto)}</span>
              {' · '}Neto <span className="font-mono text-emerald-400">{formatCurrency(resumen.neto)}</span>
              {resumen.porAprobar > 0 && <> · <span className="text-blue-300">{resumen.porAprobar} por aprobar</span></>}
              {resumen.porPagar > 0 && <> · <span className="text-primary-300">{resumen.porPagar} por pagar</span></>}
            </>
          )}
        </p>
      )}

      {cargando ? (
        <div className="space-y-3" aria-busy="true">
          {[0, 1].map(i => (
            <div key={i} className="h-20 rounded-xl bg-navy-900 border border-navy-700 overflow-hidden">
              <div className="h-1 bg-primary-500/60 animate-pulse w-1/3" />
            </div>
          ))}
        </div>
      ) : (
        <div className="space-y-3">
          {liquidaciones.map(l => (
            <FilaLiquidacion
              key={l.id}
              liquidacion={l}
              abierta={abierta === l.id}
              ocupada={ocupada === l.id}
              atenuada={ocupada != null && ocupada !== l.id}
              error={errores[l.id]}
              onAbrir={() => setAbierta(prev => (prev === l.id ? null : l.id))}
              onAccion={(tipo) => ejecutar(l, tipo)}
            />
          ))}

          {canWrite() && (
            <NuevaLiquidacionInline
              anio={anio}
              mes={mes}
              onCreada={async (nueva, mesDestino) => {
                if (mesDestino) cambiarMes(mesDestino.anio, mesDestino.mes);
                else await cargar(true);
                setAbierta(nueva.id);
              }}
            />
          )}
        </div>
      )}

      <ConfirmModal
        isOpen={conf?.tipo === 'eliminar'}
        onClose={() => setConfirmacion(null)}
        onConfirm={() => correr(conf.liquidacion, 'eliminar')}
        title="Eliminar liquidación"
        message={conf ? `Se eliminará ${conf.liquidacion.numero} (${conf.liquidacion.empleadoNombre}). Solo se pueden eliminar las que siguen calculadas; una aprobada o pagada se anula.` : ''}
        confirmText="Eliminar"
        variant="danger"
        isLoading={ocupada === conf?.liquidacion?.id}
      />

      <Modal isOpen={conf?.tipo === 'anular'} onClose={() => setConfirmacion(null)} title="Anular liquidación" size="md">
        {conf && (
          <div className="space-y-4">
            <p className="text-sm text-gray-300">
              {conf.liquidacion.numero} ({conf.liquidacion.empleadoNombre}) quedará anulada: no cuenta para el mes ni
              para el SIPE, pero se conserva para auditoría y el empleado podrá liquidarse de nuevo.
            </p>
            <div>
              <label htmlFor="motivo-anulacion-liq" className="block text-xs text-gray-400 mb-1">Motivo</label>
              <input
                id="motivo-anulacion-liq"
                value={motivo}
                onChange={e => setMotivo(e.target.value)}
                placeholder="Ej. fecha de salida equivocada"
                className="w-full bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-3 py-2 text-sm"
              />
            </div>
            <div className="flex gap-3 pt-1">
              <button onClick={() => setConfirmacion(null)} className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2.5 rounded-lg font-medium">Cancelar</button>
              <button onClick={() => correr(conf.liquidacion, 'anular')} disabled={ocupada != null} className="flex-1 bg-red-700 hover:bg-red-600 disabled:opacity-60 text-white px-4 py-2.5 rounded-lg font-semibold">Anular</button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
