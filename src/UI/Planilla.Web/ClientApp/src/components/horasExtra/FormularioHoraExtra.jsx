import React, { useEffect, useState, lazy, Suspense } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import { toDateInputValue, todayInputValue } from '../../utils/date';

// ============================================================
// Registrar o corregir una hora extra. Mientras se llena, avisa si el
// día es feriado, sugiere el tipo según el horario y enseña cuánto le
// queda al colaborador de los límites legales (3 h al día, 9 a la semana).
// Corregir una rechazada la devuelve al director como pendiente.
// ============================================================

const OvertimeLimitsChart = lazy(() => import('../charts/OvertimeLimitsChart'));

const VACIO = { empleadoId: '', fecha: todayInputValue(), tipoHoraExtra: '1', horaInicio: '09:00', horaFin: '10:00', motivo: '' };
const LIMITES_VACIOS = {
    horasDelDia: 0, horasDeLaSemana: 0, horasDisponiblesDia: 3, horasDisponiblesSemana: 9,
    porcentajeDia: 0, porcentajeSemana: 0, esExceso: false, mensaje: '',
};

const hhmm = (timeSpan) => (timeSpan || '').substring(0, 5);

const calcularHoras = (inicio, fin) => {
    const [hInicio, mInicio] = inicio.split(':').map(Number);
    const [hFin, mFin] = fin.split(':').map(Number);
    return Math.max(0, (hFin * 60 + mFin - (hInicio * 60 + mInicio)) / 60);
};

const Spinner = () => (
    <div className="flex items-center justify-center py-8">
        <div className="w-6 h-6 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
    </div>
);

