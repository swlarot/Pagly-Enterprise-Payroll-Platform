import React, { useState, useEffect } from 'react';
import { useToast } from '../components/ToastContext';

const AusenciasPage = () => {
    const { showToast } = useToast();

    // State management
    const [ausencias, setAusencias] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [tipos, setTipos] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [editingId, setEditingId] = useState(null);

    // Filters
    const [filters, setFilters] = useState({
        empleadoId: '',
        tipo: ''
    });

    // Form data
    const [formData, setFormData] = useState({
        empleadoId: '',
        tipoAusencia: '1',
        fechaInicio: new Date().toISOString().split('T')[0],
        fechaFin: new Date().toISOString().split('T')[0],
        motivo: '',
        tieneJustificacion: false,
        documentoReferencia: ''
    });

    useEffect(() => {
        fetchAusencias();
        fetchEmpleados();
        fetchTipos();
    }, []);

    const fetchAusencias = async () => {
        try {
            setLoading(true);
            const response = await fetch('/api/ausencias');
            if (!response.ok) throw new Error(`Error ${response.status}`);
            const data = await response.json();
            setAusencias(data);
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar ausencias: ${err.message}` });
        } finally {
            setLoading(false);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const response = await fetch('/api/empleados');
            if (!response.ok) throw new Error(`Error ${response.status}`);
            const data = await response.json();
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar empleados: ${err.message}` });
        }
    };

    const fetchTipos = async () => {
        try {
            const response = await fetch('/api/ausencias/tipos');
            if (!response.ok) throw new Error(`Error ${response.status}`);
            const data = await response.json();
            setTipos(data);
        } catch (err) {
            showToast({ type: 'error', message: `Error al cargar tipos: ${err.message}` });
        }
    };

    // Filter ausencias
    const filteredAusencias = ausencias.filter(a => {
        if (filters.empleadoId && a.empleadoId !== parseInt(filters.empleadoId)) return false;
        if (filters.tipo && a.tipoAusencia !== parseInt(filters.tipo)) return false;
        return true;
    });

    // Calculate stats
    const totalAusencias = ausencias.length;
    const diasPerdidos = ausencias.reduce((sum, a) => sum + a.diasAusencia, 0);
    const injustificadas = ausencias.filter(a => a.tipoAusencia === 1).length;
    const conJustificacion = ausencias.filter(a => a.tieneJustificacion).length;

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        try {
            const url = editingId ? `/api/ausencias/${editingId}` : '/api/ausencias';
            const method = editingId ? 'PUT' : 'POST';

            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                tipoAusencia: parseInt(formData.tipoAusencia),
                fechaInicio: formData.fechaInicio,
                fechaFin: formData.fechaFin,
                motivo: formData.motivo,
                tieneJustificacion: formData.tieneJustificacion,
                documentoReferencia: formData.documentoReferencia || null
            };

            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            await fetchAusencias();
            showToast({
                type: 'success',
                message: editingId ? 'Ausencia actualizada' : 'Ausencia registrada'
            });
            resetForm();
        } catch (err) {
            showToast({ type: 'error', message: `Error: ${err.message}` });
        }
    };

    const handleEdit = (ausencia) => {
        setFormData({
            empleadoId: ausencia.empleadoId.toString(),
            tipoAusencia: ausencia.tipoAusencia.toString(),
            fechaInicio: new Date(ausencia.fechaInicio).toISOString().split('T')[0],
            fechaFin: new Date(ausencia.fechaFin).toISOString().split('T')[0],
            motivo: ausencia.motivo || '',
            tieneJustificacion: ausencia.tieneJustificacion,
            documentoReferencia: ausencia.documentoReferencia || ''
        });
        setEditingId(ausencia.id);
        setShowModal(true);
    };

    const handleDelete = async (id) => {
        if (!window.confirm('¿Está seguro de eliminar esta ausencia?')) return;

        try {
            const response = await fetch(`/api/ausencias/${id}`, { method: 'DELETE' });
            if (!response.ok) throw new Error('Error al eliminar');
            await fetchAusencias();
            showToast({ type: 'success', message: 'Ausencia eliminada' });
        } catch (err) {
            showToast({ type: 'error', message: err.message });
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            empleadoId: '',
            tipoAusencia: '1',
            fechaInicio: new Date().toISOString().split('T')[0],
            fechaFin: new Date().toISOString().split('T')[0],
            motivo: '',
            tieneJustificacion: false,
            documentoReferencia: ''
        });
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-600">Cargando ausencias...</p>
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
                            <p className="text-sm font-medium text-gray-600">Total Ausencias</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{totalAusencias}</p>
                        </div>
                        <div className="w-12 h-12 bg-gray-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Días Perdidos</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{diasPerdidos}</p>
                        </div>
                        <div className="w-12 h-12 bg-red-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Injustificadas</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{injustificadas}</p>
                        </div>
                        <div className="w-12 h-12 bg-yellow-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-yellow-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Con Justificación</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{conJustificacion}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>
            </div>

            {/* Filters and Action Bar */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
                <div className="flex flex-col md:flex-row gap-4 items-start md:items-center justify-between">
                    <div className="flex flex-col sm:flex-row gap-3 flex-1">
                        <select
                            value={filters.empleadoId}
                            onChange={(e) => setFilters({ ...filters, empleadoId: e.target.value })}
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
                            value={filters.tipo}
                            onChange={(e) => setFilters({ ...filters, tipo: e.target.value })}
                            className="px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                        >
                            <option value="">Todos los tipos</option>
                            {tipos.map(tipo => (
                                <option key={tipo.Id} value={tipo.Id}>
                                    {tipo.Nombre}
                                </option>
                            ))}
                        </select>
                    </div>

                    <button
                        onClick={() => setShowModal(true)}
                        className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                        </svg>
                        Registrar Ausencia
                    </button>
                </div>
            </div>

            {/* Table */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
                        Ausencias Registradas
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredAusencias.length} registros)
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50 border-b border-gray-200">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Período</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Días</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Justificación</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Afecta Salario</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Monto</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {filteredAusencias.map((aus) => (
                                <tr key={aus.id} className="hover:bg-gray-50 transition-colors">
                                    <td className="py-4 px-6 text-sm text-gray-900">{aus.empleadoNombre}</td>
                                    <td className="py-4 px-6">
                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                            aus.afectaSalario
                                                ? 'bg-red-100 text-red-800'
                                                : 'bg-blue-100 text-blue-800'
                                        }`}>
                                            {aus.tipoNombre}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-500">
                                        {new Date(aus.fechaInicio).toLocaleDateString('es-PA', { day: '2-digit', month: 'short' })} - {new Date(aus.fechaFin).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {aus.diasAusencia}
                                    </td>
                                    <td className="py-4 px-6">
                                        {aus.tieneJustificacion ? (
                                            <div className="flex items-center gap-2">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                                                    <svg className="w-3 h-3 mr-1" fill="currentColor" viewBox="0 0 20 20">
                                                        <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                                                    </svg>
                                                    Con justificación
                                                </span>
                                                {aus.documentoReferencia && (
                                                    <svg className="w-4 h-4 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                                    </svg>
                                                )}
                                            </div>
                                        ) : (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                                                Sin justificación
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-4 px-6">
                                        {aus.afectaSalario ? (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800">
                                                Sí
                                            </span>
                                        ) : (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                                                No
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {aus.montoDescontado ? formatCurrency(aus.montoDescontado) : '-'}
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2">
                                            <button
                                                onClick={() => handleEdit(aus)}
                                                className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                </svg>
                                                Editar
                                            </button>
                                            <button
                                                onClick={() => handleDelete(aus.id)}
                                                className="inline-flex items-center gap-1 text-red-600 hover:text-red-800 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                                </svg>
                                                Eliminar
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {filteredAusencias.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-900 mb-1">
                                No hay ausencias registradas
                            </h3>
                            <p className="text-gray-500 mb-4">
                                Comienza registrando la primera ausencia
                            </p>
                            <button
                                onClick={() => setShowModal(true)}
                                className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                </svg>
                                Registrar Ausencia
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
                            <h3 className="text-xl font-semibold text-gray-900">
                                {editingId ? 'Editar Ausencia' : 'Registrar Ausencia'}
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
                                                {emp.nombre} {emp.apellido} - {emp.posicionNombre || 'Sin posición'}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Tipo <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoAusencia}
                                        onChange={(e) => setFormData({ ...formData, tipoAusencia: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    >
                                        {tipos.map(tipo => (
                                            <option key={tipo.Id} value={tipo.Id}>
                                                {tipo.Nombre}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div className="flex items-center pt-6">
                                    <label className="flex items-center gap-2 cursor-pointer">
                                        <input
                                            type="checkbox"
                                            checked={formData.tieneJustificacion}
                                            onChange={(e) => setFormData({ ...formData, tieneJustificacion: e.target.checked })}
                                            className="w-4 h-4 text-blue-600 rounded focus:ring-2 focus:ring-blue-500"
                                        />
                                        <span className="text-sm font-medium text-gray-700">Tiene justificación</span>
                                    </label>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha Inicio <span className="text-red-500">*</span>
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
                                        Fecha Fin <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaFin}
                                        onChange={(e) => setFormData({ ...formData, fechaFin: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                </div>

                                {formData.tieneJustificacion && (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Documento de Referencia
                                        </label>
                                        <input
                                            type="text"
                                            value={formData.documentoReferencia}
                                            onChange={(e) => setFormData({ ...formData, documentoReferencia: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                            placeholder="Ej: Certificado médico #12345"
                                        />
                                        <p className="text-xs text-gray-500 mt-1">
                                            Número de certificado médico, carta, u otro documento de justificación
                                        </p>
                                    </div>
                                )}

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Motivo <span className="text-red-500">*</span>
                                    </label>
                                    <textarea
                                        required
                                        value={formData.motivo}
                                        onChange={(e) => setFormData({ ...formData, motivo: e.target.value })}
                                        rows="3"
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        placeholder="Describir el motivo de la ausencia..."
                                    />
                                </div>
                            </div>

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
                                    {editingId ? 'Actualizar' : 'Registrar'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AusenciasPage;
