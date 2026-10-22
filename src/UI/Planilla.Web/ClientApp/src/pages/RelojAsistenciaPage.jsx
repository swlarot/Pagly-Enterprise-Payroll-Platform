import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Upload, Send, Loader2, Search } from 'lucide-react';
import { api } from '../services/api';
import { NOMBRES_MES } from '../utils/periodos';
import { minutosAHoras } from '../utils/reloj';
import SelectorMes from '../components/planillas/SelectorMes';
import ImportarArchivoReloj from '../components/reloj/ImportarArchivoReloj';
import FilaAsistencia from '../components/reloj/FilaAsistencia';
import HorariosReloj from '../components/reloj/HorariosReloj';

// ============================================================
// Reloj de asistencia.
//
// Sitio donde va: manda la asistencia del mes por persona (entrada,
// salida, llegadas tarde, ausencias y posibles horas extra contra su
// horario). Arriba: el mes, «Importar archivo» y «Proponer al director».
// Importar abre su panel encima de la lista; los horarios van al final.
//
// Estados: cargando → esqueleto; importando o proponiendo → el botón con
// su giro y lo demás quieto; fallo → la frase del servidor. Nada se paga
// solo: las horas extra van como pendientes a la decisión del director.
// ============================================================

const CLAVE_MES = 'pagly.reloj.mes';

function mesInicial() {
    try {
        const g = JSON.parse(localStorage.getItem(CLAVE_MES) || 'null');
        if (g?.anio && g?.mes) return g;
    } catch { /* sin memoria del mes: el actual */ }
    const h = new Date();
    return { anio: h.getFullYear(), mes: h.getMonth() + 1 };
}

