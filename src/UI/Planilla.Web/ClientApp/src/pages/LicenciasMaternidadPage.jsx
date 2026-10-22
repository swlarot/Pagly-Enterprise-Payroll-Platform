import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Plus, Loader2, CalendarClock, Ban } from 'lucide-react';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatDate } from '../utils/date';
import DatePicker from '../components/ui/DatePicker';

// ============================================================
// Licencias de maternidad.
//
// Sitio donde va: manda la lista de licencias (en curso primero). Arriba,
// registrar una nueva en una sola fila: colaboradora, inicio y el último
// día, que se propone a las 14 semanas y se puede cambiar. Mientras dura,
// la planilla no le paga salario (la CSS paga el subsidio) y su fila lo
// dice; vuelve sola el día de su regreso.
//
// Estados: cargando → esqueleto; guardando → esa fila con su giro y el
// resto sin acciones; fallo → la frase del servidor. Anular no borra.
// ============================================================

const DIAS_LICENCIA = 98;
const ORDEN = { 'En curso': 0, Programada: 1, Terminada: 2, Anulada: 3 };
const ESTADO_CLS = {
    'En curso': 'bg-blue-900/70 text-blue-200',
    Programada: 'bg-slate-700 text-gray-200',
    Terminada: 'bg-navy-800 text-gray-400 border border-navy-600',
    Anulada: 'bg-navy-800 text-gray-500 border border-navy-700',
};

function sumarDias(iso, dias) {
    if (!iso) return '';
    const [a, m, d] = iso.split('-').map(Number);
    const f = new Date(Date.UTC(a, m - 1, d + dias));
    return f.toISOString().slice(0, 10);
}

const control = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 h-[34px]';

