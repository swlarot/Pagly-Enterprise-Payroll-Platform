import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import ConfirmModal from '../components/ConfirmModal';
import { useAuth } from '../contexts/AuthContext';
import { TenantRole } from '../types/api';

const DepartamentosPage = () => {
    // Auth context - Owner o usuarios con permisos de escritura pueden gestionar departamentos
    const { canWrite, canDelete, isReadOnly, hasRole } = useAuth();
    const canManageDepartments = hasRole(TenantRole.Owner) || canWrite();

    const [departamentos, setDepartamentos] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [showModal, setShowModal] = useState(false);
    const [editingDept, setEditingDept] = useState(null);
    const [showConfirm, setShowConfirm] = useState(false);
    const [deptToDeactivate, setDeptToDeactivate] = useState(null);

    const [formData, setFormData] = useState({
        codigo: '',
        nombre: '',
        descripcion: '',
        managerId: ''
    });

    useEffect(() => {
        fetchDepartamentos();
        fetchEmpleados();
    }, []);

    const fetchDepartamentos = async () => {
        try {
            setLoading(true);
            const data = await api.get('/api/departamentos');
            setDepartamentos(data);
        } catch (error) {
            toast.error(error.message || 'Error al cargar departamentos');
        } finally {
            setLoading(false);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const data = await api.get('/api/empleados');
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (error) {
            console.error('Error al cargar empleados:', error);
        }
    };

    const handleOpenModal = (dept = null) => {
        if (dept) {
            setEditingDept(dept);
            setFormData({
                codigo: dept.codigo,
                nombre: dept.nombre,
                descripcion: dept.descripcion || '',
                managerId: dept.managerId || ''
            });
        } else {
            setEditingDept(null);
            setFormData({
                codigo: '',
                nombre: '',
                descripcion: '',
                managerId: ''
            });
        }
        setShowModal(true);
    };

    const handleCloseModal = () => {
        setShowModal(false);
        setEditingDept(null);
        setFormData({
            codigo: '',
            nombre: '',
            descripcion: '',
            managerId: ''
        });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const payload = {
            codigo: formData.codigo.trim(),
            nombre: formData.nombre.trim(),
            descripcion: formData.descripcion.trim() || null,
            managerId: formData.managerId ? parseInt(formData.managerId) : null
        };

        if (editingDept) {
            payload.estaActivo = editingDept.estaActivo;
        }

        try {
            if (editingDept) {
                await api.put(`/api/departamentos/${editingDept.id}`, payload);
                toast.success('Departamento actualizado exitosamente');
            } else {
                await api.post('/api/departamentos', payload);
                toast.success('Departamento creado exitosamente');
            }

            handleCloseModal();
            fetchDepartamentos();
        } catch (error) {
            toast.error(error.message || 'Error al guardar departamento');
        }
    };

    const handleDeactivate = async () => {
        try {
            await api.delete(`/api/departamentos/${deptToDeactivate.id}`);

            toast.success('Departamento desactivado exitosamente');
            setShowConfirm(false);
            setDeptToDeactivate(null);
            fetchDepartamentos();
        } catch (error) {
            toast.error(error.message || 'Error al desactivar departamento');
        }
    };

    const filteredDepartamentos = departamentos.filter(dept =>
        (dept.nombre ?? '').toLowerCase().includes(searchTerm.toLowerCase()) ||
        (dept.codigo ?? '').toLowerCase().includes(searchTerm.toLowerCase())
    );

    const stats = {
        total: departamentos.length,
        conEmpleados: departamentos.filter(d => d.cantidadEmpleados > 0).length,
        sinEmpleados: departamentos.filter(d => d.cantidadEmpleados === 0).length
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
            {/* Stats Cards */}
            <div className="grid grid-cols-3 gap-6">
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Total Departamentos</p>
                            <p className="text-3xl font-bold font-display text-gray-100 mt-2">{stats.total}</p>
                        </div>
                        <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Con Empleados</p>
                            <p className="text-3xl font-bold text-green-400 mt-2">{stats.conEmpleados}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Sin Empleados</p>
                            <p className="text-3xl font-bold text-gray-500 mt-2">{stats.sinEmpleados}</p>
                        </div>
                        <div className="w-12 h-12 bg-navy-800 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-gray-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                            </svg>
                        </div>
                    </div>
                </div>
            </div>

            {/* Actions Bar */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-4">
                <div className="flex items-center justify-between gap-4">
                    <div className="flex-1">
                        <div className="relative">
                            <svg className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                            </svg>
                            <input
                                type="text"
                                placeholder="Buscar por nombre o código..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="w-full pl-10 pr-4 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                            />
                        </div>
                    </div>
                    {canManageDepartments && (
                        <button
                            onClick={() => handleOpenModal()}
                            className="px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition flex items-center gap-2"
                        >
                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                            </svg>
                            Nuevo Departamento
                        </button>
                    )}
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
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Descripción</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Empleados</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Jefe</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {filteredDepartamentos.length === 0 ? (
                                <tr>
                                    <td colSpan="7" className="px-6 py-12 text-center text-gray-500">
                                        <svg className="w-12 h-12 mx-auto text-gray-400 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                                        </svg>
                                        <p className="text-sm">No hay departamentos para mostrar</p>
                                    </td>
                                </tr>
                            ) : (
                                filteredDepartamentos.map((dept) => (
                                    <tr key={dept.id} className="hover:bg-navy-800 transition">
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            <span className="text-sm font-medium text-gray-100">{dept.codigo}</span>
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            <span className="text-sm text-gray-100">{dept.nombre}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="text-sm text-gray-500">{dept.descripcion || '-'}</span>
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                                {dept.cantidadEmpleados}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            {dept.managerNombre ? (
                                                <span className="text-sm text-gray-100">{dept.managerNombre}</span>
                                            ) : (
                                                <span className="text-sm text-gray-400">Sin asignar</span>
                                            )}
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap">
                                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                                dept.estaActivo
                                                    ? 'bg-green-500/15 text-green-400'
                                                    : 'bg-red-500/15 text-red-400'
                                            }`}>
                                                {dept.estaActivo ? 'Activo' : 'Inactivo'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 whitespace-nowrap text-sm">
                                            {canManageDepartments ? (
                                                <div className="flex items-center gap-2">
                                                    <button
                                                        onClick={() => handleOpenModal(dept)}
                                                        className="text-primary-400 hover:text-primary-300 transition"
                                                        title="Editar"
                                                    >
                                                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                        </svg>
                                                    </button>
                                                    {dept.estaActivo && (
                                                        <button
                                                            onClick={() => {
                                                                setDeptToDeactivate(dept);
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
                                            ) : (
                                                <span className="text-xs text-gray-400 italic">Solo lectura</span>
                                            )}
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            {/* Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="sticky top-0 bg-navy-900 border-b border-navy-700 px-6 py-4 flex items-center justify-between">
                            <h3 className="text-lg font-bold font-display text-gray-100">
                                {editingDept ? 'Editar Departamento' : 'Nuevo Departamento'}
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
                                    placeholder="Ej: ADM"
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
                                    placeholder="Ej: Administración"
                                />
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
                                    placeholder="Descripción opcional del departamento..."
                                />
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">
                                    Jefe de Departamento
                                </label>
                                <select
                                    value={formData.managerId}
                                    onChange={(e) => setFormData({ ...formData, managerId: e.target.value })}
                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                >
                                    <option value="">Sin asignar</option>
                                    {empleados.map(emp => (
                                        <option key={emp.id} value={emp.id}>
                                            {emp.nombre} {emp.apellido}
                                        </option>
                                    ))}
                                </select>
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
                                    {editingDept ? 'Actualizar' : 'Crear'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Confirm Modal */}
            <ConfirmModal
                isOpen={showConfirm}
                onClose={() => {
                    setShowConfirm(false);
                    setDeptToDeactivate(null);
                }}
                onConfirm={handleDeactivate}
                title="Desactivar Departamento"
                message={`¿Está seguro que desea desactivar el departamento "${deptToDeactivate?.nombre}"?`}
                confirmText="Desactivar"
                confirmColor="red"
            />
        </div>
    );
};

export default DepartamentosPage;
