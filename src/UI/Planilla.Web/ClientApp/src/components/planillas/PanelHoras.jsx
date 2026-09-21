import React, { useEffect, useRef, useState, useCallback } from 'react';
import { Clock, Download, Loader2, Wand2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { Modal } from '../ui/Modal';

// ============================================================
// Horas trabajadas de una planilla: una fila por empleado con las once
// columnas, autoguardado con retraso por empleado, auto-llenar regulares e
// importar novedades (horas extra y ausencias aprobadas).
//
// Antes "Importar novedades" mandaba siempre overwrite, así que la pregunta
// "¿sobrescribir o sumar?" nunca aparecía. Ahora se pregunta primero sin
// modo; si el servidor pide confirmación, se muestra el modal.
// ============================================================

const COLS = [
  ['regularHours', 'Regulares', 'emerald', 'Horas regulares'],
  ['sundayHours', 'Domingo', 'emerald', 'Horas domingo'],
  ['holidayHours', 'Feriado', 'emerald', 'Horas feriado'],
  ['overtimeDayHours', 'Extra diurna', 'orange', 'Horas extra diurnas'],
  ['overtimeNightHours', 'Extra nocturna', 'orange', 'Horas extra nocturnas'],
  ['overtimeHolidayHours', 'Extra festivos', 'purple', 'Horas extra en festivos nacionales'],
  ['overtimeMixedHours', 'Extra mixtas', 'purple', 'Horas extra mixtas'],
  ['overtimeExcessHours', 'Extra exceso', 'purple', 'Horas extra con exceso'],
  ['absenceHours', 'Ausencias', 'red', 'Horas de ausencia'],
  ['commissions', 'Comisión B/.', 'blue', 'Comisiones del período (B/.)'],
];

const COLOR = {
  emerald: 'text-emerald-500 border-emerald-300/40 focus:ring-emerald-500',
  orange: 'text-orange-400 border-orange-300/40 focus:ring-orange-500',
  purple: 'text-purple-400 border-purple-300/40 focus:ring-purple-500',
  red: 'text-red-400 border-red-300/40 focus:ring-red-500',
  blue: 'text-blue-400 border-blue-300/40 focus:ring-blue-500',
};

const num = (v) => (v != null && v !== '' && !Number.isNaN(Number(v))) ? Number(v) : 0;
const normalizar = (row) => Object.fromEntries([['empleadoId', row.empleadoId], ['empleado', row.empleado], ...COLS.map(([k]) => [k, num(row[k])])]);

export default function PanelHoras({ planillaId, editable, onCambio }) {
  const [filas, setFilas] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [accion, setAccion] = useState(null);
  const [confirmar, setConfirmar] = useState(null);
  const timers = useRef({});
  const filasRef = useRef(filas);
  filasRef.current = filas;

  const cargar = useCallback(async () => {
    try {
      setCargando(true);
      const data = await api.get(`/api/payrollheaders/${planillaId}/hours`);
      setFilas(Array.isArray(data) ? data.map(normalizar) : []);
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar las horas');
    } finally {
      setCargando(false);
    }
  }, [planillaId]);

  useEffect(() => { cargar(); }, [cargar]);

  // Guarda leyendo el estado más reciente (no el del cierre del debounce).
  const guardar = async (empleadoId) => {
    const row = filasRef.current.find(r => r.empleadoId === empleadoId);
    if (!row) return;
    try {
      await api.put(`/api/payrollheaders/${planillaId}/hours/${empleadoId}`, {
        empleadoId, ...Object.fromEntries(COLS.map(([k]) => [k, row[k] || 0])),
      });
      onCambio?.();
    } catch (e) {
      toast.error(`No se guardaron las horas de ${row.empleado?.nombre ?? 'un empleado'}: ${e.message}`);
    }
  };

  const cambiar = (empleadoId, campo, valor) => {
    setFilas(prev => prev.map(r => (r.empleadoId === empleadoId ? { ...r, [campo]: num(valor) } : r)));
    clearTimeout(timers.current[empleadoId]);
    timers.current[empleadoId] = setTimeout(() => guardar(empleadoId), 800);
  };

  const autoLlenar = async () => {
    try {
      setAccion('auto');
      await api.post(`/api/payrollheaders/${planillaId}/hours/generate-defaults`);
      toast.success('Horas regulares generadas para todos los empleados activos');
      await cargar(); onCambio?.();
    } catch (e) {
      toast.error(e.message || 'No se pudieron generar las horas');
    } finally {
      setAccion(null);
    }
  };

  const importar = async (modo) => {
    try {
      setAccion('importar');
      // El servidor toma "overwrite" por defecto y solo pregunta cuando el modo NO
      // es overwrite; "ask" hace que pregunte si hay valores previos.
      const url = `/api/payrollheaders/${planillaId}/hours/import-novedades?mode=${modo ?? 'ask'}`;
      const r = await api.post(url);
      if (r.requiresConfirmation) { setConfirmar(r); return; }
      const s = r.summary ?? {};
      toast.success(`Importadas ${(s.totalOvertimeHours || 0).toFixed(1)} h extra y ${(s.absenceHours || 0).toFixed(1)} h de ausencias de ${s.employeesProcessed || 0} empleado(s)`, { duration: 5000 });
      setConfirmar(null);
      await cargar(); onCambio?.();
    } catch (e) {
      toast.error(e.message || 'No se pudieron importar las novedades');
    } finally {
      setAccion(null);
    }
  };

  return (
    <div>
      {editable && (
        <div className="flex flex-wrap items-center gap-2 px-4 py-3 border-b border-navy-700">
          <button onClick={autoLlenar} disabled={!!accion} className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-navy-700 hover:bg-navy-600 text-gray-100 disabled:opacity-50">
            {accion === 'auto' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Wand2 className="w-3.5 h-3.5" />} Auto-llenar regulares
          </button>
          <button onClick={() => importar(null)} disabled={!!accion} className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-navy-700 hover:bg-navy-600 text-gray-100 disabled:opacity-50">
            {accion === 'importar' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />} Importar novedades
          </button>
          <span className="text-xs text-gray-500 ml-auto">Se guarda solo al escribir</span>
        </div>
      )}

      {cargando ? (
        <div className="flex items-center justify-center py-10"><span className="w-6 h-6 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" /></div>
      ) : filas.length === 0 ? (
        <div className="text-center py-10 text-gray-400">
          <Clock className="w-10 h-10 mx-auto mb-2 text-gray-600" />
          <p className="font-medium">Sin horas registradas</p>
          {editable && <p className="text-sm mt-1 text-gray-500">Usa «Auto-llenar regulares» para crear las filas con las horas estándar de cada empleado</p>}
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[1000px]">
            <thead className="bg-navy-950 border-b border-navy-700">
              <tr>
                <th className="text-left py-2.5 px-3 text-[11px] font-semibold text-gray-400 uppercase">Empleado</th>
                {COLS.map(([k, label, color, title]) => (
                  <th key={k} title={title} className={`text-center py-2.5 px-2 text-[11px] font-semibold uppercase ${COLOR[color].split(' ')[0]}`}>{label}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-700/50">
              {filas.map(row => (
                <tr key={row.empleadoId} className="hover:bg-navy-800/50">
                  <td className="py-2 px-3 text-gray-100 whitespace-nowrap">{row.empleado ? `${row.empleado.nombre} ${row.empleado.apellido}` : `Empleado #${row.empleadoId}`}</td>
                  {COLS.map(([k, label, color]) => (
                    <td key={k} className="py-1.5 px-1.5 text-center">
                      {editable ? (
                        <input
                          type="number" min="0" step={k === 'commissions' ? '0.01' : '0.5'}
                          aria-label={`${label} de ${row.empleado?.nombre ?? row.empleadoId}`}
                          value={row[k]}
                          onChange={e => cambiar(row.empleadoId, k, e.target.value)}
                          className={`w-[72px] px-2 py-1.5 bg-navy-800 border rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 ${COLOR[color].split(' ').slice(1).join(' ')}`}
                        />
                      ) : (
                        <span className="font-mono text-gray-300">{row[k]}</span>
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal isOpen={!!confirmar} onClose={() => setConfirmar(null)} title="Ya hay novedades cargadas" size="md">
        <div className="space-y-4">
          <p className="text-sm text-gray-300">{confirmar?.message ?? `${confirmar?.employeesWithExistingValues ?? ''} empleado(s) ya tienen horas extra o ausencias en esta planilla.`}</p>
          <div className="flex flex-col sm:flex-row gap-2">
            <button onClick={() => importar('overwrite')} className="flex-1 px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg text-sm font-medium">Sobrescribir con lo aprobado</button>
            <button onClick={() => importar('sum')} className="flex-1 px-4 py-2 bg-navy-700 hover:bg-navy-600 text-gray-100 rounded-lg text-sm">Sumar a lo que hay</button>
            <button onClick={() => setConfirmar(null)} className="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm">Cancelar</button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
