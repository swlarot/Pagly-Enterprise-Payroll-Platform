import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import ConfirmModal from '../components/ConfirmModal';
import { useAuth } from '../contexts/AuthContext';
import { TenantRole } from '../types/api';
import { formatCurrency } from '../utils/currency';

const PosicionesPage = () => {
    // Auth context - Solo Owner puede gestionar posiciones (permisos granulares en desarrollo)
    const { hasRole } = useAuth();
    const canManagePositions = hasRole(TenantRole.Owner);

    const [posiciones, setPosiciones] = useState([]);
    const [departamentos, setDepartamentos] = useState([]);
    const [loading, setLoading] = useState(true);
    const [selectedDeptId, setSelectedDeptId] = useState('');
    const [showModal, setShowModal] = useState(false);
    const [editingPos, setEditingPos] = useState(null);
    const [showConfirm, setShowConfirm] = useState(false);
    const [posToDeactivate, setPosToDeactivate] = useState(null);

    const [formData, setFormData] = useState({
        codigo: '',
        nombre: '',
        descripcion: '',
        departamentoId: '',
        salarioMinimo: '',
        salarioMaximo: '',
        nivelRiesgo: 0
    });

    useEffect(() => {
        fetchDepartamentos();
    }, []);

    useEffect(() => {
        fetchPosiciones();
    }, [selectedDeptId]);

    const fetchPosiciones = async () => {
        try {
            setLoading(true);
            const url = selectedDeptId
                ? `/api/posiciones?departamentoId=${selectedDeptId}`
                : '/api/posiciones';
            const data = await api.get(url);
            setPosiciones(data);
        } catch (error) {
            toast.error(error.message || 'Error al cargar posiciones');
        } finally {
            setLoading(false);
        }
    };

    const fetchDepartamentos = async () => {
        try {
            const data = await api.get('/api/departamentos');
            setDepartamentos(data.filter(d => d.estaActivo));
        } catch (error) {
            toast.error(error.message || 'Error al cargar departamentos');
        }
    };

    const handleOpenModal = (pos = null) => {
        if (pos) {
            setEditingPos(pos);
            setFormData({
                codigo: pos.codigo,
                nombre: pos.nombre,
                descripcion: pos.descripcion || '',
                departamentoId: pos.departamentoId.toString(),
                salarioMinimo: pos.salarioMinimo.toString(),
                salarioMaximo: pos.salarioMaximo.toString(),
                nivelRiesgo: pos.nivelRiesgo
            });
        } else {
            setEditingPos(null);
            setFormData({
                codigo: '',
                nombre: '',
                descripcion: '',
                departamentoId: selectedDeptId || '',
                salarioMinimo: '',
                salarioMaximo: '',
                nivelRiesgo: 0
            });
        }
        setShowModal(true);
    };

    const handleCloseModal = () => {
        setShowModal(false);
        setEditingPos(null);
        setFormData({
            codigo: '',
            nombre: '',
            descripcion: '',
            departamentoId: '',
            salarioMinimo: '',
            salarioMaximo: '',
            nivelRiesgo: 0
        });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const salarioMin = parseFloat(formData.salarioMinimo);
        const salarioMax = parseFloat(formData.salarioMaximo);

        if (salarioMax < salarioMin) {
            toast.error('El salario máximo no puede ser menor que el mínimo');
            return;
        }

        const payload = {
            codigo: formData.codigo.trim(),
            nombre: formData.nombre.trim(),
            descripcion: formData.descripcion.trim() || null,
            departamentoId: parseInt(formData.departamentoId),
            salarioMinimo: salarioMin,
            salarioMaximo: salarioMax,
            nivelRiesgo: parseInt(formData.nivelRiesgo)
        };

        if (editingPos) {
            payload.estaActivo = editingPos.estaActivo;
        }

        try {
            if (editingPos) {
                await api.put(`/api/posiciones/${editingPos.id}`, payload);
                toast.success('Posición actualizada exitosamente');
            } else {
                await api.post('/api/posiciones', payload);
                toast.success('Posición creada exitosamente');
            }

            handleCloseModal();
            fetchPosiciones();
        } catch (error) {
            toast.error(error.message || 'Error al guardar posición');
        }
    };

    const handleDeactivate = async () => {
        try {
            await api.delete(`/api/posiciones/${posToDeactivate.id}`);

            toast.success('Posición desactivada exitosamente');
            setShowConfirm(false);
            setPosToDeactivate(null);
            fetchPosiciones();
        } catch (error) {
            toast.error(error.message || 'Error al desactivar posición');
        }
    };

    const getNivelRiesgoBadge = (nivel) => {
        const badges = {
            0: { text: 'Bajo (0.56%)', className: 'bg-green-500/15 text-green-400' },
            1: { text: 'Medio (2.50%)', className: 'bg-amber-500/15 text-amber-400' },
            2: { text: 'Alto (5.39%)', className: 'bg-red-500/15 text-red-400' }
        };
        return badges[nivel] || badges[0];
    };

    const filteredPosiciones = selectedDeptId
        ? posiciones.filter(p => p.departamentoId === parseInt(selectedDeptId))
        : posiciones;

    const stats = {
        total: posiciones.length,
        enDept: selectedDeptId ? filteredPosiciones.length : 0,
        avgSalario: posiciones.length > 0
            ? posiciones.reduce((sum, p) => sum + ((p.salarioMinimo + p.salarioMaximo) / 2), 0) / posiciones.length
            : 0,
        porRiesgo: {
            bajo: posiciones.filter(p => p.nivelRiesgo === 0).length,
            medio: posiciones.filter(p => p.nivelRiesgo === 1).length,
            alto: posiciones.filter(p => p.nivelRiesgo === 2).length
        }
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center h-64">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600"></div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Filtro Departamento */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-4">
                <div className="flex items-center gap-4">
                    <label className="text-sm font-medium text-gray-300">Filtrar por Departamento:</label>
                    <select
                        value={selectedDeptId}
                        onChange={(e) => setSelectedDeptId(e.target.value)}
                        className="px-4 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                    >
                        <option value="">Todos los Departamentos</option>
                        {departamentos.map(dept => (
                            <option key={dept.id} value={dept.id}>{dept.nombre}</option>
                        ))}
                    </select>
                </div>
            </div>

            {/* Stats Cards */}
            <div className="grid grid-cols-4 gap-6">
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Total Posiciones</p>
                            <p className="text-3xl font-bold font-display text-gray-100 mt-2">{stats.total}</p>
                        </div>
                        <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 13.255A23.931 23.931 0 0112 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v2m4 6h.01M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                            </svg>
                        </div>
                    </div>
                </div>

                {selectedDeptId && (
                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm font-medium text-gray-400">En Departamento</p>
                                <p className="text-3xl font-bold text-primary-400 mt-2">{stats.enDept}</p>
                            </div>
                            <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
                                <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                                </svg>
                            </div>
                        </div>
                    </div>
                )}

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Salario Promedio</p>
                            <p className="text-lg font-bold font-mono text-green-400 mt-2">{formatCurrency(stats.avgSalario)}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div>
                        <p className="text-sm font-medium text-gray-400 mb-3">Por Nivel de Riesgo</p>
                        <div className="space-y-2">
                            <div className="flex items-center justify-between">
                                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-green-500/15 text-green-400">
                                    Bajo
                                </span>
                                <span className="text-sm font-semibold text-gray-100">{stats.porRiesgo.bajo}</span>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-amber-500/15 text-amber-400">
                                    Medio
                                </span>
                                <span className="text-sm font-semibold text-gray-100">{stats.porRiesgo.medio}</span>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-red-500/15 text-red-400">
                                    Alto
                                </span>
                                <span className="text-sm font-semibold text-gray-100">{stats.porRiesgo.alto}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Actions Bar */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-4">
                <div className="flex items-center justify-end">
                    <button
                        onClick={() => handleOpenModal()}
                        className="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition flex items-center gap-2"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                        </svg>
                        Nueva Posición
                    </button>
                </div>
            </div>

            {/* Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-950 border-b border-navy-700">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Código</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Nombre</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Departamento</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Salario Mín</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Salario Máx</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Nivel Riesgo</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {filteredPosiciones.length === 0 ? (
                                <tr>
                                    <td colSpan="8" className="px-6 py-12 text-center text-gray-500">
                                        <svg className="w-12 h-12 mx-auto text-gray-400 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                                        </svg>
                                        <p className="text-sm">No hay posiciones para mostrar</p>
                                    </td>
                                </tr>
                            ) : (
                                filteredPosiciones.map((pos) => {
                                    const riesgoBadge = getNivelRiesgoBadge(pos.nivelRiesgo);
                                    return (
                                        <tr key={pos.id} className="hover:bg-navy-800 transition">
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className="text-sm font-medium text-gray-100">{pos.codigo}</span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className="text-sm text-gray-100">{pos.nombre}</span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className="text-sm text-gray-400">{pos.departamentoNombre}</span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className="text-sm font-medium font-mono text-gray-100">{formatCurrency(pos.salarioMinimo)}</span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className="text-sm font-medium font-mono text-gray-100">{formatCurrency(pos.salarioMaximo)}</span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${riesgoBadge.className}`}>
                                                    {riesgoBadge.text}
                                                </span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap">
                                                <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                                    pos.estaActivo
                                                        ? 'bg-green-500/15 text-green-400'
                                                        : 'bg-red-500/15 text-red-400'
                                                }`}>
                                                    {pos.estaActivo ? 'Activo' : 'Inactivo'}
                                                </span>
                                            </td>
                                            <td className="px-6 py-4 whitespace-nowrap text-sm">
                                                <div className="flex items-center gap-2">
                                                    <button
                                                        onClick={() => handleOpenModal(pos)}
                                                        className="text-primary-400 hover:text-primary-300 transition"
                                                        title="Editar"
                                                    >
                                                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                        </svg>
                                                    </button>
                                                    {pos.estaActivo && (
                                                        <button
                                                            onClick={() => {
                                                                setPosToDeactivate(pos);
                                                                setShowConfirm(true);
                                                            }}
                                                            className="text-red-400 hover:text-red-300 transition"
                                                            title="Desactivar"
                                                        >
                                                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                            </svg>
                                                        </button>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            {/* Modal */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="sticky top-0 bg-navy-900 border-b border-navy-700 px-6 py-4 flex items-center justify-between">
                            <h3 className="text-lg font-bold font-display text-gray-100">
                                {editingPos ? 'Editar Posición' : 'Nueva Posición'}
                            </h3>
                            <button
                                onClick={handleCloseModal}
                                className="text-gray-400 hover:text-gray-200 transition"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6 space-y-4">
                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Código <span className="text-red-400">*</span>
                                </label>
                                <input
                                    type="text"
                                    required
                                    maxLength={20}
                                    value={formData.codigo}
                                    onChange={(e) => setFormData({ ...formData, codigo: e.target.value })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                    placeholder="Ej: GER-VEN"
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Nombre <span className="text-red-400">*</span>
                                </label>
                                <input
                                    type="text"
                                    required
                                    maxLength={100}
                                    value={formData.nombre}
                                    onChange={(e) => setFormData({ ...formData, nombre: e.target.value })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                    placeholder="Ej: Gerente de Ventas"
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Departamento <span className="text-red-400">*</span>
                                </label>
                                <select
                                    required
                                    value={formData.departamentoId}
                                    onChange={(e) => setFormData({ ...formData, departamentoId: e.target.value })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                >
                                    <option value="">Seleccione un departamento</option>
                                    {departamentos.map(dept => (
                                        <option key={dept.id} value={dept.id}>{dept.nombre}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-1">
                                        Salario Mínimo <span className="text-red-400">*</span>
                                    </label>
                                    <input
                                        type="number"
                                        required
                                        min="0"
                                        step="0.01"
                                        value={formData.salarioMinimo}
                                        onChange={(e) => setFormData({ ...formData, salarioMinimo: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                        placeholder="0.00"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-1">
                                        Salario Máximo <span className="text-red-400">*</span>
                                    </label>
                                    <input
                                        type="number"
                                        required
                                        min="0"
                                        step="0.01"
                                        value={formData.salarioMaximo}
                                        onChange={(e) => setFormData({ ...formData, salarioMaximo: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                        placeholder="0.00"
                                    />
                                </div>
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Nivel de Riesgo <span className="text-red-400">*</span>
                                </label>
                                <select
                                    required
                                    value={formData.nivelRiesgo}
                                    onChange={(e) => setFormData({ ...formData, nivelRiesgo: parseInt(e.target.value) })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                >
                                    <option value={0}>Bajo (0.56%)</option>
                                    <option value={1}>Medio (2.50%)</option>
                                    <option value={2}>Alto (5.39%)</option>
                                </select>
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Descripción
                                </label>
                                <textarea
                                    rows={3}
                                    maxLength={500}
                                    value={formData.descripcion}
                                    onChange={(e) => setFormData({ ...formData, descripcion: e.target.value })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                    placeholder="Descripción opcional de la posición..."
                                />
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={handleCloseModal}
                                    className="px-4 py-2 text-gray-300 bg-navy-800 rounded-lg hover:bg-navy-700 transition"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition"
                                >
                                    {editingPos ? 'Actualizar' : 'Crear'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* Confirm Modal */}
            <ConfirmModal
                isOpen={showConfirm}
                onClose={() => {
                    setShowConfirm(false);
                    setPosToDeactivate(null);
                }}
                onConfirm={handleDeactivate}
                title="Desactivar Posición"
                message={`¿Está seguro que desea desactivar la posición "${posToDeactivate?.nombre}"?`}
                confirmText="Desactivar"
                confirmColor="red"
            />
        </div>
    );
};

export default PosicionesPage;
