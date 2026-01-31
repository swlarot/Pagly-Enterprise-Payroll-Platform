import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

const HorasExtraPage = () => {

    // State management
    const [horasExtra, setHorasExtra] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [tipos, setTipos] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [editingId, setEditingId] = useState(null);

    // Filters
    const [filters, setFilters] = useState({
        empleadoId: '',
        tipo: '',
        soloPendientes: false
    });

    // Form data
    const [formData, setFormData] = useState({
        empleadoId: '',
        fecha: new Date().toISOString().split('T')[0],
        tipoHoraExtra: '1',
        horaInicio: '09:00',
        horaFin: '10:00',
        motivo: ''
    });

    useEffect(() => {
        fetchHorasExtra();
        fetchEmpleados();
        fetchTipos();
    }, []);

    const fetchHorasExtra = async () => {
        try {
            setLoading(true);
            const data = await api.get('/api/horasextra');
            setHorasExtra(data);
        } catch (err) {
            toast.error(`Error al cargar horas extra: ${err.message}`);
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

    const fetchTipos = async () => {
        try {
            const data = await api.get('/api/horasextra/tipos');
            setTipos(data);
        } catch (err) {
            toast.error(`Error al cargar tipos: ${err.message}`);
        }
    };

    // Filter hours extra
    const filteredHorasExtra = horasExtra.filter(he => {
        if (filters.empleadoId && he.empleadoId !== parseInt(filters.empleadoId)) return false;
        if (filters.tipo && he.tipoHoraExtra !== parseInt(filters.tipo)) return false;
        if (filters.soloPendientes && he.estaAprobada) return false;
        return true;
    });

    // Calculate stats
    const pendientes = horasExtra.filter(h => !h.estaAprobada).length;
    const thisMonth = new Date();
    const horasEsteMes = horasExtra
        .filter(h => {
            const fecha = new Date(h.fecha);
            return fecha.getMonth() === thisMonth.getMonth() &&
                   fecha.getFullYear() === thisMonth.getFullYear();
        })
        .reduce((sum, h) => sum + h.cantidadHoras, 0);

    const montoEstimado = horasExtra
        .filter(h => !h.estaAprobada)
        .reduce((sum, h) => sum + (h.montoCalculado || 0), 0);

    const porTipo = tipos.map(tipo => ({
        ...tipo,
        cantidad: horasExtra.filter(h => h.tipoHoraExtra === tipo.valor).length
    }));

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    const formatTime = (timeSpan) => {
        // timeSpan viene como "09:00:00" del backend
        return timeSpan.substring(0, 5);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        try {
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                fecha: formData.fecha,
                tipoHoraExtra: parseInt(formData.tipoHoraExtra),
                horaInicio: formData.horaInicio + ':00',
                horaFin: formData.horaFin + ':00',
                motivo: formData.motivo
            };

            if (editingId) {
                await api.put(`/api/horasextra/${editingId}`, payload);
            } else {
                await api.post('/api/horasextra', payload);
            }

            await fetchHorasExtra();
            toast.success(editingId ? 'Hora extra actualizada' : 'Hora extra registrada');
            resetForm();
        } catch (err) {
            toast.error(`Error: ${err.message}`);
        }
    };

    const handleAprobar = async (id) => {
        try {
            await api.post(`/api/horasextra/${id}/aprobar`, {});
            await fetchHorasExtra();
            toast.success('Hora extra aprobada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const handleRechazar = async (id) => {
        if (!window.confirm('¿Está seguro de rechazar esta hora extra?')) return;

        try {
            await api.post(`/api/horasextra/${id}/rechazar`, {});
            await fetchHorasExtra();
            toast.success('Hora extra rechazada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const handleEdit = (horaExtra) => {
        setFormData({
            empleadoId: horaExtra.empleadoId.toString(),
            fecha: new Date(horaExtra.fecha).toISOString().split('T')[0],
            tipoHoraExtra: horaExtra.tipoHoraExtra.toString(),
            horaInicio: formatTime(horaExtra.horaInicio),
            horaFin: formatTime(horaExtra.horaFin),
            motivo: horaExtra.motivo
        });
        setEditingId(horaExtra.id);
        setShowModal(true);
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            empleadoId: '',
            fecha: new Date().toISOString().split('T')[0],
            tipoHoraExtra: '1',
            horaInicio: '09:00',
            horaFin: '10:00',
            motivo: ''
        });
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-600">Cargando horas extra...</p>
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
                            <p className="text-sm font-medium text-gray-600">Pendientes de Aprobar</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{pendientes}</p>
                        </div>
                        <div className="w-12 h-12 bg-yellow-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-yellow-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Horas este Mes</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{horasEsteMes.toFixed(1)}</p>
                        </div>
                        <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-600">Monto Estimado</p>
                            <p className="text-2xl font-bold text-gray-900 mt-2">{formatCurrency(montoEstimado)}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                    <p className="text-sm font-medium text-gray-600 mb-3">Por Tipo</p>
                    <div className="space-y-2">
                        {porTipo.map(tipo => (
                            <div key={tipo.valor} className="flex items-center justify-between">
                                <span className="text-xs text-gray-600">{tipo.nombre.split(' ')[0]}</span>
                                <span className="px-2 py-0.5 bg-blue-100 text-blue-800 text-xs font-medium rounded-full">
                                    {tipo.cantidad}
                                </span>
                            </div>
                        ))}
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
                                <option key={tipo.valor} value={tipo.valor}>
                                    {tipo.nombre}
                                </option>
                            ))}
                        </select>

                        <label className="flex items-center gap-2 px-3 py-2 bg-gray-50 rounded-lg cursor-pointer">
                            <input
                                type="checkbox"
                                checked={filters.soloPendientes}
                                onChange={(e) => setFilters({ ...filters, soloPendientes: e.target.checked })}
                                className="w-4 h-4 text-blue-600 rounded focus:ring-2 focus:ring-blue-500"
                            />
                            <span className="text-sm font-medium text-gray-700">Solo pendientes</span>
                        </label>
                    </div>

                    <button
                        onClick={() => setShowModal(true)}
                        className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                        </svg>
                        Registrar Horas Extra
                    </button>
                </div>
            </div>

            {/* Table */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
                        Horas Extra Registradas
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredHorasExtra.length} registros)
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50 border-b border-gray-200">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Fecha</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Horario</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Horas</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Monto</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {filteredHorasExtra.map((he) => (
                                <tr key={he.id} className="hover:bg-gray-50 transition-colors">
                                    <td className="py-4 px-6 text-sm text-gray-900">{he.empleadoNombre}</td>
                                    <td className="py-4 px-6 text-sm text-gray-500">
                                        {new Date(he.fecha).toLocaleDateString('es-PA', {
                                            day: '2-digit',
                                            month: 'short',
                                            year: 'numeric'
                                        })}
                                    </td>
                                    <td className="py-4 px-6">
                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-100 text-purple-800">
                                            {he.tipoNombre}
                                        </span>
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-900">
                                        {formatTime(he.horaInicio)} - {formatTime(he.horaFin)}
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {he.cantidadHoras.toFixed(2)}h
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-900">
                                        {formatCurrency(he.montoCalculado)}
                                    </td>
                                    <td className="py-4 px-6">
                                        {he.estaAprobada ? (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                                                <span className="w-1.5 h-1.5 bg-green-600 rounded-full mr-1.5"></span>
                                                Aprobada
                                            </span>
                                        ) : (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">
                                                <span className="w-1.5 h-1.5 bg-yellow-600 rounded-full mr-1.5"></span>
                                                Pendiente
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2">
                                            {!he.estaAprobada && (
                                                <>
                                                    <button
                                                        onClick={() => handleAprobar(he.id)}
                                                        className="inline-flex items-center gap-1 text-green-600 hover:text-green-800 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                                        </svg>
                                                        Aprobar
                                                    </button>
                                                    <button
                                                        onClick={() => handleEdit(he)}
                                                        className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                        </svg>
                                                        Editar
                                                    </button>
                                                    <button
                                                        onClick={() => handleRechazar(he.id)}
                                                        className="inline-flex items-center gap-1 text-red-600 hover:text-red-800 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                        </svg>
                                                        Rechazar
                                                    </button>
                                                </>
                                            )}
                                            {he.estaAprobada && he.aprobadoPor && (
                                                <span className="text-xs text-gray-500">
                                                    Aprobado por {he.aprobadoPor}
                                                </span>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {filteredHorasExtra.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-900 mb-1">
                                No hay horas extra registradas
                            </h3>
                            <p className="text-gray-500 mb-4">
                                Comienza registrando la primera hora extra
                            </p>
                            <button
                                onClick={() => setShowModal(true)}
                                className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                </svg>
                                Registrar Horas Extra
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
                                {editingId ? 'Editar Hora Extra' : 'Registrar Hora Extra'}
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
                                        Fecha <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fecha}
                                        onChange={(e) => setFormData({ ...formData, fecha: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Tipo <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoHoraExtra}
                                        onChange={(e) => setFormData({ ...formData, tipoHoraExtra: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    >
                                        {tipos.map(tipo => (
                                            <option key={tipo.valor} value={tipo.valor}>
                                                {tipo.nombre}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Hora Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="time"
                                        required
                                        value={formData.horaInicio}
                                        onChange={(e) => setFormData({ ...formData, horaInicio: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Hora Fin <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="time"
                                        required
                                        value={formData.horaFin}
                                        onChange={(e) => setFormData({ ...formData, horaFin: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    />
                                </div>

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
                                        placeholder="Describir el motivo de las horas extra..."
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

export default HorasExtraPage;