export default function FormularioHoraExtra({ editando, empleados, tipos, onCerrar, onGuardado }) {
    const [formData, setFormData] = useState(() => editando ? {
        empleadoId: editando.empleadoId.toString(),
        fecha: toDateInputValue(editando.fecha),
        tipoHoraExtra: editando.tipoHoraExtra.toString(),
        horaInicio: hhmm(editando.horaInicio),
        horaFin: hhmm(editando.horaFin),
        motivo: editando.motivo,
    } : VACIO);
    const [limites, setLimites] = useState(LIMITES_VACIOS);
    const [festivoInfo, setFestivoInfo] = useState({ esFestivo: false, nombreFestivo: null });
    const [sugerenciaTipo, setSugerenciaTipo] = useState(null);
    const [validando, setValidando] = useState(false);
    const [guardando, setGuardando] = useState(false);

    // Límites legales cuando cambian empleado, fecha u horario.
    useEffect(() => {
        if (!formData.empleadoId || !formData.fecha || !formData.horaInicio || !formData.horaFin) return;
        let vigente = true;
        (async () => {
            try {
                setValidando(true);
                const horasNuevas = calcularHoras(formData.horaInicio, formData.horaFin);
                const r = await api.get(`/api/horasextra/validar-limites?empleadoId=${formData.empleadoId}&fecha=${formData.fecha}&horasNuevas=${horasNuevas}`);
                if (!vigente) return;
                setLimites({
                    horasDelDia: r.horasDelDia || 0,
                    horasDeLaSemana: r.horasDeLaSemana || 0,
                    horasDisponiblesDia: r.horasDisponiblesDia ?? 3,
                    horasDisponiblesSemana: r.horasDisponiblesSemana ?? 9,
                    porcentajeDia: r.porcentajeDia || 0,
                    porcentajeSemana: r.porcentajeSemana || 0,
                    esExceso: r.esExceso || false,
                    mensaje: r.mensaje || '',
                });
            } catch (err) {
                if (import.meta.env.DEV) console.error('Error al validar límites:', err);
            } finally {
                if (vigente) setValidando(false);
            }
        })();
        return () => { vigente = false; };
    }, [formData.empleadoId, formData.fecha, formData.horaInicio, formData.horaFin]);

    // Feriado nacional del día elegido.
    useEffect(() => {
        if (!formData.fecha) return;
        api.get(`/api/horasextra/es-festivo?fecha=${formData.fecha}`)
            .then(r => setFestivoInfo({ esFestivo: r.esFestivo || false, nombreFestivo: r.nombreFestivo || null }))
            .catch(err => { if (import.meta.env.DEV) console.error('Error al verificar festivo:', err); });
    }, [formData.fecha]);

    // Tipo sugerido por fecha y horario.
    useEffect(() => {
        if (!formData.fecha || !formData.horaInicio || !formData.horaFin) return;
        api.get(`/api/horasextra/sugerir-tipo?fecha=${formData.fecha}&horaInicio=${formData.horaInicio}&horaFin=${formData.horaFin}`)
            .then(setSugerenciaTipo)
            .catch(err => { if (import.meta.env.DEV) console.error('Error al sugerir tipo:', err); });
    }, [formData.fecha, formData.horaInicio, formData.horaFin]);

    const handleSubmit = async (e) => {
        e.preventDefault();
        try {
            setGuardando(true);
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                fecha: formData.fecha,
                tipoHoraExtra: parseInt(formData.tipoHoraExtra),
                horaInicio: formData.horaInicio + ':00',
                horaFin: formData.horaFin + ':00',
                motivo: formData.motivo,
            };
            if (editando) await api.put(`/api/horasextra/${editando.id}`, payload);
            else await api.post('/api/horasextra', payload);
            toast.success(editando ? 'Hora extra corregida' : 'Hora extra registrada; queda pendiente del director');
            onGuardado(formData.fecha);
        } catch (err) {
            toast.error(err.message || 'No se pudo guardar la hora extra');
        } finally {
            setGuardando(false);
        }
    };

    const campo = 'w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500';

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                    <h3 className="text-xl font-semibold text-gray-100">
                        {editando ? 'Corregir hora extra' : 'Registrar hora extra'}
                    </h3>
                    <button onClick={onCerrar} aria-label="Cerrar" className="text-gray-400 hover:text-gray-200 transition-colors">
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                <form onSubmit={handleSubmit} className="p-6">
                    {editando?.estado === 'Rechazada' && (
                        <p className="mb-4 text-sm text-gray-300 bg-navy-800 border border-navy-600 rounded-lg px-3 py-2">
                            El director la rechazó{editando.motivoRechazo ? `: «${editando.motivoRechazo}»` : ''}. Al corregirla vuelve a quedar pendiente de su decisión.
                        </p>
                    )}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                        <div className="md:col-span-2">
                            <label htmlFor="he-empleado" className="block text-sm font-medium text-gray-300 mb-2">
                                Colaborador <span className="text-red-500">*</span>
                            </label>
                            <select id="he-empleado" required value={formData.empleadoId}
                                onChange={(e) => setFormData({ ...formData, empleadoId: e.target.value })} className={campo}>
                                <option value="">Seleccionar…</option>
                                {empleados.map(emp => (
                                    <option key={emp.id} value={emp.id}>
                                        {emp.nombre} {emp.apellido}{emp.posicionNombre ? ` — ${emp.posicionNombre}` : ''}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div>
                            <label htmlFor="he-fecha" className="block text-sm font-medium text-gray-300 mb-2">
                                Fecha <span className="text-red-500">*</span>
                            </label>
                            <input id="he-fecha" type="date" required value={formData.fecha}
                                onChange={(e) => setFormData({ ...formData, fecha: e.target.value })} className={campo} />
                            {festivoInfo.esFestivo && (
                                <p className="mt-2 px-3 py-2 bg-purple-500/15 border border-purple-500/30 rounded-lg text-sm text-purple-300">
                                    <strong>{festivoInfo.nombreFestivo}</strong>: feriado nacional
                                </p>
                            )}
                        </div>

                        <div>
                            <label htmlFor="he-tipo" className="block text-sm font-medium text-gray-300 mb-2">
                                Tipo <span className="text-red-500">*</span>
                            </label>
                            <select id="he-tipo" required value={formData.tipoHoraExtra}
                                onChange={(e) => setFormData({ ...formData, tipoHoraExtra: e.target.value })}
                                className={`${campo} ${sugerenciaTipo && parseInt(formData.tipoHoraExtra) !== sugerenciaTipo.tipoSugerido ? 'border-blue-500/50' : ''}`}>
                                {tipos.map((tipo, index) => (
                                    <option key={tipo?.valor || `tipo-${index}`} value={tipo?.valor}>{tipo.nombre}</option>
                                ))}
                            </select>
                            {sugerenciaTipo && parseInt(formData.tipoHoraExtra) !== sugerenciaTipo.tipoSugerido && (
                                <div className="mt-2 px-3 py-2 bg-blue-500/15 border border-blue-500/30 rounded-lg">
                                    <p className="text-sm text-blue-300">
                                        Por el día y el horario corresponde <strong>{sugerenciaTipo.nombreTipo}</strong> ({sugerenciaTipo.factor}x)
                                    </p>
                                    <button type="button"
                                        onClick={() => setFormData({ ...formData, tipoHoraExtra: sugerenciaTipo.tipoSugerido.toString() })}
                                        className="mt-1 text-xs text-blue-400 hover:text-blue-300 underline">
                                        Usar ese tipo
                                    </button>
                                </div>
                            )}
                        </div>

                        <div>
                            <label htmlFor="he-inicio" className="block text-sm font-medium text-gray-300 mb-2">
                                Hora de inicio <span className="text-red-500">*</span>
                            </label>
                            <input id="he-inicio" type="time" required value={formData.horaInicio}
                                onChange={(e) => setFormData({ ...formData, horaInicio: e.target.value })} className={campo} />
                        </div>

                        <div>
                            <label htmlFor="he-fin" className="block text-sm font-medium text-gray-300 mb-2">
                                Hora de fin <span className="text-red-500">*</span>
                            </label>
                            <input id="he-fin" type="time" required value={formData.horaFin}
                                onChange={(e) => setFormData({ ...formData, horaFin: e.target.value })} className={campo} />
                        </div>

                        <div className="md:col-span-2">
                            <label htmlFor="he-motivo" className="block text-sm font-medium text-gray-300 mb-2">
                                Motivo <span className="text-red-500">*</span>
                            </label>
                            <textarea id="he-motivo" required rows="3" value={formData.motivo}
                                onChange={(e) => setFormData({ ...formData, motivo: e.target.value })}
                                className={campo} placeholder="Por ejemplo: reunión de padres, acto cívico…" />
                        </div>

                        {formData.empleadoId && formData.fecha && (
                            <div className="md:col-span-2 bg-navy-800 rounded-lg p-4 border border-navy-700">
                                <h4 className="text-sm font-semibold text-gray-300 mb-3">Límites legales</h4>
                                {validando ? <Spinner /> : (
                                    <>
                                        <Suspense fallback={<Spinner />}>
                                            <OvertimeLimitsChart horasDelDia={limites.horasDelDia} horasDeLaSemana={limites.horasDeLaSemana} limiteDiario={3} limiteSemanal={9} />
                                        </Suspense>
                                        {limites.esExceso && (
                                            <p className="mt-3 px-3 py-2 bg-red-500/15 border border-red-500/30 rounded-lg text-sm text-red-300">
                                                {limites.mensaje} Se puede registrar igual; se paga con el recargo por exceso.
                                            </p>
                                        )}
                                        {!limites.esExceso && (limites.porcentajeDia >= 70 || limites.porcentajeSemana >= 70) && (
                                            <p className="mt-3 px-3 py-2 bg-yellow-500/15 border border-yellow-500/30 rounded-lg text-sm text-yellow-300">
                                                Se está acercando al límite {limites.porcentajeDia >= 70 ? 'diario' : 'semanal'}.
                                            </p>
                                        )}
                                    </>
                                )}
                            </div>
                        )}
                    </div>

                    <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                        <button type="button" onClick={onCerrar}
                            className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors">
                            Cancelar
                        </button>
                        <button type="submit" disabled={guardando}
                            className={`px-4 py-2 rounded-lg font-medium transition-colors disabled:opacity-50 ${limites.esExceso ? 'bg-red-600 hover:bg-red-700' : 'bg-primary-600 hover:bg-primary-700'} text-white`}>
                            {guardando ? 'Guardando…' : editando ? 'Guardar corrección' : limites.esExceso ? 'Registrar con exceso' : 'Registrar'}
                        </button>
                    </div>
                </form>
            </div>
        </div>,
        document.body
    );
}
