import React, { useEffect, useRef, useState, useCallback } from 'react';
import { Clock, Search, ChevronLeft, ChevronRight, Check, Loader2, AlertCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';

// ============================================================
// Horas trabajadas de una planilla: una fila por empleado con las diez
// columnas, autoguardado con retraso por empleado. Las filas ya vienen
// creadas al nacer la planilla (regulares + novedades aprobadas), así que
// aquí solo se corrige lo que difiere.
//
// Pensado para miles de empleados: página de 50 desde el servidor,
// búsqueda por nombre o cédula, y captura rápida —Enter baja a la misma
// columna de la fila siguiente, y pegar una columna copiada de Excel
// rellena hacia abajo desde la celda donde se pega.
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

const TAMANO = 50;
const num = (v) => (v != null && v !== '' && !Number.isNaN(Number(v))) ? Number(v) : 0;
const normalizar = (row) => Object.fromEntries([['empleadoId', row.empleadoId], ['empleado', row.empleado], ...COLS.map(([k]) => [k, num(row[k])])]);
const nombreDe = (row) => (row.empleado ? `${row.empleado.nombre} ${row.empleado.apellido}` : 'un empleado');

export default function PanelHoras({ planillaId, editable, onCambio }) {
  const [filas, setFilas] = useState([]);
  const [total, setTotal] = useState(0);
  const [pagina, setPagina] = useState(1);
  const [busqueda, setBusqueda] = useState('');
  const [q, setQ] = useState('');
  const [cargando, setCargando] = useState(true);
  const [estado, setEstado] = useState({}); // empleadoId → 'guardando' | 'ok' | 'error'
  const timers = useRef({});
  const filasRef = useRef(filas);
  filasRef.current = filas;
  const tabla = useRef(null);

  // Guarda leyendo el estado más reciente (no el del cierre del debounce).
  const guardar = async (empleadoId) => {
    const row = filasRef.current.find(r => r.empleadoId === empleadoId);
    if (!row) return;
    setEstado(prev => ({ ...prev, [empleadoId]: 'guardando' }));
    try {
      await api.put(`/api/payrollheaders/${planillaId}/hours/${empleadoId}`, {
        empleadoId, ...Object.fromEntries(COLS.map(([k]) => [k, row[k] || 0])),
      });
      setEstado(prev => ({ ...prev, [empleadoId]: 'ok' }));
      onCambio?.();
    } catch (e) {
      setEstado(prev => ({ ...prev, [empleadoId]: 'error' }));
      toast.error(`No se guardaron las horas de ${nombreDe(row)}: ${e.message}`);
    }
  };

  // Antes de cambiar de página o de búsqueda, lo pendiente se guarda ya:
  // si no, el retraso dispararía sobre filas que ya no están en pantalla.
  const vaciarPendientes = async () => {
    const ids = Object.keys(timers.current);
    ids.forEach(id => clearTimeout(timers.current[id]));
    timers.current = {};
    await Promise.all(ids.map(id => guardar(Number(id))));
  };

  const cargar = useCallback(async () => {
    try {
      setCargando(true);
      await vaciarPendientes();
      const data = await api.get(`/api/payrollheaders/${planillaId}/hours?page=${pagina}&size=${TAMANO}&q=${encodeURIComponent(q)}`);
      setFilas((data.items ?? []).map(normalizar));
      setTotal(data.total ?? 0);
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar las horas');
    } finally {
      setCargando(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [planillaId, pagina, q]);

  useEffect(() => { cargar(); }, [cargar]);

  // Al cerrar el panel (cambio de pestaña o de planilla) lo pendiente también se guarda.
  useEffect(() => () => { vaciarPendientes(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // La búsqueda espera a que se deje de escribir.
  useEffect(() => {
    const t = setTimeout(() => { setQ(busqueda.trim()); setPagina(1); }, 350);
    return () => clearTimeout(t);
  }, [busqueda]);

  const programar = (empleadoId) => {
    clearTimeout(timers.current[empleadoId]);
    timers.current[empleadoId] = setTimeout(() => { delete timers.current[empleadoId]; guardar(empleadoId); }, 800);
  };

  const cambiar = (empleadoId, campo, valor) => {
    setFilas(prev => prev.map(r => (r.empleadoId === empleadoId ? { ...r, [campo]: num(valor) } : r)));
    programar(empleadoId);
  };

  // Enter: a la misma columna de la fila siguiente (Shift+Enter: la anterior).
  const tecla = (e, fila, col) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const destino = tabla.current?.querySelector(`input[data-fila="${fila + (e.shiftKey ? -1 : 1)}"][data-col="${col}"]`);
    if (destino) { destino.focus(); destino.select(); }
  };

  // Pegar una columna (una cifra por línea) rellena hacia abajo desde esta celda.
  const pegar = (e, fila, campo) => {
    const texto = e.clipboardData?.getData('text') ?? '';
    const lineas = texto.split(/\r?\n/).map(l => l.trim()).filter(l => l !== '');
    if (lineas.length <= 1) return; // un solo valor: pegado normal del navegador
    e.preventDefault();
    const valores = lineas.map(l => num(l.replace(',', '.')));
    const ids = [];
    // Se calcula fuera del updater: React puede diferirlo y aquí hacen falta los ids ya.
    const nuevas = filasRef.current.map((r, i) => {
      const k = i - fila;
      if (k < 0 || k >= valores.length) return r;
      ids.push(r.empleadoId);
      return { ...r, [campo]: valores[k] };
    });
    filasRef.current = nuevas;
    setFilas(nuevas);
    ids.forEach(programar);
    toast.success(`${ids.length} ${ids.length === 1 ? 'fila rellenada' : 'filas rellenadas'} desde el portapapeles`);
  };

  const paginas = Math.max(1, Math.ceil(total / TAMANO));
  const desde = total === 0 ? 0 : (pagina - 1) * TAMANO + 1;
  const hasta = Math.min(pagina * TAMANO, total);

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3 px-4 py-2.5 border-b border-navy-700">
        <label className="relative">
          <span className="sr-only">Buscar empleado</span>
          <Search className="w-3.5 h-3.5 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
          <input
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
            placeholder="Nombre o cédula"
            className="w-56 pl-8 pr-2.5 py-1.5 bg-navy-800 border border-navy-600 rounded-lg text-sm text-gray-100 placeholder:text-gray-600 focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </label>
        <span className="text-xs text-gray-500">
          {total === 0 ? 'Sin empleados' : `${desde}–${hasta} de ${total}`}
        </span>
        {editable && (
          <span className="text-xs text-gray-500 ml-auto hidden md:inline">
            Se guarda solo al escribir · Enter baja una fila · pega una columna de Excel para rellenar
          </span>
        )}
      </div>

      {cargando && filas.length === 0 ? (
        <div className="flex items-center justify-center py-10"><span className="w-6 h-6 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" /></div>
      ) : filas.length === 0 ? (
        <div className="text-center py-10 text-gray-400">
          <Clock className="w-10 h-10 mx-auto mb-2 text-gray-600" />
          <p className="font-medium">{q ? `Nadie coincide con «${q}»` : 'Sin empleados activos'}</p>
          {!q && editable && <p className="text-sm mt-1 text-gray-500">Las horas se crean con la planilla para cada empleado activo; activa empleados y vuelve a crearla.</p>}
        </div>
      ) : (
        <div className={`overflow-x-auto transition-opacity ${cargando ? 'opacity-50' : ''}`} aria-busy={cargando || undefined}>
          <table ref={tabla} className="w-full text-sm min-w-[1000px]">
            <thead className="bg-navy-950 border-b border-navy-700">
              <tr>
                <th className="text-left py-2.5 px-3 text-[11px] font-semibold text-gray-400 uppercase">Empleado</th>
                {COLS.map(([k, label, color, title]) => (
                  <th key={k} title={title} className={`text-center py-2.5 px-2 text-[11px] font-semibold uppercase ${COLOR[color].split(' ')[0]}`}>{label}</th>
                ))}
                <th className="w-8"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-700/50">
              {filas.map((row, i) => (
                <tr key={row.empleadoId} className="hover:bg-navy-800/50">
                  <td className="py-2 px-3 text-gray-100 whitespace-nowrap">
                    {nombreDe(row)}
                    {row.empleado?.numeroIdentificacion && <span className="ml-2 text-[11px] text-gray-500 font-mono">{row.empleado.numeroIdentificacion}</span>}
                  </td>
                  {COLS.map(([k, label, color]) => (
                    <td key={k} className="py-1.5 px-1.5 text-center">
                      {editable ? (
                        <input
                          type="number" min="0" step={k === 'commissions' ? '0.01' : '0.5'}
                          data-fila={i} data-col={k}
                          aria-label={`${label} de ${nombreDe(row)}`}
                          value={row[k]}
                          onChange={e => cambiar(row.empleadoId, k, e.target.value)}
                          onKeyDown={e => tecla(e, i, k)}
                          onPaste={e => pegar(e, i, k)}
                          onFocus={e => e.target.select()}
                          className={`w-[72px] px-2 py-1.5 bg-navy-800 border rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 ${COLOR[color].split(' ').slice(1).join(' ')}`}
                        />
                      ) : (
                        <span className="font-mono text-gray-300">{row[k]}</span>
                      )}
                    </td>
                  ))}
                  <td className="px-2 text-center">
                    {estado[row.empleadoId] === 'guardando' && <Loader2 className="w-3.5 h-3.5 animate-spin text-gray-400 inline" aria-label="Guardando" />}
                    {estado[row.empleadoId] === 'ok' && <Check className="w-3.5 h-3.5 text-primary-400 inline" aria-label="Guardado" />}
                    {estado[row.empleadoId] === 'error' && <AlertCircle className="w-3.5 h-3.5 text-red-400 inline" aria-label="No se guardó" />}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {paginas > 1 && (
        <div className="flex items-center justify-between px-4 py-2.5 border-t border-navy-700 text-xs text-gray-400">
          <span>Página {pagina} de {paginas}</span>
          <div className="flex items-center gap-1">
            <button onClick={() => setPagina(p => Math.max(1, p - 1))} disabled={pagina === 1 || cargando} aria-label="Página anterior" className="p-1.5 rounded-lg bg-navy-800 border border-navy-600 text-gray-200 disabled:opacity-40"><ChevronLeft className="w-4 h-4" /></button>
            <button onClick={() => setPagina(p => Math.min(paginas, p + 1))} disabled={pagina === paginas || cargando} aria-label="Página siguiente" className="p-1.5 rounded-lg bg-navy-800 border border-navy-600 text-gray-200 disabled:opacity-40"><ChevronRight className="w-4 h-4" /></button>
          </div>
        </div>
      )}
    </div>
  );
}
