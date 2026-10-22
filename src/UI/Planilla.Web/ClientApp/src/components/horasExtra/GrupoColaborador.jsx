import React, { useState } from 'react';
import { Check, X, RotateCcw, Pencil, Trash2, Loader2 } from 'lucide-react';
import { formatCurrency } from '../../utils/currency';
import { formatDate } from '../../utils/date';

// ============================================================
// Las horas extra de un colaborador en el mes, con la decisión del
// director en cada una: aprobar, rechazar (con su razón) o devolver a
// pendiente. Arriba, aprobar o rechazar todas las pendientes de una vez.
//
// Rechazar no borra: la fila queda con la razón y quién decidió. Lo
// pagado en una planilla aprobada ya no se toca.
// ============================================================

const ESTADO = {
    Pendiente: 'bg-amber-500/15 text-amber-300',
    Aprobada: 'bg-blue-900/70 text-blue-200',
    Rechazada: 'bg-slate-700 text-gray-300',
    Pagada: 'bg-emerald-900/70 text-emerald-200',
};

const hhmm = (t) => (t || '').substring(0, 5);

// eslint-disable-next-line no-unused-vars -- Icon se usa como componente en el JSX
const Accion = ({ onClick, icon: Icon, children, disabled, peligro }) => (
    <button
        type="button"
        onClick={onClick}
        disabled={disabled}
        className={`inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium border border-navy-600 bg-navy-800 transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${
            peligro ? 'text-gray-300 hover:text-red-200 hover:bg-red-900/30' : 'text-gray-200 hover:bg-navy-700'}`}
    >
        <Icon className="w-3.5 h-3.5" /> {children}
    </button>
);

