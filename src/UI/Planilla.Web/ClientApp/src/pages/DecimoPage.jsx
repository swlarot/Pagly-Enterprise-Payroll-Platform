import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatCurrency } from '../utils/currency';
import { NOMBRES_MES } from '../utils/periodos';
import SelectorMes from '../components/planillas/SelectorMes';
import FilaDecimo from '../components/decimo/FilaDecimo';
import NuevaPartidaInline from '../components/decimo/NuevaPartidaInline';
import { Modal } from '../components/ui/Modal';

// ============================================================
// Décimo tercer mes, con la misma UI por mes que Planillas.
//
// Sitio donde va: manda la partida desplegada del mes. Arriba, solo el
// selector de mes y una línea de resumen; cada partida lleva sus acciones.
// Crear es una fila más al final, con la previsualización completa antes
// de crear nada.
//
// Estados: cargando el mes → esqueleto; una partida corriendo → esa fila
// marcada y las demás atenuadas; fallo → frase del producto en la fila.
// Borrar es permanente y pide escribir el número de la partida.
// ============================================================

const CLAVE_MES = 'pagly.decimo.mes';

function mesInicial() {
  try {
    const g = JSON.parse(localStorage.getItem(CLAVE_MES) || 'null');
    if (g?.anio && g?.mes) return g;
  } catch { /* sin memoria del mes: se usa la partida más cercana */ }
  const h = new Date();
  // El décimo se paga en abril, agosto y diciembre: se abre en la partida más próxima.
  const m = h.getMonth() + 1;
  const partida = m <= 4 ? 4 : m <= 8 ? 8 : 12;
  return { anio: h.getFullYear(), mes: partida };
}

