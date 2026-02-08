import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

const AnticiposPage = () => {
    // Auth context - Solo Manager+ puede aprobar anticipos
    const { canWrite, canDelete, isReadOnly } = useAuth();

    // State management
    const [anticipos, setAnticipos] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [showApprovalModal, setShowApprovalModal] = useState(false);
    const [showRejectModal, setShowRejectModal] = useState(false);
    const [showCancelModal, setShowCancelModal] = useState(false);
    const [selectedAnticipo, setSelectedAnticipo] = useState(null);
    const [filterEmpleado, setFilterEmpleado] = useState('');
    const [filterEstado, setFilterEstado] = useState('');
    const [activeTab, setActiveTab] = useState('pendientes');
    const [motivoRechazo, setMotivoRechazo] = useState('');
    const [formData, setFormData] = useState({
        empleadoId: '',
        monto: '',
        fechaDescuento: '',
        motivo: ''
    });

    useEffect(() => {
        fetchAnticipos();
        fetchEmpleados();
    }, []);

    const fetchAnticipos = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterEmpleado) params.append('empleadoId', filterEmpleado);
            if (filterEstado) params.append('estado', filterEstado);

            const data = await api.get(`/api/anticipos?${params.toString()}`);
            setAnticipos(data);
        } catch (err) {
            toast.error(`Error al cargar anticipos: ${err.message}`);
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

    useEffect(() => {
        fetchAnticipos();
    }, [filterEmpleado, filterEstado]);

    // Format currency
    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    // Get empleado salary
    const getEmpleadoSalary = (empleadoId) => {
        const empleado = empleados.find(e => e.id === parseInt(empleadoId));
        return empleado ? empleado.salarioBase : 0;
    };

    // Filter anticipos
    const anticiposPendientes = anticipos.filter(a => a.estado === 'Pendiente');
    const anticiposMostrar = activeTab === 'pendientes' ? anticiposPendientes : anticipos;

    // Stats calculations
    const pendientesAprobar = anticiposPendientes.length;
    const mesActual = new Date().getMonth();
    const anioActual = new Date().getFullYear();

    const aprobadosEsteMes = anticipos.filter(a => {
        if (a.estado !== 'Aprobado') return false;
        const fecha = new Date(a.fechaSolicitud);
        return fecha.getMonth() === mesActual && fecha.getFullYear() === anioActual;
    }).length;

    const totalAnticipado = anticipos
        .filter(a => a.estado === 'Aprobado' && new Date(a.fechaSolicitud).getMonth() === mesActual)
        .reduce((sum, a) => sum + a.monto, 0);

    const descontados = anticipos.filter(a => a.estado === 'Descontado').length;

    // Estado badge color
    const getEstadoBadgeColor = (estado) => {
        const colors = {
            'Pendiente': 'bg-amber-500/15 text-amber-400',
            'Aprobado': 'bg-green-500/15 text-green-400',
            'Descontado': 'bg-blue-500/15 text-blue-400',
            'Rechazado': 'bg-red-500/15 text-red-400',
            'Cancelado': 'bg-navy-700 text-gray-300'
        };
        return colors[estado] || 'bg-navy-700 text-gray-300';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        // Validaciones
        const salario = getEmpleadoSalary(formData.empleadoId);
        const maxAnticipo = salario * 0.5;

        if (parseFloat(formData.monto) > maxAnticipo) {
            toast.error(`El anticipo no puede exceder el 50% del salario (${formatCurrency(maxAnticipo)})`);
            return;
        }

        if (formData.motivo.length < 10) {
            toast.error('El motivo debe tener al menos 10 caracteres');
            return;
        }

        try {
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                monto: parseFloat(formData.monto),
                fechaDescuento: formData.fechaDescuento,
                motivo: formData.motivo
            };

            await api.post('/api/anticipos', payload);

            await fetchAnticipos();
            toast.success('Anticipo creado exitosamente');
            resetForm();
        } catch (err) {
            toast.error(`Error al guardar anticipo: ${err.message}`);
        }
    };

    const handleAprobar = async () => {
        if (!selectedAnticipo) return;

        try {
            await api.post(`/api/anticipos/${selectedAnticipo.id}/aprobar`, {});

            await fetchAnticipos();
            toast.success('Anticipo aprobado exitosamente');
            setShowApprovalModal(false);
            setSelectedAnticipo(null);
        } catch (err) {
            toast.error(`Error al aprobar anticipo: ${err.message}`);
        }
    };

    const handleRechazar = async () => {
        if (!selectedAnticipo || !motivoRechazo.trim()) {
            toast.error('Debe proporcionar un motivo para el rechazo');
            return;
        }

        try {
            await api.post(`/api/anticipos/${selectedAnticipo.id}/rechazar`, { motivo: motivoRechazo });

            await fetchAnticipos();
            toast.success('Anticipo rechazado');
            setShowRejectModal(false);
            setSelectedAnticipo(null);
            setMotivoRechazo('');
        } catch (err) {
            toast.error(`Error al rechazar anticipo: ${err.message}`);
        }
    };

    const handleCancelar = async () => {
        if (!selectedAnticipo) return;

        try {
            await api.delete(`/api/anticipos/${selectedAnticipo.id}/cancelar`);

            await fetchAnticipos();
            toast.success('Anticipo cancelado exitosamente');
            setShowCancelModal(false);
            setSelectedAnticipo(null);
        } catch (err) {
            toast.error(`Error al cancelar anticipo: ${err.message}`);
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setFormData({
            empleadoId: '',
            monto: '',
            fechaDescuento: '',
            motivo: ''
        });
    };

    const openApprovalModal = (anticipo) => {
        setSelectedAnticipo(anticipo);
        setShowApprovalModal(true);
    };

    const openRejectModal = (anticipo) => {
        setSelectedAnticipo(anticipo);
        setShowRejectModal(true);
    };

    const openCancelModal = (anticipo) => {
        setSelectedAnticipo(anticipo);
        setShowCancelModal(true);
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando anticipos...</p>
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
                            <p className="text-sm font-medium text-gray-400">Pendientes de Aprobar</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{pendientesAprobar}</p>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center relative">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            {pendientesAprobar > 0 && (
                                <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
                                    {pendientesAprobar}
                                </span>
                            )}
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-amber-400 font-medium">Requieren acción</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Aprobados este Mes</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{aprobadosEsteMes}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Autorizados</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Total Anticipado</p>
                            <p className="text-3xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(totalAnticipado)}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Este mes</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Descontados</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{descontados}</p>
                        </div>
                        <div className="w-12 h-12 bg-navy-800 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Procesados</span>
                    </div>
                </div>
            </div>

            {/* Filters and Actions */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={() => setShowModal(true)}
                    className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Solicitar Anticipo
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
                        <option value="Pendiente">Pendiente</option>
                        <option value="Aprobado">Aprobado</option>
                        <option value="Descontado">Descontado</option>
                        <option value="Rechazado">Rechazado</option>
                        <option value="Cancelado">Cancelado</option>
                    </select>
                </div>
            </div>

            {/* Tabs */}
            <div className="border-b border-navy-700">
                <nav className="-mb-px flex space-x-8">
                    <button
                        onClick={() => setActiveTab('pendientes')}
                        className={`py-2 px-1 border-b-2 font-medium text-sm transition-colors ${
                            activeTab === 'pendientes'
                                ? 'border-primary-500 text-primary-400'
                                : 'border-transparent text-gray-500 hover:text-gray-300 hover:border-navy-600'
                        }`}
                    >
                        Pendientes
                        {pendientesAprobar > 0 && (
                            <span className="ml-2 bg-amber-500/15 text-amber-400 py-0.5 px-2 rounded-full text-xs font-semibold">
                                {pendientesAprobar}
                            </span>
                        )}
                    </button>
                    <button
                        onClick={() => setActiveTab('todos')}
                        className={`py-2 px-1 border-b-2 font-medium text-sm transition-colors ${
                            activeTab === 'todos'
                                ? 'border-primary-500 text-primary-400'
                                : 'border-transparent text-gray-500 hover:text-gray-300 hover:border-navy-600'
                        }`}
                    >
                        Todos
                    </button>
                </nav>
            </div>

            {/* Anticipos Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        {activeTab === 'pendientes' ? 'Anticipos Pendientes' : 'Historial de Anticipos'}
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({anticiposMostrar.length} {anticiposMostrar.length === 1 ? 'anticipo' : 'anticipos'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-800 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Monto</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Fecha Solicitud</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Fecha Descuento</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Motivo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {anticiposMostrar.map((anticipo) => (
                                <tr key={anticipo.id} className="hover:bg-navy-800 transition-colors">
                                    <td className="py-4 px-6 text-sm font-medium text-gray-100">
                                        {anticipo.empleadoNombre || 'N/A'}
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium font-mono text-gray-100">
                                        {formatCurrency(anticipo.monto)}
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-400">
                                        {new Date(anticipo.fechaSolicitud).toLocaleDateString('es-PA', {
                                            day: '2-digit',
                                            month: 'short',
                                            year: 'numeric'
                                        })}
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-400">
                                        {anticipo.fechaDescuento
                                            ? new Date(anticipo.fechaDescuento).toLocaleDateString('es-PA', {
                                                day: '2-digit',
                                                month: 'short',
                                                year: 'numeric'
                                            })
                                            : '-'
                                        }
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-100 max-w-xs truncate" title={anticipo.motivo}>
                                        {anticipo.motivo}
                                    </td>
                                    <td className="py-4 px-6">
                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoBadgeColor(anticipo.estado)}`}>
                                            {anticipo.estado}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2 flex-wrap">
                                            {anticipo.estado === 'Pendiente' && (
                                                <>
                                                    <button
                                                        onClick={() => openApprovalModal(anticipo)}
                                                        className="inline-flex items-center gap-1 text-green-400 hover:text-green-300 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                                        </svg>
                                                        Aprobar
                                                    </button>
                                                    <button
                                                        onClick={() => openRejectModal(anticipo)}
                                                        className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                        </svg>
                                                        Rechazar
                                                    </button>
                                                </>
                                            )}
                                            {anticipo.estado === 'Aprobado' && (
                                                <button
                                                    onClick={() => openCancelModal(anticipo)}
                                                    className="inline-flex items-center gap-1 text-amber-400 hover:text-amber-300 font-medium text-sm"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                    </svg>
                                                    Cancelar
                                                </button>
                                            )}
                                            {(anticipo.estado === 'Rechazado' || anticipo.estado === 'Descontado' || anticipo.estado === 'Cancelado') && (
                                                <span className="text-sm text-gray-500">-</span>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {/* Empty State */}
                    {anticiposMostrar.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-navy-700 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">
                                {activeTab === 'pendientes' ? 'No hay anticipos pendientes' : 'No hay anticipos registrados'}
                            </h3>
                            <p className="text-gray-500">
                                {activeTab === 'pendientes'
                                    ? 'Todos los anticipos han sido procesados'
                                    : 'Comienza solicitando el primer anticipo'
                                }
                            </p>
                        </div>
                    )}
                </div>
            </div>

            {/* Create Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">Solicitar Anticipo</h3>
                            <button onClick={resetForm} className="text-gray-400 hover:text-gray-200">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6">
                            <div className="space-y-4 mb-6">
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Empleado <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.empleadoId}
                                        onChange={(e) => setFormData({ ...formData, empleadoId: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar empleado...</option>
                                        {empleados.map(emp => (
                                            <option key={emp.id} value={emp.id}>
                                                {emp.nombre} {emp.apellido} - Salario: {formatCurrency(emp.salarioBase)}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Monto <span className="text-red-500">*</span>
                                    </label>
                                    <div className="relative">
                                        <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                        <input
                                            type="number"
                                            step="0.01"
                                            min="0.01"
                                            required
                                            value={formData.monto}
                                            onChange={(e) => setFormData({ ...formData, monto: e.target.value })}
                                            className="w-full pl-8 pr-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                            placeholder="500.00"
                                        />
                                    </div>
                                    {formData.empleadoId && (
                                        <p className="text-xs text-primary-400 mt-1">
                                            Salario del empleado: {formatCurrency(getEmpleadoSalary(formData.empleadoId))}
                                            {' - '}
                                            Máximo permitido (50%): {formatCurrency(getEmpleadoSalary(formData.empleadoId) * 0.5)}
                                        </p>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha de Descuento <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaDescuento}
                                        onChange={(e) => setFormData({ ...formData, fechaDescuento: e.target.value })}
                                        min={new Date().toISOString().split('T')[0]}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Fecha en que se descontará de la planilla</p>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Motivo <span className="text-red-500">*</span>
                                    </label>
                                    <textarea
                                        rows="4"
                                        required
                                        minLength="10"
                                        value={formData.motivo}
                                        onChange={(e) => setFormData({ ...formData, motivo: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Explique detalladamente el motivo del anticipo (mínimo 10 caracteres)..."
                                    ></textarea>
                                    <p className="text-xs text-gray-500 mt-1">
                                        {formData.motivo.length}/10 caracteres mínimos
                                    </p>
                                </div>
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg font-medium"
                                >
                                    Solicitar Anticipo
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Approval Modal */}
            {showApprovalModal && selectedAnticipo && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-green-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                Aprobar Anticipo
                            </h3>
                            <p className="text-gray-400 text-center mb-6">
                                ¿Está seguro de que desea aprobar este anticipo de{' '}
                                <strong className="font-mono">{formatCurrency(selectedAnticipo.monto)}</strong> para{' '}
                                <strong>{selectedAnticipo.empleadoNombre}</strong>?
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => { setShowApprovalModal(false); setSelectedAnticipo(null); }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleAprobar}
                                    className="flex-1 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium"
                                >
                                    Aprobar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Reject Modal */}
            {showRejectModal && selectedAnticipo && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-red-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                Rechazar Anticipo
                            </h3>
                            <p className="text-gray-400 text-center mb-4">
                                Anticipo de <strong className="font-mono">{formatCurrency(selectedAnticipo.monto)}</strong> para{' '}
                                <strong>{selectedAnticipo.empleadoNombre}</strong>
                            </p>
                            <div className="mb-6">
                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                    Motivo del Rechazo <span className="text-red-500">*</span>
                                </label>
                                <textarea
                                    rows="3"
                                    required
                                    value={motivoRechazo}
                                    onChange={(e) => setMotivoRechazo(e.target.value)}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    placeholder="Explique el motivo del rechazo..."
                                ></textarea>
                            </div>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => { setShowRejectModal(false); setSelectedAnticipo(null); setMotivoRechazo(''); }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleRechazar}
                                    className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium"
                                >
                                    Rechazar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Cancel Modal */}
            {showCancelModal && selectedAnticipo && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-amber-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                Cancelar Anticipo
                            </h3>
                            <p className="text-gray-400 text-center mb-6">
                                ¿Está seguro de que desea cancelar este anticipo de{' '}
                                <strong className="font-mono">{formatCurrency(selectedAnticipo.monto)}</strong>?
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => { setShowCancelModal(false); setSelectedAnticipo(null); }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium"
                                >
                                    No, volver
                                </button>
                                <button
                                    onClick={handleCancelar}
                                    className="flex-1 px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg font-medium"
                                >
                                    Sí, cancelar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AnticiposPage;
