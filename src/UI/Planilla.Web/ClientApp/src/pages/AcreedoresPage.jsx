import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import CollapsibleSection from '../components/CollapsibleSection';

// Enum TipoAcreedor — valores del backend
const TIPO_ACREEDOR_OPTIONS = [
    { id: 1,  label: 'Persona Natural' },
    { id: 2,  label: 'Banco' },
    { id: 3,  label: 'Cooperativa' },
    { id: 4,  label: 'Sindicato' },
    { id: 5,  label: 'Aseguradora' },
    { id: 6,  label: 'Entidad Gubernamental' },
    { id: 99, label: 'Otro' },
];

// Colores de badge por tipo
const TIPO_BADGE = {
    2:  'bg-blue-500/15 text-blue-400',
    1:  'bg-green-500/15 text-green-400',
    3:  'bg-teal-500/15 text-teal-400',
    4:  'bg-orange-500/15 text-orange-400',
    5:  'bg-purple-500/15 text-purple-400',
    6:  'bg-gray-500/15 text-gray-400',
    99: 'bg-gray-500/15 text-gray-400',
};

const getTipoLabel = (id) => TIPO_ACREEDOR_OPTIONS.find(t => t.id === id)?.label || 'Otro';
const getTipoBadgeClass = (id) => TIPO_BADGE[id] || TIPO_BADGE[99];

// Estado inicial del formulario
const INITIAL_FORM = {
    nombre: '',
    tipoAcreedor: '',
    identificacion: '',
    tipoIdentificacion: '',
    banco: '',
    tipoCuenta: '',
    numeroCuenta: '',
    iban: '',
    telefono: '',
    email: '',
    direccion: '',
    contactoNombre: '',
    observaciones: '',
};


