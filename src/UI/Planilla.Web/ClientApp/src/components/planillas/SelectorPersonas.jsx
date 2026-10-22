import React, { useMemo, useState } from 'react';
import { Search } from 'lucide-react';

// ============================================================
// Elegir a las personas de una planilla extraordinaria: búsqueda por
// nombre o cédula y una lista con casillas que crece hacia abajo con su
// propio scroll (pensado para cientos de empleados).
// ============================================================

export default function SelectorPersonas({ empleados, seleccion, onCambio }) {
    const [q, setQ] = useState('');
    const elegidos = useMemo(() => new Set(seleccion), [seleccion]);

    const visibles = useMemo(() => {
        const t = q.trim().toLowerCase();
        const lista = t
            ? empleados.filter(e => `${e.nombre} ${e.apellido}`.toLowerCase().includes(t) || (e.numeroIdentificacion || '').toLowerCase().includes(t))
            : empleados;
        return lista.slice(0, 300);
    }, [empleados, q]);

    const alternar = (id) => {
        const s = new Set(elegidos);
        if (s.has(id)) s.delete(id); else s.add(id);
        onCambio([...s]);
    };

    return (
        <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-3">
                <div className="relative w-[260px] max-w-full">
                    <Search className="w-4 h-4 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
                    <label htmlFor="sp-buscar" className="sr-only">Buscar por nombre o cédula</label>
                    <input id="sp-buscar" value={q} onChange={e => setQ(e.target.value)} placeholder="Nombre o cédula"
                        className="w-full pl-8 pr-2.5 py-1.5 text-sm bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500" />
                </div>
                <span className="text-xs text-gray-400">
                    {elegidos.size === 0 ? 'Nadie elegido todavía' : `${elegidos.size} ${elegidos.size === 1 ? 'persona elegida' : 'personas elegidas'}`}
                </span>
                {elegidos.size > 0 && (
                    <button type="button" onClick={() => onCambio([])} className="text-xs text-gray-500 hover:text-gray-300">Quitar todas</button>
                )}
            </div>
            <ul className="max-h-56 overflow-y-auto rounded-lg border border-navy-700 bg-navy-900 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3">
                {visibles.map(e => (
                    <li key={e.id}>
                        <label className="flex items-center gap-2 px-3 py-1.5 text-sm text-gray-200 hover:bg-navy-800 cursor-pointer">
                            <input type="checkbox" checked={elegidos.has(e.id)} onChange={() => alternar(e.id)} className="accent-primary-600" />
                            <span className="truncate">{e.nombre} {e.apellido}</span>
                            <span className="ml-auto text-xs text-gray-500 font-mono shrink-0">{e.numeroIdentificacion}</span>
                        </label>
                    </li>
                ))}
                {visibles.length === 0 && <li className="px-3 py-2 text-sm text-gray-500">Nadie coincide con «{q}».</li>}
            </ul>
        </div>
    );
}