const iso = (a, m, d) => `${a}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;

export default function RelojAsistenciaPage() {
    const [{ anio, mes }, setPeriodo] = useState(mesInicial);
    const [resumen, setResumen] = useState(null);
    const [cargando, setCargando] = useState(true);
    const [horarios, setHorarios] = useState([]);
    const [personas, setPersonas] = useState([]);
    const [importando, setImportando] = useState(false);
    const [abierta, setAbierta] = useState(null);
    const [proponiendo, setProponiendo] = useState(false);
    const [ocupada, setOcupada] = useState(null);
    const [q, setQ] = useState('');

    const desde = iso(anio, mes, 1);
    const hasta = iso(anio, mes, new Date(anio, mes, 0).getDate());

    const cambiarMes = (a, m) => {
        setPeriodo({ anio: a, mes: m });
        setAbierta(null);
        try { localStorage.setItem(CLAVE_MES, JSON.stringify({ anio: a, mes: m })); } catch { /* sin memoria */ }
    };

    const cargar = useCallback(async (silencioso = false) => {
        try {
            if (!silencioso) setCargando(true);
            const [r, h, p] = await Promise.all([
                api.get(`/api/reloj/resumen?desde=${desde}&hasta=${hasta}`),
                api.get('/api/reloj/horarios'),
                api.get('/api/reloj/personas'),
            ]);
            setResumen(r);
            setHorarios(Array.isArray(h) ? h : []);
            setPersonas(Array.isArray(p) ? p : []);
        } catch (e) {
            toast.error(e.message || 'No se pudo cargar la asistencia');
        } finally {
            setCargando(false);
        }
    }, [desde, hasta]);

    useEffect(() => { cargar(); }, [cargar]);

    const totales = useMemo(() => {
        const e = resumen?.empleados ?? [];
        return {
            personas: e.length,
            tardanzas: e.reduce((s, x) => s + x.tardanzas, 0),
            ausencias: e.reduce((s, x) => s + x.ausencias, 0),
            extra: e.reduce((s, x) => s + x.minutosExtra, 0),
            porProponer: e.reduce((s, x) => s + x.horasExtraPorProponer, 0),
        };
    }, [resumen]);

    const visibles = useMemo(() => {
        const t = q.trim().toLowerCase();
        const e = resumen?.empleados ?? [];
        return t ? e.filter(x => x.nombre.toLowerCase().includes(t) || (x.codigoReloj ?? '').includes(t)) : e;
    }, [resumen, q]);

    const proponer = async () => {
        setProponiendo(true);
        try {
            const r = await api.post('/api/reloj/proponer-horas-extra', { desde, hasta });
            toast.success(r.creadas > 0 ? `${r.creadas} horas extra enviadas al director como pendientes` : 'No había horas extra nuevas que proponer');
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudieron proponer');
        } finally {
            setProponiendo(false);
        }
    };

    const cambiarHorario = async (empleadoId, horarioId) => {
        setOcupada(empleadoId);
        try {
            await api.put('/api/reloj/horarios/asignar', { horarioId, empleadoIds: [empleadoId] });
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo cambiar el horario');
        } finally {
            setOcupada(null);
        }
    };

    const asignarCodigo = async (codigo, empleadoId) => {
        try {
            await api.put('/api/reloj/codigos', { codigo, empleadoId });
            toast.success(`El número ${codigo} quedó asociado`);
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo asociar el número');
        }
    };

    const titulo = `${NOMBRES_MES[mes - 1]} ${anio}`;
    const sinCodigoLibres = personas.filter(p => !p.codigoReloj);

    return (
        <div className="space-y-5">
            <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold text-white">Reloj de asistencia</h1>
                    <p className="text-gray-400 mt-1">Las marcaciones del reloj contra el horario de cada persona</p>
                </div>
                <SelectorMes anio={anio} mes={mes} onChange={cambiarMes} />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
                {!cargando && resumen && (
                    <p className="text-sm text-gray-400">
                        {totales.personas === 0 ? <>Sin marcaciones en {titulo}.</> : (
                            <>
                                <span className="text-gray-200">{totales.personas} {totales.personas === 1 ? 'persona' : 'personas'}</span>
                                {totales.tardanzas > 0 && <> · <span className="text-amber-300">{totales.tardanzas} {totales.tardanzas === 1 ? 'llegada tarde' : 'llegadas tarde'}</span></>}
                                {totales.ausencias > 0 && <> · <span className="text-red-300">{totales.ausencias} {totales.ausencias === 1 ? 'ausencia' : 'ausencias'}</span></>}
                                {' · '}posibles horas extra <span className="font-mono text-gray-200">{minutosAHoras(totales.extra)}</span>
                            </>
                        )}
                    </p>
                )}
                <div className="flex gap-2 ml-auto">
                    {totales.porProponer > 0 && (
                        <button onClick={proponer} disabled={proponiendo}
                            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200 disabled:opacity-50">
                            {proponiendo ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                            Proponer {totales.porProponer} al director
                        </button>
                    )}
                    {!importando && (
                        <button onClick={() => setImportando(true)}
                            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white">
                            <Upload className="w-4 h-4" /> Importar archivo
                        </button>
                    )}
                </div>
            </div>

            {importando && (
                <ImportarArchivoReloj
                    personas={personas}
                    onCerrar={() => setImportando(false)}
                    onImportado={(vista) => {
                        setImportando(false);
                        // Lleva al mes de las marcas importadas.
                        if (vista?.hasta) {
                            const f = new Date(vista.hasta);
                            if (f.getFullYear() !== anio || f.getMonth() + 1 !== mes) cambiarMes(f.getFullYear(), f.getMonth() + 1);
                            else cargar(true);
                        } else cargar(true);
                    }}
                />
            )}

            {!cargando && resumen && !resumen.hayHorario && (
                <p className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-200">
                    Todavía no hay un horario: se ven las marcas, pero no las llegadas tarde ni las horas extra. Créalo abajo, en Horarios.
                </p>
            )}

            {!cargando && resumen?.codigosSinAsignar.length > 0 && (
                <div className="rounded-lg border border-navy-600 bg-navy-900 px-3 py-2 text-sm space-y-2">
                    <p className="text-gray-300">Hay números del reloj sin persona; sus marcas pasan a su ficha en cuanto se asocian:</p>
                    <ul className="flex flex-wrap gap-3">
                        {resumen.codigosSinAsignar.map(c => (
                            <li key={c.codigo} className="flex items-center gap-2">
                                <span className="font-mono text-gray-200">n.º {c.codigo}</span>
                                <span className="text-xs text-gray-500">{c.marcas} marcas</span>
                                <label className="sr-only" htmlFor={`sin-${c.codigo}`}>Persona del número {c.codigo}</label>
                                <select id={`sin-${c.codigo}`} defaultValue="" onChange={e => e.target.value && asignarCodigo(c.codigo, Number(e.target.value))}
                                    className="bg-navy-800 border border-navy-600 text-gray-100 rounded-md px-2 py-1 text-sm">
                                    <option value="">¿De quién es?</option>
                                    {sinCodigoLibres.map(p => <option key={p.id} value={p.id}>{p.nombre}</option>)}
                                </select>
                            </li>
                        ))}
                    </ul>
                </div>
            )}

            {cargando ? (
                <div className="space-y-3" aria-busy="true">
                    {[0, 1, 2].map(i => (
                        <div key={i} className="h-14 rounded-xl bg-navy-900 border border-navy-700 overflow-hidden">
                            <div className="h-1 bg-primary-500/60 animate-pulse w-1/3" />
                        </div>
                    ))}
                </div>
            ) : (
                <>
                    {(resumen?.empleados.length ?? 0) > 8 && (
                        <div className="relative w-[260px] max-w-full">
                            <Search className="w-4 h-4 text-gray-500 absolute left-2.5 top-1/2 -translate-y-1/2" />
                            <label htmlFor="reloj-buscar" className="sr-only">Buscar persona</label>
                            <input id="reloj-buscar" value={q} onChange={e => setQ(e.target.value)} placeholder="Nombre o número"
                                className="w-full pl-8 pr-2.5 py-1.5 text-sm bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500" />
                        </div>
                    )}
                    <ul className="space-y-2">
                        {visibles.map(p => (
                            <FilaAsistencia
                                key={p.empleadoId}
                                persona={p}
                                horarios={horarios}
                                abierta={abierta === p.empleadoId}
                                ocupada={ocupada === p.empleadoId}
                                onAbrir={() => setAbierta(a => (a === p.empleadoId ? null : p.empleadoId))}
                                onCambiarHorario={h => cambiarHorario(p.empleadoId, h)}
                            />
                        ))}
                    </ul>
                    {resumen?.empleadosSinCodigo > 0 && (
                        <p className="text-xs text-gray-500">
                            {resumen.empleadosSinCodigo} {resumen.empleadosSinCodigo === 1 ? 'persona activa no tiene' : 'personas activas no tienen'} número del reloj todavía.
                        </p>
                    )}
                </>
            )}

            <div className="pt-4 border-t border-navy-800">
                <HorariosReloj horarios={horarios} onCambio={() => cargar(true)} />
            </div>
        </div>
    );
}
