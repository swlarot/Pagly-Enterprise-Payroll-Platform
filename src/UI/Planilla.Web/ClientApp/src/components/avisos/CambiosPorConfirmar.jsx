import React, { useCallback, useEffect, useState } from 'react';
import { Check, X, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatCurrency } from '../../utils/currency';
import { formatDate } from '../../utils/date';

// ============================================================
// Cambios de salario y bajas que esperan a otra persona (si la empresa
// activó la confirmación). Quien puede, confirma o dice por qué no; quien
// los pidió puede retirarlos. Los ya resueltos que pidió quedan a la vista
// con su resultado.
// ============================================================

export default function CambiosPorConfirmar() {
    const [cambios, setCambios] = useState(null);
    const [ocupado, setOcupado] = useState(null);
    const [rechazando, setRechazando] = useState(null);
    const [razon, setRazon] = useState('');

    const cargar = useCallback(async () => {
        try {
            const r = await api.get('/api/cambios-pendientes');
            setCambios(Array.isArray(r) ? r : []);
        } catch {
            setCambios([]);
        }
    }, []);

    useEffect(() => { cargar(); }, [cargar]);

    const accion = async (c, que, cuerpo) => {
        setOcupado(c.id);
        try {
            await api.post(`/api/cambios-pendientes/${c.id}/${que}`, cuerpo);
            toast.success({ confirmar: 'Cambio aplicado', rechazar: 'No se aplicó; quien lo pidió verá tu razón', retirar: 'Pedido retirado' }[que]);
            setRechazando(null);
            setRazon('');
            await cargar();
        } catch (e) {
            toast.error(e.message || 'No se pudo completar');
        } finally {
            setOcupado(null);
        }
    };

    if (cambios == null || cambios.length === 0) return null;

    const que = (c) => c.tipo === 'Salario'
        ? <>Salario de <span className="text-gray-100">{c.empleado}</span>: <span className="font-mono">{formatCurrency(c.salarioAnterior)}</span> → <span className="font-mono text-gray-100">{formatCurrency(c.salarioNuevo)}</span></>
        : <>Baja de <span className="text-gray-100">{c.empleado}</span></>;

    return (
        <section aria-labelledby="cambios-titulo" className="space-y-3">
            <h2 id="cambios-titulo" className="text-lg font-semibold text-gray-100">
                Cambios por confirmar <span className="text-sm font-normal text-gray-500">{cambios.filter(c => c.estado === 'Pendiente').length}</span>
            </h2>
            <ul className="space-y-2">
                {cambios.map(c => (
                    <li key={c.id} aria-busy={ocupado === c.id || undefined}
                        className={`bg-navy-900 border rounded-xl px-4 py-3 space-y-2 text-sm ${ocupado === c.id ? 'border-primary-500/60' : 'border-navy-700'} ${c.estado !== 'Pendiente' ? 'opacity-70' : ''}`}>
                        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                            {ocupado === c.id && <Loader2 className="w-4 h-4 text-primary-400 animate-spin" />}
                            <span className="text-gray-300">{que(c)}</span>
                            <span className="text-xs text-gray-500">{c.esMio ? 'lo pediste tú' : `pedido por ${c.solicitadoPor}`} · {formatDate(c.createdAt)}</span>
                            {c.estado !== 'Pendiente' && (
                                <span className="text-xs px-2 py-0.5 rounded-full bg-navy-800 border border-navy-600 text-gray-300">
                                    {c.estado}{c.resueltoPor ? ` por ${c.resueltoPor}` : ''}
                                </span>
                            )}
                        </div>
                        {c.comentario && <p className="text-xs text-gray-400">«{c.comentario}»</p>}

                        {c.estado === 'Pendiente' && (
                            rechazando === c.id ? (
                                <div className="flex flex-wrap items-center gap-2">
                                    <label className="sr-only" htmlFor={`razon-cambio-${c.id}`}>Por qué no se aplica</label>
                                    <input id={`razon-cambio-${c.id}`} autoFocus value={razon} maxLength={500} onChange={e => setRazon(e.target.value)}
                                        placeholder="Por qué no se aplica (lo verá quien lo pidió)"
                                        className="flex-1 min-w-[12rem] px-2 py-1.5 text-sm bg-navy-800 border border-navy-600 rounded-md text-gray-200 focus:outline-none focus:border-gray-400" />
                                    <button onClick={() => accion(c, 'rechazar', { comentario: razon })} disabled={!razon.trim()}
                                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200 disabled:opacity-50">
                                        <X className="w-3.5 h-3.5" /> No aplicar
                                    </button>
                                    <button onClick={() => setRechazando(null)} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                                </div>
                            ) : (
                                <div className="flex flex-wrap items-center gap-2">
                                    {c.puedoConfirmar && (
                                        <>
                                            <button onClick={() => accion(c, 'confirmar')} disabled={ocupado != null}
                                                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-primary-600 hover:bg-primary-700 text-white disabled:opacity-50">
                                                <Check className="w-3.5 h-3.5" /> Confirmar y aplicar
                                            </button>
                                            <button onClick={() => { setRechazando(c.id); setRazon(''); }} disabled={ocupado != null}
                                                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                                                <X className="w-3.5 h-3.5" /> No aplicar
                                            </button>
                                        </>
                                    )}
                                    {c.esMio && (
                                        <>
                                            <span className="text-xs text-gray-500">Esperando que otra persona lo confirme.</span>
                                            <button onClick={() => accion(c, 'retirar')} disabled={ocupado != null}
                                                className="text-xs text-gray-400 hover:text-gray-200 underline-offset-2 hover:underline">Retirar</button>
                                        </>
                                    )}
                                </div>
                            )
                        )}
                    </li>
                ))}
            </ul>
        </section>
    );
}