/** Campo de la razón del rechazo, en la misma fila: sin ventanas emergentes. */
function RazonRechazo({ etiqueta, onConfirmar, onCancelar, ocupado }) {
    const [motivo, setMotivo] = useState('');
    return (
        <div className="flex flex-wrap items-center gap-2 w-full">
            <label className="sr-only" htmlFor={etiqueta}>Razón del rechazo</label>
            <input
                id={etiqueta}
                autoFocus
                value={motivo}
                maxLength={500}
                onChange={e => setMotivo(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') onConfirmar(motivo); if (e.key === 'Escape') onCancelar(); }}
                placeholder="Razón (la verá quien la registró)"
                className="flex-1 min-w-[12rem] px-2 py-1 text-sm bg-navy-800 border border-navy-600 rounded-md text-gray-200 focus:outline-none focus:border-gray-400"
            />
            <Accion icon={X} peligro disabled={ocupado} onClick={() => onConfirmar(motivo)}>Rechazar</Accion>
            <button type="button" onClick={onCancelar} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
        </div>
    );
}

export default function GrupoColaborador({ nombre, horas, puedeDecidir, puedeEditar, ocupado, onDecidir, onEditar, onEliminar }) {
    const [rechazando, setRechazando] = useState(null); // id de la fila, o 'todas'
    const pendientes = horas.filter(h => h.estado === 'Pendiente');
    const totalHoras = horas.reduce((s, h) => s + h.cantidadHoras, 0);
    const aprobado = horas.filter(h => h.estado === 'Aprobada' || h.estado === 'Pagada').reduce((s, h) => s + (h.montoCalculado || 0), 0);

    const decidir = async (ids, decision, motivo) => {
        await onDecidir(ids, decision, motivo);
        setRechazando(null);
    };

    return (
        <section aria-busy={ocupado || undefined} className={`bg-navy-900 border rounded-xl overflow-hidden ${ocupado ? 'border-primary-500/60' : 'border-navy-700'}`}>
            <header className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 border-b border-navy-700">
                <div className="flex items-center gap-2 min-w-0">
                    {ocupado && <Loader2 className="w-4 h-4 text-primary-400 animate-spin shrink-0" />}
                    <h3 className="font-semibold text-gray-100 truncate">{nombre}</h3>
                </div>
                <span className="text-sm text-gray-400">
                    {totalHoras.toFixed(2)} h
                    {pendientes.length > 0 && <> · <span className="text-amber-300">{pendientes.length} por decidir</span></>}
                    {aprobado > 0 && <> · aprobado <span className="font-mono text-gray-200">{formatCurrency(aprobado)}</span></>}
                </span>
                {puedeDecidir && pendientes.length > 1 && rechazando !== 'todas' && (
                    <div className="flex gap-2 ml-auto">
                        <Accion icon={Check} disabled={ocupado} onClick={() => decidir(pendientes.map(h => h.id), 'aprobar')}>
                            Aprobar las {pendientes.length}
                        </Accion>
                        <Accion icon={X} peligro disabled={ocupado} onClick={() => setRechazando('todas')}>
                            Rechazar las {pendientes.length}
                        </Accion>
                    </div>
                )}
                {rechazando === 'todas' && (
                    <RazonRechazo
                        etiqueta={`razon-todas-${horas[0]?.empleadoId}`}
                        ocupado={ocupado}
                        onCancelar={() => setRechazando(null)}
                        onConfirmar={motivo => decidir(pendientes.map(h => h.id), 'rechazar', motivo)}
                    />
                )}
            </header>

            <ul className="divide-y divide-navy-800">
                {horas.map(h => (
                    <li key={h.id} className="px-4 py-2.5 flex flex-wrap items-center gap-x-4 gap-y-2">
                        <div className="basis-full md:basis-0 flex-1 min-w-0">
                            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
                                <span className="w-24 text-gray-300">{formatDate(h.fecha)}</span>
                                <span className="w-32 text-gray-400 truncate" title={h.tipoNombre}>{h.tipoNombre}</span>
                                <span className="w-24 text-gray-400 font-mono">{hhmm(h.horaInicio)}–{hhmm(h.horaFin)}</span>
                                <span className="w-12 text-right text-gray-200 font-mono">{h.cantidadHoras.toFixed(2)}</span>
                                <span className="w-24 text-right text-gray-200 font-mono">{formatCurrency(h.montoCalculado)}</span>
                                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ESTADO[h.estado]}`}>{h.estado}</span>
                                {h.esExceso && <span className="text-xs text-red-300" title="Pasa de 3 h en el día o 9 en la semana">exceso</span>}
                            </div>
                            <p className="mt-0.5 text-xs text-gray-500 truncate" title={h.estado === 'Rechazada' ? h.motivoRechazo || '' : h.motivo}>
                                {h.estado === 'Rechazada'
                                    ? <>Rechazó {h.rechazadoPor}{h.motivoRechazo ? `: ${h.motivoRechazo}` : ''}</>
                                    : (h.estado === 'Aprobada' || h.estado === 'Pagada') && h.aprobadoPor
                                        ? <>Aprobó {h.aprobadoPor} · {h.motivo}</>
                                        : h.motivo}
                            </p>
                        </div>

                        <span className="flex gap-1.5 shrink-0">
                            {puedeDecidir && h.estado === 'Pendiente' && rechazando !== h.id && (
                                <>
                                    <Accion icon={Check} disabled={ocupado} onClick={() => decidir([h.id], 'aprobar')}>Aprobar</Accion>
                                    <Accion icon={X} peligro disabled={ocupado} onClick={() => setRechazando(h.id)}>Rechazar</Accion>
                                </>
                            )}
                            {puedeDecidir && (h.estado === 'Aprobada' || h.estado === 'Rechazada') && (
                                <Accion icon={RotateCcw} disabled={ocupado} onClick={() => decidir([h.id], 'pendiente')}>A pendiente</Accion>
                            )}
                            {puedeEditar && (h.estado === 'Pendiente' || h.estado === 'Rechazada') && (
                                <Accion icon={Pencil} disabled={ocupado} onClick={() => onEditar(h)}>Corregir</Accion>
                            )}
                            {puedeEditar && h.estado === 'Pendiente' && (
                                <button type="button" onClick={() => onEliminar(h)} disabled={ocupado} aria-label="Eliminar, fue un error"
                                    title="Eliminar (solo si se registró por error)"
                                    className="p-1 rounded text-gray-500 hover:text-red-300 hover:bg-red-900/30 disabled:opacity-50">
                                    <Trash2 className="w-3.5 h-3.5" />
                                </button>
                            )}
                        </span>

                        {rechazando === h.id && (
                            <RazonRechazo
                                etiqueta={`razon-${h.id}`}
                                ocupado={ocupado}
                                onCancelar={() => setRechazando(null)}
                                onConfirmar={motivo => decidir([h.id], 'rechazar', motivo)}
                            />
                        )}
                    </li>
                ))}
            </ul>
        </section>
    );
}
