import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Plus, FileDown } from 'lucide-react';
import { api } from '../services/api';
import { leerEnlaceMes, useEnlaceMes } from '../utils/enlaceMes';
import { useAuth } from '../contexts/AuthContext';
import ConfirmModal from '../components/ConfirmModal';
import { formatCurrency } from '../utils/currency';
import { NOMBRES_MES } from '../utils/periodos';
import SelectorMes from '../components/planillas/SelectorMes';
import GrupoColaborador from '../components/horasExtra/GrupoColaborador';
import FormularioHoraExtra from '../components/horasExtra/FormularioHoraExtra';
import PedirRevision from '../components/revisiones/PedirRevision';

// ============================================================
// Horas extra por mes, con la decisión del director.
//
// Sitio donde va: manda la lista del mes agrupada por colaborador. Arriba,
// el selector de mes, una línea de resumen y un filtro por estado; quien
// tiene el permiso de aprobar abre en «Por decidir». Registrar abre el
// formulario; el reporte interno del mes se descarga desde aquí.
//
// Estados: cargando el mes → esqueleto; decidiendo → ese colaborador con
// el borde y su giro, los demás sin acciones; fallo → aviso con la frase
// del servidor. Lo aprobado entra a la planilla del período; lo rechazado
// se guarda con su razón y no se paga.
// ============================================================

const CLAVE_MES = 'pagly.horasExtra.mes';
const FILTROS = [
    { clave: 'Pendiente', label: 'Por decidir' },
    { clave: 'Aprobada', label: 'Aprobadas' },
    { clave: 'Rechazada', label: 'Rechazadas' },
    { clave: 'Pagada', label: 'Pagadas' },
    { clave: '', label: 'Todas' },
];

function mesInicial() {
  const enlace = leerEnlaceMes(window.location.search);
  if (enlace) return { anio: enlace.anio, mes: enlace.mes };
    try {
        const g = JSON.parse(localStorage.getItem(CLAVE_MES) || 'null');
        if (g?.anio && g?.mes) return g;
    } catch { /* sin memoria del mes: el actual */ }
    const h = new Date();
    return { anio: h.getFullYear(), mes: h.getMonth() + 1 };
}