const AcreedoresPage = () => {
    const { canWrite, canDelete } = useAuth();

    // Estado principal
    const [acreedores, setAcreedores] = useState([]);
    const [loading, setLoading] = useState(true);

    // Filtros
    const [filterTipo, setFilterTipo] = useState('');
    const [filterActivos, setFilterActivos] = useState(true);
    const [filterSearch, setFilterSearch] = useState('');

    // Modal crear/editar
    const [showModal, setShowModal] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [formData, setFormData] = useState(INITIAL_FORM);

    // Modal eliminar
    const [showConfirmDelete, setShowConfirmDelete] = useState(false);
    const [acreedorToDelete, setAcreedorToDelete] = useState(null);
    const [deleteWarningDeducciones, setDeleteWarningDeducciones] = useState([]);

    useEffect(() => {
        fetchAcreedores();
    }, []);

    useEffect(() => {
        fetchAcreedores();
    }, [filterTipo, filterActivos, filterSearch]);

    const fetchAcreedores = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterTipo) params.append('tipo', filterTipo);
            if (filterActivos) params.append('activos', 'true');
            if (filterSearch) params.append('search', filterSearch);

            const data = await api.get(`/api/acreedores?${params.toString()}`);
            setAcreedores(Array.isArray(data) ? data : []);
        } catch (err) {
            toast.error(`Error al cargar acreedores: ${err.message}`);
        } finally {
            setLoading(false);
        }
    };

    // Helper para actualizar campos del formulario
    const setField = (field, value) => setFormData(prev => ({ ...prev, [field]: value }));

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (!formData.nombre.trim()) {
            toast.error('El nombre es requerido');
            return;
        }
        if (!formData.tipoAcreedor) {
            toast.error('El tipo de acreedor es requerido');
            return;
        }

        try {
            const payload = {
                nombre: formData.nombre.trim(),
                tipoAcreedor: parseInt(formData.tipoAcreedor),
                identificacion: formData.identificacion || null,
                tipoIdentificacion: formData.tipoIdentificacion || null,
                banco: formData.banco || null,
                tipoCuenta: formData.tipoCuenta || null,
                numeroCuenta: formData.numeroCuenta || null,
                iban: formData.iban || null,
                telefono: formData.telefono || null,
                email: formData.email || null,
                direccion: formData.direccion || null,
                contactoNombre: formData.contactoNombre || null,
                observaciones: formData.observaciones || null,
            };

            if (editingId) {
                await api.put(`/api/acreedores/${editingId}`, payload);
                toast.success('Acreedor actualizado exitosamente');
            } else {
                await api.post('/api/acreedores', payload);
                toast.success('Acreedor creado exitosamente');
            }

            await fetchAcreedores();
            resetForm();
        } catch (err) {
            toast.error(`Error al guardar acreedor: ${err.message}`);
        }
    };

    const handleEdit = (acreedor) => {
        setFormData({
            nombre: acreedor.nombre || '',
            tipoAcreedor: acreedor.tipoAcreedor?.toString() || '',
            identificacion: acreedor.identificacion || '',
            tipoIdentificacion: acreedor.tipoIdentificacion || '',
            banco: acreedor.banco || '',
            tipoCuenta: acreedor.tipoCuenta || '',
            numeroCuenta: acreedor.numeroCuenta || '',
            iban: acreedor.iban || '',
            telefono: acreedor.telefono || '',
            email: acreedor.email || '',
            direccion: acreedor.direccion || '',
            contactoNombre: acreedor.contactoNombre || '',
            observaciones: acreedor.observaciones || '',
        });
        setEditingId(acreedor.id);
        setShowModal(true);
    };

    const confirmDelete = (acreedor) => {
        setAcreedorToDelete(acreedor);
        setDeleteWarningDeducciones([]);
        setShowConfirmDelete(true);
    };

    const handleDelete = async () => {
        if (!acreedorToDelete) return;
        try {
            await api.delete(`/api/acreedores/${acreedorToDelete.id}`);
            toast.success('Acreedor eliminado exitosamente');
            await fetchAcreedores();
            setShowConfirmDelete(false);
            setAcreedorToDelete(null);
            setDeleteWarningDeducciones([]);
        } catch (err) {
            // El backend puede devolver 400 con lista de deducciones vinculadas
            try {
                const parsed = JSON.parse(err.message);
                if (parsed?.deducciones) {
                    setDeleteWarningDeducciones(parsed.deducciones);
                    return;
                }
            } catch {
                // mensaje plano
            }
            toast.error(`Error al eliminar acreedor: ${err.message}`);
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData(INITIAL_FORM);
    };

    // Estadísticas de resumen
    const activos = acreedores.filter(a => a.estaActivo);
    const contarPorTipo = (tipoId) => activos.filter(a => a.tipoAcreedor === tipoId).length;

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando acreedores...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">

            {/* ── TARJETAS DE RESUMEN ── */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {/* Total activos */}
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-5">
                    <p className="text-xs font-medium text-gray-400 mb-1">Total Activos</p>
                    <p className="text-3xl font-bold text-gray-100">{activos.length}</p>
                    <p className="text-xs text-gray-500 mt-1">Acreedores en catálogo</p>
                </div>

                {/* Bancos */}
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-5">
                    <p className="text-xs font-medium text-gray-400 mb-1">Bancos</p>
                    <p className="text-3xl font-bold text-blue-400">{contarPorTipo(2)}</p>
                    <p className="text-xs text-gray-500 mt-1">Entidades bancarias</p>
                </div>

                {/* Personas */}
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-5">
                    <p className="text-xs font-medium text-gray-400 mb-1">Personas</p>
                    <p className="text-3xl font-bold text-green-400">{contarPorTipo(1)}</p>
                    <p className="text-xs text-gray-500 mt-1">Personas naturales</p>
                </div>

                {/* Cooperativas + Sindicatos */}
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-5">
                    <p className="text-xs font-medium text-gray-400 mb-1">Coop. / Sind.</p>
                    <p className="text-3xl font-bold text-teal-400">{contarPorTipo(3) + contarPorTipo(4)}</p>
                    <p className="text-xs text-gray-500 mt-1">Cooperativas y sindicatos</p>
                </div>
            </div>

            {/* ── FILTROS Y ACCIÓN ── */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={() => setShowModal(true)}
                    className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo Acreedor
                </button>

                <div className="flex flex-col sm:flex-row gap-3 w-full sm:w-auto">
                    {/* Búsqueda */}
                    <input
                        type="text"
                        value={filterSearch}
                        onChange={(e) => setFilterSearch(e.target.value)}
                        placeholder="Buscar por nombre o RUC..."
                        className="px-3 py-2 bg-navy-900 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm w-full sm:w-56"
                    />

                    {/* Filtro por tipo */}
                    <select
                        value={filterTipo}
                        onChange={(e) => setFilterTipo(e.target.value)}
                        className="px-3 py-2 bg-navy-900 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                    >
                        <option value="">Todos los tipos</option>
                        {TIPO_ACREEDOR_OPTIONS.map(t => (
                            <option key={t.id} value={t.id}>{t.label}</option>
                        ))}
                    </select>

                    {/* Toggle activos */}
                    <label className="inline-flex items-center px-3 py-2 bg-navy-900 border border-navy-600 rounded-lg cursor-pointer">
                        <input
                            type="checkbox"
                            checked={filterActivos}
                            onChange={(e) => setFilterActivos(e.target.checked)}
                            className="mr-2"
                        />
                        <span className="text-sm font-medium text-gray-300">Solo activos</span>
                    </label>
                </div>
            </div>

            {/* ── TABLA ── */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Catálogo de Acreedores
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({acreedores.length} {acreedores.length === 1 ? 'acreedor' : 'acreedores'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-950 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Nombre</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Tipo</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">RUC / Cédula</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Banco</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Cuenta</th>
                                <th className="text-center py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Deducciones</th>
                                <th className="text-center py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Préstamos</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-5 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {acreedores.map(acreedor => (
                                <tr key={acreedor.id} className="hover:bg-navy-800 transition-colors">
                                    <td className="py-3.5 px-5 text-sm font-medium text-gray-100">
                                        {acreedor.nombre}
                                        {acreedor.contactoNombre && (
                                            <span className="block text-xs text-gray-500 font-normal mt-0.5">{acreedor.contactoNombre}</span>
                                        )}
                                    </td>
                                    <td className="py-3.5 px-5">
                                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${getTipoBadgeClass(acreedor.tipoAcreedor)}`}>
                                            {getTipoLabel(acreedor.tipoAcreedor)}
                                        </span>
                                    </td>
                                    <td className="py-3.5 px-5 text-sm text-gray-300">
                                        {acreedor.identificacion ? (
                                            <span>
                                                <span className="text-xs text-gray-500">{acreedor.tipoIdentificacion || 'ID'}: </span>
                                                {acreedor.identificacion}
                                            </span>
                                        ) : (
                                            <span className="text-gray-600">—</span>
                                        )}
                                    </td>
                                    <td className="py-3.5 px-5 text-sm text-gray-300">
                                        {acreedor.banco || <span className="text-gray-600">—</span>}
                                    </td>
                                    <td className="py-3.5 px-5 text-sm text-gray-300">
                                        {acreedor.numeroCuenta ? (
                                            <span>
                                                {acreedor.tipoCuenta && (
                                                    <span className="text-xs text-gray-500 block">{acreedor.tipoCuenta}</span>
                                                )}
                                                {acreedor.numeroCuenta}
                                            </span>
                                        ) : (
                                            <span className="text-gray-600">—</span>
                                        )}
                                    </td>
                                    <td className="py-3.5 px-5 text-center">
                                        {(acreedor.deduccionesActivas ?? 0) > 0 ? (
                                            <span className="inline-flex items-center justify-center w-6 h-6 bg-amber-500/15 text-amber-400 rounded-full text-xs font-bold">
                                                {acreedor.deduccionesActivas}
                                            </span>
                                        ) : (
                                            <span className="text-gray-600 text-xs">0</span>
                                        )}
                                    </td>
                                    <td className="py-3.5 px-5 text-center">
                                        {(acreedor.prestamosActivos ?? 0) > 0 ? (
                                            <span className="inline-flex items-center justify-center w-6 h-6 bg-blue-500/15 text-blue-400 rounded-full text-xs font-bold">
                                                {acreedor.prestamosActivos}
                                            </span>
                                        ) : (
                                            <span className="text-gray-600 text-xs">0</span>
                                        )}
                                    </td>
                                    <td className="py-3.5 px-5">
                                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                                            acreedor.estaActivo
                                                ? 'bg-green-500/15 text-green-400'
                                                : 'bg-navy-700 text-gray-400'
                                        }`}>
                                            <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${acreedor.estaActivo ? 'bg-green-400' : 'bg-gray-500'}`}></span>
                                            {acreedor.estaActivo ? 'Activo' : 'Inactivo'}
                                        </span>
                                    </td>
                                    <td className="py-3.5 px-5">
                                        <div className="flex items-center gap-2">
                                            <button
                                                onClick={() => handleEdit(acreedor)}
                                                className="inline-flex items-center gap-1 text-primary-400 hover:text-primary-300 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                </svg>
                                                Editar
                                            </button>
                                            {acreedor.estaActivo && (
                                                <button
                                                    onClick={() => confirmDelete(acreedor)}
                                                    className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                    </svg>
                                                    Eliminar
                                                </button>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {/* Estado vacío */}
                    {acreedores.length === 0 && (
                        <div className="text-center py-14">
                            <svg className="w-16 h-16 text-gray-600 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">No hay acreedores registrados</h3>
                            <p className="text-gray-500">Comienza creando el primer acreedor en el catálogo</p>
                        </div>
                    )}
                </div>
            </div>

            {/* ── MODAL CREAR / EDITAR ── */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-60 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/40 max-w-2xl w-full max-h-[92vh] overflow-y-auto">

                        {/* Cabecera sticky */}
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900 z-10">
                            <h3 className="text-xl font-semibold text-gray-100">
                                {editingId ? 'Editar Acreedor' : 'Nuevo Acreedor'}
                            </h3>
                            <button onClick={resetForm} className="text-gray-400 hover:text-gray-200 transition-colors">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6 space-y-4">

                            {/* ── INFORMACIÓN GENERAL ── */}
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                {/* Nombre */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Nombre <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.nombre}
                                        onChange={(e) => setField('nombre', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Nombre completo o razón social"
                                    />
                                </div>

                                {/* Tipo */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo de Acreedor <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoAcreedor}
                                        onChange={(e) => setField('tipoAcreedor', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar tipo...</option>
                                        {TIPO_ACREEDOR_OPTIONS.map(t => (
                                            <option key={t.id} value={t.id}>{t.label}</option>
                                        ))}
                                    </select>
                                </div>

                                {/* Tipo de Identificación */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Tipo de Identificación</label>
                                    <select
                                        value={formData.tipoIdentificacion}
                                        onChange={(e) => setField('tipoIdentificacion', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Sin especificar</option>
                                        <option value="RUC">RUC</option>
                                        <option value="Cédula">Cédula</option>
                                        <option value="Pasaporte">Pasaporte</option>
                                    </select>
                                </div>

                                {/* Identificación */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Número de Identificación</label>
                                    <input
                                        type="text"
                                        value={formData.identificacion}
                                        onChange={(e) => setField('identificacion', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="RUC o cédula"
                                    />
                                </div>
                            </div>

                            {/* ── DATOS BANCARIOS (colapsable) ── */}
                            <CollapsibleSection
                                title="Datos Bancarios"
                                icon={
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                                    </svg>
                                }
                            >
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Banco</label>
                                        <input
                                            type="text"
                                            value={formData.banco}
                                            onChange={(e) => setField('banco', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="Ej: Banco Nacional de Panamá"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Tipo de Cuenta</label>
                                        <select
                                            value={formData.tipoCuenta}
                                            onChange={(e) => setField('tipoCuenta', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                        >
                                            <option value="">Sin especificar</option>
                                            <option value="Ahorro">Ahorro</option>
                                            <option value="Corriente">Corriente</option>
                                        </select>
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Número de Cuenta</label>
                                        <input
                                            type="text"
                                            value={formData.numeroCuenta}
                                            onChange={(e) => setField('numeroCuenta', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="Número de cuenta bancaria"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">IBAN</label>
                                        <input
                                            type="text"
                                            value={formData.iban}
                                            onChange={(e) => setField('iban', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="IBAN (internacional)"
                                        />
                                    </div>
                                </div>
                            </CollapsibleSection>

                            {/* ── CONTACTO (colapsable) ── */}
                            <CollapsibleSection
                                title="Información de Contacto"
                                icon={
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                                    </svg>
                                }
                            >
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Teléfono</label>
                                        <input
                                            type="text"
                                            value={formData.telefono}
                                            onChange={(e) => setField('telefono', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="+507 xxx-xxxx"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Correo Electrónico</label>
                                        <input
                                            type="email"
                                            value={formData.email}
                                            onChange={(e) => setField('email', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="correo@ejemplo.com"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Nombre del Contacto</label>
                                        <input
                                            type="text"
                                            value={formData.contactoNombre}
                                            onChange={(e) => setField('contactoNombre', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="Persona de contacto"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Dirección</label>
                                        <input
                                            type="text"
                                            value={formData.direccion}
                                            onChange={(e) => setField('direccion', e.target.value)}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            placeholder="Dirección del acreedor"
                                        />
                                    </div>
                                </div>
                            </CollapsibleSection>

                            {/* ── OBSERVACIONES ── */}
                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-2">Observaciones</label>
                                <textarea
                                    rows="3"
                                    value={formData.observaciones}
                                    onChange={(e) => setField('observaciones', e.target.value)}
                                    className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none text-sm"
                                    placeholder="Notas adicionales sobre el acreedor..."
                                />
                            </div>

                            {/* Botones */}
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
                                    className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg font-medium transition-colors"
                                >
                                    {editingId ? 'Actualizar Acreedor' : 'Crear Acreedor'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* ── MODAL CONFIRMAR ELIMINACIÓN ── */}
            {showConfirmDelete && acreedorToDelete && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-60 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-red-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                Eliminar Acreedor
                            </h3>
                            <p className="text-gray-400 text-center mb-4">
                                ¿Está seguro de que desea eliminar a <strong className="text-gray-200">{acreedorToDelete.nombre}</strong> permanentemente? Esta acción no se puede deshacer.
                            </p>

                            {/* Error si tiene deducciones vinculadas */}
                            {deleteWarningDeducciones.length > 0 && (
                                <div className="mb-4 bg-red-500/10 border border-red-500/30 rounded-lg p-3">
                                    <p className="text-sm text-red-400 font-medium mb-2">
                                        Este acreedor tiene {deleteWarningDeducciones.length} deducción(es) vinculada(s). Elimínelas primero:
                                    </p>
                                    <ul className="space-y-1">
                                        {deleteWarningDeducciones.map((d, idx) => (
                                            <li key={idx} className="text-xs text-red-300/80">
                                                - {d.descripcion || d.nombre || `Deducción #${d.id}`}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}

                            <div className="flex gap-3">
                                <button
                                    onClick={() => {
                                        setShowConfirmDelete(false);
                                        setAcreedorToDelete(null);
                                        setDeleteWarningDeducciones([]);
                                    }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleDelete}
                                    className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-medium transition-colors"
                                    disabled={deleteWarningDeducciones.length > 0}
                                >
                                    Eliminar
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

export default AcreedoresPage;
