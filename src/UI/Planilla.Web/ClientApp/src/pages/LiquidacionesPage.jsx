import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatCurrency } from '../utils/currency';

const TIPOS_TERMINACION = [
    { value: 0, label: 'Despido Injustificado' },
    { value: 1, label: 'Renuncia Voluntaria' },
    { value: 2, label: 'Mutuo Acuerdo' },
    { value: 3, label: 'Despido Justificado' },
    { value: 4, label: 'Jubilación' },
];

const LiquidacionesPage = () => {
    const { canWrite } = useAuth();

    const [liquidaciones, setLiquidaciones] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [showDetail, setShowDetail] = useState(null);
    const [showApproveModal, setShowApproveModal] = useState(null);
    const [showPayModal, setShowPayModal] = useState(null);
    const [showDeleteModal, setShowDeleteModal] = useState(null);
    const [filterEmpleado, setFilterEmpleado] = useState('');
    const [filterEstado, setFilterEstado] = useState('');
    const [submitting, setSubmitting] = useState(false);

    const [formData, setFormData] = useState({
        empleadoId: '',
        fechaTerminacion: '',
        tipoTerminacion: 0,
        incluyePreaviso: false,
        diasVacacionesPendientes: '',
        diasSalarioPendiente: '',
        observaciones: ''
    });

    useEffect(() => {
        fetchLiquidaciones();
        fetchEmpleados();
    }, []);

    useEffect(() => {
        fetchLiquidaciones();
    }, [filterEmpleado, filterEstado]);

    const fetchLiquidaciones = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterEmpleado) params.append('empleadoId', filterEmpleado);
            if (filterEstado !== '') params.append('estado', filterEstado);
            const data = await api.get(`/api/liquidaciones?${params.toString()}`);
            setLiquidaciones(data);
        } catch (err) {
            toast.error(`Error al cargar liquidaciones: ${err.message}`);
        } finally {
            setLoading(false);
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

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (!formData.empleadoId || !formData.fechaTerminacion) {
            toast.error('Debe seleccionar un empleado y fecha de terminación');
            return;
        }

        try {
            setSubmitting(true);
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                fechaTerminacion: formData.fechaTerminacion,
                tipoTerminacion: parseInt(formData.tipoTerminacion),
                incluyePreaviso: formData.incluyePreaviso,
                diasVacacionesPendientes: formData.diasVacacionesPendientes ? parseFloat(formData.diasVacacionesPendientes) : null,
                diasSalarioPendiente: formData.diasSalarioPendiente ? parseFloat(formData.diasSalarioPendiente) : null,
                observaciones: formData.observaciones || null
            };

            const result = await api.post('/api/liquidaciones', payload);
            await fetchLiquidaciones();
            toast.success(`Liquidación ${result.numero} creada exitosamente`);
            resetForm();
            // Show detail of newly created liquidacion
            setShowDetail(result);
        } catch (err) {
            toast.error(`Error al crear liquidación: ${err.message}`);
        } finally {
            setSubmitting(false);
        }
    };

    const handleAprobar = async () => {
        if (!showApproveModal) return;
        try {
            await api.post(`/api/liquidaciones/${showApproveModal.id}/aprobar`, {});
            await fetchLiquidaciones();
            toast.success('Liquidación aprobada exitosamente');
            setShowApproveModal(null);
            setShowDetail(null);
        } catch (err) {
            toast.error(`Error al aprobar: ${err.message}`);
        }
    };

    const handlePagar = async () => {
        if (!showPayModal) return;
        try {
            await api.post(`/api/liquidaciones/${showPayModal.id}/pagar`, {});
            await fetchLiquidaciones();
            toast.success('Liquidación marcada como pagada');
            setShowPayModal(null);
            setShowDetail(null);
        } catch (err) {
            toast.error(`Error al pagar: ${err.message}`);
        }
    };

    const handleDelete = async () => {
        if (!showDeleteModal) return;
        try {
            await api.delete(`/api/liquidaciones/${showDeleteModal.id}`);
            await fetchLiquidaciones();
            toast.success('Liquidación eliminada');
            setShowDeleteModal(null);
            setShowDetail(null);
        } catch (err) {
            toast.error(`Error al eliminar: ${err.message}`);
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setFormData({
            empleadoId: '',
            fechaTerminacion: '',
            tipoTerminacion: 0,
            incluyePreaviso: false,
            diasVacacionesPendientes: '',
            diasSalarioPendiente: '',
            observaciones: ''
        });
    };

    const getEstadoBadgeColor = (estadoNombre) => {
        const colors = {
            'Borrador': 'bg-gray-500/15 text-gray-400',
            'Calculada': 'bg-blue-500/15 text-blue-400',
            'Aprobada': 'bg-green-500/15 text-green-400',
            'Pagada': 'bg-emerald-500/15 text-emerald-400',
        };
        return colors[estadoNombre] || 'bg-gray-500/15 text-gray-400';
    };

    const getTipoBadgeColor = (tipo) => {
        const colors = {
            'Despido Injustificado': 'bg-red-500/15 text-red-400',
            'Renuncia Voluntaria': 'bg-amber-500/15 text-amber-400',
            'Mutuo Acuerdo': 'bg-blue-500/15 text-blue-400',
            'Despido Justificado': 'bg-orange-500/15 text-orange-400',
            'Jubilación': 'bg-violet-500/15 text-violet-400',
        };
        return colors[tipo] || 'bg-gray-500/15 text-gray-400';
    };

    const formatDate = (dateStr) => {
        if (!dateStr) return '-';
        const d = new Date(dateStr);
        return d.toLocaleDateString('es-PA', { day: '2-digit', month: '2-digit', year: 'numeric' });
    };

    const formatYears = (anos) => {
        if (!anos) return '0';
        const years = Math.floor(anos);
        const months = Math.round((anos - years) * 12);
        if (years === 0) return `${months} meses`;
        if (months === 0) return `${years} ${years === 1 ? 'año' : 'años'}`;
        return `${years} ${years === 1 ? 'año' : 'años'}, ${months} ${months === 1 ? 'mes' : 'meses'}`;
    };

    // Stats
    const totalLiquidaciones = liquidaciones.length;
    const pendientes = liquidaciones.filter(l => l.estadoNombre === 'Calculada').length;
    const aprobadas = liquidaciones.filter(l => l.estadoNombre === 'Aprobada').length;
    const totalPagado = liquidaciones
        .filter(l => l.estadoNombre === 'Pagada')
        .reduce((sum, l) => sum + l.totalNeto, 0);

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando liquidaciones...</p>
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
                            <p className="text-sm font-medium text-gray-400">Total Liquidaciones</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{totalLiquidaciones}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Pendientes de Aprobar</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{pendientes}</p>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center relative">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            {pendientes > 0 && (
                                <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
                                    {pendientes}
                                </span>
                            )}
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Aprobadas</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{aprobadas}</p>
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
                            <p className="text-sm font-medium text-gray-400">Total Pagado</p>
                            <p className="text-3xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(totalPagado)}</p>
                        </div>
                        <div className="w-12 h-12 bg-emerald-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>
            </div>

            {/* Actions and Filters */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={() => setShowModal(true)}
                    className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Nueva Liquidación
                </button>

                <div className="flex flex-col sm:flex-row gap-3 w-full sm:w-auto">
                    <select
                        value={filterEmpleado}
                        onChange={(e) => setFilterEmpleado(e.target.value)}
                        className="px-3 py-2 border border-navy-600 bg-navy-900 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                    >
                        <option value="">Todos los empleados</option>
                        {empleados.map(emp => (
                            <option key={emp.id} value={emp.id}>
                                {emp.nombre} {emp.apellido}
                            </option>
                        ))}
                    </select>

                    <select
                        value={filterEstado}
                        onChange={(e) => setFilterEstado(e.target.value)}
                        className="px-3 py-2 border border-navy-600 bg-navy-900 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                    >
                        <option value="">Todos los estados</option>
                        <option value="1">Calculada</option>
                        <option value="2">Aprobada</option>
                        <option value="3">Pagada</option>
                    </select>
                </div>
            </div>

            {/* Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Liquidaciones
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({liquidaciones.length} {liquidaciones.length === 1 ? 'registro' : 'registros'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-800 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Número</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Fecha Term.</th>
                                <th className="text-right py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Total Neto</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-navy-700">
                            {liquidaciones.length === 0 ? (
                                <tr>
                                    <td colSpan={7} className="py-12 text-center text-gray-500">
                                        No se encontraron liquidaciones
                                    </td>
                                </tr>
                            ) : (
                                liquidaciones.map(liq => (
                                    <tr key={liq.id} className="hover:bg-navy-800/50 transition-colors cursor-pointer" onClick={() => setShowDetail(liq)}>
                                        <td className="py-3 px-6 text-sm font-mono text-gray-300">{liq.numero}</td>
                                        <td className="py-3 px-6 text-sm text-gray-100">{liq.empleadoNombre}</td>
                                        <td className="py-3 px-6">
                                            <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium ${getTipoBadgeColor(liq.tipoTerminacionNombre)}`}>
                                                {liq.tipoTerminacionNombre}
                                            </span>
                                        </td>
                                        <td className="py-3 px-6 text-sm text-gray-300">{formatDate(liq.fechaTerminacion)}</td>
                                        <td className="py-3 px-6 text-sm text-right font-mono text-gray-100">{formatCurrency(liq.totalNeto)}</td>
                                        <td className="py-3 px-6">
                                            <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoBadgeColor(liq.estadoNombre)}`}>
                                                {liq.estadoNombre}
                                            </span>
                                        </td>
                                        <td className="py-3 px-6" onClick={(e) => e.stopPropagation()}>
                                            <div className="flex items-center gap-2">
                                                <button
                                                    onClick={() => setShowDetail(liq)}
                                                    className="text-blue-400 hover:text-blue-300 text-sm font-medium"
                                                    title="Ver detalle"
                                                >
                                                    Ver
                                                </button>
                                                {liq.estadoNombre === 'Calculada' && (
                                                    <>
                                                        <button
                                                            onClick={() => setShowApproveModal(liq)}
                                                            className="text-green-400 hover:text-green-300 text-sm font-medium"
                                                        >
                                                            Aprobar
                                                        </button>
                                                        <button
                                                            onClick={() => setShowDeleteModal(liq)}
                                                            className="text-red-400 hover:text-red-300 text-sm font-medium"
                                                        >
                                                            Eliminar
                                                        </button>
                                                    </>
                                                )}
                                                {liq.estadoNombre === 'Aprobada' && (
                                                    <button
                                                        onClick={() => setShowPayModal(liq)}
                                                        className="text-emerald-400 hover:text-emerald-300 text-sm font-medium"
                                                    >
                                                        Pagar
                                                    </button>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            {/* Create Modal */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
                    <div className="bg-navy-900 rounded-xl border border-navy-700 shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700">
                            <h3 className="text-lg font-semibold text-gray-100">Nueva Liquidación</h3>
                            <p className="text-sm text-gray-500 mt-1">Calcular liquidación laboral</p>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6 space-y-4">
                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">Empleado *</label>
                                <select
                                    value={formData.empleadoId}
                                    onChange={(e) => setFormData(prev => ({ ...prev, empleadoId: e.target.value }))}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    required
                                >
                                    <option value="">Seleccionar empleado...</option>
                                    {empleados.map(emp => (
                                        <option key={emp.id} value={emp.id}>
                                            {emp.nombre} {emp.apellido} - {formatCurrency(emp.salarioBase)}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">Tipo de Terminación *</label>
                                <select
                                    value={formData.tipoTerminacion}
                                    onChange={(e) => setFormData(prev => ({ ...prev, tipoTerminacion: parseInt(e.target.value) }))}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                >
                                    {TIPOS_TERMINACION.map(t => (
                                        <option key={t.value} value={t.value}>{t.label}</option>
                                    ))}
                                </select>
                                {formData.tipoTerminacion === 0 && (
                                    <p className="text-xs text-amber-400 mt-1">
                                        Incluye indemnización (3.4 semanas/año de servicio)
                                    </p>
                                )}
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">Fecha de Terminación *</label>
                                <input
                                    type="date"
                                    value={formData.fechaTerminacion}
                                    onChange={(e) => setFormData(prev => ({ ...prev, fechaTerminacion: e.target.value }))}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    required
                                />
                            </div>

                            <div className="flex items-center gap-3">
                                <input
                                    type="checkbox"
                                    id="incluyePreaviso"
                                    checked={formData.incluyePreaviso}
                                    onChange={(e) => setFormData(prev => ({ ...prev, incluyePreaviso: e.target.checked }))}
                                    className="w-4 h-4 rounded border-navy-600 bg-navy-800 text-primary-500 focus:ring-primary-500"
                                />
                                <label htmlFor="incluyePreaviso" className="text-sm text-gray-300">
                                    Incluir Preaviso (30 días de salario)
                                </label>
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-1">Días Vacaciones Pend.</label>
                                    <input
                                        type="number"
                                        step="0.5"
                                        min="0"
                                        value={formData.diasVacacionesPendientes}
                                        onChange={(e) => setFormData(prev => ({ ...prev, diasVacacionesPendientes: e.target.value }))}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Auto"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Dejar vacío para cálculo automático</p>
                                </div>
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-1">Días Salario Pend.</label>
                                    <input
                                        type="number"
                                        step="1"
                                        min="0"
                                        max="30"
                                        value={formData.diasSalarioPendiente}
                                        onChange={(e) => setFormData(prev => ({ ...prev, diasSalarioPendiente: e.target.value }))}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="0"
                                    />
                                </div>
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">Observaciones</label>
                                <textarea
                                    value={formData.observaciones}
                                    onChange={(e) => setFormData(prev => ({ ...prev, observaciones: e.target.value }))}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none"
                                    rows={3}
                                    placeholder="Notas adicionales sobre la terminación..."
                                />
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    disabled={submitting}
                                    className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
                                >
                                    {submitting ? 'Calculando...' : 'Calcular Liquidación'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* Detail Modal */}
            {showDetail && createPortal(
                <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
                    <div className="bg-navy-900 rounded-xl border border-navy-700 shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
                            <div>
                                <h3 className="text-lg font-semibold text-gray-100">Liquidación {showDetail.numero}</h3>
                                <p className="text-sm text-gray-500 mt-0.5">{showDetail.empleadoNombre}</p>
                            </div>
                            <div className="flex items-center gap-2">
                                <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoBadgeColor(showDetail.estadoNombre)}`}>
                                    {showDetail.estadoNombre}
                                </span>
                                <span className={`inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium ${getTipoBadgeColor(showDetail.tipoTerminacionNombre)}`}>
                                    {showDetail.tipoTerminacionNombre}
                                </span>
                            </div>
                        </div>

                        <div className="p-6 space-y-6">
                            {/* Info general */}
                            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                                <div>
                                    <p className="text-xs text-gray-500 uppercase tracking-wider">Contratación</p>
                                    <p className="text-sm text-gray-200 mt-1">{formatDate(showDetail.fechaContratacion)}</p>
                                </div>
                                <div>
                                    <p className="text-xs text-gray-500 uppercase tracking-wider">Terminación</p>
                                    <p className="text-sm text-gray-200 mt-1">{formatDate(showDetail.fechaTerminacion)}</p>
                                </div>
                                <div>
                                    <p className="text-xs text-gray-500 uppercase tracking-wider">Antigüedad</p>
                                    <p className="text-sm text-gray-200 mt-1">{formatYears(showDetail.anosServicio)}</p>
                                </div>
                                <div>
                                    <p className="text-xs text-gray-500 uppercase tracking-wider">Salario Base</p>
                                    <p className="text-sm font-mono text-gray-200 mt-1">{formatCurrency(showDetail.salarioBase)}</p>
                                </div>
                            </div>

                            {/* Componentes */}
                            <div>
                                <h4 className="text-sm font-semibold text-gray-300 uppercase tracking-wider mb-3">Componentes</h4>
                                <div className="bg-navy-800 rounded-lg border border-navy-700 divide-y divide-navy-700">
                                    {showDetail.primaAntiguedad > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Prima de Antigüedad (Art. 224)</p>
                                                <p className="text-xs text-gray-500">1 semana de salario por año trabajado</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.primaAntiguedad)}</p>
                                        </div>
                                    )}
                                    {showDetail.cesantia > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Cesantía (Decreto 60/1995)</p>
                                                <p className="text-xs text-gray-500">Fondo de cesantía</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.cesantia)}</p>
                                        </div>
                                    )}
                                    {showDetail.indemnizacion > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Indemnización (Art. 225)</p>
                                                <p className="text-xs text-gray-500">{showDetail.indemnizacionSemanas?.toFixed(1)} semanas</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.indemnizacion)}</p>
                                        </div>
                                    )}
                                    {showDetail.recargoArt219 > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Recargo por Despido (Art. 219)</p>
                                                <p className="text-xs text-gray-500">25% sobre indemnización</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.recargoArt219)}</p>
                                        </div>
                                    )}
                                    {showDetail.preaviso > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Preaviso (Art. 212)</p>
                                                <p className="text-xs text-gray-500">30 días de salario</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.preaviso)}</p>
                                        </div>
                                    )}
                                    <div className="flex justify-between items-center px-4 py-3">
                                        <div>
                                            <p className="text-sm text-gray-200">Vacaciones Proporcionales (Art. 54)</p>
                                            <p className="text-xs text-gray-500">{showDetail.diasVacacionesProporcionales?.toFixed(2)} días</p>
                                        </div>
                                        <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.vacacionesProporcionales)}</p>
                                    </div>
                                    <div className="flex justify-between items-center px-4 py-3">
                                        <div>
                                            <p className="text-sm text-gray-200">Décimo Tercer Mes Proporcional</p>
                                            <p className="text-xs text-gray-500">Tercio en curso</p>
                                        </div>
                                        <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.decimoTercerMesProporcional)}</p>
                                    </div>
                                    {showDetail.salarioPendiente > 0 && (
                                        <div className="flex justify-between items-center px-4 py-3">
                                            <div>
                                                <p className="text-sm text-gray-200">Salario Pendiente</p>
                                                <p className="text-xs text-gray-500">{showDetail.diasSalarioPendiente?.toFixed(0)} días</p>
                                            </div>
                                            <p className="text-sm font-mono text-gray-100">{formatCurrency(showDetail.salarioPendiente)}</p>
                                        </div>
                                    )}
                                    <div className="flex justify-between items-center px-4 py-3 bg-navy-700/50">
                                        <p className="text-sm font-semibold text-gray-100">Total Bruto</p>
                                        <p className="text-sm font-mono font-semibold text-gray-100">{formatCurrency(showDetail.totalBruto)}</p>
                                    </div>
                                </div>
                            </div>

                            {/* Deducciones */}
                            {showDetail.totalDeducciones > 0 && (
                                <div>
                                    <h4 className="text-sm font-semibold text-gray-300 uppercase tracking-wider mb-3">Deducciones</h4>
                                    <div className="bg-navy-800 rounded-lg border border-navy-700 divide-y divide-navy-700">
                                        {showDetail.cssEmpleado > 0 && (
                                            <div className="flex justify-between items-center px-4 py-3">
                                                <p className="text-sm text-gray-200">CSS Empleado</p>
                                                <p className="text-sm font-mono text-red-400">-{formatCurrency(showDetail.cssEmpleado)}</p>
                                            </div>
                                        )}
                                        {showDetail.seEmpleado > 0 && (
                                            <div className="flex justify-between items-center px-4 py-3">
                                                <p className="text-sm text-gray-200">Seguro Educativo</p>
                                                <p className="text-sm font-mono text-red-400">-{formatCurrency(showDetail.seEmpleado)}</p>
                                            </div>
                                        )}
                                        {showDetail.isr > 0 && (
                                            <div className="flex justify-between items-center px-4 py-3">
                                                <p className="text-sm text-gray-200">ISR</p>
                                                <p className="text-sm font-mono text-red-400">-{formatCurrency(showDetail.isr)}</p>
                                            </div>
                                        )}
                                        <div className="flex justify-between items-center px-4 py-3 bg-navy-700/50">
                                            <p className="text-sm font-semibold text-gray-100">Total Deducciones</p>
                                            <p className="text-sm font-mono font-semibold text-red-400">-{formatCurrency(showDetail.totalDeducciones)}</p>
                                        </div>
                                    </div>
                                </div>
                            )}

                            {/* Total Neto */}
                            <div className="bg-primary-500/10 border border-primary-500/30 rounded-lg px-4 py-4 flex justify-between items-center">
                                <p className="text-base font-semibold text-primary-300">Total Neto a Pagar</p>
                                <p className="text-xl font-mono font-bold text-primary-200">{formatCurrency(showDetail.totalNeto)}</p>
                            </div>

                            {/* Costos patronales */}
                            {(showDetail.cssPatronal > 0 || showDetail.sePatronal > 0) && (
                                <div>
                                    <h4 className="text-sm font-semibold text-gray-300 uppercase tracking-wider mb-3">Costos Patronales</h4>
                                    <div className="bg-navy-800 rounded-lg border border-navy-700 divide-y divide-navy-700">
                                        {showDetail.cssPatronal > 0 && (
                                            <div className="flex justify-between items-center px-4 py-3">
                                                <p className="text-sm text-gray-200">CSS Patronal</p>
                                                <p className="text-sm font-mono text-gray-300">{formatCurrency(showDetail.cssPatronal)}</p>
                                            </div>
                                        )}
                                        {showDetail.sePatronal > 0 && (
                                            <div className="flex justify-between items-center px-4 py-3">
                                                <p className="text-sm text-gray-200">SE Patronal</p>
                                                <p className="text-sm font-mono text-gray-300">{formatCurrency(showDetail.sePatronal)}</p>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            )}

                            {/* Observaciones */}
                            {showDetail.observaciones && (
                                <div>
                                    <h4 className="text-sm font-semibold text-gray-300 uppercase tracking-wider mb-2">Observaciones</h4>
                                    <p className="text-sm text-gray-400 bg-navy-800 rounded-lg border border-navy-700 p-3">
                                        {showDetail.observaciones}
                                    </p>
                                </div>
                            )}
                        </div>

                        {/* Footer actions */}
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-between items-center">
                            <div className="text-xs text-gray-500">
                                Creada: {formatDate(showDetail.createdAt)}
                                {showDetail.approvedDate && ` | Aprobada: ${formatDate(showDetail.approvedDate)}`}
                            </div>
                            <div className="flex gap-3">
                                {showDetail.estadoNombre === 'Calculada' && (
                                    <>
                                        <button
                                            onClick={() => setShowDeleteModal(showDetail)}
                                            className="px-3 py-1.5 border border-red-500/30 text-red-400 rounded-lg hover:bg-red-500/10 transition-colors text-sm"
                                        >
                                            Eliminar
                                        </button>
                                        <button
                                            onClick={() => setShowApproveModal(showDetail)}
                                            className="px-3 py-1.5 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors text-sm"
                                        >
                                            Aprobar
                                        </button>
                                    </>
                                )}
                                {showDetail.estadoNombre === 'Aprobada' && (
                                    <button
                                        onClick={() => setShowPayModal(showDetail)}
                                        className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium transition-colors text-sm"
                                    >
                                        Marcar Pagada
                                    </button>
                                )}
                                <button
                                    onClick={() => setShowDetail(null)}
                                    className="px-4 py-1.5 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors text-sm"
                                >
                                    Cerrar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>,
                document.body
            )}

            {/* Approve Confirmation Modal */}
            {showApproveModal && createPortal(
                <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-[60] p-4">
                    <div className="bg-navy-900 rounded-xl border border-navy-700 shadow-2xl w-full max-w-sm">
                        <div className="p-6 text-center">
                            <div className="w-12 h-12 bg-green-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-2">Aprobar Liquidación</h3>
                            <p className="text-sm text-gray-400 mb-1">
                                {showApproveModal.numero} - {showApproveModal.empleadoNombre}
                            </p>
                            <p className="text-lg font-mono font-semibold text-gray-100">
                                {formatCurrency(showApproveModal.totalNeto)}
                            </p>
                        </div>
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
                            <button
                                onClick={() => setShowApproveModal(null)}
                                className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                            >
                                Cancelar
                            </button>
                            <button
                                onClick={handleAprobar}
                                className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors"
                            >
                                Aprobar
                            </button>
                        </div>
                    </div>
                </div>,
                document.body
            )}

            {/* Pay Confirmation Modal */}
            {showPayModal && createPortal(
                <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-[60] p-4">
                    <div className="bg-navy-900 rounded-xl border border-navy-700 shadow-2xl w-full max-w-sm">
                        <div className="p-6 text-center">
                            <div className="w-12 h-12 bg-emerald-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-2">Marcar como Pagada</h3>
                            <p className="text-sm text-gray-400 mb-1">
                                {showPayModal.numero} - {showPayModal.empleadoNombre}
                            </p>
                            <p className="text-lg font-mono font-semibold text-gray-100">
                                {formatCurrency(showPayModal.totalNeto)}
                            </p>
                            <p className="text-xs text-amber-400 mt-2">Esta acción no se puede deshacer</p>
                        </div>
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
                            <button
                                onClick={() => setShowPayModal(null)}
                                className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                            >
                                Cancelar
                            </button>
                            <button
                                onClick={handlePagar}
                                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg font-medium transition-colors"
                            >
                                Confirmar Pago
                            </button>
                        </div>
                    </div>
                </div>,
                document.body
            )}

            {/* Delete Confirmation Modal */}
            {showDeleteModal && createPortal(
                <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-[60] p-4">
                    <div className="bg-navy-900 rounded-xl border border-navy-700 shadow-2xl w-full max-w-sm">
                        <div className="p-6 text-center">
                            <div className="w-12 h-12 bg-red-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-2">Eliminar Liquidación</h3>
                            <p className="text-sm text-gray-400">
                                Se eliminará la liquidación {showDeleteModal.numero} de {showDeleteModal.empleadoNombre}
                            </p>
                        </div>
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
                            <button
                                onClick={() => setShowDeleteModal(null)}
                                className="px-4 py-2 border border-navy-600 text-gray-300 rounded-lg hover:bg-navy-800 transition-colors"
                            >
                                Cancelar
                            </button>
                            <button
                                onClick={handleDelete}
                                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium transition-colors"
                            >
                                Eliminar
                            </button>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
};

export default LiquidacionesPage;
