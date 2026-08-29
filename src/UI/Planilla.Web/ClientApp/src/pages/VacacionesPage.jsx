import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatDate, formatDateShort, formatDayMonth, todayInputValue } from '../utils/date';

const VacacionesPage = () => {
    // Auth context for permissions
    const { canWrite, canDelete, isReadOnly } = useAuth();

    // State management
    const [vacaciones, setVacaciones] = useState([]);
    const [saldos, setSaldos] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [showRejectModal, setShowRejectModal] = useState(false);
    const [activeTab, setActiveTab] = useState('solicitudes');
    const [solicitudToReject, setSolicitudToReject] = useState(null);
    const [motivoRechazo, setMotivoRechazo] = useState('');

    // Form data
    const [formData, setFormData] = useState({
        empleadoId: '',
        fechaInicio: todayInputValue(),
        fechaFin: todayInputValue(),
        observaciones: ''
    });

    // Cálculo vacacional automático
    const [calculoVacacional, setCalculoVacacional] = useState(null);
    const [calculandoSalario, setCalculandoSalario] = useState(false);
    const [numPeriodosCalculo, setNumPeriodosCalculo] = useState('');

    useEffect(() => {
        fetchVacaciones();
        fetchEmpleados();
    }, []);

    useEffect(() => {
        if (activeTab === 'saldos') {
            fetchSaldos();
        }
    }, [activeTab]);

    const fetchVacaciones = async () => {
        try {
            setLoading(true);
            const data = await api.get('/api/vacaciones');
            setVacaciones(data);
        } catch (err) {
            toast.error(`Error al cargar vacaciones: ${err.message}`);
        } finally {
            setLoading(false);
        }
    };

    const fetchSaldos = async () => {
        try {
            const saldosPromises = empleados.map(emp =>
                api.get(`/api/vacaciones/saldo/${emp.id}`).catch(() => null)
            );
            const saldosData = await Promise.all(saldosPromises);
            setSaldos(saldosData.filter(s => s !== null));
        } catch (err) {
            toast.error(`Error al cargar saldos: ${err.message}`);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const data = await api.get('/api/empleados');
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (err) {
            toast.error(`Error al cargar empleados: ${err.message}`);
        }
    };

    // Calculate stats
    const pendientes = vacaciones.filter(v => v.estado === 1).length; // Pendiente
    const enCurso = vacaciones.filter(v => v.estado === 3).length; // EnCurso
    const diasOtorgados = vacaciones
        .filter(v => v.estado === 2 || v.estado === 3 || v.estado === 4) // Aprobada, EnCurso, Completada
        .reduce((sum, v) => sum + v.diasVacaciones, 0);

    const today = new Date();
    const proximas = vacaciones.filter(v => {
        if (v.estado !== 2) return false; // Solo aprobadas
        const inicio = new Date(v.fechaInicio);
        const diffDays = Math.ceil((inicio - today) / (1000 * 60 * 60 * 24));
        return diffDays >= 0 && diffDays <= 30;
    }).length;

    const getEstadoNombre = (estado) => {
        const estados = {
            1: 'Pendiente',
            2: 'Aprobada',
            3: 'En Curso',
            4: 'Completada',
            5: 'Cancelada',
            6: 'Rechazada'
        };
        return estados[estado] || 'Desconocido';
    };

    const getEstadoColor = (estado) => {
        const colores = {
            1: 'bg-amber-500/15 text-amber-400',
            2: 'bg-green-500/15 text-green-400',
            3: 'bg-blue-500/15 text-blue-400',
            4: 'bg-navy-700 text-gray-300',
            5: 'bg-navy-700 text-gray-400',
            6: 'bg-red-500/15 text-red-400'
        };
        return colores[estado] || 'bg-navy-700 text-gray-300';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        try {
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                fechaInicio: formData.fechaInicio,
                fechaFin: formData.fechaFin,
                observaciones: formData.observaciones || null
            };

            await api.post('/api/vacaciones', payload);

            await fetchVacaciones();
            toast.success('Solicitud de vacaciones creada');
            resetForm();
        } catch (err) {
            toast.error(`Error: ${err.message}`);
        }
    };

    const handleAprobar = async (id) => {
        try {
            await api.post(`/api/vacaciones/${id}/aprobar`, {});
            await fetchVacaciones();
            toast.success('Solicitud aprobada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const openRejectModal = (vacacion) => {
        setSolicitudToReject(vacacion);
        setMotivoRechazo('');
        setShowRejectModal(true);
    };

    const handleRechazar = async () => {
        if (!motivoRechazo.trim()) {
            toast.error('Debe especificar el motivo del rechazo');
            return;
        }

        try {
            await api.post(`/api/vacaciones/${solicitudToReject.id}/rechazar`, { motivo: motivoRechazo });

            await fetchVacaciones();
            toast.success('Solicitud rechazada');
            setShowRejectModal(false);
            setSolicitudToReject(null);
            setMotivoRechazo('');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const handleCancelar = async (id) => {
        if (!window.confirm('¿Está seguro de cancelar esta solicitud?')) return;

        try {
            await api.delete(`/api/vacaciones/${id}/cancelar`);
            await fetchVacaciones();
            toast.success('Solicitud cancelada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setFormData({
            empleadoId: '',
            fechaInicio: todayInputValue(),
            fechaFin: todayInputValue(),
            observaciones: ''
        });
        setCalculoVacacional(null);
        setNumPeriodosCalculo('');
    };

    const calcularSalarioVacacional = async (empleadoId, fechaInicio, fechaFin, periodos) => {
        if (!empleadoId || !fechaInicio || !fechaFin) return;
        const dias = Math.ceil((new Date(fechaFin) - new Date(fechaInicio)) / (1000 * 60 * 60 * 24)) + 1;
        if (dias <= 0) return;

        setCalculandoSalario(true);
        try {
            const params = new URLSearchParams({
                empleadoId,
                fechaInicio,
                diasVacaciones: dias,
                ...(periodos ? { numPeriodos: periodos } : {})
            });
            const data = await api.get(`/api/vacaciones/calcular-salario?${params}`);
            setCalculoVacacional(data);
        } catch {
            setCalculoVacacional(null);
        } finally {
            setCalculandoSalario(false);
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando vacaciones...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Pendientes</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{pendientes}</p>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">En Curso</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{enCurso}</p>
                        </div>
                        <div className="w-12 h-12 bg-primary-500/10 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Días Otorgados</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{diasOtorgados}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Próximas (30 días)</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{proximas}</p>
                        </div>
                        <div className="w-12 h-12 bg-purple-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                        </div>
                    </div>
                </div>
            </div>

            {/* Tabs */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="border-b border-navy-700">
                    <div className="flex gap-4 px-6">
                        <button
                            onClick={() => setActiveTab('solicitudes')}
                            className={`py-4 px-2 border-b-2 font-medium text-sm transition-colors ${
                                activeTab === 'solicitudes'
                                    ? 'border-primary-500 text-primary-400'
                                    : 'border-transparent text-gray-500 hover:text-gray-200'
                            }`}
                        >
                            Solicitudes
                        </button>
                        <button
                            onClick={() => setActiveTab('calendario')}
                            className={`py-4 px-2 border-b-2 font-medium text-sm transition-colors ${
                                activeTab === 'calendario'
                                    ? 'border-primary-500 text-primary-400'
                                    : 'border-transparent text-gray-500 hover:text-gray-200'
                            }`}
                        >
                            Calendario
                        </button>
                        <button
                            onClick={() => setActiveTab('saldos')}
                            className={`py-4 px-2 border-b-2 font-medium text-sm transition-colors ${
                                activeTab === 'saldos'
                                    ? 'border-primary-500 text-primary-400'
                                    : 'border-transparent text-gray-500 hover:text-gray-200'
                            }`}
                        >
                            Saldos
                        </button>
                        <div className="flex-1"></div>
                        <div className="flex items-center">
                            <button
                                onClick={() => setShowModal(true)}
                                className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 my-2"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                </svg>
                                Nueva Solicitud
                            </button>
                        </div>
                    </div>
                </div>

                {/* Tab: Solicitudes */}
                {activeTab === 'solicitudes' && (
                    <div className="p-6">
                        <div className="overflow-x-auto">
                            <table className="w-full">
                                <thead className="bg-navy-950 border-b border-navy-700">
                                    <tr>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Período</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Días</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Solicitado</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Estado</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Acciones</th>
                                    </tr>
                                </thead>
                                <tbody className="bg-navy-900 divide-y divide-navy-700">
                                    {vacaciones.map((vac) => (
                                        <tr key={vac.id} className="hover:bg-navy-800 transition-colors">
                                            <td className="py-4 px-4 text-sm text-gray-100">{vac.empleadoNombre}</td>
                                            <td className="py-4 px-4 text-sm text-gray-500">
                                                {formatDayMonth(vac.fechaInicio)} - {formatDate(vac.fechaFin)}
                                            </td>
                                            <td className="py-4 px-4 text-sm font-medium text-gray-100">{vac.diasVacaciones}</td>
                                            <td className="py-4 px-4 text-sm text-gray-500">
                                                {formatDate(vac.fechaSolicitud)}
                                            </td>
                                            <td className="py-4 px-4">
                                                <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoColor(vac.estado)}`}>
                                                    {vac.estadoNombre}
                                                </span>
                                            </td>
                                            <td className="py-4 px-4">
                                                <div className="flex items-center gap-2">
                                                    {vac.estado === 1 && (
                                                        <>
                                                            <button
                                                                onClick={() => handleAprobar(vac.id)}
                                                                className="inline-flex items-center gap-1 text-green-400 hover:text-green-300 font-medium text-sm"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                                                </svg>
                                                                Aprobar
                                                            </button>
                                                            <button
                                                                onClick={() => openRejectModal(vac)}
                                                                className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                                </svg>
                                                                Rechazar
                                                            </button>
                                                        </>
                                                    )}
                                                    {(vac.estado === 1 || vac.estado === 2) && (
                                                        <button
                                                            onClick={() => handleCancelar(vac.id)}
                                                            className="inline-flex items-center gap-1 text-gray-400 hover:text-gray-200 font-medium text-sm"
                                                        >
                                                            Cancelar
                                                        </button>
                                                    )}
                                                    {vac.aprobadoPor && (
                                                        <span className="text-xs text-gray-500">
                                                            Por {vac.aprobadoPor}
                                                        </span>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>

                            {vacaciones.length === 0 && (
                                <div className="text-center py-12">
                                    <h3 className="text-lg font-medium text-gray-100 mb-1">
                                        No hay solicitudes de vacaciones
                                    </h3>
                                    <p className="text-gray-500">Comienza creando una nueva solicitud</p>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* Tab: Calendario */}
                {activeTab === 'calendario' && (
                    <div className="p-6">
                        <div className="space-y-4">
                            {vacaciones
                                .filter(v => v.estado === 2 || v.estado === 3)
                                .map((vac) => (
                                    <div key={vac.id} className="border border-navy-700 rounded-lg p-4 hover:bg-navy-800 transition-colors">
                                        <div className="flex items-start justify-between">
                                            <div className="flex-1">
                                                <div className="flex items-center gap-2 mb-2">
                                                    <div className="w-3 h-3 rounded-full bg-primary-600"></div>
                                                    <h4 className="font-medium text-gray-100">{vac.empleadoNombre}</h4>
                                                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${getEstadoColor(vac.estado)}`}>
                                                        {vac.estadoNombre}
                                                    </span>
                                                </div>
                                                <div className="flex items-center gap-4 text-sm text-gray-400">
                                                    <div className="flex items-center gap-1">
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                                        </svg>
                                                        {formatDateShort(vac.fechaInicio)} - {formatDateShort(vac.fechaFin)}
                                                    </div>
                                                    <div className="flex items-center gap-1">
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                                        </svg>
                                                        {vac.diasVacaciones} días
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ))}

                            {vacaciones.filter(v => v.estado === 2 || v.estado === 3).length === 0 && (
                                <div className="text-center py-12">
                                    <h3 className="text-lg font-medium text-gray-100 mb-1">
                                        No hay vacaciones activas
                                    </h3>
                                    <p className="text-gray-500">Las vacaciones aprobadas y en curso aparecerán aquí</p>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* Tab: Saldos */}
                {activeTab === 'saldos' && (
                    <div className="p-6">
                        <div className="overflow-x-auto">
                            <table className="w-full">
                                <thead className="bg-navy-950 border-b border-navy-700">
                                    <tr>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Acumulados</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Tomados</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Disponibles</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Progreso</th>
                                        <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Período</th>
                                    </tr>
                                </thead>
                                <tbody className="bg-navy-900 divide-y divide-navy-700">
                                    {saldos.map((saldo) => {
                                        const porcentajeUsado = saldo.diasAcumulados > 0
                                            ? (saldo.diasTomados / saldo.diasAcumulados) * 100
                                            : 0;

                                        return (
                                            <tr key={saldo.empleadoId} className="hover:bg-navy-800 transition-colors">
                                                <td className="py-4 px-4 text-sm text-gray-100">{saldo.empleadoNombre}</td>
                                                <td className="py-4 px-4 text-sm font-medium text-gray-100">{saldo.diasAcumulados.toFixed(1)}</td>
                                                <td className="py-4 px-4 text-sm text-gray-400">{saldo.diasTomados.toFixed(1)}</td>
                                                <td className="py-4 px-4 text-sm font-bold text-green-400">{saldo.diasDisponibles.toFixed(1)}</td>
                                                <td className="py-4 px-4">
                                                    <div className="flex items-center gap-2">
                                                        <div className="flex-1 bg-navy-700 rounded-full h-2 w-24">
                                                            <div
                                                                className={`h-2 rounded-full ${
                                                                    porcentajeUsado >= 80 ? 'bg-red-600' :
                                                                    porcentajeUsado >= 50 ? 'bg-yellow-600' :
                                                                    'bg-green-600'
                                                                }`}
                                                                style={{ width: `${Math.min(porcentajeUsado, 100)}%` }}
                                                            ></div>
                                                        </div>
                                                        <span className="text-xs text-gray-400">{porcentajeUsado.toFixed(0)}%</span>
                                                    </div>
                                                </td>
                                                <td className="py-4 px-4 text-sm text-gray-500">
                                                    {new Date(saldo.periodoInicio).getFullYear()}
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>

                            {saldos.length === 0 && (
                                <div className="text-center py-12">
                                    <h3 className="text-lg font-medium text-gray-100 mb-1">
                                        No hay saldos disponibles
                                    </h3>
                                    <p className="text-gray-500">Los saldos se crean al registrar la primera solicitud</p>
                                </div>
                            )}
                        </div>
                    </div>
                )}
            </div>

            {/* Modal Nueva Solicitud */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">Nueva Solicitud de Vacaciones</h3>
                            <button
                                onClick={resetForm}
                                className="text-gray-400 hover:text-gray-400 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Empleado <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.empleadoId}
                                        onChange={(e) => {
                                            setFormData({ ...formData, empleadoId: e.target.value });
                                            setCalculoVacacional(null);
                                        }}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 bg-navy-800 text-gray-100"
                                    >
                                        <option value="">Seleccionar empleado...</option>
                                        {empleados.map(emp => (
                                            <option key={emp.id} value={emp.id}>
                                                {emp.nombre} {emp.apellido} - {emp.posicionNombre || 'Sin posición'}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaInicio}
                                        onChange={(e) => {
                                            setFormData({ ...formData, fechaInicio: e.target.value });
                                            setCalculoVacacional(null);
                                        }}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 bg-navy-800 text-gray-100"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha Fin <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaFin}
                                        onChange={(e) => {
                                            setFormData({ ...formData, fechaFin: e.target.value });
                                            setCalculoVacacional(null);
                                        }}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 bg-navy-800 text-gray-100"
                                    />
                                </div>

                                {/* Panel de cálculo vacacional automático */}
                                <div className="md:col-span-2">
                                    <div className="bg-navy-800/60 border border-navy-600/50 rounded-xl p-4">
                                        <div className="flex items-center justify-between mb-3">
                                            <p className="text-sm font-semibold text-emerald-400">Salario Vacacional</p>
                                            <div className="flex items-center gap-2">
                                                <label className="text-xs text-gray-400">Períodos:</label>
                                                <input
                                                    type="number"
                                                    min="1"
                                                    max="100"
                                                    value={numPeriodosCalculo}
                                                    onChange={e => setNumPeriodosCalculo(e.target.value)}
                                                    placeholder="Auto"
                                                    className="w-16 px-2 py-1 bg-navy-700 border border-navy-600 rounded text-gray-100 text-xs text-center focus:outline-none focus:ring-1 focus:ring-emerald-500"
                                                />
                                                <button
                                                    type="button"
                                                    onClick={() => calcularSalarioVacacional(formData.empleadoId, formData.fechaInicio, formData.fechaFin, numPeriodosCalculo || undefined)}
                                                    disabled={!formData.empleadoId || !formData.fechaInicio || !formData.fechaFin || calculandoSalario}
                                                    className="px-3 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded text-xs font-medium transition-colors disabled:opacity-40"
                                                >
                                                    {calculandoSalario ? 'Calculando...' : 'Calcular'}
                                                </button>
                                            </div>
                                        </div>
                                        {calculoVacacional ? (
                                            <div className="space-y-2">
                                                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                                                    <div className="bg-navy-700/50 rounded-lg p-2.5">
                                                        <p className="text-gray-400 mb-0.5">Períodos usados</p>
                                                        <p className="text-gray-100 font-semibold">{calculoVacacional.numPeriodosUsados}</p>
                                                    </div>
                                                    <div className="bg-navy-700/50 rounded-lg p-2.5">
                                                        <p className="text-gray-400 mb-0.5">Total devengado</p>
                                                        <p className="text-gray-100 font-semibold font-mono">B/. {calculoVacacional.totalDevengado?.toFixed(2)}</p>
                                                    </div>
                                                    <div className="bg-navy-700/50 rounded-lg p-2.5">
                                                        <p className="text-gray-400 mb-0.5">Salario diario</p>
                                                        <p className="text-emerald-400 font-semibold font-mono">B/. {calculoVacacional.salarioDiario?.toFixed(2)}</p>
                                                    </div>
                                                    <div className="bg-emerald-600/20 border border-emerald-500/30 rounded-lg p-2.5">
                                                        <p className="text-emerald-400 mb-0.5">Monto vacaciones</p>
                                                        <p className="text-emerald-300 font-bold font-mono text-sm">B/. {calculoVacacional.montoVacaciones?.toFixed(2)}</p>
                                                    </div>
                                                </div>
                                                <p className="text-xs text-gray-500">
                                                    Período ref: {calculoVacacional.periodoDesde ? formatDateShort(calculoVacacional.periodoDesde) : '—'} — {calculoVacacional.periodoHasta ? formatDateShort(calculoVacacional.periodoHasta) : '—'} ({calculoVacacional.diasCalendarioCubiertos} días calendario)
                                                </p>
                                            </div>
                                        ) : (
                                            <p className="text-xs text-gray-500">
                                                {!formData.empleadoId ? 'Selecciona un empleado para calcular el salario vacacional' :
                                                 !formData.fechaFin ? 'Completa las fechas para calcular' :
                                                 'Haz clic en "Calcular" para ver el salario vacacional estimado'}
                                            </p>
                                        )}
                                    </div>
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Observaciones
                                    </label>
                                    <textarea
                                        value={formData.observaciones}
                                        onChange={(e) => setFormData({ ...formData, observaciones: e.target.value })}
                                        rows="3"
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 bg-navy-800 text-gray-100"
                                        placeholder="Observaciones adicionales..."
                                    />
                                </div>
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                                >
                                    Crear Solicitud
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* Modal Rechazar */}
            {showRejectModal && solicitudToReject && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="px-6 py-4 border-b border-navy-700">
                            <h3 className="text-xl font-semibold text-gray-100">Rechazar Solicitud</h3>
                        </div>

                        <div className="p-6">
                            <p className="text-gray-400 mb-4">
                                ¿Está seguro de rechazar la solicitud de <strong>{solicitudToReject.empleadoNombre}</strong>?
                            </p>

                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Motivo del Rechazo <span className="text-red-500">*</span>
                            </label>
                            <textarea
                                required
                                value={motivoRechazo}
                                onChange={(e) => setMotivoRechazo(e.target.value)}
                                rows="3"
                                className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 bg-navy-800 text-gray-100"
                                placeholder="Especifique el motivo del rechazo..."
                            />
                        </div>

                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
                            <button
                                onClick={() => {
                                    setShowRejectModal(false);
                                    setSolicitudToReject(null);
                                    setMotivoRechazo('');
                                }}
                                className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
                            >
                                Cancelar
                            </button>
                            <button
                                onClick={handleRechazar}
                                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium transition-colors"
                            >
                                Rechazar Solicitud
                            </button>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
};

export default VacacionesPage;