export default function HorasExtraPage() {
    const { canWrite, hasPermission } = useAuth();
    const puedeDecidir = hasPermission('overtime.approve');
    const puedeEditar = canWrite() && hasPermission('overtime.manage');

    const [{ anio, mes }, setPeriodo] = useState(mesInicial);
    const [horas, setHoras] = useState([]);
    const [cargando, setCargando] = useState(true);
    const [filtro, setFiltro] = useState(puedeDecidir ? 'Pendiente' : '');
    const [ocupado, setOcupado] = useState(null);          // empleadoId que se está decidiendo
    const [empleados, setEmpleados] = useState([]);
    const [tipos, setTipos] = useState([]);
    const [formulario, setFormulario] = useState(null);    // null | { editando }
    const [aEliminar, setAEliminar] = useState(null);
    const [eliminando, setEliminando] = useState(false);

    const cambiarMes = (a, m) => {
        setPeriodo({ anio: a, mes: m });
        try { localStorage.setItem(CLAVE_MES, JSON.stringify({ anio: a, mes: m })); } catch { /* sin memoria */ }
    };

    // Un aviso o un correo puede pedir un mes concreto.
    useEnlaceMes(({ anio: a, mes: m }) => cambiarMes(a, m));

    const cargar = useCallback(async (silencioso = false) => {
        try {
            if (!silencioso) setCargando(true);
            const data = await api.get(`/api/horasextra?anio=${anio}&mes=${mes}`);
            setHoras(Array.isArray(data) ? data : []);
        } catch (e) {
            toast.error(e.message || 'No se pudieron cargar las horas extra del mes');
        } finally {
            setCargando(false);
        }
    }, [anio, mes]);

    useEffect(() => { cargar(); }, [cargar]);

    // Los permisos llegan después del primer render: el director abre en «Por decidir».
    useEffect(() => { if (puedeDecidir) setFiltro('Pendiente'); }, [puedeDecidir]);

    useEffect(() => {
        if (!puedeEditar) return;
        api.get('/api/empleados').then(d => setEmpleados(d.filter(e => e.estaActivo))).catch(() => { });
        api.get('/api/horasextra/tipos').then(setTipos).catch(() => { });
    }, [puedeEditar]);

    const resumen = useMemo(() => {
        const de = (estado) => horas.filter(h => h.estado === estado);
        return {
            total: horas.length,
            porDecidir: de('Pendiente').length,
            aprobado: [...de('Aprobada'), ...de('Pagada')].reduce((s, h) => s + (h.montoCalculado || 0), 0),
            horasRechazadas: de('Rechazada').reduce((s, h) => s + h.cantidadHoras, 0),
            conteo: Object.fromEntries(FILTROS.map(f => [f.clave, f.clave ? de(f.clave).length : horas.length])),
        };
    }, [horas]);

    // Si no queda nada por decidir, el filtro no se queda en una lista vacía.
    const filtroEfectivo = filtro === 'Pendiente' && resumen.porDecidir === 0 && !cargando ? '' : filtro;

    const grupos = useMemo(() => {
        const visibles = filtroEfectivo ? horas.filter(h => h.estado === filtroEfectivo) : horas;
        const porEmpleado = new Map();
        for (const h of [...visibles].sort((a, b) => a.fecha.localeCompare(b.fecha) || a.horaInicio.localeCompare(b.horaInicio))) {
            if (!porEmpleado.has(h.empleadoId)) porEmpleado.set(h.empleadoId, { nombre: h.empleadoNombre, horas: [] });
            porEmpleado.get(h.empleadoId).horas.push(h);
        }
        return [...porEmpleado.entries()].sort((a, b) => a[1].nombre.localeCompare(b[1].nombre));
    }, [horas, filtroEfectivo]);

    const decidir = async (empleadoId, ids, decision, motivo) => {
        setOcupado(empleadoId);
        try {
            await api.post('/api/horasextra/decidir', { ids, decision, motivo: motivo || null });
            const n = ids.length;
            toast.success({
                aprobar: n === 1 ? 'Hora extra aprobada' : `${n} horas extra aprobadas`,
                rechazar: n === 1 ? 'Hora extra rechazada; queda guardada con su razón' : `${n} horas extra rechazadas`,
                pendiente: 'Volvió a pendiente',
            }[decision]);
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo guardar la decisión');
        } finally {
            setOcupado(null);
        }
    };

    const eliminar = async () => {
        try {
            setEliminando(true);
            await api.delete(`/api/horasextra/${aEliminar.id}`);
            toast.success('Hora extra eliminada');
            setAEliminar(null);
            await cargar(true);
        } catch (e) {
            toast.error(e.message || 'No se pudo eliminar');
        } finally {
            setEliminando(false);
        }
    };

    const descargarReporte = async () => {
        try {
            await api.download(`/api/reportes/horas-extra/excel?mes=${mes}&anio=${anio}`, `HorasExtra_${anio}-${String(mes).padStart(2, '0')}.xlsx`);
        } catch (e) {
            toast.error(e.message || 'No se pudo descargar el reporte');
        }
    };

    const titulo = `${NOMBRES_MES[mes - 1]} ${anio}`;

    return (
        <div className="space-y-5">
            <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold text-white">Horas extra</h1>
                    <p className="text-gray-400 mt-1">Lo que se registra, lo que decide el director y lo que se paga</p>
                </div>
                <SelectorMes anio={anio} mes={mes} onChange={cambiarMes} />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3">
                {!cargando && (
                    <p className="text-sm text-gray-400">
                        {resumen.total === 0 ? <>Sin horas extra en {titulo}.</> : (
                            <>
                                <span className="text-gray-200">{resumen.total} {resumen.total === 1 ? 'registro' : 'registros'}</span>
                                {resumen.porDecidir > 0 && <> · <span className="text-amber-300">{resumen.porDecidir} por decidir</span></>}
                                {' · '}Aprobado <span className="font-mono text-gray-200">{formatCurrency(resumen.aprobado)}</span>
                                {resumen.horasRechazadas > 0 && <> · {resumen.horasRechazadas.toFixed(2)} h rechazadas</>}
                            </>
                        )}
                    </p>
                )}
                <div className="flex gap-2 ml-auto">
                    {resumen.total > 0 && (
                        <button onClick={descargarReporte}
                            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-navy-800 hover:bg-navy-700 border border-navy-600 text-gray-200">
                            <FileDown className="w-4 h-4" /> Reporte interno
                        </button>
                    )}
                    {puedeEditar && (
                        <button onClick={() => setFormulario({ editando: null })}
                            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-primary-600 hover:bg-primary-700 text-white">
                            <Plus className="w-4 h-4" /> Registrar horas extra
                        </button>
                    )}
                </div>
            </div>

            {resumen.total > 0 && (
                <div className="flex flex-wrap items-center gap-2">
                    <PedirRevision key={`${anio}-${mes}`} tipo={4} entidadId={anio * 100 + mes} />
                </div>
            )}

            {resumen.total > 0 && (
                <div role="tablist" aria-label="Filtrar por estado" className="flex flex-wrap gap-1 p-1 bg-navy-900 border border-navy-700 rounded-lg w-fit max-w-full">
                    {FILTROS.map(f => (
                        <button key={f.label} role="tab" aria-selected={filtroEfectivo === f.clave}
                            onClick={() => setFiltro(f.clave)}
                            className={`px-3 py-1.5 rounded-md text-sm transition-colors ${filtroEfectivo === f.clave ? 'bg-navy-700 text-white' : 'text-gray-400 hover:text-gray-200'}`}>
                            {f.label} <span className="text-xs text-gray-500">{resumen.conteo[f.clave]}</span>
                        </button>
                    ))}
                </div>
            )}

            {cargando ? (
                <div className="space-y-3" aria-busy="true">
                    {[0, 1].map(i => (
                        <div key={i} className="h-24 rounded-xl bg-navy-900 border border-navy-700 overflow-hidden">
                            <div className="h-1 bg-primary-500/60 animate-pulse w-1/3" />
                        </div>
                    ))}
                </div>
            ) : grupos.length === 0 ? (
                resumen.total > 0 && <p className="text-sm text-gray-500">Nada en este estado.</p>
            ) : (
                <div className="space-y-3">
                    {grupos.map(([empleadoId, g]) => (
                        <div key={empleadoId} className={ocupado != null && ocupado !== empleadoId ? 'opacity-40 pointer-events-none' : ''}>
                            <GrupoColaborador
                                nombre={g.nombre}
                                horas={g.horas}
                                puedeDecidir={puedeDecidir}
                                puedeEditar={puedeEditar}
                                ocupado={ocupado === empleadoId}
                                onDecidir={(ids, decision, motivo) => decidir(empleadoId, ids, decision, motivo)}
                                onEditar={h => setFormulario({ editando: h })}
                                onEliminar={setAEliminar}
                            />
                        </div>
                    ))}
                </div>
            )}

            {formulario && (
                <FormularioHoraExtra
                    editando={formulario.editando}
                    empleados={empleados}
                    tipos={tipos}
                    onCerrar={() => setFormulario(null)}
                    onGuardado={(fecha) => {
                        setFormulario(null);
                        const [a, m] = fecha.split('-').map(Number);
                        if (a !== anio || m !== mes) cambiarMes(a, m); else cargar(true);
                    }}
                />
            )}

            <ConfirmModal
                isOpen={!!aEliminar}
                onClose={() => setAEliminar(null)}
                onConfirm={eliminar}
                title="Eliminar hora extra"
                message="Solo para una hora registrada por error. Si el director no debe pagarla, es mejor rechazarla: así queda constancia."
                confirmText="Eliminar"
                variant="danger"
                isLoading={eliminando}
            />
        </div>
    );
}
