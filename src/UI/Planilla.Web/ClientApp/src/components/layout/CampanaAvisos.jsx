import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { api } from '../../services/api';

// ============================================================
// La campana de la barra superior: cuántos avisos faltan por leer y los
// últimos, con un clic para ir a lo que piden. Se actualiza cada minuto y
// al volver a la pestaña. Sin avisos nuevos, no hay punto.
// ============================================================

const CADA = 60_000;

function haceCuanto(fecha) {
    const min = Math.round((Date.now() - new Date(fecha).getTime()) / 60000);
    if (min < 1) return 'ahora';
    if (min < 60) return `hace ${min} min`;
    const h = Math.round(min / 60);
    if (h < 24) return `hace ${h} h`;
    const d = Math.round(h / 24);
    return d === 1 ? 'ayer' : `hace ${d} días`;
}

export default function CampanaAvisos() {
    const navigate = useNavigate();
    const [datos, setDatos] = useState({ items: [], noLeidos: 0 });
    const [abierta, setAbierta] = useState(false);
    const ref = useRef(null);

    const cargar = useCallback(async () => {
        try {
            const r = await api.get('/api/avisos?limite=8');
            setDatos({ items: r.items ?? [], noLeidos: r.noLeidos ?? 0 });
        } catch { /* la campana no interrumpe nada si falla */ }
    }, []);

    useEffect(() => {
        cargar();
        const t = setInterval(cargar, CADA);
        const alVolver = () => { if (document.visibilityState === 'visible') cargar(); };
        document.addEventListener('visibilitychange', alVolver);
        return () => { clearInterval(t); document.removeEventListener('visibilitychange', alVolver); };
    }, [cargar]);

    useEffect(() => {
        if (!abierta) return;
        const fuera = (e) => { if (ref.current && !ref.current.contains(e.target)) setAbierta(false); };
        const escape = (e) => { if (e.key === 'Escape') setAbierta(false); };
        document.addEventListener('mousedown', fuera);
        window.addEventListener('keydown', escape);
        return () => { document.removeEventListener('mousedown', fuera); window.removeEventListener('keydown', escape); };
    }, [abierta]);

    const ir = async (aviso) => {
        setAbierta(false);
        if (!aviso.leido) {
            setDatos(d => ({ items: d.items.map(a => a.id === aviso.id ? { ...a, leido: true } : a), noLeidos: Math.max(0, d.noLeidos - 1) }));
            api.post(`/api/avisos/${aviso.id}/leido`).catch(() => { });
        }
        if (aviso.enlace) navigate(aviso.enlace);
    };

    const leerTodos = async () => {
        setDatos(d => ({ items: d.items.map(a => ({ ...a, leido: true })), noLeidos: 0 }));
        try { await api.post('/api/avisos/leer-todos'); } catch { cargar(); }
    };

    const n = datos.noLeidos;

    return (
        <div className="relative" ref={ref}>
            <button
                onClick={() => { setAbierta(a => !a); if (!abierta) cargar(); }}
                aria-haspopup="menu"
                aria-expanded={abierta}
                aria-label={n > 0 ? `Avisos: ${n} sin leer` : 'Avisos'}
                className="relative p-1.5 rounded-lg text-gray-400 hover:text-gray-200 hover:bg-white/[0.06] transition-all duration-150"
            >
                <Bell className="w-4 h-4" />
                {n > 0 && (
                    <span className="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 px-1 rounded-full bg-primary-500 text-[10px] font-semibold text-white flex items-center justify-center">
                        {n > 9 ? '9+' : n}
                    </span>
                )}
            </button>

            {abierta && (
                <div role="menu" className="absolute right-0 top-full mt-1.5 w-[22rem] max-w-[calc(100vw-1rem)] bg-navy-900 border border-white/[0.08] rounded-xl shadow-2xl shadow-black/40 z-50 overflow-hidden">
                    <div className="flex items-center justify-between px-3 py-2.5 border-b border-white/[0.06]">
                        <p className="text-[12px] font-semibold text-gray-100">Avisos</p>
                        {n > 0 && (
                            <button onClick={leerTodos} className="text-[11px] text-gray-400 hover:text-gray-200">Marcar todo como leído</button>
                        )}
                    </div>
                    {datos.items.length === 0 ? (
                        <p className="px-3 py-6 text-center text-[12px] text-gray-500">No tienes avisos.</p>
                    ) : (
                        <ul className="max-h-[22rem] overflow-y-auto divide-y divide-white/[0.04]">
                            {datos.items.map(a => (
                                <li key={a.id}>
                                    <button role="menuitem" onClick={() => ir(a)}
                                        className="w-full text-left px-3 py-2.5 hover:bg-white/[0.04] flex gap-2">
                                        <span className={`mt-1.5 w-1.5 h-1.5 rounded-full shrink-0 ${a.leido ? 'bg-transparent' : 'bg-primary-400'}`} />
                                        <span className="min-w-0">
                                            <span className={`block text-[12px] truncate ${a.leido ? 'text-gray-400' : 'text-gray-100 font-medium'}`}>{a.titulo}</span>
                                            <span className="block text-[11px] text-gray-500 line-clamp-2">{a.mensaje}</span>
                                            <span className="block text-[10px] text-gray-600 mt-0.5">{haceCuanto(a.createdAt)}</span>
                                        </span>
                                    </button>
                                </li>
                            ))}
                        </ul>
                    )}
                    <button onClick={() => { setAbierta(false); navigate('/avisos'); }}
                        className="w-full px-3 py-2 text-[12px] text-gray-300 hover:bg-white/[0.04] border-t border-white/[0.06]">
                        Ver todo y lo que tengo por revisar
                    </button>
                </div>
            )}
        </div>
    );
}
