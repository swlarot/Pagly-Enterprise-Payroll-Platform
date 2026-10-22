import React, { useState } from 'react';
import { Plus, Loader2, Pencil } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { DIAS_SEMANA } from '../../utils/reloj';

// ============================================================
// Horarios contra los que se comparan las marcaciones: entrada, salida,
// días, tolerancia de llegada y desde cuántos minutos después de la salida
// se propone hora extra. Uno es el predeterminado (el de quien no tiene
// otro asignado).
// ============================================================

const VACIO = { nombre: '', horaEntrada: '07:00', horaSalida: '14:30', diasLaborables: 31, toleranciaMinutos: 10, minutosParaHoraExtra: 30, esPredeterminado: false };
const control = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 h-[34px]';

function Formulario({ inicial, onGuardar, onCancelar, guardando }) {
    const [f, setF] = useState(inicial);
    const dia = (bit) => setF(x => ({ ...x, diasLaborables: x.diasLaborables ^ bit }));
    return (
        <div className="flex flex-wrap items-end gap-3">
            <div className="w-[180px]">
                <label htmlFor="h-nombre" className="block text-xs text-gray-400 mb-1">Nombre</label>
                <input id="h-nombre" value={f.nombre} maxLength={100} onChange={e => setF({ ...f, nombre: e.target.value })} placeholder="Docentes" className={`${control} w-full`} />
            </div>
            <div className="w-[110px]">
                <label htmlFor="h-entrada" className="block text-xs text-gray-400 mb-1">Entrada</label>
                <input id="h-entrada" type="time" value={f.horaEntrada} onChange={e => setF({ ...f, horaEntrada: e.target.value })} className={`${control} w-full`} />
            </div>
            <div className="w-[110px]">
                <label htmlFor="h-salida" className="block text-xs text-gray-400 mb-1">Salida</label>
                <input id="h-salida" type="time" value={f.horaSalida} onChange={e => setF({ ...f, horaSalida: e.target.value })} className={`${control} w-full`} />
            </div>
            <fieldset>
                <legend className="block text-xs text-gray-400 mb-1">Días</legend>
                <div className="flex gap-1">
                    {DIAS_SEMANA.map(d => (
                        <button key={d.bit} type="button" title={d.nombre} aria-pressed={(f.diasLaborables & d.bit) !== 0} onClick={() => dia(d.bit)}
                            className={`w-8 h-[34px] rounded-md text-xs font-medium border ${(f.diasLaborables & d.bit) !== 0 ? 'bg-navy-700 border-navy-500 text-white' : 'bg-navy-900 border-navy-700 text-gray-500'}`}>
                            {d.corto}
                        </button>
                    ))}
                </div>
            </fieldset>
            <div className="w-[120px]">
                <label htmlFor="h-tol" className="block text-xs text-gray-400 mb-1">Tolerancia (min)</label>
                <input id="h-tol" type="number" min="0" max="120" value={f.toleranciaMinutos} onChange={e => setF({ ...f, toleranciaMinutos: Number(e.target.value) })} className={`${control} w-full font-mono`} />
            </div>
            <div className="w-[150px]">
                <label htmlFor="h-extra" className="block text-xs text-gray-400 mb-1">Extra desde (min)</label>
                <input id="h-extra" type="number" min="1" max="240" value={f.minutosParaHoraExtra} onChange={e => setF({ ...f, minutosParaHoraExtra: Number(e.target.value) })} className={`${control} w-full font-mono`} />
            </div>
            <label className="flex items-center gap-2 h-[34px] text-xs text-gray-300">
                <input type="checkbox" checked={f.esPredeterminado} onChange={e => setF({ ...f, esPredeterminado: e.target.checked })} className="accent-primary-600" />
                Predeterminado
            </label>
            <button onClick={() => onGuardar(f)} disabled={guardando || !f.nombre.trim() || f.diasLaborables === 0}
                className="h-[34px] inline-flex items-center gap-2 px-4 rounded-lg text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white disabled:opacity-50">
                {guardando && <Loader2 className="w-4 h-4 animate-spin" />} Guardar
            </button>
            {onCancelar && <button onClick={onCancelar} className="h-[34px] text-sm text-gray-400 hover:text-gray-200">Cancelar</button>}
        </div>
    );
}

export default function HorariosReloj({ horarios, onCambio }) {
    const [editando, setEditando] = useState(null); // 'nuevo' | id
    const [guardando, setGuardando] = useState(false);

    const guardar = async (f, id) => {
        setGuardando(true);
        try {
            if (id) await api.put(`/api/reloj/horarios/${id}`, f);
            else await api.post('/api/reloj/horarios', f);
            toast.success('Horario guardado');
            setEditando(null);
            await onCambio();
        } catch (e) {
            toast.error(e.message || 'No se pudo guardar el horario');
        } finally {
            setGuardando(false);
        }
    };

    const dias = (bits) => DIAS_SEMANA.filter(d => (bits & d.bit) !== 0).map(d => d.corto).join(' ');

    return (
        <section className="space-y-3" aria-labelledby="horarios-titulo">
            <div className="flex items-center justify-between gap-3">
                <h2 id="horarios-titulo" className="text-lg font-semibold text-gray-100">Horarios</h2>
                {editando == null && (
                    <button onClick={() => setEditando('nuevo')} className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                        <Plus className="w-3.5 h-3.5" /> Nuevo horario
                    </button>
                )}
            </div>
            {editando === 'nuevo' && (
                <div className="bg-navy-900/60 border border-dashed border-navy-600 rounded-xl px-4 py-3">
                    <Formulario inicial={{ ...VACIO, esPredeterminado: horarios.length === 0 }} guardando={guardando}
                        onGuardar={f => guardar(f)} onCancelar={() => setEditando(null)} />
                </div>
            )}
            {horarios.length === 0 && editando == null && (
                <p className="text-sm text-gray-400">Sin horario no se pueden ver llegadas tarde ni horas extra. Crea el de la mayoría; el primero queda como predeterminado.</p>
            )}
            <ul className="space-y-2">
                {horarios.map(h => (
                    <li key={h.id} className="bg-navy-900 border border-navy-700 rounded-xl px-4 py-3">
                        {editando === h.id ? (
                            <Formulario inicial={h} guardando={guardando} onGuardar={f => guardar(f, h.id)} onCancelar={() => setEditando(null)} />
                        ) : (
                            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
                                <span className="font-medium text-gray-100">{h.nombre}</span>
                                {h.esPredeterminado && <span className="text-xs px-2 py-0.5 rounded-full bg-navy-800 border border-navy-600 text-gray-300">Predeterminado</span>}
                                <span className="text-gray-300 font-mono">{h.horaEntrada}–{h.horaSalida}</span>
                                <span className="text-gray-400">{dias(h.diasLaborables)}</span>
                                <span className="text-gray-500">tolerancia {h.toleranciaMinutos} min · extra desde {h.minutosParaHoraExtra} min</span>
                                <span className="text-gray-500">{h.personas} {h.personas === 1 ? 'persona asignada' : 'personas asignadas'}</span>
                                <button onClick={() => setEditando(h.id)} className="ml-auto inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                                    <Pencil className="w-3.5 h-3.5" /> Editar
                                </button>
                            </div>
                        )}
                    </li>
                ))}
            </ul>
        </section>
    );
}
