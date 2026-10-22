import React, { useCallback, useEffect, useState } from 'react';
import { Send, Check, Undo2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';

// ============================================================
// «Pedir revisión» dentro de las acciones de una fila (planilla, décimo,
// liquidación u horas extra del mes), con la última revisión a la vista:
// en revisión con quién, devuelta y por qué, o revisada.
//
// Sitio donde va: la fila de acciones, que es flex-wrap. El botón y el
// estado ocupan su sitio en la línea; el formulario (a quién y qué mirar)
// y la respuesta del revisor bajan a una línea propia con basis-full.
// Quien revisa puede aprobar o devolver desde la misma fila.
// ============================================================

const PENDIENTE = 0, APROBADA = 1, DEVUELTA = 2;

const boton = 'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200';
const campo = 'px-2 py-1.5 text-sm bg-navy-800 border border-navy-600 rounded-md text-gray-200 focus:outline-none focus:border-gray-400';

export default function PedirRevision({ tipo, entidadId, disabled }) {
    const { user } = useAuth();
    const [ultima, setUltima] = useState(null);
    const [modo, setModo] = useState(null);           // null | 'pedir' | 'devolver'
    const [revisores, setRevisores] = useState(null);
    const [revisorId, setRevisorId] = useState('');
    const [texto, setTexto] = useState('');
    const [enviando, setEnviando] = useState(false);

    const cargar = useCallback(async () => {
        try {
            const h = await api.get(`/api/revisiones?tipo=${tipo}&entidadId=${entidadId}`);
            setUltima(Array.isArray(h) && h.length > 0 ? h[0] : null);
        } catch { /* sin historial visible: el botón sigue disponible */ }
    }, [tipo, entidadId]);

    useEffect(() => { cargar(); }, [cargar]);

    const abrirPedir = async (e) => {
        e.stopPropagation();
        setModo('pedir');
        setTexto('');
        if (revisores == null) {
            try {
                const r = await api.get('/api/revisiones/revisores');
                setRevisores(r);
                if (r.length === 1) setRevisorId(r[0].userId);
            } catch (err) {
                toast.error(err.message || 'No se pudo cargar quién puede revisar');
                setRevisores([]);
            }
        }
    };

    const enviar = async (accion) => {
        setEnviando(true);
        try {
            if (accion === 'pedir') {
                await api.post('/api/revisiones', { tipo, entidadId, revisorId, comentario: texto || null });
                toast.success('Revisión pedida; le llegó un aviso y un correo');
            } else if (accion === 'aprobar') {
                await api.post(`/api/revisiones/${ultima.id}/aprobar`, { comentario: texto || null });
                toast.success('Revisión aprobada');
            } else if (accion === 'devolver') {
                await api.post(`/api/revisiones/${ultima.id}/devolver`, { comentario: texto });
                toast.success('Devuelta con tu comentario');
            } else if (accion === 'retirar') {
                await api.post(`/api/revisiones/${ultima.id}/retirar`);
                toast.success('Pedido de revisión retirado');
            }
            setModo(null);
            setTexto('');
            await cargar();
        } catch (err) {
            toast.error(err.message || 'No se pudo completar');
        } finally {
            setEnviando(false);
        }
    };

    const pendiente = ultima?.estado === PENDIENTE;
    const soyRevisor = pendiente && ultima.revisorId === user?.userId;
    const soySolicitante = pendiente && ultima.solicitadoPorId === user?.userId;
    const parar = (e) => e.stopPropagation();

    return (
        <>
            {ultima && (
                <span
                    className="text-xs text-gray-400 max-w-[22rem] truncate"
                    title={ultima.estado === DEVUELTA ? ultima.respuesta : ultima.comentario || ''}
                >
                    {ultima.estado === PENDIENTE && <>En revisión con <span className="text-amber-300">{ultima.revisor}</span></>}
                    {ultima.estado === APROBADA && <>Revisada por <span className="text-gray-200">{ultima.revisor}</span></>}
                    {ultima.estado === DEVUELTA && <>Devuelta por <span className="text-gray-200">{ultima.revisor}</span>: {ultima.respuesta}</>}
                </span>
            )}

            {soyRevisor && modo == null && (
                <>
                    <button type="button" onClick={(e) => { parar(e); enviar('aprobar'); }} disabled={disabled || enviando} className={boton}>
                        <Check className="w-3.5 h-3.5" /> Aprobar revisión
                    </button>
                    <button type="button" onClick={(e) => { parar(e); setModo('devolver'); setTexto(''); }} disabled={disabled || enviando} className={boton}>
                        <Undo2 className="w-3.5 h-3.5" /> Devolver
                    </button>
                </>
            )}
            {soySolicitante && (
                <button type="button" onClick={(e) => { parar(e); enviar('retirar'); }} disabled={disabled || enviando}
                    className="text-xs text-gray-500 hover:text-gray-300 underline-offset-2 hover:underline">
                    Retirar pedido
                </button>
            )}
            {!pendiente && modo == null && (
                <button type="button" onClick={abrirPedir} disabled={disabled} className={boton}>
                    <Send className="w-3.5 h-3.5" /> Pedir revisión
                </button>
            )}

            {modo === 'pedir' && (
                <div className="basis-full order-last flex flex-wrap items-center gap-2 pt-1" onClick={parar}>
                    {revisores == null ? (
                        <Loader2 className="w-4 h-4 text-primary-400 animate-spin" aria-label="Cargando" />
                    ) : revisores.length === 0 ? (
                        <p className="text-xs text-gray-400">
                            No hay otra persona con acceso a esta empresa. Invítala desde Usuarios para poder pedirle revisiones.
                        </p>
                    ) : (
                        <>
                            <label className="sr-only" htmlFor={`revisor-${tipo}-${entidadId}`}>Quién revisa</label>
                            <select id={`revisor-${tipo}-${entidadId}`} value={revisorId} onChange={e => setRevisorId(e.target.value)} className={campo}>
                                <option value="">¿Quién revisa?</option>
                                {revisores.map(r => <option key={r.userId} value={r.userId}>{r.nombre}</option>)}
                            </select>
                            <label className="sr-only" htmlFor={`nota-${tipo}-${entidadId}`}>Qué mirar</label>
                            <input id={`nota-${tipo}-${entidadId}`} value={texto} maxLength={1000} onChange={e => setTexto(e.target.value)}
                                placeholder="Qué quieres que mire (opcional)" className={`${campo} flex-1 min-w-[12rem]`} />
                            <button type="button" onClick={() => enviar('pedir')} disabled={!revisorId || enviando}
                                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-primary-600 hover:bg-primary-700 text-white disabled:opacity-50">
                                {enviando ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />} Enviar
                            </button>
                        </>
                    )}
                    <button type="button" onClick={() => setModo(null)} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                </div>
            )}

            {modo === 'devolver' && (
                <div className="basis-full order-last flex flex-wrap items-center gap-2 pt-1" onClick={parar}>
                    <label className="sr-only" htmlFor={`devolver-${ultima.id}`}>Qué hay que corregir</label>
                    <input id={`devolver-${ultima.id}`} autoFocus value={texto} maxLength={1000} onChange={e => setTexto(e.target.value)}
                        placeholder="Qué hay que corregir (lo verá quien la pidió)" className={`${campo} flex-1 min-w-[12rem]`} />
                    <button type="button" onClick={() => enviar('devolver')} disabled={!texto.trim() || enviando} className={boton}>
                        {enviando ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Undo2 className="w-3.5 h-3.5" />} Devolver
                    </button>
                    <button type="button" onClick={() => setModo(null)} className="text-xs text-gray-400 hover:text-gray-200">Cancelar</button>
                </div>
            )}
        </>
    );
}
