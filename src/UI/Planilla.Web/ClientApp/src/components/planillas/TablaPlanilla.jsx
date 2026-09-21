import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatCurrency } from '../../utils/currency';

// ============================================================
// La planilla tal como se ve: una fila por empleado con bruto, deducciones y
// neto, y al hacer clic el desglose completo. Es la tabla que antes vivía
// en el modal de "Ver detalles", extraída tal cual, más el bloque RENTA que
// enseña de dónde sale el ISR con las columnas de la ficha anual.
// ============================================================

const fmt = (n) => formatCurrency(n);
const fmt3 = (n) => Number(n ?? 0).toFixed(3);

function BloqueRenta({ isr, empleadoId, anio }) {
  if (!isr) return null;
  const cuadra = Math.abs((isr.aDescontar + isr.isrGastoRepresentacion) - isr.isrPeriodo) < 0.01;
  const Fila = ({ label, valor, fuerte, color }) => (
    <div className="flex justify-between gap-3">
      <span className="text-gray-400">{label}</span>
      <span className={`font-mono ${fuerte ? 'font-semibold' : ''} ${color ?? 'text-gray-200'}`}>{valor}</span>
    </div>
  );
  return (
    <div className="bg-navy-800/60 rounded-lg p-3">
      <div className="flex items-baseline justify-between mb-1.5">
        <p className="font-semibold text-gray-300">Renta — método acumulativo</p>
        <Link to={`/ficha-isr?empleado=${empleadoId}&anio=${anio}`} className="text-[11px] text-sky-300 hover:underline">Ver ficha del año</Link>
      </div>
      <div className="space-y-0.5">
        <Fila label={`Corrida ${isr.numeroPeriodo} · períodos`} valor={fmt3(isr.periodoEquivalente) + ` de ${Number(isr.periodosDePago).toFixed(0)}`} />
        <Fila label="Gravable del período" valor={fmt(isr.ingresoGravablePeriodo)} />
        <Fila label="Acumulado a la fecha" valor={fmt(isr.acumulado)} />
        <Fila label="Ingreso gravable proyectado" valor={fmt(isr.ingresoAnualProyectado)} fuerte />
        <Fila label="Renta anual" valor={fmt(isr.rentaAnual)} />
        <Fila label="Renta por período" valor={fmt(isr.rentaPorPeriodo)} />
        <Fila label="Impuesto causado a la fecha" valor={fmt(isr.impuestoCausado)} />
        <Fila label="Retenido antes de esta planilla" valor={fmt(isr.retenidoAntes)} />
        <div className="flex justify-between gap-3 font-semibold border-t border-navy-700 pt-1 mt-1">
          <span className="text-gray-300">A descontar</span>
          <span className="font-mono text-red-400">{fmt(isr.aDescontar)}</span>
        </div>
        {isr.gastoRepresentacion > 0 && (
          <Fila label="Gasto de representación (tarifa propia)" valor={fmt(isr.isrGastoRepresentacion)} color="text-purple-300" />
        )}
        {!cuadra && (
          <p className="text-[11px] text-amber-400 mt-1">
            Lo guardado en la planilla ({fmt(isr.isrPeriodo)}) no coincide con el cálculo actual: recalcula la planilla.
          </p>
        )}
        {(isr.tieneSaldoInicial || isr.tieneMesesImportados) && (
          <p className="text-[11px] text-sky-300 mt-1">
            Incluye {isr.tieneMesesImportados ? 'meses importados' : ''}{isr.tieneMesesImportados && isr.tieneSaldoInicial ? ' y ' : ''}{isr.tieneSaldoInicial ? 'saldo inicial' : ''} del año.
          </p>
        )}
      </div>
    </div>
  );
}

