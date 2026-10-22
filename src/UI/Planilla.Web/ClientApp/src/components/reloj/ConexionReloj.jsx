import React, { useCallback, useEffect, useState } from 'react';
import { Plus, Loader2, Wifi, WifiOff } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { formatDate } from '../../utils/date';

// ============================================================
// Conexión automática del reloj (opción «servidor en la nube» / ADMS):
// qué escribir en el menú del reloj y la lista de relojes registrados.
// Solo se aceptan marcas de relojes registrados y activos; a Pagly solo
// llega quién marcó y a qué hora.
// ============================================================

function haceCuanto(fecha) {
    if (!fecha) return 'nunca';
    const min = Math.round((Date.now() - new Date(fecha).getTime()) / 60000);
    if (min < 2) return 'hace un momento';
    if (min < 60) return `hace ${min} min`;
    const h = Math.round(min / 60);
    if (h < 24) return `hace ${h} h`;
    return formatDate(fecha);
}

const control = 'bg-navy-800 border border-navy-600 text-gray-100 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 h-[34px]';

export default function ConexionReloj() {
    const [conexion, setConexion] = useState(null);
    const [relojes, setRelojes] = useState([]);
    const [nuevo, setNuevo] = useState({ numeroSerie: '', nombre: '' });
    const [guardando, setGuardando] = useState(false);
    const [ocupado, setOcupado] = useState(null);

    const cargar = useCallback(async () => {
        try {
            const [c, d] = await Promise.all([api.get('/api/reloj/conexion'), api.get('/api/reloj/dispositivos')]);
            setConexion(c);
            setRelojes(Array.isArray(d) ? d : []);
        } catch (e) {
            toast.error(e.message || 'No se pudo cargar la conexión del reloj');
        }
    }, []);

    useEffect(() => { cargar(); }, [cargar]);

    const registrar = async () => {
        setGuardando(true);
        try {
            await api.post('/api/reloj/dispositivos', nuevo);
            toast.success('Reloj registrado; sus marcas entrarán en cuanto se conecte');
            setNuevo({ numeroSerie: '', nombre: '' });
            await cargar();
        } catch (e) {
            toast.error(e.message || 'No se pudo registrar el reloj');
        } finally {
            setGuardando(false);
        }
    };

    const cambiar = async (r) => {
        setOcupado(r.id);
        try {
            await api.post(`/api/reloj/dispositivos/${r.id}/${r.activo ? 'desactivar' : 'activar'}`);
            await cargar();
        } catch (e) {
            toast.error(e.message || 'No se pudo cambiar el reloj');
        } finally {
            setOcupado(null);
        }
    };

    return (
        <section className="space-y-3" aria-labelledby="conexion-titulo">
            <h2 id="conexion-titulo" className="text-lg font-semibold text-gray-100">Conexión automática del reloj</h2>
            <p className="text-sm text-gray-400">
                Si el reloj tiene la opción «Servidor en la nube» (ADMS), envía cada marcación solo y no hace falta bajar el archivo.
                A Pagly solo llega quién marcó y a qué hora: las huellas y los rostros se quedan en el reloj.
            </p>

            {conexion?.configurado ? (
                <ol className="list-decimal pl-5 text-sm text-gray-300 space-y-1">
                    <li>En el reloj: <span className="text-gray-100">Menú → Comunicación → Servidor en la nube</span> (puede decir «ADMS» o «Cloud Server»).</li>
                    <li>
                        Dirección del servidor: <span className="font-mono text-gray-100">{conexion.servidor}</span>
                        {' · '}Puerto: <span className="font-mono text-gray-100">{conexion.puerto}</span>
                        {' · '}Usar nombre de dominio: <span className="text-gray-100">Sí</span>
                        {conexion.https && <> · HTTPS: <span className="text-gray-100">Sí</span></>}
                    </li>
                    <li>Registra aquí su número de serie (está en <span className="text-gray-100">Menú → Información del sistema</span> o en la etiqueta trasera).</li>
                </ol>
            ) : (
                <p className="text-sm text-amber-200">Falta configurar la dirección de Pagly en el servidor; avísanos para activarlo.</p>
            )}
            <p className="text-xs text-gray-500">Si el reloj no ofrece HTTPS, avísanos: hay que habilitarle la conexión sin cifrar solo para el reloj.</p>

            <div className="flex flex-wrap items-end gap-3">
                <div className="w-[200px]">
                    <label htmlFor="rel-sn" className="block text-xs text-gray-400 mb-1">Número de serie</label>
                    <input id="rel-sn" value={nuevo.numeroSerie} maxLength={50} onChange={e => setNuevo(n => ({ ...n, numeroSerie: e.target.value }))}
                        placeholder="CKPG212345678" className={`${control} w-full font-mono uppercase`} />
                </div>
                <div className="w-[220px]">
                    <label htmlFor="rel-nombre" className="block text-xs text-gray-400 mb-1">Nombre</label>
                    <input id="rel-nombre" value={nuevo.nombre} maxLength={100} onChange={e => setNuevo(n => ({ ...n, nombre: e.target.value }))}
                        placeholder="Entrada principal" className={`${control} w-full`} />
                </div>
                <button onClick={registrar} disabled={guardando || nuevo.numeroSerie.trim().length < 5}
                    className="h-[34px] inline-flex items-center gap-2 px-4 rounded-lg text-sm font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-100 disabled:opacity-50">
                    {guardando ? <Loader2 className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />} Registrar reloj
                </button>
            </div>

            {relojes.length > 0 && (
                <ul className="space-y-2">
                    {relojes.map(r => (
                        <li key={r.id} aria-busy={ocupado === r.id || undefined}
                            className={`bg-navy-900 border border-navy-700 rounded-xl px-4 py-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm ${r.activo ? '' : 'opacity-60'}`}>
                            {r.activo && r.ultimaConexion ? <Wifi className="w-4 h-4 text-gray-300" /> : <WifiOff className="w-4 h-4 text-gray-500" />}
                            <span className="font-medium text-gray-100">{r.nombre}</span>
                            <span className="font-mono text-xs text-gray-500">{r.numeroSerie}</span>
                            <span className="text-gray-400">{r.activo ? `última conexión ${haceCuanto(r.ultimaConexion)}` : 'desactivado'}</span>
                            <span className="text-gray-500">{r.marcasRecibidas} {r.marcasRecibidas === 1 ? 'marca recibida' : 'marcas recibidas'}</span>
                            <button onClick={() => cambiar(r)} disabled={ocupado === r.id}
                                className="ml-auto inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200 disabled:opacity-50">
                                {ocupado === r.id && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
                                {r.activo ? 'Desactivar' : 'Activar'}
                            </button>
                        </li>
                    ))}
                </ul>
            )}
        </section>
    );
}
