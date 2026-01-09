import React, { useState, useEffect } from 'react';
import { useToast } from '../components/ToastContext';

const DeduccionesPage = () => {
    const { showToast } = useToast();

    // State management
    const [deducciones, setDeducciones] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [tiposDeducciones, setTiposDeducciones] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [showConfirmDelete, setShowConfirmDelete] = useState(false);
    const [deduccionToDelete, setDeduccionToDelete] = useState(null);
    const [filterEmpleado, setFilterEmpleado] = useState('');
    const [filterTipo, setFilterTipo] = useState('');
    const [filterActivas, setFilterActivas] = useState(true);
    const [editingId, setEditingId] = useState(null);
    const [formData, setFormData] = useState({
        empleadoId: '',
        tipoDeduccion: '',
        descripcion: '',
        esPorcentaje: false,
        monto: '',
        porcentaje: '',
        fechaInicio: new Date().toISOString().split('T')[0],
        fechaFin: '',
        prioridad: '10',
        referencia: '',
        observaciones: ''
    });

    useEffect(() => {
        fetchDeducciones();
        fetchEmpleados();
        fetchTiposDeducciones();
    }, []);

    const fetchDeducciones = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterEmpleado) params.append('empleadoId', filterEmpleado);
            if (filterTipo) params.append('tipo', filterTipo);
            if (filterActivas) params.append('activas', 'true');

            const response = await fetch(`/api/deducciones?${params.toString()}`);
            if (!response.ok) throw new Error(`Error ${response.status}: ${response.statusText}`);

            const data = await response.json();
            setDeducciones(data);
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar deducciones: ${err.message}` });
        } finally {
            setLoading(false);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const response = await fetch('/api/empleados');
            if (!response.ok) throw new Error(`Error ${response.status}: ${response.statusText}`);
            const data = await response.json();
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar empleados: ${err.message}` });
        }
    };

    const fetchTiposDeducciones = async () => {
        try {
            const response = await fetch('/api/deducciones/tipos');
            if (!response.ok) throw new Error(`Error ${response.status}: ${response.statusText}`);
            const data = await response.json();
            setTiposDeducciones(data);
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar tipos de deducciones: ${err.message}` });
        }
    };

    useEffect(() => {
        fetchDeducciones();
    }, [filterEmpleado, filterTipo, filterActivas]);

    // Format currency
    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    // Stats calculations
    const deduccionesActivas = deducciones.filter(d => d.estaActivo);
    const salarioPromedio = empleados.length > 0
        ? empleados.reduce((sum, e) => sum + e.salarioBase, 0) / empleados.length
        : 1500;

    const totalMensual = deduccionesActivas.reduce((sum, d) => {
        if (d.esPorcentaje) {
            return sum + (salarioPromedio * d.porcentaje / 100);
        }
        return sum + d.monto;
    }, 0);

    const empleadosAfectados = new Set(deducciones.map(d => d.empleadoId)).size;

    // Contar por tipo
    const contarPorTipo = () => {
        const counts = {};
        deducciones.forEach(d => {
            counts[d.tipoDeduccion] = (counts[d.tipoDeduccion] || 0) + 1;
        });
        return counts;
    };

    const countsporTipo = contarPorTipo();

    // Tipo badge color
    const getTipoBadgeColor = (tipo) => {
        const colors = {
            'SeguridadSocial': 'bg-blue-100 text-blue-800',
            'SeguroEducativo': 'bg-green-100 text-green-800',
            'ImpuestoRenta': 'bg-purple-100 text-purple-800',
            'Prestamo': 'bg-yellow-100 text-yellow-800',
            'Embargo': 'bg-red-100 text-red-800',
            'Descuento': 'bg-gray-100 text-gray-800',
            'Otro': 'bg-gray-100 text-gray-800'
        };
        return colors[tipo] || 'bg-gray-100 text-gray-800';
    };

    const getTipoNombre = (tipo) => {
        const nombres = {
            'SeguridadSocial': 'Seguridad Social',
            'SeguroEducativo': 'Seguro Educativo',
            'ImpuestoRenta': 'Impuesto sobre la Renta',
            'Prestamo': 'Préstamo',
            'Embargo': 'Embargo',
            'Descuento': 'Descuento',
            'Otro': 'Otro'
        };
        return nombres[tipo] || tipo;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        // Validaciones
        if (formData.esPorcentaje) {
            if (parseFloat(formData.porcentaje) <= 0 || parseFloat(formData.porcentaje) > 100) {
                showToast({ type: 'error', message: 'El porcentaje debe estar entre 0.01 y 100' });
                return;
            }
        } else {
            if (parseFloat(formData.monto) <= 0) {
                showToast({ type: 'error', message: 'El monto debe ser mayor a 0' });
                return;
            }
        }

        try {
            const url = editingId ? `/api/deducciones/${editingId}` : '/api/deducciones';
            const method = editingId ? 'PUT' : 'POST';

            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                tipoDeduccion: parseInt(formData.tipoDeduccion),
                descripcion: formData.descripcion,
                esPorcentaje: formData.esPorcentaje,
                monto: formData.esPorcentaje ? 0 : parseFloat(formData.monto),
                porcentaje: formData.esPorcentaje ? parseFloat(formData.porcentaje) : 0,
                fechaInicio: formData.fechaInicio,
                fechaFin: formData.fechaFin || null,
                prioridad: parseInt(formData.prioridad),
                referencia: formData.referencia || null,
                observaciones: formData.observaciones || null
            };

            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Error ${response.status}: ${errorText}`);
            }

            await fetchDeducciones();
            showToast({
                type: 'success',
                message: editingId ? 'Deducción actualizada exitosamente' : 'Deducción creada exitosamente'
            });
            resetForm();
        } catch (err) {
            showToast({ type: 'error', message: `Error al guardar deducción: ${err.message}` });
        }
    };

    const handleEdit = (deduccion) => {
        setFormData({
            empleadoId: deduccion.empleadoId.toString(),
            tipoDeduccion: deduccion.tipoDeduccion,
            descripcion: deduccion.descripcion,
            esPorcentaje: deduccion.esPorcentaje,
            monto: deduccion.monto.toString(),
            porcentaje: deduccion.porcentaje.toString(),
            fechaInicio: new Date(deduccion.fechaInicio).toISOString().split('T')[0],
            fechaFin: deduccion.fechaFin ? new Date(deduccion.fechaFin).toISOString().split('T')[0] : '',
            prioridad: deduccion.prioridad.toString(),
            referencia: deduccion.referencia || '',
            observaciones: deduccion.observaciones || ''
        });
        setEditingId(deduccion.id);
        setShowModal(true);
    };

    const confirmDelete = (deduccion) => {
        setDeduccionToDelete(deduccion);
        setShowConfirmDelete(true);
    };

    const handleDelete = async () => {
        if (!deduccionToDelete) return;

        try {
            const response = await fetch(`/api/deducciones/${deduccionToDelete.id}`, { method: 'DELETE' });
            if (!response.ok) throw new Error(`Error ${response.status}: ${response.statusText}`);

            await fetchDeducciones();
            showToast({ type: 'success', message: 'Deducción desactivada exitosamente' });
            setShowConfirmDelete(false);
            setDeduccionToDelete(null);
        } catch (err) {
            showToast({ type: 'error', message: `Error al desactivar deducción: ${err.message}` });
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            empleadoId: '',
            tipoDeduccion: '',
            descripcion: '',
            esPorcentaje: false,
            monto: '',
            porcentaje: '',
            fechaInicio: new Date().toISOString().split('T')[0],
            fechaFin: '',
            prioridad: '10',
            referencia: '',
            observaciones: ''
        });
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-600">Cargando deducciones...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Deducciones Activas</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{deduccionesActivas.length}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Vigentes actualmente</span>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Total Mensual Est.</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{formatCurrency(totalMensual)}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Estimación mensual</span>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Por Tipo</p>
                            <div className="mt-2 flex flex-wrap gap-1">
                                {Object.entries(countsporTipo).slice(0, 3).map(([tipo, count]) => (
                                    <span key={tipo} className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${getTipoBadgeColor(tipo)}`}>
                                        {count}
                                    </span>
                                ))}
                            </div>
                        </div>
                        <div className="w-12 h-12 bg-yellow-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-yellow-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Clasificadas</span>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Empleados Afectados</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{empleadosAfectados}</p>
                        </div>
                        <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Con deducciones</span>
                    </div>
                </div>
            </div>

            {/* Filters and Actions */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={() => setShowModal(true)}
                    className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Agregar Deducción
                </button>

                <div className="flex flex-col sm:flex-row gap-3 w-full sm:w-auto">
                    <select
                        value={filterEmpleado}
                        onChange={(e) => setFilterEmpleado(e.target.value)}
                        className="px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                        <option value="">Todos los empleados</option>
                        {empleados.map(emp => (
                            <option key={emp.id} value={emp.id}>
                                {emp.nombre} {emp.apellido}
                            </option>
                        ))}
                    </select>

                    <select
                        value={filterTipo}
                        onChange={(e) => setFilterTipo(e.target.value)}
                        className="px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                        <option value="">Todos los tipos</option>
                        {tiposDeducciones.map(tipo => (
                            <option key={tipo.id || tipo} value={tipo.id || tipo}>
                                {tipo.nombre || getTipoNombre(tipo)}
                            </option>
                        ))}
                    </select>

                    <label className="inline-flex items-center px-3 py-2 bg-white border border-gray-300 rounded-lg cursor-pointer">
                        <input
                            type="checkbox"
                            checked={filterActivas}
                            onChange={(e) => setFilterActivas(e.target.checked)}
                            className="mr-2"
                        />
                        <span className="text-sm font-medium text-gray-700">Solo activas</span>
                    </label>
                </div>
            </div>

            {/* Deducciones Table */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
                        Lista de Deducciones
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({deducciones.length} {deducciones.length === 1 ? 'deducción' : 'deducciones'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50 border-b border-gray-200">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Descripción</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Monto/%</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Vigencia</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Prioridad</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {deducciones.map((deduccion) => (
                                <tr key={deduccion.id} className="hover:bg-gray-50 transition-colors">
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {deduccion.empleadoNombre || 'N/A'}
                                    </td>
                                    <td className="py-4 px-6">
                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getTipoBadgeColor(deduccion.tipoDeduccion)}`}>
                                            {getTipoNombre(deduccion.tipoDeduccion)}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">{deduccion.descripcion}</td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {deduccion.esPorcentaje
                                            ? `${deduccion.porcentaje}%`
                                            : formatCurrency(deduccion.monto)
                                        }
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-600">
                                        {new Date(deduccion.fechaInicio).toLocaleDateString('es-PA')}
                                        {' - '}
                                        {deduccion.fechaFin
                                            ? new Date(deduccion.fechaFin).toLocaleDateString('es-PA')
                                            : 'Indefinida'
                                        }
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">{deduccion.prioridad}</td>
                                    <td className="py-4 px-6">
                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                            deduccion.estaActivo
                                                ? 'bg-green-100 text-green-800'
                                                : 'bg-gray-100 text-gray-800'
                                        }`}>
                                            <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${deduccion.estaActivo ? 'bg-green-600' : 'bg-gray-600'}`}></span>
                                            {deduccion.estaActivo ? 'Activo' : 'Inactivo'}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2">
                                            <button
                                                onClick={() => handleEdit(deduccion)}
                                                className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                </svg>
                                                Editar
                                            </button>
                                            {deduccion.estaActivo && (
                                                <button
                                                    onClick={() => confirmDelete(deduccion)}
                                                    className="inline-flex items-center gap-1 text-red-600 hover:text-red-800 font-medium text-sm"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                    </svg>
                                                    Desactivar
                                                </button>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {/* Empty State */}
                    {deducciones.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-900 mb-1">No hay deducciones registradas</h3>
                            <p className="text-gray-500">Comienza agregando la primera deducción</p>
                        </div>
                    )}
                </div>
            </div>

            {/* Create/Edit Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
                            <h3 className="text-xl font-semibold text-gray-900">
                                {editingId ? 'Editar Deducción' : 'Nueva Deducción'}
                            </h3>
                            <button onClick={resetForm} className="text-gray-400 hover:text-gray-600">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Empleado <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.empleadoId}
                                        onChange={(e) => setFormData({ ...formData, empleadoId: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
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
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Tipo de Deducción <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoDeduccion}
                                        onChange={(e) => setFormData({ ...formData, tipoDeduccion: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    >
                                        <option value="">Seleccionar tipo...</option>
                                        {tiposDeducciones.map(tipo => (
                                            <option key={tipo.id || tipo} value={tipo.id || tipo}>
                                                {tipo.nombre || getTipoNombre(tipo)}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Descripción <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.descripcion}
                                        onChange={(e) => setFormData({ ...formData, descripcion: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        placeholder="Descripción de la deducción"
                                    />
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Tipo de Cálculo <span className="text-red-500">*</span>
                                    </label>
                                    <div className="flex gap-4">
                                        <label className="inline-flex items-center">
                                            <input
                                                type="radio"
                                                checked={!formData.esPorcentaje}
                                                onChange={() => setFormData({ ...formData, esPorcentaje: false })}
                                                className="mr-2"
                                            />
                                            <span className="text-sm text-gray-700">Monto Fijo</span>
                                        </label>
                                        <label className="inline-flex items-center">
                                            <input
                                                type="radio"
                                                checked={formData.esPorcentaje}
                                                onChange={() => setFormData({ ...formData, esPorcentaje: true })}
                                                className="mr-2"
                                            />
                                            <span className="text-sm text-gray-700">Porcentaje sobre salario</span>
                                        </label>
                                    </div>
                                </div>

                                {!formData.esPorcentaje && (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Monto <span className="text-red-500">*</span>
                                        </label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                            <input
                                                type="number"
                                                step="0.01"
                                                min="0.01"
                                                required={!formData.esPorcentaje}
                                                value={formData.monto}
                                                onChange={(e) => setFormData({ ...formData, monto: e.target.value })}
                                                className="w-full pl-8 pr-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                                placeholder="150.00"
                                            />
                                        </div>
                                    </div>
                                )}

                                {formData.esPorcentaje && (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Porcentaje <span className="text-red-500">*</span>
                                        </label>
                                        <div className="relative">
                                            <input
                                                type="number"
                                                step="0.01"
                                                min="0.01"
                                                max="100"
                                                required={formData.esPorcentaje}
                                                value={formData.porcentaje}
                                                onChange={(e) => setFormData({ ...formData, porcentaje: e.target.value })}
                                                className="w-full pr-10 pl-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                                placeholder="5.00"
                                            />
                                            <span className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-500">%</span>
                                        </div>
                                    </div>
                                )}

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha de Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaInicio}
                                        onChange={(e) => setFormData({ ...formData, fechaInicio: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha de Fin
                                    </label>
                                    <input
                                        type="date"
                                        value={formData.fechaFin}
                                        onChange={(e) => setFormData({ ...formData, fechaFin: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Dejar vacío para deducción indefinida</p>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Prioridad
                                    </label>
                                    <input
                                        type="number"
                                        min="1"
                                        max="99"
                                        value={formData.prioridad}
                                        onChange={(e) => setFormData({ ...formData, prioridad: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Orden de aplicación (menor = mayor prioridad)</p>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Referencia
                                    </label>
                                    <input
                                        type="text"
                                        value={formData.referencia}
                                        onChange={(e) => setFormData({ ...formData, referencia: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        placeholder="Número de referencia"
                                    />
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Observaciones
                                    </label>
                                    <textarea
                                        rows="3"
                                        value={formData.observaciones}
                                        onChange={(e) => setFormData({ ...formData, observaciones: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        placeholder="Notas adicionales..."
                                    ></textarea>
                                </div>
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium"
                                >
                                    {editingId ? 'Actualizar Deducción' : 'Crear Deducción'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Confirmation Delete Modal */}
            {showConfirmDelete && deduccionToDelete && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-900 text-center mb-2">
                                Desactivar Deducción
                            </h3>
                            <p className="text-gray-600 text-center mb-6">
                                ¿Está seguro de que desea desactivar esta deducción? Esta acción marcará la deducción como inactiva.
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => {
                                        setShowConfirmDelete(false);
                                        setDeduccionToDelete(null);
                                    }}
                                    className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleDelete}
                                    className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium"
                                >
                                    Desactivar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default DeduccionesPage;
