import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatCurrency } from '../utils/currency';
import { NOMBRES_MES } from '../utils/periodos';
import SelectorMes from '../components/planillas/SelectorMes';
import FilaPlanilla from '../components/planillas/FilaPlanilla';
import NuevaPlanillaInline from '../components/planillas/NuevaPlanillaInline';
import ConfirmModal from '../components/ConfirmModal';
import { Modal } from '../components/ui/Modal';

// ============================================================
// Planillas por mes.
//
// Sitio donde va: manda la planilla desplegada del mes. Arriba, solo el
// selector de mes y una línea de resumen. Cada planilla lleva sus acciones;
// no hay botones globales. Crear es una fila más al final, sin modal.
//
// Estados: cargando el mes → esqueleto; una planilla corriendo (calcular,
// aprobar, pagar, anular) → esa fila marcada y las demás atenuadas; fallo →
// frase del producto en la fila. La planilla recién creada queda abierta.
// ============================================================

const CLAVE_MES = 'pagly.planillas.mes';

function mesInicial() {
  try {
    const g = JSON.parse(localStorage.getItem(CLAVE_MES) || 'null');
    if (g?.anio && g?.mes) return g;
  } catch { /* sin memoria del mes: se usa el actual */ }
  const h = new Date();
  return { anio: h.getFullYear(), mes: h.getMonth() + 1 };
}