export default function LicenciasMaternidadPage() {
    const { canWrite, hasPermission } = useAuth();
    const puedeEditar = canWrite() && hasPermission('absences.manage');

    const [licencias, setLicencias] = useState([]);
    const [cargando, setCargando] = useState(true);
    const [verAnuladas, setVerAnuladas] = useState(false);
    const [empleadas, setEmpleadas] = useState([]);
    const [nueva, setNueva] = useState({ empleadoId: '', inicio: '', fin: '', observaciones: '' });
    const [creando, setCreando] = useState(false);
    const [avisos, setAvisos] = useState([]);
    const [ocupada, setOcupada] = useState(null);
    const [editando, setEditando] = useState(null);   // { id, modo: 'regreso'|'anular', valor }

    const cargar = useCallback(async (silencioso = false) => {
        try {
            if (!silencioso) setCargando(true);
            const data = await api.get(`/api/licencias-maternidad?incluirAnuladas=${verAnuladas}`);
            setLicencias(Array.isArray(data) ? data : []);
        } catch (e) {
            toast.error(e.message || 'No se pudieron cargar las licencias');
        } finally {
            setCargando(false);
        }
    }, [verAnuladas]);

    useEffect(() => { cargar(); }, [cargar]);

    useEffect(() => {
        if (!puedeEditar) return;
        api.get('/api/empleados')
            .then(d => setEmpleadas((Array.isArray(d) ? d : d?.items ?? []).filter(e => e.estaActivo !== false)))
            .catch(() => { });
    }, [puedeEditar]);

    const ordenadas = useMemo(
        () => [...licencias].sort((a, b) => (ORDEN[a.estado] - ORDEN[b.estado]) || b.fechaInicio.localeCompare(a.fechaInicio)),
        [licencias]);

    const cambiarInicio = (v) => setNueva(n => ({ ...n, inicio: v, fin: v ? sumarDias(v, DIAS_LICENCIA - 1) : '' }));

    const registrar = async () => {
        setCreando(true);
        setAvisos([]);
        try {
            const r = await api.post('/api/licencias-maternidad', {
                empleadoId: Number(nueva.empleadoId),
                fechaInicio: nueva.inicio,
                fechaFin: nueva.fin || null,
                observaciones: nueva.observaciones || null,
            });
            toast.success(`Licencia registrada; regresa el ${formatDate(r.licencia.regreso)}`);
            setAvisos(r.avisos ?? []);
            setNueva({ empleadoId: '', inicio: '', fin: '', observaciones: '' });
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo registrar la licencia');
        } finally {
            setCreando(false);
        }
    };

    const guardarRegreso = async (l) => {
        setOcupada(l.id);
        try {
            const r = await api.put(`/api/licencias-maternidad/${l.id}`, {
                empleadoId: l.empleadoId, fechaInicio: l.fechaInicio.slice(0, 10),
                fechaFin: sumarDias(editando.valor, -1), observaciones: l.observaciones,
            });
            toast.success(`Regreso cambiado al ${formatDate(r.licencia.regreso)}`);
            setAvisos(r.avisos ?? []);
            setEditando(null);
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo cambiar el regreso');
        } finally {
            setOcupada(null);
        }
    };

    const anular = async (l) => {
        setOcupada(l.id);
        try {
            await api.post(`/api/licencias-maternidad/${l.id}/anular`, { motivo: editando.valor || null });
            toast.success('Licencia anulada; queda en el historial');
            setEditando(null);
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo anular');
        } finally {
            setOcupada(null);
        }
    };

    return (
        <div className="space-y-5">
            <div>
                <h1 className="text-3xl font-bold text-white">Licencias de maternidad</h1>
                <p className="text-gray-400 mt-1">Mientras dura no cobra salario en la planilla (la CSS paga el subsidio) y vuelve sola el día de su regreso</p>
            </div>

            {puedeEditar && (
                <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3 space-y-3">
                    <div className="flex items-center gap-2 text-sm text-gray-300">
                        <Plus className="w-4 h-4 text-gray-500" /> Registrar una licencia
                    </div>
                    <div className="flex flex-wrap items-end gap-3">
                        <div className="w-[240px] max-w-full">
                            <label htmlFor="lm-empleada" className="block text-xs text-gray-400 mb-1">Colaboradora</label>
                            <select id="lm-empleada" value={nueva.empleadoId} onChange={e => setNueva(n => ({ ...n, empleadoId: e.target.value }))} className={`${control} w-full`}>
                                <option value="">Elige a la colaboradora</option>
                                {empleadas.map(e => <option key={e.id} value={e.id}>{e.nombre} {e.apellido}</option>)}
                            </select>
                        </div>
                        <div className="w-[150px]">
                            <label htmlFor="lm-inicio" className="block text-xs text-gray-400 mb-1">Inicio</label>
                            <DatePicker id="lm-inicio" value={nueva.inicio} onChange={cambiarInicio} ariaLabel="Inicio de la licencia" />
                        </div>
                        <div className="w-[150px]">
                            <label htmlFor="lm-fin" className="block text-xs text-gray-400 mb-1">Último día <span className="text-gray-500">(14 semanas)</span></label>
                            <DatePicker id="lm-fin" value={nueva.fin} onChange={v => setNueva(n => ({ ...n, fin: v }))} ariaLabel="Último día de la licencia" />
                        </div>
                        <div className="flex-1 min-w-[12rem]">
                            <label htmlFor="lm-obs" className="block text-xs text-gray-400 mb-1">Observaciones</label>
                            <input id="lm-obs" value={nueva.observaciones} maxLength={500}
                                onChange={e => setNueva(n => ({ ...n, observaciones: e.target.value }))}
                                placeholder="Certificado, fecha probable de parto…" className={`${control} w-full`} />
                        </div>
                        <button onClick={registrar} disabled={!nueva.empleadoId || !nueva.inicio || creando}
                            className="h-[34px] flex items-center gap-2 px-4 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white rounded-lg text-sm font-medium">
                            {creando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Registrar
                        </button>
                    </div>
                    {nueva.fin && (
                        <p className="text-xs text-gray-400">Regresa el {formatDate(sumarDias(nueva.fin, 1))}. Tendrá fuero de maternidad hasta un año después.</p>
                    )}
                </div>
            )}

            {avisos.length > 0 && (
                <ul className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-200 space-y-1">
                    {avisos.map((a, i) => <li key={i}>{a}</li>)}
                </ul>
            )}

            <div className="flex items-center justify-between gap-3">
                <p className="text-sm text-gray-400">
                    {cargando ? '' : licencias.length === 0 ? 'No hay licencias registradas.' : `${licencias.length} ${licencias.length === 1 ? 'licencia' : 'licencias'}`}
                </p>
                <label className="flex items-center gap-2 text-xs text-gray-400">
                    <input type="checkbox" checked={verAnuladas} onChange={e => setVerAnuladas(e.target.checked)} className="accent-primary-600" />
                    Ver anuladas
                </label>
            </div>

            {cargando ? (
                <div className="space-y-3" aria-busy="true">
                    {[0, 1].map(i => (
                        <div key={i} className="h-16 rounded-xl bg-navy-900 border border-navy-700 overflow-hidden">
                            <div className="h-1 bg-primary-500/60 animate-pulse w-1/3" />
                        </div>
                    ))}
                </div>
            ) : (
                <ul className="space-y-3">
                    {ordenadas.map(l => (
                        <li key={l.id} aria-busy={ocupada === l.id || undefined}
                            className={`bg-navy-900 border rounded-xl px-4 py-3 space-y-2 ${ocupada === l.id ? 'border-primary-500/60' : 'border-navy-700'} ${ocupada != null && ocupada !== l.id ? 'opacity-40 pointer-events-none' : ''}`}>
                            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                                {ocupada === l.id && <Loader2 className="w-4 h-4 text-primary-400 animate-spin" />}
                                <span className="font-semibold text-gray-100">{l.empleado}</span>
                                <span className="text-sm text-gray-300">{formatDate(l.fechaInicio)} – {formatDate(l.fechaFin)} <span className="text-gray-500">· {l.dias} días</span></span>
                                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ESTADO_CLS[l.estado]}`}>{l.estado}</span>
                                {!l.anulada && <span className="text-xs text-gray-400">regresa el {formatDate(l.regreso)} · fuero hasta {formatDate(l.fueroHasta)}</span>}
                                {puedeEditar && !l.anulada && editando?.id !== l.id && (
                                    <span className="flex gap-2 ml-auto">
                                        <button onClick={() => setEditando({ id: l.id, modo: 'regreso', valor: l.regreso.slice(0, 10) })}
                                            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                                            <CalendarClock className="w-3.5 h-3.5" /> Cambiar regreso
                                        </button>
                                        <button onClick={() => setEditando({ id: l.id, modo: 'anular', valor: '' })}
                                            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-red-900/40 border border-navy-600 text-gray-300 hover:text-red-200">
                                            <Ban className="w-3.5 h-3.5" /> Anular
                                        </button>
                                    </span>
                                )}
                            </div>
                            {(l.observaciones || l.motivoAnulacion) && (
                                <p className="text-xs text-gray-500">{l.anulada ? `Anulada${l.motivoAnulacion ? `: ${l.motivoAnulacion}` : ''}` : l.observaciones}</p>
                            )}

                            {editando?.id === l.id && editando.modo === 'regreso' && (
                                <div className="flex flex-wrap items-end gap-2">
                                    <div className="w-[150px]">
                                        <label htmlFor={`regreso-${l.id}`} className="block text-xs text-gray-400 mb-1">Día de regreso</label>
                                        <DatePicker id={`regreso-${l.id}`} value={editando.valor} onChange={v => setEditando(x => ({ ...x, valor: v }))} ariaLabel="Día de regreso" />
                                    </div>
                                    <button onClick={() => guardarRegreso(l)} disabled={!editando.valor}
                                        className="h-[34px] px-3 rounded-lg text-xs font-medium bg-primary-600 hover:bg-primary-700 text-white disabled:opacity-50">Guardar</button>
                                    <button onClick={() => setEditando(null)} className="h-[34px] text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                                </div>
                            )}
                            {editando?.id === l.id && editando.modo === 'anular' && (
                                <div className="flex flex-wrap items-center gap-2">
                                    <label className="sr-only" htmlFor={`anular-${l.id}`}>Por qué se anula</label>
                                    <input id={`anular-${l.id}`} autoFocus value={editando.valor} maxLength={500}
                                        onChange={e => setEditando(x => ({ ...x, valor: e.target.value }))}
                                        placeholder="Por qué se anula (queda en el historial)" className={`${control} flex-1 min-w-[12rem]`} />
                                    <button onClick={() => anular(l)}
                                        className="h-[34px] px-3 rounded-lg text-xs font-medium bg-navy-800 hover:bg-red-900/40 border border-navy-600 text-gray-200">Anular</button>
                                    <button onClick={() => setEditando(null)} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                                </div>
                            )}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