export default function DecimoPage() {
  const { canWrite } = useAuth();
  const [{ anio, mes }, setPeriodo] = useState(mesInicial);
  const [partidas, setPartidas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [abierta, setAbierta] = useState(null);
  const [ocupada, setOcupada] = useState(null);
  const [errores, setErrores] = useState({});
  const [borrando, setBorrando] = useState(null);   // partida a borrar
  const [confirmacion, setConfirmacion] = useState('');

  const cambiarMes = (a, m) => {
    setPeriodo({ anio: a, mes: m });
    setAbierta(null);
    try { localStorage.setItem(CLAVE_MES, JSON.stringify({ anio: a, mes: m })); } catch { /* sin memoria */ }
  };

  const cargar = useCallback(async (silencioso = false) => {
    try {
      if (!silencioso) setCargando(true);
      const data = await api.get(`/api/decimo?ano=${anio}&mes=${mes}`);
      setPartidas(Array.isArray(data) ? data : []);
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar las partidas del mes');
    } finally {
      setCargando(false);
    }
  }, [anio, mes]);

  useEffect(() => { cargar(); }, [cargar]);

  const resumen = useMemo(() => ({
    total: partidas.length,
    decimo: partidas.reduce((s, p) => s + (p.totalDecimo || 0), 0),
    neto: partidas.reduce((s, p) => s + (p.totalNetoPago || 0), 0),
    borradores: partidas.filter(p => p.estado === 'Borrador').length,
    porPagar: partidas.filter(p => p.estado === 'Calculada').length,
  }), [partidas]);

  const correr = async (partida, tipo, ajustes) => {
    const id = partida.id;
    setOcupada(id);
    setErrores(prev => ({ ...prev, [id]: null }));
    try {
      if (tipo === 'calcular') {
        const lista = Object.entries(ajustes ?? {}).map(([k, monto]) => {
          const [empleadoId, a, m] = k.split('-').map(Number);
          return { empleadoId, anio: a, mes: m, monto: Number(monto) || 0 };
        });
        const r = await api.post(`/api/decimo/${id}/calcular`, { ajustes: lista });
        toast.success(`${partida.numero}: ${r.message}`);
        setAbierta(id);
      } else if (tipo === 'pagar') {
        await api.patch(`/api/decimo/${id}/pagar`);
        toast.success(`${partida.numero} marcada como pagada`);
      } else if (tipo === 'reabrir') {
        await api.patch(`/api/decimo/${id}/reabrir`);
        toast.success(`${partida.numero} volvió a Calculada`);
      }
      await cargar(true);
    } catch (e) {
      const frase = { calcular: 'No se pudo calcular', pagar: 'No se pudo marcar como pagada', reabrir: 'No se pudo reabrir' }[tipo];
      setErrores(prev => ({ ...prev, [id]: `${frase}: ${e.message}` }));
    } finally {
      setOcupada(null);
    }
  };

  const borrar = async () => {
    const p = borrando;
    setOcupada(p.id);
    try {
      const r = await api.delete(`/api/decimo/${p.id}`, { confirmacion });
      toast.success(r?.message || `La partida ${p.numero} se borró`);
      if (abierta === p.id) setAbierta(null);
      setBorrando(null); setConfirmacion('');
      await cargar(true);
    } catch (e) {
      toast.error(e.message || 'No se pudo borrar la partida');
    } finally {
      setOcupada(null);
    }
  };

  const titulo = `${NOMBRES_MES[mes - 1]} ${anio}`;
  const esMesDePartida = [4, 8, 12].includes(mes);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white">Décimo 13°</h1>
          <p className="text-gray-400 mt-1">Cada partida con sus meses, su cálculo y sus acciones</p>
        </div>
        <SelectorMes anio={anio} mes={mes} onChange={cambiarMes} />
      </div>

      {!cargando && (
        <p className="text-sm text-gray-400">
          {resumen.total === 0 ? (
            <>Sin partidas en {titulo}.{!esMesDePartida && ' El décimo se paga el 15 de abril, de agosto y de diciembre.'}</>
          ) : (
            <>
              <span className="text-gray-200">{resumen.total} {resumen.total === 1 ? 'partida' : 'partidas'}</span>
              {' · '}Décimo <span className="font-mono text-gray-200">{formatCurrency(resumen.decimo)}</span>
              {' · '}Neto <span className="font-mono text-emerald-400">{formatCurrency(resumen.neto)}</span>
              {resumen.porPagar > 0 && <> · <span className="text-blue-300">{resumen.porPagar} por pagar</span></>}
              {resumen.borradores > 0 && <> · <span className="text-gray-300">{resumen.borradores} en borrador</span></>}
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
          {partidas.map(p => (
            <FilaDecimo
              key={p.id}
              partida={p}
              abierta={abierta === p.id}
              ocupada={ocupada === p.id}
              atenuada={ocupada != null && ocupada !== p.id}
              error={errores[p.id]}
              onAbrir={() => setAbierta(prev => (prev === p.id ? null : p.id))}
              onAccion={(tipo, ajustes) => correr(p, tipo, ajustes)}
              onBorrar={() => { setBorrando(p); setConfirmacion(''); }}
            />
          ))}

          {canWrite() && (
            <NuevaPartidaInline
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

      <Modal isOpen={!!borrando} onClose={() => { setBorrando(null); setConfirmacion(''); }} title="Borrar la partida para siempre" size="md">
        {borrando && (
          <div className="space-y-4">
            <p className="text-sm text-gray-300">
              La partida <span className="font-mono text-white">{borrando.numero}</span> ({borrando.estado.toLowerCase()},
              {' '}{formatCurrency(borrando.totalDecimo)} de décimo para {borrando.numEmpleados} {borrando.numEmpleados === 1 ? 'empleado' : 'empleados'})
              se borrará por completo, con sus detalles. Esto no se puede deshacer; queda registrado en auditoría quién la borró.
            </p>
            <div>
              <label htmlFor="conf-borrar" className="block text-xs text-gray-400 mb-1">Escribe su número para confirmar</label>
              <input
                id="conf-borrar"
                value={confirmacion}
                onChange={e => setConfirmacion(e.target.value)}
                placeholder={borrando.numero}
                className="w-full bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-red-500"
              />
            </div>
            <div className="flex gap-3 pt-1">
              <button onClick={() => { setBorrando(null); setConfirmacion(''); }} className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2.5 rounded-lg font-medium">Cancelar</button>
              <button
                onClick={borrar}
                disabled={confirmacion.trim().toUpperCase() !== borrando.numero.toUpperCase() || ocupada === borrando.id}
                className="flex-1 bg-red-700 hover:bg-red-600 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2.5 rounded-lg font-semibold"
              >
                Borrar para siempre
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