export default function TablaPlanilla({ planilla }) {
  const [expandidos, setExpandidos] = useState(new Set());
  const [cargando, setCargando] = useState(null);
  const [desgloses, setDesgloses] = useState({});

  const det = planilla?.details ?? [];
  if (det.length === 0) {
    return (
      <div className="px-4 py-10 text-center text-gray-400 text-sm">
        Esta planilla todavía no tiene empleados calculados. Carga las horas y pulsa Calcular.
      </div>
    );
  }

  const anio = new Date(planilla.periodEndDate).getUTCFullYear();
  const hasPension = det.some(d => (d.pensionAlimenticia || 0) > 0);
  const hasEmbargos = det.some(d => (d.embargos || 0) > 0);
  const hasFijas = det.some(d => (d.deduccionesFijas || 0) > 0);
  const hasPrestamos = det.some(d => (d.prestamos || 0) > 0);
  const hasAnticipos = det.some(d => (d.anticipos || 0) > 0);
  const colSpan = 8 + [hasPension, hasEmbargos, hasFijas, hasPrestamos, hasAnticipos].filter(Boolean).length;
  const suma = (k) => det.reduce((s, d) => s + (d[k] || 0), 0);

  const alternar = async (detailId) => {
    const n = new Set(expandidos);
    if (n.has(detailId)) { n.delete(detailId); setExpandidos(n); return; }
    n.add(detailId); setExpandidos(n);
    if (desgloses[detailId]) return;
    try {
      setCargando(detailId);
      const bd = await api.get(`/api/payrollheaders/${planilla.id}/details/${detailId}/breakdown`);
      setDesgloses(prev => ({ ...prev, [detailId]: bd }));
    } catch (e) {
      toast.error(e.message || 'No se pudo cargar el desglose');
    } finally {
      setCargando(null);
    }
  };

  const th = (txt, extra = 'text-gray-500') => <th className={`text-right py-2.5 px-3 text-[11px] font-medium ${extra} uppercase`}>{txt}</th>;

  return (
    <div className="overflow-x-auto">
      <p className="text-xs text-gray-500 px-3 pt-3 pb-2">▸ Haz clic en un empleado para ver el desglose y de dónde sale su renta</p>
      <table className="w-full min-w-[820px]">
        <thead className="bg-navy-950 border-b border-navy-700">
          <tr>
            <th className="py-2.5 px-2 w-8"></th>
            <th className="text-left py-2.5 px-3 text-[11px] font-medium text-gray-500 uppercase">Empleado</th>
            {th('Bruto')}{th('CSS')}{th('SE')}{th('ISR')}
            {hasPension && th('Pensión', 'text-red-400')}
            {hasEmbargos && th('Embargos', 'text-orange-400')}
            {hasFijas && th('Ded. fijas', 'text-blue-400')}
            {hasPrestamos && th('Préstamos', 'text-purple-400')}
            {hasAnticipos && th('Anticipos', 'text-teal-400')}
            {th('Total ded.')}{th('Neto')}
          </tr>
        </thead>
        <tbody className="divide-y divide-navy-700">
          {det.map(d => {
            const abierto = expandidos.has(d.id);
            const bd = desgloses[d.id];
            const td = (v, cls = 'text-gray-100') => <td className={`py-2.5 px-3 text-sm text-right font-mono ${cls}`}>{fmt(v)}</td>;
            return (
              <React.Fragment key={d.id}>
                <tr className="hover:bg-navy-800 cursor-pointer" onClick={() => alternar(d.id)}>
                  <td className="py-2.5 px-2 text-center text-gray-400 text-xs select-none">
                    {cargando === d.id ? <span className="inline-block w-3 h-3 border border-gray-400 border-t-transparent rounded-full animate-spin" /> : (abierto ? '▾' : '▸')}
                  </td>
                  <td className="py-2.5 px-3 text-sm text-gray-100">{d.empleado?.nombre} {d.empleado?.apellido}</td>
                  {td(d.grossPay)}{td(d.cssEmployee)}{td(d.educationalInsuranceEmployee)}{td(d.incomeTax)}
                  {hasPension && td(d.pensionAlimenticia || 0, 'text-red-400')}
                  {hasEmbargos && td(d.embargos || 0, 'text-orange-400')}
                  {hasFijas && td(d.deduccionesFijas || 0, 'text-blue-400')}
                  {hasPrestamos && td(d.prestamos || 0, 'text-purple-400')}
                  {hasAnticipos && td(d.anticipos || 0, 'text-teal-400')}
                  {td(d.totalDeductions)}
                  <td className="py-2.5 px-3 text-sm text-right font-mono font-medium text-gray-100">
                    {fmt(d.netPay)}
                    {d.tuvoLimitacionSalarioMinimo && (
                      <span className="ml-1 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-500/15 text-amber-400" title="Deducción limitada por salario mínimo">SM</span>
                    )}
                  </td>
                </tr>
                {abierto && (
                  <tr className="bg-navy-950/60">
                    <td colSpan={colSpan} className="p-0">
                      {!bd ? (
                        <div className="flex items-center justify-center py-6 text-gray-400 text-sm">
                          <span className="w-4 h-4 border-2 border-primary-400 border-t-transparent rounded-full animate-spin mr-2" /> Cargando desglose…
                        </div>
                      ) : (
                        <div className="px-6 py-4 space-y-4 text-xs">
                          <div>
                            <h4 className="font-semibold text-emerald-400 uppercase tracking-wider mb-2">Ingresos</h4>
                            <div className="grid grid-cols-2 md:grid-cols-4 gap-x-6 gap-y-1">
                              {[
                                ['Salario base', bd.ingresos.horasRegulares], ['H. domingo', bd.ingresos.horasDomingo], ['H. feriado', bd.ingresos.horasFeriado],
                                ['H.E. diurnas', bd.ingresos.horasExtraDiurnas], ['H.E. nocturnas', bd.ingresos.horasExtraNocturnas], ['H.E. festivos', bd.ingresos.horasExtraFestivos],
                                ['H.E. mixtas', bd.ingresos.horasExtraMixtas], ['H.E. exceso', bd.ingresos.horasExtraExceso], ['Comisiones', bd.ingresos.comisiones], ['Bonificaciones', bd.ingresos.bonos],
                              ].filter(([, v]) => v > 0).map(([l, v]) => (
                                <div key={l} className="flex justify-between"><span className="text-gray-400">{l}</span><span className="font-mono text-gray-200">{fmt(v)}</span></div>
                              ))}
                              <div className="flex justify-between font-semibold border-t border-navy-700 pt-1 col-span-2 md:col-span-4">
                                <span className="text-gray-300">Total bruto</span><span className="font-mono text-emerald-400">{fmt(bd.ingresos.grossPay)}</span>
                              </div>
                            </div>
                          </div>

                          <div>
                            <h4 className="font-semibold text-amber-400 uppercase tracking-wider mb-2">Deducciones legales</h4>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                              <div className="bg-navy-800/60 rounded-lg p-3">
                                <p className="font-semibold text-gray-300 mb-1.5">Seguro Social — {Number(bd.css.tasa ?? 9.75).toFixed(2)}%</p>
                                <div className="flex justify-between"><span className="text-gray-400">Base</span><span className="font-mono text-gray-200">{fmt(bd.css.baseUsada)}</span></div>
                                <div className="flex justify-between font-semibold border-t border-navy-700 pt-1 mt-1"><span className="text-gray-300">Deducción</span><span className="font-mono text-red-400">{fmt(bd.css.monto)}</span></div>
                              </div>
                              <div className="bg-navy-800/60 rounded-lg p-3">
                                <p className="font-semibold text-gray-300 mb-1.5">Seguro Educativo — {Number(bd.se.tasa ?? 1.25).toFixed(2)}%</p>
                                <div className="flex justify-between"><span className="text-gray-400">Base</span><span className="font-mono text-gray-200">{fmt(bd.se.base)}</span></div>
                                <div className="flex justify-between font-semibold border-t border-navy-700 pt-1 mt-1"><span className="text-gray-300">Deducción</span><span className="font-mono text-red-400">{fmt(bd.se.monto)}</span></div>
                              </div>
                              <BloqueRenta isr={bd.isr} empleadoId={d.empleadoId} anio={anio} />
                            </div>
                          </div>

                          {bd.acreedores?.length > 0 && (
                            <div>
                              <h4 className="font-semibold text-orange-400 uppercase tracking-wider mb-2">Acreedores y deducciones adicionales</h4>
                              <div className="space-y-1">
                                {bd.acreedores.map((a, i) => (
                                  <div key={i} className="flex items-center justify-between bg-navy-800/40 rounded px-3 py-1.5">
                                    <div>
                                      <span className="text-gray-200">{a.descripcion}</span>
                                      <span className="ml-2 px-1.5 py-0.5 rounded text-[10px] bg-navy-700 text-gray-400">{a.categoria}</span>
                                      {a.fueLimitado && <span className="ml-1 px-1.5 py-0.5 rounded text-[10px] bg-amber-500/20 text-amber-400" title={a.razonLimitacion || ''}>Limitado</span>}
                                    </div>
                                    <span className="font-mono text-orange-300">{fmt(a.montoAplicado)}</span>
                                  </div>
                                ))}
                              </div>
                            </div>
                          )}

                          <div className="flex justify-between items-center pt-2 border-t border-navy-700 text-sm font-semibold">
                            <span className="text-gray-300">Neto a pagar</span>
                            <span className="font-mono text-emerald-400 text-base">{fmt(bd.netPay)}</span>
                          </div>
                        </div>
                      )}
                    </td>
                  </tr>
                )}
              </React.Fragment>
            );
          })}
        </tbody>
        <tfoot className="bg-navy-950 border-t-2 border-navy-600">
          <tr>
            <td></td>
            <td className="py-2.5 px-3 text-sm font-bold text-gray-100">TOTALES</td>
            {[planilla.totalGrossPay, suma('cssEmployee'), suma('educationalInsuranceEmployee'), suma('incomeTax')].map((v, i) => (
              <td key={i} className="py-2.5 px-3 text-sm text-right font-bold text-gray-100 font-mono">{fmt(v)}</td>
            ))}
            {hasPension && <td className="py-2.5 px-3 text-sm text-right font-bold text-red-400 font-mono">{fmt(suma('pensionAlimenticia'))}</td>}
            {hasEmbargos && <td className="py-2.5 px-3 text-sm text-right font-bold text-orange-400 font-mono">{fmt(suma('embargos'))}</td>}
            {hasFijas && <td className="py-2.5 px-3 text-sm text-right font-bold text-blue-400 font-mono">{fmt(suma('deduccionesFijas'))}</td>}
            {hasPrestamos && <td className="py-2.5 px-3 text-sm text-right font-bold text-purple-400 font-mono">{fmt(suma('prestamos'))}</td>}
            {hasAnticipos && <td className="py-2.5 px-3 text-sm text-right font-bold text-teal-400 font-mono">{fmt(suma('anticipos'))}</td>}
            <td className="py-2.5 px-3 text-sm text-right font-bold text-gray-100 font-mono">{fmt(planilla.totalDeductions)}</td>
            <td className="py-2.5 px-3 text-sm text-right font-bold text-gray-100 font-mono">{fmt(planilla.totalNetPay)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
