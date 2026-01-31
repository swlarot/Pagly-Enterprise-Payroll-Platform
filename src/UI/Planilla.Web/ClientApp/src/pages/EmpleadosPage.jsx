import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

const EmpleadosPage = () => {
    // State management
    const [empleados, setEmpleados] = useState([]);
    const [departamentos, setDepartamentos] = useState([]);
    const [posiciones, setPosiciones] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [showModal, setShowModal] = useState(false);
    const [showConfirmDelete, setShowConfirmDelete] = useState(false);
    const [empleadoToDelete, setEmpleadoToDelete] = useState(null);
    const [editingId, setEditingId] = useState(null);
    const [formData, setFormData] = useState({
        nombre: '',
        apellido: '',
        numeroIdentificacion: '',
        salarioBase: '',
        fechaContratacion: '',
        departamentoId: '',
        posicionId: ''
    });

    // Fetch employees and departments on mount
    useEffect(() => {
        fetchEmpleados();
        fetchDepartamentos();
    }, []);

    // Fetch positions when department changes
    useEffect(() => {
        if (formData.departamentoId) {
            fetchPosiciones(formData.departamentoId);
        } else {
            setPosiciones([]);
        }
    }, [formData.departamentoId]);

    const fetchEmpleados = async () => {
        try {
            setLoading(true);
            const data = await api.get('/api/empleados');
            setEmpleados(data);
        } catch (err) {
            toast.error(err.message || 'Error al cargar empleados');
        } finally {
            setLoading(false);
        }
    };

    const fetchDepartamentos = async () => {
        try {
            const data = await api.get('/api/departamentos');
            setDepartamentos(data.filter(d => d.estaActivo));
        } catch (err) {
            toast.error(err.message || 'Error al cargar departamentos');
        }
    };

    const fetchPosiciones = async (departamentoId) => {
        try {
            const data = await api.get(`/api/posiciones?departamentoId=${departamentoId}`);
            setPosiciones(data.filter(p => p.estaActivo));
        } catch (err) {
            toast.error(err.message || 'Error al cargar posiciones');
        }
    };

    // Filter employees based on search term
    const filteredEmpleados = empleados.filter(emp =>
        (emp.nombre ?? '').toLowerCase().includes(searchTerm.toLowerCase()) ||
        (emp.apellido ?? '').toLowerCase().includes(searchTerm.toLowerCase()) ||
        (emp.numeroIdentificacion ?? '').includes(searchTerm)
    );

    const activeEmpleados = empleados.filter(emp => emp.estaActivo);
    const totalNomina = activeEmpleados.reduce((sum, emp) => sum + emp.salarioBase, 0);

    // Format currency
    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    // Handle form submission (Create/Update)
    const handleSubmit = async (e) => {
        e.preventDefault();

        try {
            const url = editingId ? `/api/empleados/${editingId}` : '/api/empleados';
            const method = editingId ? 'PUT' : 'POST';

            // Prepare payload
            const payload = {
                nombre: formData.nombre,
                apellido: formData.apellido,
                salarioBase: parseFloat(formData.salarioBase),
                departamentoId: formData.departamentoId ? parseInt(formData.departamentoId) : null,
                posicionId: formData.posicionId ? parseInt(formData.posicionId) : null,
                ...(editingId ? { estaActivo: true } : {
                    numeroIdentificacion: formData.numeroIdentificacion,
                    fechaContratacion: formData.fechaContratacion || new Date().toISOString().split('T')[0]
                })
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

            // Refresh the list
            await fetchEmpleados();

            // Show success message
            toast.success(editingId ? 'Empleado actualizado exitosamente' : 'Empleado creado exitosamente');

            // Reset form and close modal
            resetForm();

        } catch (err) {
            toast.error(`Error al guardar empleado: ${err.message}`);
        }
    };

    // Handle edit
    const handleEdit = (empleado) => {
        setFormData({
            nombre: empleado.nombre,
            apellido: empleado.apellido,
            numeroIdentificacion: empleado.numeroIdentificacion,
            salarioBase: empleado.salarioBase.toString(),
            fechaContratacion: empleado.fechaContratacion ? new Date(empleado.fechaContratacion).toISOString().split('T')[0] : '',
            departamentoId: empleado.departamentoId ? empleado.departamentoId.toString() : '',
            posicionId: empleado.posicionId ? empleado.posicionId.toString() : ''
        });
        setEditingId(empleado.id);
        setShowModal(true);
    };

    // Handle delete (soft delete)
    const confirmDelete = (empleado) => {
        setEmpleadoToDelete(empleado);
        setShowConfirmDelete(true);
    };

    const handleDelete = async () => {
        if (!empleadoToDelete) return;

        try {
            const response = await fetch(`/api/empleados/${empleadoToDelete.id}`, { method: 'DELETE' });
            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }
            await fetchEmpleados();
            toast.success('Empleado desactivado exitosamente');
            setShowConfirmDelete(false);
            setEmpleadoToDelete(null);
        } catch (err) {
            toast.error(`Error al desactivar empleado: ${err.message}`);
        }
    };

    // Reset form
    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            nombre: '',
            apellido: '',
            numeroIdentificacion: '',
            salarioBase: '',
            fechaContratacion: '',
            departamentoId: '',
            posicionId: ''
        });
    };

    // Open new employee modal
    const openNewModal = () => {
        resetForm();
        setShowModal(true);
    };

    // Handle department change - reset position
    const handleDepartmentChange = (e) => {
        setFormData({
            ...formData,
            departamentoId: e.target.value,
            posicionId: ''
        });
    };

    // Get selected position salary range
    const getSelectedPositionSalaryRange = () => {
        if (!formData.posicionId) return null;
        const posicion = posiciones.find(p => p.id === parseInt(formData.posicionId));
        if (!posicion) return null;
        return {
            min: posicion.salarioMinimo,
            max: posicion.salarioMaximo
        };
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-600">Cargando empleados...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Total Empleados</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{empleados.length}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Registrados en el sistema</span>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Empleados Activos</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{activeEmpleados.length}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-green-600 font-medium">
                            {empleados.length > 0 ? ((activeEmpleados.length / empleados.length) * 100).toFixed(1) : 0}%
                        </span>
                        <span className="text-gray-500 ml-2">del total</span>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Nómina Mensual Est.</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{formatCurrency(totalNomina)}</p>
                        </div>
                        <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Solo salarios base</span>
                    </div>
                </div>
            </div>

            {/* Action Bar */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={openNewModal}
                    className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Agregar Empleado
                </button>

                <div className="relative w-full sm:w-auto">
                    <input
                        type="text"
                        placeholder="Buscar por nombre, apellido o cédula..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        className="w-full sm:w-80 pl-10 pr-4 py-2.5 bg-white border border-gray-300 rounded-lg text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                    <svg className="w-5 h-5 text-gray-400 absolute left-3 top-1/2 transform -translate-y-1/2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                </div>
            </div>

            {/* Employee Table */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
                        Lista de Empleados
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredEmpleados.length} {filteredEmpleados.length === 1 ? 'empleado' : 'empleados'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50 border-b border-gray-200">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Nombre</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Identificación</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Salario Base</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Fecha Contratación</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Departamento</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Posición</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {filteredEmpleados.map((empleado) => (
                                <tr key={empleado.id} className="hover:bg-gray-50 transition-colors">
                                    <td className="py-4 px-6">
                                        <div className="flex items-center">
                                            <div className="w-10 h-10 bg-gradient-to-br from-blue-500 to-blue-600 rounded-full flex items-center justify-center text-white font-semibold text-sm mr-3">
                                                {empleado.nombre.charAt(0)}{empleado.apellido.charAt(0)}
                                            </div>
                                            <div>
                                                <div className="text-sm font-medium text-gray-900">
                                                    {empleado.nombre} {empleado.apellido}
                                                </div>
                                                <div className="text-sm text-gray-500">ID: {empleado.id}</div>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">{empleado.numeroIdentificacion}</td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">{formatCurrency(empleado.salarioBase)}</td>
                                    <td className="py-4 px-6 text-sm text-gray-500">
                                        {new Date(empleado.fechaContratacion).toLocaleDateString('es-PA', {
                                            day: '2-digit',
                                            month: 'short',
                                            year: 'numeric'
                                        })}
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">
                                        {empleado.departamentoNombre || <span className="text-gray-400">-</span>}
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">
                                        {empleado.posicionNombre || <span className="text-gray-400">-</span>}
                                    </td>
                                    <td className="py-4 px-6">
                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                            empleado.estaActivo
                                                ? 'bg-green-100 text-green-800'
                                                : 'bg-red-100 text-red-800'
                                        }`}>
                                            <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${empleado.estaActivo ? 'bg-green-600' : 'bg-red-600'}`}></span>
                                            {empleado.estaActivo ? 'Activo' : 'Inactivo'}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2">
                                            <button
                                                onClick={() => handleEdit(empleado)}
                                                className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                </svg>
                                                Editar
                                            </button>
                                            {empleado.estaActivo && (
                                                <button
                                                    onClick={() => confirmDelete(empleado)}
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
                    {filteredEmpleados.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-900 mb-1">
                                {searchTerm ? 'No se encontraron empleados' : 'No hay empleados registrados'}
                            </h3>
                            <p className="text-gray-500">
                                {searchTerm
                                    ? `No hay resultados para "${searchTerm}"`
                                    : 'Comienza agregando tu primer empleado'
                                }
                            </p>
                            {!searchTerm && (
                                <button
                                    onClick={openNewModal}
                                    className="mt-4 inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                                >
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                    </svg>
                                    Agregar Empleado
                                </button>
                            )}
                        </div>
                    )}
                </div>
            </div>

            {/* Add/Edit Employee Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        {/* Modal Header */}
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
                            <h3 className="text-xl font-semibold text-gray-900">
                                {editingId ? 'Editar Empleado' : 'Nuevo Empleado'}
                            </h3>
                            <button
                                onClick={resetForm}
                                className="text-gray-400 hover:text-gray-600 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        {/* Modal Body */}
                        <form onSubmit={handleSubmit} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Nombre <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.nombre}
                                        onChange={(e) => setFormData({ ...formData, nombre: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                        placeholder="Ej: Juan"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Apellido <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.apellido}
                                        onChange={(e) => setFormData({ ...formData, apellido: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                        placeholder="Ej: Pérez"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Número de Identificación <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required={!editingId}
                                        disabled={editingId}
                                        value={formData.numeroIdentificacion}
                                        onChange={(e) => setFormData({ ...formData, numeroIdentificacion: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:bg-gray-100 disabled:cursor-not-allowed"
                                        placeholder="Ej: 8-123-4567"
                                    />
                                    {editingId && (
                                        <p className="text-xs text-gray-500 mt-1">No se puede editar la identificación</p>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Salario Base <span className="text-red-500">*</span>
                                    </label>
                                    <div className="relative">
                                        <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                        <input
                                            type="number"
                                            step="0.01"
                                            min="0.01"
                                            required
                                            value={formData.salarioBase}
                                            onChange={(e) => setFormData({ ...formData, salarioBase: e.target.value })}
                                            className="w-full pl-8 pr-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                            placeholder="1500.00"
                                        />
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Departamento
                                    </label>
                                    <select
                                        value={formData.departamentoId}
                                        onChange={handleDepartmentChange}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                    >
                                        <option value="">Seleccionar departamento...</option>
                                        {departamentos.map(dept => (
                                            <option key={dept.id} value={dept.id}>
                                                {dept.nombre}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Posición
                                    </label>
                                    <select
                                        value={formData.posicionId}
                                        onChange={(e) => setFormData({ ...formData, posicionId: e.target.value })}
                                        disabled={!formData.departamentoId}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:bg-gray-100 disabled:cursor-not-allowed"
                                    >
                                        <option value="">Seleccionar posición...</option>
                                        {posiciones.map(pos => (
                                            <option key={pos.id} value={pos.id}>
                                                {pos.nombre}
                                            </option>
                                        ))}
                                    </select>
                                    {!formData.departamentoId && (
                                        <p className="text-xs text-gray-500 mt-1">Seleccione primero un departamento</p>
                                    )}
                                    {formData.posicionId && getSelectedPositionSalaryRange() && (
                                        <p className="text-xs text-blue-600 mt-1">
                                            Rango salarial: {formatCurrency(getSelectedPositionSalaryRange().min)} - {formatCurrency(getSelectedPositionSalaryRange().max)}
                                        </p>
                                    )}
                                </div>

                                {!editingId && (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Fecha de Contratación <span className="text-red-500">*</span>
                                        </label>
                                        <input
                                            type="date"
                                            required={!editingId}
                                            value={formData.fechaContratacion}
                                            onChange={(e) => setFormData({ ...formData, fechaContratacion: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                        />
                                    </div>
                                )}
                            </div>

                            {/* Modal Footer */}
                            <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors shadow-sm"
                                >
                                    {editingId ? 'Actualizar Empleado' : 'Crear Empleado'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Confirmation Delete Modal */}
            {showConfirmDelete && empleadoToDelete && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-900 text-center mb-2">
                                Desactivar Empleado
                            </h3>
                            <p className="text-gray-600 text-center mb-6">
                                ¿Está seguro de que desea desactivar a <strong>{empleadoToDelete.nombre} {empleadoToDelete.apellido}</strong>?
                                Esta acción marcará al empleado como inactivo.
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => {
                                        setShowConfirmDelete(false);
                                        setEmpleadoToDelete(null);
                                    }}
                                    className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleDelete}
                                    className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium transition-colors"
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

export default EmpleadosPage;
