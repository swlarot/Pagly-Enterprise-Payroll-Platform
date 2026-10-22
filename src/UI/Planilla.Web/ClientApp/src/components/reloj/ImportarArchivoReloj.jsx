import React, { useRef, useState } from 'react';
import { Upload, Loader2, FileCheck2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatDate } from '../../utils/date';

// ============================================================
// Subir el archivo del reloj: primero se ve qué trae (cuántas marcas, de
// qué fechas, qué columnas se reconocieron y a quién corresponde cada
// número) y solo al confirmar se guarda. Los números sin dueño se asocian
// aquí mismo, una sola vez. Subir el mismo archivo otra vez no duplica.
// ============================================================

export default function ImportarArchivoReloj({ personas, onImportado, onCerrar }) {
    const [archivo, setArchivo] = useState(null);
    const [progreso, setProgreso] = useState(null);
    const [vista, setVista] = useState(null);
    const [asignaciones, setAsignaciones] = useState({});
    const [importando, setImportando] = useState(false);
    const [arrastrando, setArrastrando] = useState(false);
    const input = useRef(null);

    const elegir = async (f) => {
        if (!f) return;
        setArchivo(f);
        setVista(null);
        setAsignaciones({});
        setProgreso(0);
        try {
            const r = await api.upload('/api/reloj/importar/previsualizar', f, 'archivo', setProgreso);
            setVista(r);
        } catch (e) {
            toast.error(e.message || 'No se pudo leer el archivo');
            setArchivo(null);
        } finally {
            setProgreso(null);
        }
    };

    const importar = async () => {
        setImportando(true);
        try {
            const r = await api.upload('/api/reloj/importar', archivo, 'archivo', undefined,
                { asignaciones: JSON.stringify(Object.fromEntries(Object.entries(asignaciones).filter(([, v]) => v))) });
            toast.success(`${r.nuevas} marcas nuevas importadas${r.repetidas ? ` · ${r.repetidas} ya estaban` : ''}${r.sinAsignar ? ` · ${r.sinAsignar} sin persona` : ''}`, { duration: 5000 });
            onImportado(vista);
        } catch (e) {
            toast.error(e.message || 'No se pudo importar');
        } finally {
            setImportando(false);
        }
    };

    const ocupados = new Set(Object.values(asignaciones).filter(Boolean).map(Number));
    const sinDueno = vista?.codigos.filter(c => !c.empleadoId) ?? [];

    return (
        <section className="bg-navy-900 border border-navy-700 rounded-xl p-4 space-y-4" aria-busy={progreso != null || importando || undefined}>
            <div className="flex items-center justify-between gap-3">
                <h2 className="text-lg font-semibold text-gray-100">Importar el archivo del reloj</h2>
                <button onClick={onCerrar} aria-label="Cerrar" className="p-1 rounded text-gray-400 hover:text-gray-200"><X className="w-5 h-5" /></button>
            </div>

            {!vista && (
                <div
                    role="button"
                    tabIndex={0}
                    onClick={() => input.current?.click()}
                    onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') input.current?.click(); }}
                    onDragOver={e => { e.preventDefault(); setArrastrando(true); }}
                    onDragLeave={() => setArrastrando(false)}
                    onDrop={e => { e.preventDefault(); setArrastrando(false); elegir(e.dataTransfer.files?.[0]); }}
                    className={`rounded-lg border-2 border-dashed px-4 py-8 text-center cursor-pointer ${arrastrando ? 'border-primary-500 bg-primary-500/5' : 'border-navy-600 hover:border-navy-500'}`}
                >
                    {progreso != null ? (
                        <div className="max-w-xs mx-auto space-y-2">
                            <p className="text-sm text-gray-300">Leyendo {archivo?.name}…</p>
                            <div className="h-1.5 rounded bg-navy-800 overflow-hidden"><div className="h-full bg-primary-500 transition-all" style={{ width: `${progreso}%` }} /></div>
                        </div>
                    ) : (
                        <>
                            <Upload className="w-6 h-6 text-gray-500 mx-auto mb-2" />
                            <p className="text-sm text-gray-200">Arrastra aquí el archivo o haz clic para elegirlo</p>
                            <p className="text-xs text-gray-500 mt-1">El .dat que sale del USB del reloj, o el .csv / .xlsx que exporta su programa</p>
                        </>
                    )}
                    <input ref={input} type="file" accept=".dat,.txt,.csv,.xlsx,.xls" className="hidden"
                        onChange={e => elegir(e.target.files?.[0])} aria-label="Archivo del reloj" />
                </div>
            )}

            {vista && (
                <div className="space-y-3">
                    <div className="flex flex-wrap items-start gap-x-4 gap-y-1 text-sm">
                        <FileCheck2 className="w-4 h-4 text-primary-400 mt-0.5" />
                        <span className="text-gray-100">{archivo?.name}</span>
                        <span className="text-gray-400">
                            {vista.totalMarcas} marcas
                            {vista.desde && <> del {formatDate(vista.desde)} al {formatDate(vista.hasta)}</>}
                            {' · '}{vista.nuevas} nuevas{vista.yaImportadas > 0 && <> · {vista.yaImportadas} ya estaban</>}
                        </span>
                        {vista.columnas && <span className="basis-full text-xs text-gray-500">Columnas reconocidas ({vista.formato}): {vista.columnas}</span>}
                    </div>

                    {vista.problemas.length > 0 && (
                        <details className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-200">
                            <summary className="cursor-pointer">{vista.problemas.length} {vista.problemas.length === 1 ? 'línea no se pudo leer' : 'líneas no se pudieron leer'}</summary>
                            <ul className="mt-1 text-xs text-amber-100/90 space-y-0.5">
                                {vista.problemas.map((p, i) => <li key={i}>{p.linea > 0 ? `Línea ${p.linea}: ` : ''}{p.mensaje}</li>)}
                            </ul>
                        </details>
                    )}

                    {vista.codigos.length > 0 && (
                        <div className="overflow-x-auto rounded-lg border border-navy-700">
                            <table className="w-full text-sm">
                                <thead className="bg-navy-950 text-xs text-gray-500">
                                    <tr>
                                        <th className="text-left px-3 py-2 font-medium">Número en el reloj</th>
                                        <th className="text-left px-3 py-2 font-medium">Nombre en el archivo</th>
                                        <th className="text-right px-3 py-2 font-medium">Marcas</th>
                                        <th className="text-left px-3 py-2 font-medium">Es</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-navy-800">
                                    {vista.codigos.map(c => (
                                        <tr key={c.codigo}>
                                            <td className="px-3 py-2 font-mono text-gray-200">{c.codigo}</td>
                                            <td className="px-3 py-2 text-gray-400">{c.nombreEnArchivo || '—'}</td>
                                            <td className="px-3 py-2 text-right font-mono text-gray-300">{c.marcas}</td>
                                            <td className="px-3 py-2">
                                                {c.empleadoId ? <span className="text-gray-200">{c.empleado}</span> : (
                                                    <>
                                                        <label className="sr-only" htmlFor={`asig-${c.codigo}`}>Persona del número {c.codigo}</label>
                                                        <select id={`asig-${c.codigo}`} value={asignaciones[c.codigo] ?? ''}
                                                            onChange={e => setAsignaciones(a => ({ ...a, [c.codigo]: e.target.value ? Number(e.target.value) : '' }))}
                                                            className="bg-navy-800 border border-navy-600 text-gray-100 rounded-md px-2 py-1 text-sm max-w-[16rem]">
                                                            <option value="">Sin asociar todavía</option>
                                                            {personas.filter(p => !p.codigoReloj && (!ocupados.has(p.id) || asignaciones[c.codigo] === p.id))
                                                                .map(p => <option key={p.id} value={p.id}>{p.nombre}</option>)}
                                                        </select>
                                                    </>
                                                )}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    <div className="flex flex-wrap items-center gap-3">
                        <button onClick={importar} disabled={importando || vista.totalMarcas === 0}
                            className="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white disabled:opacity-50">
                            {importando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                            {vista.nuevas > 0 ? `Importar ${vista.nuevas} marcas` : 'Guardar las asociaciones'}
                        </button>
                        <button onClick={() => { setVista(null); setArchivo(null); }} className="text-sm text-gray-400 hover:text-gray-200">Elegir otro archivo</button>
                        {sinDueno.length > 0 && (
                            <span className="text-xs text-gray-500">
                                Los números sin asociar se importan igual y pasan a la persona cuando se le asigne el número.
                            </span>
                        )}
                    </div>
                </div>
            )}
        </section>
    );
}
