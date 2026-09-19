import React, { useEffect, useState } from 'react';
import { Loader2, FileSpreadsheet, Download, Save } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

// ============================================================
// Ficha anual de ISR
//
// Es la hoja que el contador lleva en Excel, reproducida tal cual: la misma
// cabecera, las mismas 15 columnas con sus nombres, 24 filas fijas agrupadas
// por mes, los meses con décimo resaltados y la fila de totales al pie.
// Nada que no esté en su hoja.
// ============================================================

const fmt = (n) => {
  const v = typeof n === 'number' ? n : parseFloat(n ?? 0);
  return isNaN(v) ? '0.00' : v.toLocaleString('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
};
const fmtPeriodos = (n) => Number(n ?? 0).toFixed(3);

const ANIO_ACTUAL = new Date().getFullYear();
const ANIOS = [ANIO_ACTUAL - 2, ANIO_ACTUAL - 1, ANIO_ACTUAL, ANIO_ACTUAL + 1];

const COLUMNAS = [
  'MESES', 'QUINCENAS', 'PERIODOS', 'SALARIOS', 'VACACIONES', 'EXTRAS', 'COMISION', 'XIII MEX',
  'ACUMULADO', 'INGRESO GRAVABLE', 'RENTA ANUAL', 'RENTA POR PERIODO',
  'IMPUESTO CAUSADO', 'IMPUESTO A PAGAR', 'RENTA ACUMULADA',
];

const SALDOS_VACIOS = {
  ingresoGravableInicial: '',
  decimoInicial: '',
  partidasDecimoInicial: '0',
  isrRetenidoInicial: '',
  gastoRepresentacionInicial: '',
  isrGastoRepresentacionInicial: '',
};

export default function FichaIsrPage() {
  const [empleados, setEmpleados] = useState([]);
  const [empleadoId, setEmpleadoId] = useState('');
  const [anio, setAnio] = useState(ANIO_ACTUAL);

  const [ficha, setFicha] = useState(null);
  const [isLoadingEmpleados, setIsLoadingEmpleados] = useState(true);
  const [isLoadingFicha, setIsLoadingFicha] = useState(false);
  const [descargando, setDescargando] = useState(false);

  const [showSaldos, setShowSaldos] = useState(false);
  const [saldos, setSaldos] = useState(SALDOS_VACIOS);
  const [savingSaldos, setSavingSaldos] = useState(false);

  useEffect(() => { loadEmpleados(); }, []);

  useEffect(() => {
    if (empleadoId) loadFicha();
    else setFicha(null);
  }, [empleadoId, anio]);

  const loadEmpleados = async () => {
    try {
      setIsLoadingEmpleados(true);
      const data = await api.get('/api/empleados');
      const lista = Array.isArray(data) ? data : [];
      setEmpleados(lista);
      if (lista.length > 0) setEmpleadoId(String(lista[0].id));
    } catch (error) {
      toast.error(error.message || 'No se pudo cargar la lista de empleados');
    } finally {
      setIsLoadingEmpleados(false);
    }
  };

  const loadFicha = async () => {
    try {
      setIsLoadingFicha(true);
      setFicha(await api.get(`/api/acumulados-fiscales/${empleadoId}/ficha/${anio}`));
    } catch (error) {
      setFicha(null);
      toast.error(error.message || 'No se pudo cargar la ficha');
    } finally {
      setIsLoadingFicha(false);
    }
  };

  const descargarExcel = async () => {
    try {
      setDescargando(true);
      const nombre = `CALCULO RENTA ${ficha?.empleado ?? ''} ${anio}.xlsx`;
      await api.download(`/api/acumulados-fiscales/${empleadoId}/ficha/${anio}/excel`, nombre);
    } catch (error) {
      toast.error(error.message || 'No se pudo descargar el Excel');
    } finally {
      setDescargando(false);
    }
  };

  const abrirSaldos = async () => {
    try {
      const data = await api.get(`/api/acumulados-fiscales/${empleadoId}/saldos/${anio}`);
      setSaldos({
        ingresoGravableInicial: String(data?.ingresoGravableInicial ?? 0),
        decimoInicial: String(data?.decimoInicial ?? 0),
        partidasDecimoInicial: String(data?.partidasDecimoInicial ?? 0),
        isrRetenidoInicial: String(data?.isrRetenidoInicial ?? 0),
        gastoRepresentacionInicial: String(data?.gastoRepresentacionInicial ?? 0),
        isrGastoRepresentacionInicial: String(data?.isrGastoRepresentacionInicial ?? 0),
      });
      setShowSaldos(true);
    } catch (error) {
      toast.error(error.message || 'No se pudieron cargar los saldos iniciales');
    }
  };

  const guardarSaldos = async () => {
    const valores = {
      ingresoGravableInicial: parseFloat(saldos.ingresoGravableInicial) || 0,
      decimoInicial: parseFloat(saldos.decimoInicial) || 0,
      partidasDecimoInicial: parseInt(saldos.partidasDecimoInicial) || 0,
      isrRetenidoInicial: parseFloat(saldos.isrRetenidoInicial) || 0,
      gastoRepresentacionInicial: parseFloat(saldos.gastoRepresentacionInicial) || 0,
      isrGastoRepresentacionInicial: parseFloat(saldos.isrGastoRepresentacionInicial) || 0,
    };
    if (Object.values(valores).some(v => v < 0)) {
      toast.error('Los saldos iniciales no pueden ser negativos');
      return;
    }
    try {
      setSavingSaldos(true);
      await api.put(`/api/acumulados-fiscales/${empleadoId}/saldos/${anio}`, {
        empleadoId: Number(empleadoId), anio, ...valores,
      });
      toast.success('Saldos iniciales guardados');
      setShowSaldos(false);
      await loadFicha();
    } catch (error) {
      toast.error(error.message || 'No se pudieron guardar los saldos');
    } finally {
      setSavingSaldos(false);
    }
  };

  if (isLoadingEmpleados) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-blue-500" />
    </div>
  );

  // Agrupa las filas por mes para que la celda MESES abarque sus quincenas, como en la hoja.
  const filas = ficha?.filas ?? [];
  const rowSpanDeMes = {};
  let mesActual = null;
  filas.forEach((f, i) => {
    if (f.mes) { mesActual = i; rowSpanDeMes[i] = 1; }
    else if (mesActual !== null) rowSpanDeMes[mesActual] += 1;
  });

  return (
    <div className="space-y-5">
      {/* Título y acciones */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-3xl font-bold text-white">Cálculo de Renta</h1>
          <p className="text-gray-400 mt-1">Ficha anual de impuesto sobre la renta por empleado</p>
        </div>
        {empleadoId && (
          <div className="flex items-center gap-2">
            <button
              onClick={abrirSaldos}
              className="flex items-center gap-2 px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg font-medium transition-colors"
            >
              <FileSpreadsheet className="w-4 h-4" /> Saldos iniciales
            </button>
            <button
              onClick={descargarExcel}
              disabled={descargando || !ficha}
              className="flex items-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-60 text-white rounded-lg font-medium transition-colors"
            >
              {descargando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
              Exportar a Excel
            </button>
          </div>
        )}
      </div>

      {/* Cabecera de la hoja */}
      <div className="bg-slate-800 border border-slate-700 rounded-xl p-4 grid gap-4 md:grid-cols-4">
        <div>
          <label htmlFor="ficha-empleado" className="block text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Empleado</label>
          <select
            id="ficha-empleado"
            value={empleadoId}
            onChange={e => setEmpleadoId(e.target.value)}
            className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-1.5 text-sm"
          >
            {empleados.length === 0 && <option value="">No hay empleados</option>}
            {empleados.map(e => <option key={e.id} value={e.id}>{e.nombre} {e.apellido}</option>)}
          </select>
        </div>
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Salario Base</p>
          <p className="text-lg font-mono text-white py-1">{ficha ? fmt(ficha.salarioBase) : '—'}</p>
        </div>
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Conyuge es dependiente</p>
          <p className="text-lg text-white py-1">{ficha ? ficha.conyugeDependiente : '—'}</p>
        </div>
        <div className="flex gap-3">
          <div className="flex-1">
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Periodos de Pagos</p>
            <p className="text-lg font-mono text-white py-1">
              {ficha ? `${Number(ficha.periodosDePago).toFixed(0)} ${ficha.nombrePeriodo}` : '—'}
            </p>
          </div>
          <div>
            <label htmlFor="ficha-anio" className="block text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Año</label>
            <select
              id="ficha-anio"
              value={anio}
              onChange={e => setAnio(Number(e.target.value))}
              className="bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-1.5 text-sm"
            >
              {ANIOS.map(y => <option key={y} value={y}>{y}</option>)}
            </select>
          </div>
        </div>
      </div>

      {isLoadingFicha && (
        <div className="flex items-center justify-center h-40">
          <Loader2 className="w-8 h-8 animate-spin text-blue-500" />
        </div>
      )}

      {/* La hoja */}
      {!isLoadingFicha && ficha && (
        <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-x-auto">
          <table className="w-full text-[13px] min-w-[1400px] border-collapse">
            <thead>
              <tr className="bg-emerald-900/40 text-emerald-100">
                {COLUMNAS.map(c => (
                  <th
                    key={c}
                    className={`px-2 py-2.5 font-bold text-[11px] tracking-wide border border-slate-700 ${
                      c === 'MESES' ? 'text-left w-24' : 'text-center'
                    }`}
                  >
                    {c}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filas.map((f, i) => {
                const claseFila = f.esMesDecimo
                  ? 'bg-amber-400/15 text-amber-50'
                  : f.tieneDatos ? 'text-gray-100' : 'text-gray-500';
                const num = (v, bold = false) => (
                  <td className={`px-2 py-1.5 text-right font-mono border border-slate-700/70 ${bold ? 'font-semibold' : ''}`}>
                    {fmt(v)}
                  </td>
                );
                return (
                  <tr key={f.quincena} className={`${claseFila} hover:bg-slate-700/30`}>
                    {f.mes && (
                      <td
                        rowSpan={rowSpanDeMes[i]}
                        className={`px-2 py-1.5 font-semibold align-middle border border-slate-700 ${
                          f.esMesDecimo ? 'bg-amber-400/25 text-amber-100' : 'text-gray-200'
                        }`}
                      >
                        {f.mes}
                      </td>
                    )}
                    <td className="px-2 py-1.5 text-right font-mono border border-slate-700/70">{f.quincena}</td>
                    <td className="px-2 py-1.5 text-right font-mono border border-slate-700/70">{fmtPeriodos(f.periodos)}</td>
                    {num(f.salarios)}
                    {num(f.vacaciones)}
                    {num(f.extras)}
                    {num(f.comision)}
                    {num(f.xiiiMes)}
                    {num(f.acumulado, true)}
                    {num(f.ingresoGravable, true)}
                    {num(f.rentaAnual)}
                    {num(f.rentaPorPeriodo)}
                    {num(f.impuestoCausado)}
                    {num(f.impuestoAPagar)}
                    {num(f.rentaAcumulada, true)}
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr className="bg-slate-900/60 text-white font-semibold">
                <td className="px-2 py-2 border border-slate-700" colSpan={3}>TOTALES</td>
                <td className="px-2 py-2 text-right font-mono border border-slate-700">{fmt(ficha.totalSalarios)}</td>
                <td className="px-2 py-2 text-right font-mono border border-slate-700">{fmt(ficha.totalVacaciones)}</td>
                <td className="px-2 py-2 text-right font-mono border border-slate-700">{fmt(ficha.totalExtras)}</td>
                <td className="px-2 py-2 text-right font-mono border border-slate-700">{fmt(ficha.totalComision)}</td>
                <td className="px-2 py-2 text-right font-mono border border-slate-700">{fmt(ficha.totalXiiiMes)}</td>
                <td className="px-2 py-2 border border-slate-700" colSpan={7}></td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}

      {/* Modal de saldos iniciales */}
      {showSaldos && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
          <div className="bg-slate-800 border border-slate-700 rounded-xl w-full max-w-lg p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div>
              <h2 className="text-xl font-bold text-white">Saldos iniciales {anio}</h2>
              <p className="text-sm text-gray-400 mt-1">
                Lo que el empleado ya traía acumulado cuando la empresa entró a Pagly.
                Sin esto, migrar a mitad de año le vuelve a cobrar un impuesto que ya pagó.
              </p>
            </div>

            <CampoSaldo id="saldo-gravable" label="Ingreso bruto acumulado"
              ayuda="Salarios, vacaciones, extras y comisiones sumados de enero a la migración"
              value={saldos.ingresoGravableInicial}
              onChange={v => setSaldos(s => ({ ...s, ingresoGravableInicial: v }))} />
            <CampoSaldo id="saldo-decimo" label="Décimo pagado en el año"
              ayuda="Partidas de décimo ya pagadas antes de migrar"
              value={saldos.decimoInicial}
              onChange={v => setSaldos(s => ({ ...s, decimoInicial: v }))} />
            <div>
              <label htmlFor="saldo-partidas" className="block text-sm text-gray-300 mb-1">Cuántas partidas de décimo ya se pagaron</label>
              <select
                id="saldo-partidas"
                value={saldos.partidasDecimoInicial}
                onChange={e => setSaldos(s => ({ ...s, partidasDecimoInicial: e.target.value }))}
                className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-2"
              >
                <option value="0">Ninguna</option>
                <option value="1">1 (abril)</option>
                <option value="2">2 (abril y agosto)</option>
                <option value="3">3 (abril, agosto y diciembre)</option>
              </select>
              <p className="text-xs text-gray-400 mt-1">La columna PERIODOS avanza 0.667 por cada partida pagada</p>
            </div>
            <CampoSaldo id="saldo-isr" label="ISR ya retenido"
              ayuda="Se descuenta del impuesto causado para no cobrarlo dos veces"
              value={saldos.isrRetenidoInicial}
              onChange={v => setSaldos(s => ({ ...s, isrRetenidoInicial: v }))} />
            <CampoSaldo id="saldo-gasto-representacion" label="Gastos de representación pagados"
              ayuda="Solo si el empleado recibe gastos de representación"
              value={saldos.gastoRepresentacionInicial}
              onChange={v => setSaldos(s => ({ ...s, gastoRepresentacionInicial: v }))} />
            <CampoSaldo id="saldo-isr-gasto-representacion" label="ISR retenido sobre esos gastos"
              ayuda="Su tarifa es por tramos: sin este dato el 10% se cobraría dos veces"
              value={saldos.isrGastoRepresentacionInicial}
              onChange={v => setSaldos(s => ({ ...s, isrGastoRepresentacionInicial: v }))} />

            <div className="flex justify-end gap-3 pt-2">
              <button onClick={() => setShowSaldos(false)} className="px-4 py-2 text-gray-300 hover:text-white transition-colors">
                Cancelar
              </button>
              <button
                onClick={guardarSaldos}
                disabled={savingSaldos}
                className="flex items-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-60 text-white rounded-lg font-medium transition-colors"
              >
                {savingSaldos ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                Guardar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function CampoSaldo({ id, label, ayuda, value, onChange }) {
  return (
    <div>
      <label htmlFor={id} className="block text-sm text-gray-300 mb-1">{label}</label>
      <input
        id={id}
        type="number"
        step="0.01"
        min="0"
        value={value}
        onChange={e => onChange(e.target.value)}
        className="w-full bg-slate-700 border border-slate-600 text-white rounded-lg px-3 py-2 font-mono"
      />
      <p className="text-xs text-gray-400 mt-1">{ayuda}</p>
    </div>
  );
}
