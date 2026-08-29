import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { formatCurrency } from '../utils/currency';
import { formatDateShort, toDateInputValue, todayInputValue } from '../utils/date';

const PrestamosPage = () => {
    // Auth context - Solo Manager+ puede crear/aprobar préstamos
    const { canWrite, canDelete, isReadOnly } = useAuth();

    // State management
    const [prestamos, setPrestamos] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [showDetailModal, setShowDetailModal] = useState(false);
    const [showConfirmModal, setShowConfirmModal] = useState(false);
    const [selectedPrestamo, setSelectedPrestamo] = useState(null);
    const [confirmAction, setConfirmAction] = useState(null);
    const [searchTerm, setSearchTerm] = useState('');
    const [filterEmpleado, setFilterEmpleado] = useState('');
    const [filterEstado, setFilterEstado] = useState('');
    const [editingId, setEditingId] = useState(null);
    const [formData, setFormData] = useState({
        empleadoId: '',
        descripcion: '',
        montoOriginal: '',
        cuotaMensual: '',
        numeroCuotas: '',
        tasaInteres: '0',
        fechaInicio: todayInputValue(),
        referencia: '',
        observaciones: '',
        frecuenciaCuota: '3'
    });

    const frecuenciaOptions = [
        { value: '0', label: 'Semanal' },
        { value: '1', label: 'Bisemanal' },
        { value: '2', label: 'Quincenal' },
        { value: '3', label: 'Mensual' }
    ];

    useEffect(() => {
        fetchPrestamos();
        fetchEmpleados();
    }, []);

    const fetchPrestamos = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterEmpleado) params.append('empleadoId', filterEmpleado);
            if (filterEstado) params.append('estado', filterEstado);

            const data = await api.get(`/api/prestamos?${params.toString()}`);
            setPrestamos(data);
        } catch (err) {
            toast.error(err.message || 'Error al cargar préstamos');
        } finally {
            setLoading(false);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const data = await api.get('/api/empleados');
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (err) {
            toast.error(err.message || 'Error al cargar empleados');
        }
    };

    const fetchPrestamoDetalle = async (id) => {
        try {
            const data = await api.get(`/api/prestamos/${id}`);
            setSelectedPrestamo(data);
            setShowDetailModal(true);
        } catch (err) {
            toast.error(err.message || 'Error al cargar detalle');
        }
    };

    useEffect(() => {
        if (filterEmpleado || filterEstado) {
            fetchPrestamos();
        }
    }, [filterEmpleado, filterEstado]);

    // Filter prestamos
    const filteredPrestamos = prestamos.filter(p =>
        p.descripcion.toLowerCase().includes(searchTerm.toLowerCase()) ||
        p.empleadoNombre?.toLowerCase().includes(searchTerm.toLowerCase())
    );

    // Stats calculations
    const prestamosActivos = prestamos.filter(p => p.estado === 'Activo');
    const totalPendiente = prestamosActivos.reduce((sum, p) => sum + p.montoPendiente, 0);
    const cuotasMes = prestamosActivos.reduce((sum, p) => sum + p.cuotaMensual, 0);
    const prestamosPagados = prestamos.filter(p => p.estado === 'Pagado').length;

    // Estado badge color
    const getEstadoBadgeColor = (estado) => {
        const colors = {
            'Activo': 'bg-green-500/15 text-green-400',
            'Pagado': 'bg-blue-500/15 text-blue-400',
            'Cancelado': 'bg-red-500/15 text-red-400',
            'Suspendido': 'bg-amber-500/15 text-amber-400'
        };
        return colors[estado] || 'bg-navy-700 text-gray-300';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        // Validaciones
        if (parseFloat(formData.cuotaMensual) <= 0) {
            toast.error('La cuota mensual debe ser mayor a 0');
            return;
        }

        if (parseInt(formData.numeroCuotas) <= 0) {
            toast.error('El número de cuotas debe ser mayor a 0');
            return;
        }

        try {
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                descripcion: formData.descripcion,
                montoOriginal: parseFloat(formData.montoOriginal),
                cuotaMensual: parseFloat(formData.cuotaMensual),
                numeroCuotas: parseInt(formData.numeroCuotas),
                tasaInteres: parseFloat(formData.tasaInteres),
                fechaInicio: formData.fechaInicio,
                referencia: formData.referencia || null,
                observaciones: formData.observaciones || null,
                frecuenciaCuota: parseInt(formData.frecuenciaCuota)
            };

            if (editingId) {
                await api.put(`/api/prestamos/${editingId}`, payload);
                toast.success('Préstamo actualizado exitosamente');
            } else {
                await api.post('/api/prestamos', payload);
                toast.success('Préstamo creado exitosamente');
            }

            await fetchPrestamos();
            resetForm();
        } catch (err) {
            toast.error(err.message || 'Error al guardar préstamo');
        }
    };

    const handleEdit = (prestamo) => {
        setFormData({
            empleadoId: prestamo.empleadoId.toString(),
            descripcion: prestamo.descripcion,
            montoOriginal: prestamo.montoOriginal.toString(),
            cuotaMensual: prestamo.cuotaMensual.toString(),
            numeroCuotas: prestamo.numeroCuotas.toString(),
            tasaInteres: prestamo.tasaInteres.toString(),
            fechaInicio: toDateInputValue(prestamo.fechaInicio),
            referencia: prestamo.referencia || '',
            observaciones: prestamo.observaciones || '',
            frecuenciaCuota: (prestamo.frecuenciaCuota ?? 3).toString()
        });
        setEditingId(prestamo.id);
        setShowModal(true);
    };

    const confirmActionModal = (action, prestamo) => {
        setConfirmAction(action);
        setSelectedPrestamo(prestamo);
        setShowConfirmModal(true);
    };

    const handleConfirmAction = async () => {
        if (!selectedPrestamo || !confirmAction) return;

        try {
            switch (confirmAction) {
                case 'suspender':
                    await api.post(`/api/prestamos/${selectedPrestamo.id}/suspender`);
                    break;
                case 'reactivar':
                    await api.post(`/api/prestamos/${selectedPrestamo.id}/reactivar`);
                    break;
                case 'cancelar':
                    await api.delete(`/api/prestamos/${selectedPrestamo.id}/cancelar`);
                    break;
                default:
                    return;
            }

            await fetchPrestamos();
            toast.success(`Préstamo ${confirmAction} exitosamente`);
            setShowConfirmModal(false);
            setSelectedPrestamo(null);
            setConfirmAction(null);
        } catch (err) {
            toast.error(err.message || 'Error al procesar acción');
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            empleadoId: '',
            descripcion: '',
            montoOriginal: '',
            cuotaMensual: '',
            numeroCuotas: '',
            tasaInteres: '0',
            fechaInicio: todayInputValue(),
            referencia: '',
            observaciones: '',
            frecuenciaCuota: '3'
        });
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando préstamos...</p>
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
                            <p className="text-sm font-medium text-gray-400">Préstamos Activos</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{prestamosActivos.length}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">En proceso de pago</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Monto Pendiente</p>
                            <p className="text-3xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(totalPendiente)}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Total a cobrar</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Cuotas este Mes</p>
                            <p className="text-3xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(cuotasMes)}</p>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 8h6m-5 0a3 3 0 110 6H9l3 3m-3-6h6m6 1a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Descuento mensual</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Préstamos Pagados</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{prestamosPagados}</p>
                        </div>
                        <div className="w-12 h-12 bg-navy-800 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Completados</span>
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
                    Agregar Préstamo
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
                        <option value="Activo">Activo</option>
                        <option value="Pagado">Pagado</option>
                        <option value="Cancelado">Cancelado</option>
                        <option value="Suspendido">Suspendido</option>
                    </select>

                    <div className="relative">
                        <input
                            type="text"
                            placeholder="Buscar por descripción..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="w-full sm:w-64 pl-10 pr-4 py-2 border border-navy-600 bg-navy-900 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                        />
                        <svg className="w-5 h-5 text-gray-400 absolute left-3 top-1/2 transform -translate-y-1/2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                        </svg>
                    </div>
                </div>
            </div>

            {/* Prestamos Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Lista de Préstamos
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredPrestamos.length} {filteredPrestamos.length === 1 ? 'préstamo' : 'préstamos'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-800 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Descripción</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Monto Original</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Pendiente</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Cuota</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Frecuencia</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Progreso</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {filteredPrestamos.map((prestamo) => {
                                const cuotasPagadas = prestamo.numeroCuotas - Math.ceil(prestamo.montoPendiente / prestamo.cuotaMensual);
                                const porcentaje = (cuotasPagadas / prestamo.numeroCuotas) * 100;

                                return (
                                    <tr key={prestamo.id} className="hover:bg-navy-800 transition-colors">
                                        <td className="py-4 px-6 text-sm font-medium text-gray-100">
                                            {prestamo.empleadoNombre || 'N/A'}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-100">{prestamo.descripcion}</td>
                                        <td className="py-4 px-6 text-sm font-medium font-mono text-gray-100">{formatCurrency(prestamo.montoOriginal)}</td>
                                        <td className="py-4 px-6 text-sm font-medium font-mono text-green-400">{formatCurrency(prestamo.montoPendiente)}</td>
                                        <td className="py-4 px-6 text-sm font-mono text-gray-100">{formatCurrency(prestamo.cuotaMensual)}</td>
                                        <td className="py-4 px-6 text-sm text-gray-300">{prestamo.frecuenciaCuotaNombre || 'Mensual'}</td>
                                        <td className="py-4 px-6">
                                            <div className="space-y-1">
                                                <div className="flex items-center gap-2">
                                                    <div className="flex-1 bg-navy-700 rounded-full h-2">
                                                        <div
                                                            className="bg-primary-600 h-2 rounded-full transition-all duration-300"
                                                            style={{ width: `${Math.min(porcentaje, 100)}%` }}
                                                        ></div>
                                                    </div>
                                                </div>
                                                <p className="text-xs text-gray-400">
                                                    {cuotasPagadas}/{prestamo.numeroCuotas} cuotas
                                                </p>
                                            </div>
                                        </td>
                                        <td className="py-4 px-6">
                                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoBadgeColor(prestamo.estado)}`}>
                                                {prestamo.estado}
                                            </span>
                                        </td>
                                        <td className="py-4 px-6">
                                            <div className="flex items-center gap-2 flex-wrap">
                                                <button
                                                    onClick={() => fetchPrestamoDetalle(prestamo.id)}
                                                    className="inline-flex items-center gap-1 text-primary-400 hover:text-primary-300 font-medium text-sm"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                                    </svg>
                                                    Ver
                                                </button>
                                                {prestamo.estado !== 'Pagado' && prestamo.estado !== 'Cancelado' && (
                                                    <>
                                                        <button
                                                            onClick={() => handleEdit(prestamo)}
                                                            className="inline-flex items-center gap-1 text-green-400 hover:text-green-300 font-medium text-sm"
                                                        >
                                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                            </svg>
                                                            Editar
                                                        </button>
                                                        {prestamo.estado === 'Activo' && (
                                                            <button
                                                                onClick={() => confirmActionModal('suspender', prestamo)}
                                                                className="inline-flex items-center gap-1 text-amber-400 hover:text-amber-300 font-medium text-sm"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 9v6m4-6v6m7-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                                                </svg>
                                                                Suspender
                                                            </button>
                                                        )}
                                                        {prestamo.estado === 'Suspendido' && (
                                                            <button
                                                                onClick={() => confirmActionModal('reactivar', prestamo)}
                                                                className="inline-flex items-center gap-1 text-green-400 hover:text-green-300 font-medium text-sm"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                                                </svg>
                                                                Reactivar
                                                            </button>
                                                        )}
                                                        <button
                                                            onClick={() => confirmActionModal('cancelar', prestamo)}
                                                            className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
                                                        >
                                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                            </svg>
                                                            Cancelar
                                                        </button>
                                                    </>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>

                    {/* Empty State */}
                    {filteredPrestamos.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-navy-700 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">No hay préstamos registrados</h3>
                            <p className="text-gray-500">Comienza agregando el primer préstamo</p>
                        </div>
                    )}
                </div>
            </div>

            {/* Create/Edit Modal */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">
                                {editingId ? 'Editar Préstamo' : 'Nuevo Préstamo'}
                            </h3>
                            <button onClick={resetForm} className="text-gray-400 hover:text-gray-200">
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
                                        onChange={(e) => setFormData({ ...formData, empleadoId: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar empleado...</option>
                                        {empleados.map(emp => (
                                            <option key={emp.id} value={emp.id}>
                                                {emp.nombre} {emp.apellido} - {formatCurrency(emp.salarioBase)}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Descripción <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.descripcion}
                                        onChange={(e) => setFormData({ ...formData, descripcion: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Ej: Préstamo personal"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Monto Original <span className="text-red-500">*</span>
                                    </label>
                                    <div className="relative">
                                        <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                        <input
                                            type="number"
                                            step="0.01"
                                            min="0.01"
                                            required
                                            value={formData.montoOriginal}
                                            onChange={(e) => setFormData({ ...formData, montoOriginal: e.target.value })}
                                            className="w-full pl-8 pr-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                            placeholder="1000.00"
                                        />
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Cuota Mensual <span className="text-red-500">*</span>
                                    </label>
                                    <div className="relative">
                                        <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                        <input
                                            type="number"
                                            step="0.01"
                                            min="0.01"
                                            required
                                            value={formData.cuotaMensual}
                                            onChange={(e) => setFormData({ ...formData, cuotaMensual: e.target.value })}
                                            className="w-full pl-8 pr-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                            placeholder="100.00"
                                        />
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Frecuencia de Cuota <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.frecuenciaCuota}
                                        onChange={(e) => setFormData({ ...formData, frecuenciaCuota: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        {frecuenciaOptions.map(opt => (
                                            <option key={opt.value} value={opt.value}>{opt.label}</option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Número de Cuotas <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="number"
                                        min="1"
                                        required
                                        value={formData.numeroCuotas}
                                        onChange={(e) => setFormData({ ...formData, numeroCuotas: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="12"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tasa de Interés (%)
                                    </label>
                                    <input
                                        type="number"
                                        step="0.01"
                                        min="0"
                                        value={formData.tasaInteres}
                                        onChange={(e) => setFormData({ ...formData, tasaInteres: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="0.00"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha de Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaInicio}
                                        onChange={(e) => setFormData({ ...formData, fechaInicio: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Referencia
                                    </label>
                                    <input
                                        type="text"
                                        value={formData.referencia}
                                        onChange={(e) => setFormData({ ...formData, referencia: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Número de referencia"
                                    />
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Observaciones
                                    </label>
                                    <textarea
                                        rows="3"
                                        value={formData.observaciones}
                                        onChange={(e) => setFormData({ ...formData, observaciones: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Notas adicionales..."
                                    ></textarea>
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
                                    {editingId ? 'Actualizar Préstamo' : 'Crear Préstamo'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* Detail Modal */}
            {showDetailModal && selectedPrestamo && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-4xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">Detalle del Préstamo</h3>
                            <button onClick={() => { setShowDetailModal(false); setSelectedPrestamo(null); }} className="text-gray-400 hover:text-gray-200">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <div className="p-6">
                            <div className="grid grid-cols-2 gap-4 mb-6">
                                <div>
                                    <p className="text-sm text-gray-400">Empleado</p>
                                    <p className="font-medium text-gray-100">{selectedPrestamo.empleadoNombre}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Descripción</p>
                                    <p className="font-medium text-gray-100">{selectedPrestamo.descripcion}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Monto Original</p>
                                    <p className="font-medium font-mono text-gray-100">{formatCurrency(selectedPrestamo.montoOriginal)}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Monto Pendiente</p>
                                    <p className="font-medium font-mono text-green-400">{formatCurrency(selectedPrestamo.montoPendiente)}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Cuota</p>
                                    <p className="font-medium font-mono text-gray-100">{formatCurrency(selectedPrestamo.cuotaMensual)}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Frecuencia de Cuota</p>
                                    <p className="font-medium text-gray-100">{selectedPrestamo.frecuenciaCuotaNombre || 'Mensual'}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Número de Cuotas</p>
                                    <p className="font-medium text-gray-100">{selectedPrestamo.numeroCuotas}</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Tasa de Interés</p>
                                    <p className="font-medium text-gray-100">{selectedPrestamo.tasaInteres}%</p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Fecha de Inicio</p>
                                    <p className="font-medium text-gray-100">{formatDateShort(selectedPrestamo.fechaInicio)}</p>
                                </div>
                                {selectedPrestamo.referencia && (
                                    <div>
                                        <p className="text-sm text-gray-400">Referencia</p>
                                        <p className="font-medium text-gray-100">{selectedPrestamo.referencia}</p>
                                    </div>
                                )}
                                <div>
                                    <p className="text-sm text-gray-400">Estado</p>
                                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getEstadoBadgeColor(selectedPrestamo.estado)}`}>
                                        {selectedPrestamo.estado}
                                    </span>
                                </div>
                            </div>

                            {selectedPrestamo.observaciones && (
                                <div className="mb-6">
                                    <p className="text-sm text-gray-400 mb-1">Observaciones</p>
                                    <p className="text-gray-100 bg-navy-800 p-3 rounded-lg">{selectedPrestamo.observaciones}</p>
                                </div>
                            )}

                            {selectedPrestamo.historialPagos && selectedPrestamo.historialPagos.length > 0 && (
                                <div>
                                    <h4 className="font-semibold text-gray-100 mb-3">Historial de Pagos</h4>
                                    <div className="overflow-x-auto">
                                        <table className="w-full text-sm">
                                            <thead className="bg-navy-800">
                                                <tr>
                                                    <th className="text-left py-2 px-3 text-gray-400">Fecha</th>
                                                    <th className="text-left py-2 px-3 text-gray-400">Cuota #</th>
                                                    <th className="text-left py-2 px-3 text-gray-400">Monto</th>
                                                    <th className="text-left py-2 px-3 text-gray-400">Saldo Anterior</th>
                                                    <th className="text-left py-2 px-3 text-gray-400">Saldo Nuevo</th>
                                                </tr>
                                            </thead>
                                            <tbody className="divide-y divide-navy-700">
                                                {selectedPrestamo.historialPagos.map((pago, idx) => (
                                                    <tr key={idx}>
                                                        <td className="py-2 px-3 text-gray-300">{formatDateShort(pago.fecha)}</td>
                                                        <td className="py-2 px-3 text-gray-300">{pago.numeroCuota}</td>
                                                        <td className="py-2 px-3 font-medium font-mono text-gray-100">{formatCurrency(pago.monto)}</td>
                                                        <td className="py-2 px-3 font-mono text-gray-300">{formatCurrency(pago.saldoAnterior)}</td>
                                                        <td className="py-2 px-3 text-green-400 font-medium font-mono">{formatCurrency(pago.saldoNuevo)}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>,
                document.body
            )}

            {/* Confirm Action Modal */}
            {showConfirmModal && selectedPrestamo && confirmAction && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-amber-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                {confirmAction === 'suspender' && 'Suspender Préstamo'}
                                {confirmAction === 'reactivar' && 'Reactivar Préstamo'}
                                {confirmAction === 'cancelar' && 'Cancelar Préstamo'}
                            </h3>
                            <p className="text-gray-400 text-center mb-6">
                                ¿Está seguro de que desea {confirmAction} este préstamo?
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => { setShowConfirmModal(false); setSelectedPrestamo(null); setConfirmAction(null); }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleConfirmAction}
                                    className={`flex-1 px-4 py-2 rounded-lg font-medium text-white ${
                                        confirmAction === 'cancelar' ? 'bg-red-600 hover:bg-red-700' : 'bg-primary-600 hover:bg-primary-700'
                                    }`}
                                >
                                    Confirmar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
};

export default PrestamosPage;
