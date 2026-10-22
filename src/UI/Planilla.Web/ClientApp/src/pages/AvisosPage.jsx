import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Check, Undo2, ArrowRight, Loader2 } from 'lucide-react';
import { api } from '../services/api';
import { formatDate } from '../utils/date';
import { useAuth } from '../contexts/AuthContext';
import CambiosPorConfirmar from '../components/avisos/CambiosPorConfirmar';

// ============================================================
// Avisos y revisiones de la persona que entra.
//
// Sitio donde va: manda «Por revisar», lo que otra persona le pidió y
// espera respuesta; ahí mismo se aprueba o se devuelve con un comentario,
// o se abre para mirarlo antes. Debajo, todos sus avisos, los nuevos
// marcados. Cargando → esqueleto; respondiendo → esa fila con su giro y
// las demás sin acciones; fallo → la frase del servidor.
// ============================================================

const TIPO = { 1: 'Planilla', 2: 'Décimo', 3: 'Liquidación', 4: 'Horas extra' };

export default function AvisosPage() {
    const navigate = useNavigate();
    const { hasPermission } = useAuth();
    const gestionaEmpleados = hasPermission('employees.update') || hasPermission('employees.delete');
    const [porRevisar, setPorRevisar] = useState([]);
    const [avisos, setAvisos] = useState({ items: [], noLeidos: 0 });
    const [cargando, setCargando] = useState(true);
    const [ocupada, setOcupada] = useState(null);
    const [devolviendo, setDevolviendo] = useState(null);
    const [texto, setTexto] = useState('');

    const cargar = useCallback(async (silencioso = false) => {
        try {
            if (!silencioso) setCargando(true);
            const [r, a] = await Promise.all([api.get('/api/revisiones/para-mi'), api.get('/api/avisos?limite=100')]);
            setPorRevisar(Array.isArray(r) ? r : []);
            setAvisos({ items: a.items ?? [], noLeidos: a.noLeidos ?? 0 });
        } catch (e) {
            toast.error(e.message || 'No se pudieron cargar tus avisos');
        } finally {
            setCargando(false);
        }
    }, []);

    useEffect(() => { cargar(); }, [cargar]);

    const responder = async (s, aprobar) => {
        setOcupada(s.id);
        try {
            await api.post(`/api/revisiones/${s.id}/${aprobar ? 'aprobar' : 'devolver'}`, { comentario: texto || null });
            toast.success(aprobar ? `${s.referencia}: revisión aprobada` : `${s.referencia}: devuelta con tu comentario`);
            setDevolviendo(null);
            setTexto('');
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo guardar tu respuesta');
        } finally {
            setOcupada(null);
        }
    };

    const abrirAviso = (a) => {
        if (!a.leido) api.post(`/api/avisos/${a.id}/leido`).catch(() => { });
        if (a.enlace) navigate(a.enlace);
    };

    const leerTodos = async () => {
        try {
            await api.post('/api/avisos/leer-todos');
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudieron marcar');
        }
    };

    if (cargando) {
        return (
            <div className="space-y-3" aria-busy="true">
                {[0, 1, 2].map(i => (
                    <div key={i} className="h-16 rounded-xl bg-navy-900 border border-navy-700 overflow-hidden">
                        <div className="h-1 bg-primary-500/60 animate-pulse w-1/3" />
                    </div>
                ))}
            </div>
        );
    }

    return (
        <div className="space-y-8 max-w-4xl">
            <div>
                <h1 className="text-3xl font-bold text-white">Avisos y revisiones</h1>
                <p className="text-gray-400 mt-1">Lo que te pidieron revisar y lo que te respondieron</p>
            </div>

            <section aria-labelledby="por-revisar" className="space-y-3">
                <h2 id="por-revisar" className="text-lg font-semibold text-gray-100">
                    Por revisar <span className="text-sm font-normal text-gray-500">{porRevisar.length}</span>
                </h2>
                {porRevisar.length === 0 ? (
                    <p className="text-sm text-gray-500">Nadie te ha pedido revisar nada por ahora.</p>
                ) : porRevisar.map(s => (
                    <div key={s.id} aria-busy={ocupada === s.id || undefined}
                        className={`bg-navy-900 border rounded-xl px-4 py-3 space-y-2 ${ocupada === s.id ? 'border-primary-500/60' : 'border-navy-700'} ${ocupada != null && ocupada !== s.id ? 'opacity-40 pointer-events-none' : ''}`}>
                        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                            {ocupada === s.id && <Loader2 className="w-4 h-4 text-primary-400 animate-spin" />}
                            <span className="text-xs px-2 py-0.5 rounded-full bg-navy-800 border border-navy-600 text-gray-300">{TIPO[s.tipo]}</span>
                            <span className="font-medium text-gray-100">{s.referencia}</span>
                            <span className="text-xs text-gray-500">pedida por {s.solicitadoPor} · {formatDate(s.createdAt)}</span>
                        </div>
                        {s.comentario && <p className="text-sm text-gray-300">«{s.comentario}»</p>}

                        {devolviendo === s.id ? (
                            <div className="flex flex-wrap items-center gap-2">
                                <label className="sr-only" htmlFor={`devolver-${s.id}`}>Qué hay que corregir</label>
                                <input id={`devolver-${s.id}`} autoFocus value={texto} maxLength={1000} onChange={e => setTexto(e.target.value)}
                                    placeholder="Qué hay que corregir"
                                    className="flex-1 min-w-[12rem] px-2 py-1.5 text-sm bg-navy-800 border border-navy-600 rounded-md text-gray-200 focus:outline-none focus:border-gray-400" />
                                <button onClick={() => responder(s, false)} disabled={!texto.trim()}
                                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200 disabled:opacity-50">
                                    <Undo2 className="w-3.5 h-3.5" /> Devolver
                                </button>
                                <button onClick={() => { setDevolviendo(null); setTexto(''); }} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                            </div>
                        ) : (
                            <div className="flex flex-wrap items-center gap-2">
                                <button onClick={() => navigate(s.enlace)}
                                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                                    Abrir <ArrowRight className="w-3.5 h-3.5" />
                                </button>
                                <button onClick={() => { setTexto(''); responder(s, true); }}
                                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-primary-600 hover:bg-primary-700 text-white">
                                    <Check className="w-3.5 h-3.5" /> Aprobar revisión
                                </button>
                                <button onClick={() => { setDevolviendo(s.id); setTexto(''); }}
                                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                                    <Undo2 className="w-3.5 h-3.5" /> Devolver
                                </button>
                            </div>
                        )}
                    </div>
                ))}
            </section>

            {gestionaEmpleados && <CambiosPorConfirmar />}

            <section aria-labelledby="todos-avisos" className="space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                    <h2 id="todos-avisos" className="text-lg font-semibold text-gray-100">
                        Avisos {avisos.noLeidos > 0 && <span className="text-sm font-normal text-gray-400">{avisos.noLeidos} sin leer</span>}
                    </h2>
                    {avisos.noLeidos > 0 && (
                        <button onClick={leerTodos} className="text-xs text-gray-400 hover:text-gray-200">Marcar todo como leído</button>
                    )}
                </div>
                {avisos.items.length === 0 ? (
                    <p className="text-sm text-gray-500">No tienes avisos.</p>
                ) : (
                    <ul className="bg-navy-900 border border-navy-700 rounded-xl divide-y divide-navy-800 overflow-hidden">
                        {avisos.items.map(a => (
                            <li key={a.id}>
                                <button onClick={() => abrirAviso(a)} className="w-full text-left px-4 py-3 hover:bg-navy-800/60 flex gap-3">
                                    <span className={`mt-2 w-1.5 h-1.5 rounded-full shrink-0 ${a.leido ? 'bg-transparent' : 'bg-primary-400'}`} />
                                    <span className="min-w-0 flex-1">
                                        <span className={`block text-sm ${a.leido ? 'text-gray-400' : 'text-gray-100 font-medium'}`}>{a.titulo}</span>
                                        <span className="block text-sm text-gray-500">{a.mensaje}</span>
                                    </span>
                                    <span className="text-xs text-gray-600 shrink-0">{formatDate(a.createdAt)}</span>
                                </button>
                            </li>
                        ))}
                    </ul>
                )}
            </section>
        </div>
    );
}
