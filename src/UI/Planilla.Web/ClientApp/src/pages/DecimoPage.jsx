import React, { useEffect, useState } from 'react';
import { Loader2, Plus, Calculator, CheckCircle, ChevronDown, ChevronRight, Calendar, DollarSign, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

// ============================================================
// Utilidades de formato
// ============================================================
const fmt = (n) => {
  const v = typeof n === 'number' ? n : parseFloat(n ?? 0);
  return isNaN(v) ? 'B/. 0.00' : `B/. ${v.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',')}`;
};

const fmtDate = (d) => {
  if (!d) return '—';
  const date = new Date(d);
  return date.toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' });
};

const MESES = ['', 'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

const estadoConfig = {
  Borrador:  { label: 'Borrador',  cls: 'bg-gray-700 text-gray-200' },
  Calculada: { label: 'Calculada', cls: 'bg-blue-900 text-blue-200' },
  Pagada:    { label: 'Pagada',    cls: 'bg-emerald-900 text-emerald-200' },
};

// ============================================================
// Componente principal
// ============================================================
export default function DecimoPage() {
  const [planillas, setPlanillas] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [anoFiltro, setAnoFiltro] = useState(new Date().getFullYear());

  // Modal crear
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ periodoDesde: '', periodoHasta: '', fechaPago: '' });
  const [saving, setSaving] = useState(false);

  // Vista de detalle
  const [viewPlanilla, setViewPlanilla] = useState(null);
  const [viewLoading, setViewLoading] = useState(false);
  const [expandedRows, setExpandedRows] = useState(new Set());
  const [calculando, setCalculando] = useState(false);

  useEffect(() => { load(); }, [anoFiltro]);

  const load = async () => {
    try {
      setIsLoading(true);
      const res = await api.get(`/api/decimo?ano=${anoFiltro}`);
      const data = res?.data;
      setPlanillas(Array.isArray(data) ? data : []);
    } catch (e) {
      toast.error(e.response?.data?.message || 'Error al cargar planillas de décimo');
      setPlanillas([]);
    } finally {
      setIsLoading(false);
    }
  };

  // ── CREAR ──
  const handleCreate = async (e) => {
    e.preventDefault();
    if (!form.periodoDesde || !form.periodoHasta || !form.fechaPago) {
      toast.error('Complete todos los campos');
      return;
    }
    try {
      setSaving(true);
      await api.post('/api/decimo', {
        periodoDesde: form.periodoDesde,
        periodoHasta: form.periodoHasta,
        fechaPago: form.fechaPago,
      });
      toast.success('Planilla de décimo creada');
      setShowCreate(false);
      setForm({ periodoDesde: '', periodoHasta: '', fechaPago: '' });
      load();
    } catch (e) {
      toast.error(e.response?.data?.message || 'Error al crear');
    } finally {
      setSaving(false);
    }
  };

  // ── VER DETALLE ──
  const handleVer = async (planilla) => {
    try {
      setViewLoading(true);
      setExpandedRows(new Set());
      const res = await api.get(`/api/decimo/${planilla.id}`);
      setViewPlanilla(res.data);
    } catch (e) {
      toast.error('Error al cargar detalle');
    } finally {
      setViewLoading(false);
    }
  };

  // ── CALCULAR ──
  const handleCalcular = async (planillaId) => {
    try {
      setCalculando(true);
      const res = await api.post(`/api/decimo/${planillaId}/calcular`);
      toast.success(res.data.message || 'Décimo calculado');
      // Refrescar el detalle abierto
      const updated = await api.get(`/api/decimo/${planillaId}`);
      setViewPlanilla(updated.data);
      load();
    } catch (e) {
      toast.error(e.response?.data?.message || 'Error al calcular');
    } finally {
      setCalculando(false);
    }
  };

  // ── PAGAR ──
  const handlePagar = async (planillaId) => {
    if (!window.confirm('¿Marcar esta planilla de décimo como pagada? Esta acción no se puede revertir.')) return;
    try {
      await api.patch(`/api/decimo/${planillaId}/pagar`);
      toast.success('Planilla marcada como pagada');
      const updated = await api.get(`/api/decimo/${planillaId}`);
      setViewPlanilla(updated.data);
      load();
    } catch (e) {
      toast.error(e.response?.data?.message || 'Error al marcar como pagada');
    }
  };

  const toggleRow = (id) => {
    setExpandedRows(prev => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  };

  if (isLoading) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-blue-500" />
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-white">Planilla de Décimo Tercer Mes</h1>
          <p className="text-gray-400 mt-1">Ley 29 de 1976 — Tres partidas anuales (abril, agosto, diciembre)</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors"
        >
          <Plus className="w-4 h-4" /> Nueva Planilla
        </button>
      </div>

      {/* Filtro año */}
      <div className="flex items-center gap-3">
        <label className="text-sm text-gray-400">Año:</label>
        <select
          value={anoFiltro}
          onChange={e => setAnoFiltro(Number(e.target.value))}
          className="bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-1.5 text-sm"
        >
          {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
        </select>
      </div>

      {/* Tabla de planillas */}
      <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-slate-700/50 text-gray-400 text-left">
              <th className="px-4 py-3 font-medium">Número</th>
              <th className="px-4 py-3 font-medium">Período</th>
              <th className="px-4 py-3 font-medium">Fecha de Pago</th>
              <th className="px-4 py-3 font-medium text-right">Total Décimo</th>
              <th className="px-4 py-3 font-medium text-right">Neto a Pagar</th>
              <th className="px-4 py-3 font-medium text-center">Empleados</th>
              <th className="px-4 py-3 font-medium text-center">Estado</th>
              <th className="px-4 py-3 font-medium text-center">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-700">
            {planillas.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-10 text-center text-gray-500">
                  No hay planillas de décimo para {anoFiltro}
                </td>
              </tr>
            )}
            {planillas.map(p => {
              const ec = estadoConfig[p.estado] || estadoConfig.Borrador;
              return (
                <tr key={p.id} className="hover:bg-slate-700/30 transition-colors text-gray-200">
                  <td className="px-4 py-3 font-mono text-blue-400">{p.numero}</td>
                  <td className="px-4 py-3">
                    <span className="text-xs text-gray-400">{fmtDate(p.periodoDesde)}</span>
                    <span className="mx-1 text-gray-600">→</span>
                    <span className="text-xs text-gray-400">{fmtDate(p.periodoHasta)}</span>
                  </td>
                  <td className="px-4 py-3">{fmtDate(p.fechaPago)}</td>
                  <td className="px-4 py-3 text-right font-mono">{fmt(p.totalDecimo)}</td>
                  <td className="px-4 py-3 text-right font-mono text-emerald-400">{fmt(p.totalNetoPago)}</td>
                  <td className="px-4 py-3 text-center">{p.numEmpleados}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${ec.cls}`}>{ec.label}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <button
                      onClick={() => handleVer(p)}
                      className="px-3 py-1 text-xs bg-slate-600 hover:bg-slate-500 text-white rounded-md transition-colors"
                    >
                      Ver
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* ══ MODAL CREAR ══ */}
      {showCreate && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
          <div className="bg-slate-800 border border-slate-700 rounded-xl w-full max-w-md">
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-700">
              <h2 className="text-lg font-semibold text-white flex items-center gap-2">
                <Calendar className="w-5 h-5 text-blue-400" /> Nueva Planilla de Décimo
              </h2>
              <button onClick={() => setShowCreate(false)} className="text-gray-400 hover:text-white text-xl">✕</button>
            </div>
            <form onSubmit={handleCreate} className="p-6 space-y-4">
              <p className="text-xs text-gray-400">
                Las tres partidas fijas son: <strong className="text-gray-300">15 abril</strong> (dic16–abr15),
                <strong className="text-gray-300"> 15 agosto</strong> (abr16–ago15),
                <strong className="text-gray-300"> 15 diciembre</strong> (ago16–dic15).
              </p>

              <div>
                <label className="block text-sm text-gray-400 mb-1">Período desde</label>
                <input
                  type="date" value={form.periodoDesde}
                  onChange={e => setForm(f => ({ ...f, periodoDesde: e.target.value }))}
                  className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-2 text-sm"
                />
              </div>
              <div>
                <label className="block text-sm text-gray-400 mb-1">Período hasta</label>
                <input
                  type="date" value={form.periodoHasta}
                  onChange={e => setForm(f => ({ ...f, periodoHasta: e.target.value }))}
                  className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-2 text-sm"
                />
              </div>
              <div>
                <label className="block text-sm text-gray-400 mb-1">Fecha de pago</label>
                <input
                  type="date" value={form.fechaPago}
                  onChange={e => setForm(f => ({ ...f, fechaPago: e.target.value }))}
                  className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-2 text-sm"
                />
              </div>

              <div className="flex gap-3 pt-2">
                <button
                  type="button" onClick={() => setShowCreate(false)}
                  className="flex-1 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-sm transition-colors"
                >
                  Cancelar
                </button>
                <button
                  type="submit" disabled={saving}
                  className="flex-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors disabled:opacity-50"
                >
                  {saving ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : 'Crear'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ══ MODAL VER / DETALLE ══ */}
      {viewPlanilla && (
        <div className="fixed inset-0 bg-black/70 flex items-start justify-center z-50 p-4 overflow-y-auto">
          <div className="bg-slate-800 border border-slate-700 rounded-xl w-full max-w-6xl my-6">
            {/* Header del modal */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-700">
              <div>
                <h2 className="text-lg font-semibold text-white flex items-center gap-2">
                  <DollarSign className="w-5 h-5 text-amber-400" />
                  Décimo {viewPlanilla.numero}
                </h2>
                <p className="text-xs text-gray-400 mt-0.5">
                  {fmtDate(viewPlanilla.periodoDesde)} → {fmtDate(viewPlanilla.periodoHasta)} | Pago: {fmtDate(viewPlanilla.fechaPago)}
                </p>
              </div>
              <div className="flex items-center gap-3">
                {viewPlanilla.estado !== 'Pagada' && (
                  <button
                    onClick={() => handleCalcular(viewPlanilla.id)}
                    disabled={calculando}
                    className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors disabled:opacity-50"
                  >
                    {calculando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Calculator className="w-4 h-4" />}
                    {viewPlanilla.estado === 'Borrador' ? 'Calcular' : 'Recalcular'}
                  </button>
                )}
                {viewPlanilla.estado === 'Calculada' && (
                  <button
                    onClick={() => handlePagar(viewPlanilla.id)}
                    className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-medium transition-colors"
                  >
                    <CheckCircle className="w-4 h-4" /> Marcar Pagada
                  </button>
                )}
                <button onClick={() => setViewPlanilla(null)} className="text-gray-400 hover:text-white text-xl">✕</button>
              </div>
            </div>

            {viewLoading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="w-8 h-8 animate-spin text-blue-500" />
              </div>
            ) : (
              <>
                {/* Cards de totales */}
                <div className="grid grid-cols-2 md:grid-cols-5 gap-3 p-6 border-b border-slate-700">
                  {[
                    { label: 'Total Devengado', val: viewPlanilla.totalDevengado, cls: 'text-white' },
                    { label: 'Total Décimo', val: viewPlanilla.totalDecimo, cls: 'text-amber-400' },
                    { label: 'CSS Empleado (7.25%)', val: viewPlanilla.totalCssEmpleado, cls: 'text-orange-400' },
                    { label: 'ISR Total', val: viewPlanilla.totalISR, cls: 'text-red-400' },
                    { label: 'Neto a Pagar', val: viewPlanilla.totalNetoPago, cls: 'text-emerald-400' },
                  ].map(c => (
                    <div key={c.label} className="bg-slate-700/50 rounded-lg px-4 py-3">
                      <p className="text-xs text-gray-400 mb-1">{c.label}</p>
                      <p className={`text-base font-bold font-mono ${c.cls}`}>{fmt(c.val)}</p>
                    </div>
                  ))}
                </div>

                {/* Leyenda CSS especial */}
                <div className="px-6 py-2 bg-amber-900/20 border-b border-slate-700">
                  <p className="text-xs text-amber-300">
                    ⚠️ Tasas CSS especiales décimo: empleado <strong>7.25%</strong> | patrono <strong>10.75%</strong> — distintas a la planilla regular.
                  </p>
                </div>

                {/* Tabla de empleados */}
                {viewPlanilla.estado === 'Borrador' ? (
                  <div className="flex items-center justify-center py-16 text-gray-400">
                    <div className="text-center">
                      <Calculator className="w-12 h-12 mx-auto mb-3 text-gray-600" />
                      <p>Esta planilla aún no ha sido calculada.</p>
                      <p className="text-sm mt-1">Presiona <strong>Calcular</strong> para procesar todos los empleados activos.</p>
                    </div>
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="bg-slate-700/50 text-gray-400 text-left text-xs">
                          <th className="px-3 py-3 w-8"></th>
                          <th className="px-3 py-3">Empleado</th>
                          <th className="px-3 py-3 text-right">Total Devengado</th>
                          <th className="px-3 py-3 text-right">Monto Décimo</th>
                          <th className="px-3 py-3 text-right">CSS 7.25%</th>
                          <th className="px-3 py-3 text-right">SE 1.25%</th>
                          <th className="px-3 py-3 text-right">ISR</th>
                          <th className="px-3 py-3 text-right">Total Ded.</th>
                          <th className="px-3 py-3 text-right">Neto</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-700">
                        {(viewPlanilla.detalles ?? []).map(det => {
                          const isExpanded = expandedRows.has(det.id);
                          return (
                            <React.Fragment key={det.id}>
                              {/* Fila principal */}
                              <tr
                                className="hover:bg-slate-700/30 cursor-pointer text-gray-200"
                                onClick={() => toggleRow(det.id)}
                              >
                                <td className="px-3 py-3 text-gray-500">
                                  {isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                                </td>
                                <td className="px-3 py-3">
                                  <div className="font-medium">{det.nombreCompleto}</div>
                                  <div className="text-xs text-gray-500">{det.numeroIdentificacion}</div>
                                </td>
                                <td className="px-3 py-3 text-right font-mono">{fmt(det.totalDevengado)}</td>
                                <td className="px-3 py-3 text-right font-mono font-semibold text-amber-400">{fmt(det.montoDecimo)}</td>
                                <td className="px-3 py-3 text-right font-mono text-orange-400">{fmt(det.cssEmpleado)}</td>
                                <td className="px-3 py-3 text-right font-mono text-orange-300">{fmt(det.seEmpleado)}</td>
                                <td className="px-3 py-3 text-right font-mono text-red-400">{fmt(det.isr)}</td>
                                <td className="px-3 py-3 text-right font-mono text-red-300">{fmt(det.totalDeducciones)}</td>
                                <td className="px-3 py-3 text-right font-mono font-semibold text-emerald-400">{fmt(det.netoPago)}</td>
                              </tr>

                              {/* Fila expandida: desglose mensual + patrono */}
                              {isExpanded && (
                                <tr className="bg-slate-900/50">
                                  <td></td>
                                  <td colSpan={8} className="px-4 py-4">
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                      {/* Desglose mensual */}
                                      <div>
                                        <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">
                                          Desglose mensual de devengado
                                        </h4>
                                        <div className="space-y-1">
                                          {(!det.desgloseMensual || det.desgloseMensual.length === 0) && (
                                            <p className="text-xs text-gray-500 italic">Sin planillas en el período</p>
                                          )}
                                          {(det.desgloseMensual ?? []).map((m, i) => (
                                            <div key={i} className="flex justify-between text-xs">
                                              <span className="text-gray-400">{MESES[m.mes]} {m.ano}</span>
                                              <span className="font-mono text-gray-200">{fmt(m.bruto)}</span>
                                            </div>
                                          ))}
                                          <div className="flex justify-between text-xs border-t border-slate-600 pt-1 mt-1">
                                            <span className="font-semibold text-gray-300">Total Devengado</span>
                                            <span className="font-mono font-bold text-white">{fmt(det.totalDevengado)}</span>
                                          </div>
                                          <div className="flex justify-between text-xs">
                                            <span className="font-semibold text-gray-300">÷ 12 = Monto Décimo</span>
                                            <span className="font-mono font-bold text-amber-400">{fmt(det.montoDecimo)}</span>
                                          </div>
                                        </div>
                                      </div>

                                      {/* Carga patronal */}
                                      <div>
                                        <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">
                                          Carga patronal
                                        </h4>
                                        <div className="space-y-1">
                                          {[
                                            { label: 'CSS Patrono (10.75%)', val: det.cssPatrono, cls: 'text-orange-300' },
                                            { label: 'SE Patrono (1.50%)', val: det.sePatrono, cls: 'text-orange-200' },
                                          ].map(r => (
                                            <div key={r.label} className="flex justify-between text-xs">
                                              <span className="text-gray-400">{r.label}</span>
                                              <span className={`font-mono ${r.cls}`}>{fmt(r.val)}</span>
                                            </div>
                                          ))}
                                          <div className="flex justify-between text-xs border-t border-slate-600 pt-1 mt-1">
                                            <span className="font-semibold text-gray-300">Total carga patronal</span>
                                            <span className="font-mono font-bold text-orange-300">
                                              {fmt(det.cssPatrono + det.sePatrono)}
                                            </span>
                                          </div>
                                        </div>

                                        <h4 className="text-xs font-semibold text-gray-400 uppercase tracking-wide mt-4 mb-2">
                                          Deducciones empleado
                                        </h4>
                                        <div className="space-y-1">
                                          {[
                                            { label: 'CSS Empleado (7.25%)', val: det.cssEmpleado },
                                            { label: 'SE Empleado (1.25%)', val: det.seEmpleado },
                                            { label: 'ISR', val: det.isr },
                                          ].map(r => (
                                            <div key={r.label} className="flex justify-between text-xs">
                                              <span className="text-gray-400">{r.label}</span>
                                              <span className="font-mono text-red-400">{fmt(r.val)}</span>
                                            </div>
                                          ))}
                                          <div className="flex justify-between text-xs border-t border-slate-600 pt-1 mt-1">
                                            <span className="font-semibold text-gray-300">Neto a pagar</span>
                                            <span className="font-mono font-bold text-emerald-400">{fmt(det.netoPago)}</span>
                                          </div>
                                        </div>
                                      </div>
                                    </div>
                                  </td>
                                </tr>
                              )}
                            </React.Fragment>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