export default function PlanillasPage() {
  const { canWrite } = useAuth();
  const [{ anio, mes }, setPeriodo] = useState(mesInicial);
  const [planillas, setPlanillas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [abierta, setAbierta] = useState(null);      // id
  const [ocupada, setOcupada] = useState(null);      // id con acción en curso
  const [errores, setErrores] = useState({});        // id → mensaje
  const [confirmacion, setConfirmacion] = useState(null); // { tipo, planilla }
  const [motivoAnulacion, setMotivoAnulacion] = useState('');

  const cambiarMes = (a, m) => {
    setPeriodo({ anio: a, mes: m });
    setAbierta(null);
    try { localStorage.setItem(CLAVE_MES, JSON.stringify({ anio: a, mes: m })); } catch { /* sin memoria */ }
  };

  const cargar = useCallback(async (silencioso = false) => {
    try {
      if (!silencioso) setCargando(true);
      const data = await api.get(`/api/payrollheaders?anio=${anio}&mes=${mes}`);
      setPlanillas(Array.isArray(data) ? data : []);
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar las planillas del mes');
    } finally {
      setCargando(false);
    }
  }, [anio, mes]);

  useEffect(() => { cargar(); }, [cargar]);

  const resumen = useMemo(() => {
    const activas = planillas.filter(p => p.status !== 4);
    return {
      total: activas.length,
      bruto: activas.reduce((s, p) => s + (p.totalGrossPay || 0), 0),
      neto: activas.reduce((s, p) => s + (p.totalNetPay || 0), 0),
      pendientes: activas.filter(p => p.status === 1).length,
      borradores: activas.filter(p => p.status === 0).length,
    };
  }, [planillas]);

  const tipoPorDefecto = useMemo(() => {
    const ultima = [...planillas].sort((a, b) => (a.periodStartDate < b.periodStartDate ? 1 : -1))[0];
    return ultima?.payPeriodType ?? 2;
  }, [planillas]);

  // ── Acciones por planilla ──
  const ejecutar = async (planilla, tipo) => {
    if (tipo === 'aprobar' || tipo === 'anular' || tipo === 'eliminar') {
      setConfirmacion({ tipo, planilla });
      return;
    }
    await correr(planilla, tipo);
  };

  const correr = async (planilla, tipo) => {
    const id = planilla.id;
    setOcupada(id);
    setErrores(prev => ({ ...prev, [id]: null }));
    try {
      if (tipo === 'calcular') {
        // La configuración de impuestos se asegura sola; antes era un botón aparte.
        await api.post('/api/payrollheaders/ensure-tax-config').catch(() => {});
        const r = await api.post(`/api/payrollheaders/${id}/calculate`);
        toast.success(`Planilla ${planilla.payrollNumber} calculada: ${r.employeesProcessed ?? ''} empleados`);
        setAbierta(id);
      } else if (tipo === 'aprobar') {
        await api.post(`/api/payrollheaders/${id}/approve`);
        toast.success(`Planilla ${planilla.payrollNumber} aprobada`);
      } else if (tipo === 'pagar') {
        await api.post(`/api/payrollheaders/${id}/pay`);
        toast.success(`Planilla ${planilla.payrollNumber} marcada como pagada`);
      } else if (tipo === 'anular') {
        await api.post(`/api/payrollheaders/${id}/cancel`, { reason: motivoAnulacion || 'Anulada desde Planillas' });
        toast.success(`Planilla ${planilla.payrollNumber} anulada`);
      } else if (tipo === 'eliminar') {
        await api.delete(`/api/payrollheaders/${id}`);
        toast.success(`Borrador ${planilla.payrollNumber} eliminado`);
        if (abierta === id) setAbierta(null);
      }
      await cargar(true);
    } catch (e) {
      const frase = {
        calcular: 'No se pudo calcular',
        aprobar: 'No se pudo aprobar',
        pagar: 'No se pudo marcar como pagada',
        anular: 'No se pudo anular',
        eliminar: 'No se pudo eliminar',
      }[tipo];
      setErrores(prev => ({ ...prev, [id]: `${frase}: ${e.message}` }));
    } finally {
      setOcupada(null);
      setConfirmacion(null);
      setMotivoAnulacion('');
    }
  };

  const abrir = (id, forzar = false) => setAbierta(prev => (prev === id && !forzar ? null : id));

  const titulo = `${NOMBRES_MES[mes - 1]} ${anio}`;
  const conf = confirmacion;
  const empleadosDe = (p) => (p.details ?? []).filter(d => (d.grossPay || 0) > 0).length;

  return (
    <div className="space-y-5">
      {/* Cabecera */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white">Planillas</h1>
          <p className="text-gray-400 mt-1">Cada planilla con sus horas, su cálculo y sus acciones</p>
        </div>
        <SelectorMes anio={anio} mes={mes} onChange={cambiarMes} />
      </div>

      {/* Resumen del mes */}
      {!cargando && (
        <p className="text-sm text-gray-400">
          {resumen.total === 0 ? (
            <>Sin planillas en {titulo}.</>
          ) : (
            <>
              <span className="text-gray-200">{resumen.total} {resumen.total === 1 ? 'planilla' : 'planillas'}</span>
              {' · '}Bruto <span className="font-mono text-gray-200">{formatCurrency(resumen.bruto)}</span>
              {' · '}Neto <span className="font-mono text-emerald-400">{formatCurrency(resumen.neto)}</span>
              {resumen.pendientes > 0 && <> · <span className="text-blue-300">{resumen.pendientes} por aprobar</span></>}
              {resumen.borradores > 0 && <> · <span className="text-gray-300">{resumen.borradores} en borrador</span></>}
            </>
          )}
        </p>
      )}

      {/* Lista del mes */}
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
          {planillas.map(p => (
            <FilaPlanilla
              key={p.id}
              planilla={p}
              abierta={abierta === p.id}
              ocupada={ocupada === p.id}
              atenuada={ocupada != null && ocupada !== p.id}
              error={errores[p.id]}
              onAbrir={(forzar) => abrir(p.id, forzar === true)}
              onAccion={(tipo) => ejecutar(p, tipo)}
              onRecargar={() => cargar(true)}
            />
          ))}

          {canWrite() && (
            <NuevaPlanillaInline
              anio={anio}
              mes={mes}
              planillasDelMes={planillas}
              tipoPorDefecto={tipoPorDefecto}
              onCreada={async (nueva) => { await cargar(true); setAbierta(nueva.id); }}
            />
          )}
        </div>
      )}

      {/* Confirmaciones */}
      <ConfirmModal
        isOpen={conf?.tipo === 'eliminar'}
        onClose={() => setConfirmacion(null)}
        onConfirm={() => correr(conf.planilla, 'eliminar')}
        title="Eliminar borrador"
        message={conf ? `Se eliminará el borrador ${conf.planilla.payrollNumber} (${titulo}). Solo se pueden eliminar borradores; una planilla calculada se anula.` : ''}
        confirmText="Eliminar"
        variant="danger"
        isLoading={ocupada === conf?.planilla?.id}
      />

      <Modal isOpen={conf?.tipo === 'aprobar'} onClose={() => setConfirmacion(null)} title="Aprobar planilla" size="md">
        {conf && (
          <div className="space-y-4">
            <p className="text-sm text-gray-300">Al aprobar, los descuentos de préstamos, anticipos y acreedores se aplican y la planilla queda lista para pagar.</p>
            <div className="bg-navy-950/60 border border-navy-700 rounded-lg p-4">
              <p className="text-xs text-gray-500 mb-1">Total neto a pagar</p>
              <p className="text-2xl font-bold text-emerald-400 font-mono">{formatCurrency(conf.planilla.totalNetPay)}</p>
              <p className="text-xs text-gray-500 mt-1">Planilla {conf.planilla.payrollNumber} — {empleadosDe(conf.planilla)} {empleadosDe(conf.planilla) === 1 ? 'empleado' : 'empleados'}</p>
            </div>
            <div className="flex gap-3 pt-1">
              <button onClick={() => setConfirmacion(null)} className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2.5 rounded-lg font-medium">Cancelar</button>
              <button onClick={() => correr(conf.planilla, 'aprobar')} disabled={ocupada != null} className="flex-1 bg-primary-600 hover:bg-primary-700 disabled:opacity-60 text-white px-4 py-2.5 rounded-lg font-semibold">Confirmar aprobación</button>
            </div>
          </div>
        )}
      </Modal>

      <Modal isOpen={conf?.tipo === 'anular'} onClose={() => setConfirmacion(null)} title="Anular planilla" size="md">
        {conf && (
          <div className="space-y-4">
            <p className="text-sm text-gray-300">La planilla {conf.planilla.payrollNumber} quedará anulada: no cuenta para el mes, la renta ni el SIPE, pero se conserva para auditoría.</p>
            <div>
              <label htmlFor="motivo-anulacion" className="block text-xs text-gray-400 mb-1">Motivo</label>
              <input id="motivo-anulacion" value={motivoAnulacion} onChange={e => setMotivoAnulacion(e.target.value)} placeholder="Ej. fechas equivocadas" className="w-full bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-3 py-2 text-sm" />
            </div>
            <div className="flex gap-3 pt-1">
              <button onClick={() => setConfirmacion(null)} className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2.5 rounded-lg font-medium">Cancelar</button>
              <button onClick={() => correr(conf.planilla, 'anular')} disabled={ocupada != null} className="flex-1 bg-red-700 hover:bg-red-600 disabled:opacity-60 text-white px-4 py-2.5 rounded-lg font-semibold">Anular</button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
