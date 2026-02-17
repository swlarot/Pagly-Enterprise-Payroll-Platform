import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import AcreedorCombobox from '../components/AcreedorCombobox';

// Constantes de tipos de deduccion (valores numericos del backend)
const TIPO_PENSION_ALIMENTICIA = 3;
const TIPO_EMBARGO = 4;

// Categorias de deduccion
const CATEGORIA_BADGE = {
    1: { label: 'Pensión Alimenticia', className: 'bg-red-500/15 text-red-400' },
    2: { label: 'Embargo Judicial', className: 'bg-orange-500/15 text-orange-400' },
    3: { label: 'Voluntaria', className: 'bg-blue-500/15 text-blue-400' },
};

// Estado inicial completo del formulario incluyendo todos los nuevos campos
const INITIAL_FORM = {
    // Campos base
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
    observaciones: '',

    // Informacion del Acreedor
    acreedorId: null,
    nombreAcreedor: '',
    identificacionAcreedor: '',
    bancoAcreedor: '',
    cuentaBancariaAcreedor: '',

    // Orden Judicial (PensionAlimenticia=3, Embargo=4)
    numeroExpediente: '',
    juzgado: '',
    fechaOrdenJudicial: '',
    nombreJuez: '',
    estadoOrdenJudicial: '1', // 1=Vigente, 2=Suspendida, 3=Levantada

    // Autorizacion del Trabajador (tipos voluntarios)
    tieneAutorizacionEscrita: false,
    fechaAutorizacion: '',
    documentoAutorizacionRef: '',

    // Base de Calculo (cuando esPorcentaje && PensionAlimenticia)
    baseCalculo: '0', // 0=SalarioBruto, 1=SalarioNeto

    // Monto Total (solo Embargo)
    montoTotalACobrar: '',

    // Aplicacion Especial (solo PensionAlimenticia)
    aplicaADecimoTercerMes: false,
    aplicaAPrestaciones: false,
};

// Componente de seccion colapsable
const CollapsibleSection = ({ title, icon, children, defaultOpen = false, badge }) => {
    const [isOpen, setIsOpen] = useState(defaultOpen);

    return (
        <div className="border border-navy-600 rounded-lg overflow-hidden mb-4">
            <button
                type="button"
                onClick={() => setIsOpen(!isOpen)}
                className="w-full flex items-center justify-between px-4 py-3 bg-navy-800 hover:bg-navy-700 transition-colors text-left"
            >
                <div className="flex items-center gap-2">
                    {icon && <span className="text-gray-400">{icon}</span>}
                    <span className="text-sm font-medium text-gray-200">{title}</span>
                    {badge && (
                        <span className="text-xs bg-primary-500/20 text-primary-400 px-2 py-0.5 rounded-full">
                            {badge}
                        </span>
                    )}
                </div>
                {isOpen
                    ? <ChevronUp className="w-4 h-4 text-gray-400" />
                    : <ChevronDown className="w-4 h-4 text-gray-400" />
                }
            </button>
            {isOpen && (
                <div className="px-4 py-4 bg-navy-850 border-t border-navy-700">
                    {children}
                </div>
            )}
        </div>
    );
};

const DeduccionesPage = () => {
    const { canWrite, canDelete, isReadOnly } = useAuth();

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
    const [formData, setFormData] = useState(INITIAL_FORM);

    // Derivados del tipo seleccionado
    const tipoNum = parseInt(formData.tipoDeduccion) || 0;
    const esOrdenJudicial = tipoNum === TIPO_PENSION_ALIMENTICIA || tipoNum === TIPO_EMBARGO;
    const esEmbargo = tipoNum === TIPO_EMBARGO;
    const esPensionAlimenticia = tipoNum === TIPO_PENSION_ALIMENTICIA;
    const mostrarBaseCalculo = formData.esPorcentaje && esPensionAlimenticia;

    useEffect(() => {
        fetchDeducciones();
        fetchEmpleados();
        fetchTiposDeducciones();
    }, []);

    useEffect(() => {
        fetchDeducciones();
    }, [filterEmpleado, filterTipo, filterActivas]);

    const fetchDeducciones = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams();
            if (filterEmpleado) params.append('empleadoId', filterEmpleado);
            if (filterTipo) params.append('tipo', filterTipo);
            if (filterActivas) params.append('activas', 'true');

            const data = await api.get(`/api/deducciones?${params.toString()}`);
            setDeducciones(data);
        } catch (err) {
            toast.error(`Error al cargar deducciones: ${err.message}`);
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

    const fetchTiposDeducciones = async () => {
        try {
            const data = await api.get('/api/deducciones/tipos');
            setTiposDeducciones(data);
        } catch (err) {
            // Si el endpoint no existe, usar lista hardcoded incluyendo Cooperativa=8
            setTiposDeducciones([
                { id: 1, nombre: 'Seguridad Social' },
                { id: 2, nombre: 'Seguro Educativo' },
                { id: 3, nombre: 'Pensión Alimenticia' },
                { id: 4, nombre: 'Embargo' },
                { id: 5, nombre: 'Préstamo' },
                { id: 6, nombre: 'Impuesto sobre la Renta' },
                { id: 7, nombre: 'Descuento' },
                { id: 8, nombre: 'Cooperativa' },
                { id: 9, nombre: 'Otro' },
            ]);
        }
    };

    // Formatear moneda
    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    // Stats
    const deduccionesActivas = deducciones.filter(d => d.estaActivo);
    const salarioPromedio = empleados.length > 0
        ? empleados.reduce((sum, e) => sum + e.salarioBase, 0) / empleados.length
        : 1500;

    const totalMensual = deduccionesActivas.reduce((sum, d) => {
        if (d.esPorcentaje) return sum + (salarioPromedio * d.porcentaje / 100);
        return sum + d.monto;
    }, 0);

    const empleadosAfectados = new Set(deducciones.map(d => d.empleadoId)).size;

    const contarPorTipo = () => {
        const counts = {};
        deducciones.forEach(d => {
            counts[d.tipoDeduccion] = (counts[d.tipoDeduccion] || 0) + 1;
        });
        return counts;
    };

    const countsporTipo = contarPorTipo();

    const getTipoBadgeColor = (tipo) => {
        const colors = {
            'SeguridadSocial': 'bg-blue-500/15 text-blue-400',
            'SeguroEducativo': 'bg-green-500/15 text-green-400',
            'ImpuestoRenta': 'bg-purple-500/15 text-purple-400',
            'PensionAlimenticia': 'bg-red-500/15 text-red-400',
            'Prestamo': 'bg-amber-500/15 text-amber-400',
            'Embargo': 'bg-orange-500/15 text-orange-400',
            'Cooperativa': 'bg-teal-500/15 text-teal-400',
            'Descuento': 'bg-navy-700 text-gray-300',
            'Otro': 'bg-navy-700 text-gray-300'
        };
        return colors[tipo] || 'bg-navy-700 text-gray-300';
    };

    const getTipoNombre = (tipo) => {
        const nombres = {
            'SeguridadSocial': 'Seguridad Social',
            'SeguroEducativo': 'Seguro Educativo',
            'ImpuestoRenta': 'Impuesto sobre la Renta',
            'PensionAlimenticia': 'Pensión Alimenticia',
            'Prestamo': 'Préstamo',
            'Embargo': 'Embargo',
            'Cooperativa': 'Cooperativa',
            'Descuento': 'Descuento',
            'Otro': 'Otro'
        };
        return nombres[tipo] || tipo;
    };

    // Badge de categoria
    const getCategoriaBadge = (deduccion) => {
        const cat = deduccion.categoriaDeduccion;
        if (!cat && !deduccion.categoria) return null;
        const catId = cat || deduccion.categoria;
        const info = CATEGORIA_BADGE[catId];
        if (!info) return null;
        return (
            <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${info.className}`}>
                {info.label}
            </span>
        );
    };

    // Calculo del progreso de cobro para embargos
    const calcularProgreso = (deduccion) => {
        if (!deduccion.montoTotalACobrar || deduccion.montoTotalACobrar <= 0) return null;
        const pct = Math.min(
            ((deduccion.montoCobradoAcumulado || 0) / deduccion.montoTotalACobrar) * 100,
            100
        );
        return pct;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        // Validaciones base
        if (formData.esPorcentaje) {
            if (parseFloat(formData.porcentaje) <= 0 || parseFloat(formData.porcentaje) > 100) {
                toast.error('El porcentaje debe estar entre 0.01 y 100');
                return;
            }
        } else {
            if (parseFloat(formData.monto) <= 0) {
                toast.error('El monto debe ser mayor a 0');
                return;
            }
        }

        // Validacion de orden judicial
        if (esOrdenJudicial) {
            if (!formData.numeroExpediente.trim()) {
                toast.error('El número de expediente es requerido para este tipo de deducción');
                return;
            }
            if (!formData.juzgado.trim()) {
                toast.error('El juzgado es requerido para este tipo de deducción');
                return;
            }
        }

        try {
            const url = editingId ? `/api/deducciones/${editingId}` : '/api/deducciones';

            const payload = {
                // Campos base
                empleadoId: parseInt(formData.empleadoId),
                tipoDeduccion: parseInt(formData.tipoDeduccion),
                descripcion: formData.descripcion,
                esPorcentaje: formData.esPorcentaje,
                monto: formData.esPorcentaje ? 0 : parseFloat(formData.monto) || 0,
                porcentaje: formData.esPorcentaje ? parseFloat(formData.porcentaje) || 0 : 0,
                fechaInicio: formData.fechaInicio,
                fechaFin: formData.fechaFin || null,
                prioridad: parseInt(formData.prioridad) || 10,
                referencia: formData.referencia || null,
                observaciones: formData.observaciones || null,

                // Informacion del acreedor
                acreedorId: formData.acreedorId || null,
                nombreAcreedor: formData.nombreAcreedor || null,
                identificacionAcreedor: formData.identificacionAcreedor || null,
                bancoAcreedor: formData.bancoAcreedor || null,
                cuentaBancariaAcreedor: formData.cuentaBancariaAcreedor || null,

                // Orden judicial (solo si aplica)
                numeroExpediente: esOrdenJudicial ? (formData.numeroExpediente || null) : null,
                juzgado: esOrdenJudicial ? (formData.juzgado || null) : null,
                fechaOrdenJudicial: esOrdenJudicial && formData.fechaOrdenJudicial ? formData.fechaOrdenJudicial : null,
                nombreJuez: esOrdenJudicial ? (formData.nombreJuez || null) : null,
                estadoOrdenJudicial: esOrdenJudicial ? (parseInt(formData.estadoOrdenJudicial) || 1) : null,

                // Autorizacion del trabajador (solo si no es orden judicial)
                tieneAutorizacionEscrita: !esOrdenJudicial ? formData.tieneAutorizacionEscrita : false,
                fechaAutorizacion: !esOrdenJudicial && formData.tieneAutorizacionEscrita && formData.fechaAutorizacion
                    ? formData.fechaAutorizacion
                    : null,
                documentoAutorizacionRef: !esOrdenJudicial && formData.tieneAutorizacionEscrita
                    ? (formData.documentoAutorizacionRef || null)
                    : null,

                // Base de calculo
                baseCalculo: mostrarBaseCalculo ? parseInt(formData.baseCalculo) : 0,

                // Monto total (solo Embargo)
                montoTotalACobrar: esEmbargo && formData.montoTotalACobrar
                    ? parseFloat(formData.montoTotalACobrar)
                    : null,

                // Aplicacion especial (solo PensionAlimenticia)
                aplicaADecimoTercerMes: esPensionAlimenticia ? formData.aplicaADecimoTercerMes : false,
                aplicaAPrestaciones: esPensionAlimenticia ? formData.aplicaAPrestaciones : false,
            };

            if (editingId) {
                await api.put(url, payload);
            } else {
                await api.post(url, payload);
            }

            await fetchDeducciones();
            toast.success(editingId ? 'Deducción actualizada exitosamente' : 'Deducción creada exitosamente');
            resetForm();
        } catch (err) {
            toast.error(`Error al guardar deducción: ${err.message}`);
        }
    };

    const handleEdit = (deduccion) => {
        setFormData({
            // Campos base
            empleadoId: deduccion.empleadoId.toString(),
            tipoDeduccion: deduccion.tipoDeduccion?.toString() || '',
            descripcion: deduccion.descripcion || '',
            esPorcentaje: deduccion.esPorcentaje || false,
            monto: deduccion.monto?.toString() || '',
            porcentaje: deduccion.porcentaje?.toString() || '',
            fechaInicio: deduccion.fechaInicio
                ? new Date(deduccion.fechaInicio).toISOString().split('T')[0]
                : new Date().toISOString().split('T')[0],
            fechaFin: deduccion.fechaFin
                ? new Date(deduccion.fechaFin).toISOString().split('T')[0]
                : '',
            prioridad: deduccion.prioridad?.toString() || '10',
            referencia: deduccion.referencia || '',
            observaciones: deduccion.observaciones || '',

            // Informacion del acreedor
            acreedorId: deduccion.acreedorId || null,
            nombreAcreedor: deduccion.nombreAcreedor || '',
            identificacionAcreedor: deduccion.identificacionAcreedor || '',
            bancoAcreedor: deduccion.bancoAcreedor || '',
            cuentaBancariaAcreedor: deduccion.cuentaBancariaAcreedor || '',

            // Orden judicial
            numeroExpediente: deduccion.numeroExpediente || '',
            juzgado: deduccion.juzgado || '',
            fechaOrdenJudicial: deduccion.fechaOrdenJudicial
                ? new Date(deduccion.fechaOrdenJudicial).toISOString().split('T')[0]
                : '',
            nombreJuez: deduccion.nombreJuez || '',
            estadoOrdenJudicial: deduccion.estadoOrdenJudicial?.toString() || '1',

            // Autorizacion del trabajador
            tieneAutorizacionEscrita: deduccion.tieneAutorizacionEscrita || false,
            fechaAutorizacion: deduccion.fechaAutorizacion
                ? new Date(deduccion.fechaAutorizacion).toISOString().split('T')[0]
                : '',
            documentoAutorizacionRef: deduccion.documentoAutorizacionRef || '',

            // Base de calculo
            baseCalculo: deduccion.baseCalculo?.toString() || '0',

            // Monto total
            montoTotalACobrar: deduccion.montoTotalACobrar?.toString() || '',

            // Aplicacion especial
            aplicaADecimoTercerMes: deduccion.aplicaADecimoTercerMes || false,
            aplicaAPrestaciones: deduccion.aplicaAPrestaciones || false,
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
            await api.delete(`/api/deducciones/${deduccionToDelete.id}`);
            await fetchDeducciones();
            toast.success('Deducción desactivada exitosamente');
            setShowConfirmDelete(false);
            setDeduccionToDelete(null);
        } catch (err) {
            toast.error(`Error al desactivar deducción: ${err.message}`);
        }
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData(INITIAL_FORM);
    };

    // Helper para actualizar el formData facilmente
    const setField = (field, value) => setFormData(prev => ({ ...prev, [field]: value }));

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando deducciones...</p>
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
                            <p className="text-sm font-medium text-gray-400">Deducciones Activas</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{deduccionesActivas.length}</p>
                        </div>
                        <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Vigentes actualmente</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Total Mensual Est.</p>
                            <p className="text-3xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(totalMensual)}</p>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Estimación mensual</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Por Tipo</p>
                            <div className="mt-2 flex flex-wrap gap-1">
                                {Object.entries(countsporTipo).slice(0, 3).map(([tipo, count], index) => (
                                    <span key={tipo || `tipo-${index}`} className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${getTipoBadgeColor(tipo)}`}>
                                        {count}
                                    </span>
                                ))}
                            </div>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Clasificadas</span>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Empleados Afectados</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{empleadosAfectados}</p>
                        </div>
                        <div className="w-12 h-12 bg-purple-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                    <div className="mt-4 flex items-center text-sm">
                        <span className="text-gray-500">Con deducciones</span>
                    </div>
                </div>
            </div>

            {/* Filtros y Acciones */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={() => setShowModal(true)}
                    className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
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
                        className="px-3 py-2 bg-navy-900 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
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
                        className="px-3 py-2 bg-navy-900 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                    >
                        <option value="">Todos los tipos</option>
                        {tiposDeducciones.map(tipo => (
                            <option key={tipo.id || tipo} value={tipo.id || tipo}>
                                {tipo.nombre || getTipoNombre(tipo)}
                            </option>
                        ))}
                    </select>

                    <label className="inline-flex items-center px-3 py-2 bg-navy-900 border border-navy-600 rounded-lg cursor-pointer">
                        <input
                            type="checkbox"
                            checked={filterActivas}
                            onChange={(e) => setFilterActivas(e.target.checked)}
                            className="mr-2"
                        />
                        <span className="text-sm font-medium text-gray-300">Solo activas</span>
                    </label>
                </div>
            </div>

            {/* Tabla de Deducciones */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Lista de Deducciones
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({deducciones.length} {deducciones.length === 1 ? 'deducción' : 'deducciones'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-950 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Categoría</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Descripción</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Monto/%</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Vigencia</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Prioridad</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {deducciones.map((deduccion) => {
                                const progreso = calcularProgreso(deduccion);
                                return (
                                    <tr key={deduccion.id} className="hover:bg-navy-800 transition-colors">
                                        <td className="py-4 px-6 text-sm font-medium text-gray-100">
                                            {deduccion.empleadoNombre || 'N/A'}
                                        </td>
                                        <td className="py-4 px-6">
                                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getTipoBadgeColor(deduccion.tipoDeduccion)}`}>
                                                {getTipoNombre(deduccion.tipoDeduccion)}
                                            </span>
                                        </td>
                                        <td className="py-4 px-6">
                                            {getCategoriaBadge(deduccion) || (
                                                <span className="text-xs text-gray-500">—</span>
                                            )}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-100">
                                            {deduccion.descripcion}
                                            {/* Mostrar nombre del acreedor: vinculado (catálogo) o heredado (manual) */}
                                            {deduccion.acreedorId && deduccion.nombreAcreedor && (
                                                <span className="block text-xs text-primary-400 mt-0.5 font-normal">
                                                    {deduccion.nombreAcreedor}
                                                </span>
                                            )}
                                            {!deduccion.acreedorId && deduccion.nombreAcreedor && (
                                                <span className="block text-xs text-gray-500 mt-0.5 font-normal">
                                                    {deduccion.nombreAcreedor}
                                                </span>
                                            )}
                                        </td>
                                        <td className="py-4 px-6">
                                            <div className="text-sm font-medium font-mono text-gray-100">
                                                {deduccion.esPorcentaje
                                                    ? `${deduccion.porcentaje}%`
                                                    : formatCurrency(deduccion.monto)
                                                }
                                            </div>
                                            {/* Barra de progreso para embargos */}
                                            {progreso !== null && (
                                                <div className="mt-1">
                                                    <div className="w-24 bg-navy-700 rounded-full h-1.5">
                                                        <div
                                                            className="bg-orange-400 h-1.5 rounded-full"
                                                            style={{ width: `${progreso}%` }}
                                                        />
                                                    </div>
                                                    <span className="text-xs text-gray-500 mt-0.5 block">
                                                        {progreso.toFixed(0)}% cobrado
                                                    </span>
                                                </div>
                                            )}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-400">
                                            {new Date(deduccion.fechaInicio).toLocaleDateString('es-PA')}
                                            {' - '}
                                            {deduccion.fechaFin
                                                ? new Date(deduccion.fechaFin).toLocaleDateString('es-PA')
                                                : 'Indefinida'
                                            }
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-100">{deduccion.prioridad}</td>
                                        <td className="py-4 px-6">
                                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                                                deduccion.estaActivo
                                                    ? 'bg-green-500/15 text-green-400'
                                                    : 'bg-navy-700 text-gray-300'
                                            }`}>
                                                <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${deduccion.estaActivo ? 'bg-green-400' : 'bg-gray-400'}`}></span>
                                                {deduccion.estaActivo ? 'Activo' : 'Inactivo'}
                                            </span>
                                        </td>
                                        <td className="py-4 px-6">
                                            <div className="flex items-center gap-2">
                                                <button
                                                    onClick={() => handleEdit(deduccion)}
                                                    className="inline-flex items-center gap-1 text-primary-400 hover:text-primary-300 font-medium text-sm"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                    </svg>
                                                    Editar
                                                </button>
                                                {deduccion.estaActivo && (
                                                    <button
                                                        onClick={() => confirmDelete(deduccion)}
                                                        className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
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
                                );
                            })}
                        </tbody>
                    </table>

                    {/* Estado vacio */}
                    {deducciones.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-600 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">No hay deducciones registradas</h3>
                            <p className="text-gray-500">Comienza agregando la primera deducción</p>
                        </div>
                    )}
                </div>
            </div>

            {/* Modal Crear/Editar */}
            {showModal && (
                <div className="fixed inset-0 bg-black bg-opacity-60 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/40 max-w-2xl w-full max-h-[92vh] overflow-y-auto">
                        {/* Cabecera sticky */}
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900 z-10">
                            <h3 className="text-xl font-semibold text-gray-100">
                                {editingId ? 'Editar Deducción' : 'Nueva Deducción'}
                            </h3>
                            <button onClick={resetForm} className="text-gray-400 hover:text-gray-200 transition-colors">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6 space-y-4">
                            {/* ---- CAMPOS BASE ---- */}
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                {/* Empleado */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Empleado <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.empleadoId}
                                        onChange={(e) => setField('empleadoId', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar empleado...</option>
                                        {empleados.map(emp => (
                                            <option key={emp.id} value={emp.id}>
                                                {emp.nombre} {emp.apellido} - {formatCurrency(emp.salarioBase)}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                {/* Tipo de Deduccion */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo de Deducción <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoDeduccion}
                                        onChange={(e) => setField('tipoDeduccion', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar tipo...</option>
                                        {tiposDeducciones.map((tipo, index) => (
                                            <option key={tipo?.id || tipo || `tipo-${index}`} value={tipo?.id || tipo}>
                                                {tipo?.nombre || getTipoNombre(tipo)}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                {/* Descripcion */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Descripción <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.descripcion}
                                        onChange={(e) => setField('descripcion', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Descripción de la deducción"
                                    />
                                </div>

                                {/* Tipo de Calculo */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo de Cálculo <span className="text-red-500">*</span>
                                    </label>
                                    <div className="flex gap-4">
                                        <label className="inline-flex items-center cursor-pointer">
                                            <input
                                                type="radio"
                                                checked={!formData.esPorcentaje}
                                                onChange={() => setField('esPorcentaje', false)}
                                                className="mr-2"
                                            />
                                            <span className="text-sm text-gray-300">Monto Fijo</span>
                                        </label>
                                        <label className="inline-flex items-center cursor-pointer">
                                            <input
                                                type="radio"
                                                checked={formData.esPorcentaje}
                                                onChange={() => setField('esPorcentaje', true)}
                                                className="mr-2"
                                            />
                                            <span className="text-sm text-gray-300">Porcentaje sobre salario</span>
                                        </label>
                                    </div>
                                </div>

                                {/* Monto o Porcentaje */}
                                {!formData.esPorcentaje ? (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
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
                                                onChange={(e) => setField('monto', e.target.value)}
                                                className="w-full pl-8 pr-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 font-mono rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                                placeholder="150.00"
                                            />
                                        </div>
                                    </div>
                                ) : (
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
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
                                                onChange={(e) => setField('porcentaje', e.target.value)}
                                                className="w-full pr-10 pl-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 font-mono rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                                placeholder="5.00"
                                            />
                                            <span className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-500">%</span>
                                        </div>
                                    </div>
                                )}

                                {/* Fechas */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha de Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fechaInicio}
                                        onChange={(e) => setField('fechaInicio', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Fecha de Fin</label>
                                    <input
                                        type="date"
                                        value={formData.fechaFin}
                                        onChange={(e) => setField('fechaFin', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Dejar vacío para deducción indefinida</p>
                                </div>

                                {/* Prioridad y Referencia */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Prioridad</label>
                                    <input
                                        type="number"
                                        min="1"
                                        max="99"
                                        value={formData.prioridad}
                                        onChange={(e) => setField('prioridad', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Orden de aplicación (menor = mayor prioridad)</p>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Referencia</label>
                                    <input
                                        type="text"
                                        value={formData.referencia}
                                        onChange={(e) => setField('referencia', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Número de referencia"
                                    />
                                </div>

                                {/* Observaciones */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">Observaciones</label>
                                    <textarea
                                        rows="2"
                                        value={formData.observaciones}
                                        onChange={(e) => setField('observaciones', e.target.value)}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 resize-none"
                                        placeholder="Notas adicionales..."
                                    />
                                </div>
                            </div>

                            {/* ---- SECCION: INFORMACION DEL ACREEDOR ---- */}
                            <CollapsibleSection
                                title="Información del Acreedor"
                                icon={
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-2 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                                    </svg>
                                }
                            >
                                {/* Combobox de búsqueda en el catálogo de acreedores */}
                                <div className="mb-4">
                                    <label className="block text-sm font-medium text-gray-300 mb-1">
                                        Vincular Acreedor del Catálogo
                                        <span className="ml-2 text-xs text-gray-500 font-normal">(opcional — o ingresa manualmente abajo)</span>
                                    </label>
                                    <AcreedorCombobox
                                        value={formData.acreedorId}
                                        onChange={(acreedorId, acreedorData) => {
                                            if (acreedorData) {
                                                // Auto-rellenar campos embebidos desde el catálogo
                                                setFormData(prev => ({
                                                    ...prev,
                                                    acreedorId: acreedorId,
                                                    nombreAcreedor: acreedorData.nombre || '',
                                                    identificacionAcreedor: acreedorData.identificacion || '',
                                                    bancoAcreedor: acreedorData.banco || '',
                                                    cuentaBancariaAcreedor: acreedorData.numeroCuenta || '',
                                                }));
                                            } else {
                                                // Limpiar vinculación — dejar campos editables
                                                setFormData(prev => ({
                                                    ...prev,
                                                    acreedorId: null,
                                                }));
                                            }
                                        }}
                                    />
                                </div>

                                {/* Campos embebidos — solo lectura si hay acreedor vinculado */}
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">
                                            Nombre del Acreedor
                                            {formData.acreedorId && (
                                                <span className="ml-2 text-xs text-primary-400 font-normal">auto-rellenado</span>
                                            )}
                                        </label>
                                        <input
                                            type="text"
                                            value={formData.nombreAcreedor}
                                            onChange={(e) => setField('nombreAcreedor', e.target.value)}
                                            readOnly={!!formData.acreedorId}
                                            className={`w-full px-3 py-2 border rounded-lg focus:outline-none text-sm ${
                                                formData.acreedorId
                                                    ? 'bg-navy-700 border-navy-600 text-gray-400 cursor-not-allowed'
                                                    : 'bg-navy-800 border-navy-600 text-gray-200 focus:ring-2 focus:ring-primary-500'
                                            }`}
                                            placeholder="Nombre completo del acreedor"
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">
                                            Identificación del Acreedor
                                            {formData.acreedorId && (
                                                <span className="ml-2 text-xs text-primary-400 font-normal">auto-rellenado</span>
                                            )}
                                        </label>
                                        <input
                                            type="text"
                                            value={formData.identificacionAcreedor}
                                            onChange={(e) => setField('identificacionAcreedor', e.target.value)}
                                            readOnly={!!formData.acreedorId}
                                            className={`w-full px-3 py-2 border rounded-lg focus:outline-none text-sm ${
                                                formData.acreedorId
                                                    ? 'bg-navy-700 border-navy-600 text-gray-400 cursor-not-allowed'
                                                    : 'bg-navy-800 border-navy-600 text-gray-200 focus:ring-2 focus:ring-primary-500'
                                            }`}
                                            placeholder="Cédula o RUC"
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">
                                            Banco del Acreedor
                                            {formData.acreedorId && (
                                                <span className="ml-2 text-xs text-primary-400 font-normal">auto-rellenado</span>
                                            )}
                                        </label>
                                        <input
                                            type="text"
                                            value={formData.bancoAcreedor}
                                            onChange={(e) => setField('bancoAcreedor', e.target.value)}
                                            readOnly={!!formData.acreedorId}
                                            className={`w-full px-3 py-2 border rounded-lg focus:outline-none text-sm ${
                                                formData.acreedorId
                                                    ? 'bg-navy-700 border-navy-600 text-gray-400 cursor-not-allowed'
                                                    : 'bg-navy-800 border-navy-600 text-gray-200 focus:ring-2 focus:ring-primary-500'
                                            }`}
                                            placeholder="Nombre del banco"
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">
                                            Cuenta Bancaria del Acreedor
                                            {formData.acreedorId && (
                                                <span className="ml-2 text-xs text-primary-400 font-normal">auto-rellenado</span>
                                            )}
                                        </label>
                                        <input
                                            type="text"
                                            value={formData.cuentaBancariaAcreedor}
                                            onChange={(e) => setField('cuentaBancariaAcreedor', e.target.value)}
                                            readOnly={!!formData.acreedorId}
                                            className={`w-full px-3 py-2 border rounded-lg focus:outline-none text-sm ${
                                                formData.acreedorId
                                                    ? 'bg-navy-700 border-navy-600 text-gray-400 cursor-not-allowed'
                                                    : 'bg-navy-800 border-navy-600 text-gray-200 focus:ring-2 focus:ring-primary-500'
                                            }`}
                                            placeholder="Número de cuenta"
                                        />
                                    </div>
                                </div>
                            </CollapsibleSection>

                            {/* ---- SECCION: ORDEN JUDICIAL (PensionAlimenticia o Embargo) ---- */}
                            {esOrdenJudicial && (
                                <CollapsibleSection
                                    title="Orden Judicial"
                                    defaultOpen={true}
                                    badge="Requerido"
                                    icon={
                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
                                        </svg>
                                    }
                                >
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-1">
                                                Número de Expediente <span className="text-red-500">*</span>
                                            </label>
                                            <input
                                                type="text"
                                                value={formData.numeroExpediente}
                                                onChange={(e) => setField('numeroExpediente', e.target.value)}
                                                className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                placeholder="Ej: 01-PA-24-001"
                                            />
                                        </div>

                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-1">
                                                Juzgado <span className="text-red-500">*</span>
                                            </label>
                                            <input
                                                type="text"
                                                value={formData.juzgado}
                                                onChange={(e) => setField('juzgado', e.target.value)}
                                                className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                placeholder="Nombre del juzgado"
                                            />
                                        </div>

                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-1">Fecha de la Orden Judicial</label>
                                            <input
                                                type="date"
                                                value={formData.fechaOrdenJudicial}
                                                onChange={(e) => setField('fechaOrdenJudicial', e.target.value)}
                                                className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            />
                                        </div>

                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-1">Nombre del Juez</label>
                                            <input
                                                type="text"
                                                value={formData.nombreJuez}
                                                onChange={(e) => setField('nombreJuez', e.target.value)}
                                                className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                placeholder="Nombre del juez"
                                            />
                                        </div>

                                        <div className="md:col-span-2">
                                            <label className="block text-sm font-medium text-gray-300 mb-1">Estado de la Orden Judicial</label>
                                            <select
                                                value={formData.estadoOrdenJudicial}
                                                onChange={(e) => setField('estadoOrdenJudicial', e.target.value)}
                                                className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                            >
                                                <option value="1">Vigente</option>
                                                <option value="2">Suspendida</option>
                                                <option value="3">Levantada</option>
                                            </select>
                                        </div>
                                    </div>
                                </CollapsibleSection>
                            )}

                            {/* ---- SECCION: AUTORIZACION DEL TRABAJADOR (tipos voluntarios) ---- */}
                            {!esOrdenJudicial && (
                                <CollapsibleSection
                                    title="Autorización del Trabajador"
                                    icon={
                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                                        </svg>
                                    }
                                >
                                    <div className="space-y-4">
                                        <label className="inline-flex items-center gap-3 cursor-pointer">
                                            <input
                                                type="checkbox"
                                                checked={formData.tieneAutorizacionEscrita}
                                                onChange={(e) => setField('tieneAutorizacionEscrita', e.target.checked)}
                                                className="w-4 h-4 accent-primary-500"
                                            />
                                            <span className="text-sm text-gray-300">El trabajador tiene autorización escrita</span>
                                        </label>

                                        {formData.tieneAutorizacionEscrita && (
                                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pl-7">
                                                <div>
                                                    <label className="block text-sm font-medium text-gray-300 mb-1">Fecha de Autorización</label>
                                                    <input
                                                        type="date"
                                                        value={formData.fechaAutorizacion}
                                                        onChange={(e) => setField('fechaAutorizacion', e.target.value)}
                                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                    />
                                                </div>

                                                <div>
                                                    <label className="block text-sm font-medium text-gray-300 mb-1">Referencia del Documento</label>
                                                    <input
                                                        type="text"
                                                        value={formData.documentoAutorizacionRef}
                                                        onChange={(e) => setField('documentoAutorizacionRef', e.target.value)}
                                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                        placeholder="Ej: AUTH-2024-001"
                                                    />
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </CollapsibleSection>
                            )}

                            {/* ---- SECCION: BASE DE CALCULO (Porcentaje + PensionAlimenticia) ---- */}
                            {mostrarBaseCalculo && (
                                <CollapsibleSection
                                    title="Base de Cálculo"
                                    defaultOpen={true}
                                    icon={
                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 11h.01M12 11h.01M15 11h.01M9 11h6m0 4H9" />
                                        </svg>
                                    }
                                >
                                    <p className="text-xs text-gray-500 mb-3">
                                        Para pensiones alimenticias con porcentaje, indica sobre qué base se calcula el monto.
                                    </p>
                                    <div className="flex gap-6">
                                        <label className="inline-flex items-center gap-2 cursor-pointer">
                                            <input
                                                type="radio"
                                                checked={formData.baseCalculo === '0'}
                                                onChange={() => setField('baseCalculo', '0')}
                                                className="accent-primary-500"
                                            />
                                            <span className="text-sm text-gray-300">Salario Bruto</span>
                                        </label>
                                        <label className="inline-flex items-center gap-2 cursor-pointer">
                                            <input
                                                type="radio"
                                                checked={formData.baseCalculo === '1'}
                                                onChange={() => setField('baseCalculo', '1')}
                                                className="accent-primary-500"
                                            />
                                            <span className="text-sm text-gray-300">Salario Neto</span>
                                        </label>
                                    </div>
                                </CollapsibleSection>
                            )}

                            {/* ---- SECCION: MONTO TOTAL A COBRAR (solo Embargo) ---- */}
                            {esEmbargo && (
                                <CollapsibleSection
                                    title="Monto Total a Cobrar"
                                    defaultOpen={true}
                                    icon={
                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                        </svg>
                                    }
                                >
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-1">Monto Total a Cobrar ($)</label>
                                        <div className="relative">
                                            <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-500">$</span>
                                            <input
                                                type="number"
                                                step="0.01"
                                                min="0"
                                                value={formData.montoTotalACobrar}
                                                onChange={(e) => setField('montoTotalACobrar', e.target.value)}
                                                className="w-full pl-8 pr-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 font-mono rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                                placeholder="0.00"
                                            />
                                        </div>
                                        <p className="text-xs text-gray-500 mt-1">
                                            Ingresa el monto total de la deuda. La deducción se detendrá automáticamente cuando se alcance este monto.
                                        </p>
                                    </div>
                                </CollapsibleSection>
                            )}

                            {/* ---- SECCION: APLICACION ESPECIAL (solo PensionAlimenticia) ---- */}
                            {esPensionAlimenticia && (
                                <CollapsibleSection
                                    title="Aplicación Especial"
                                    icon={
                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" />
                                        </svg>
                                    }
                                >
                                    <p className="text-xs text-gray-500 mb-3">
                                        Indica si esta pensión alimenticia también aplica sobre otros pagos especiales al trabajador.
                                    </p>
                                    <div className="space-y-3">
                                        <label className="inline-flex items-center gap-3 cursor-pointer">
                                            <input
                                                type="checkbox"
                                                checked={formData.aplicaADecimoTercerMes}
                                                onChange={(e) => setField('aplicaADecimoTercerMes', e.target.checked)}
                                                className="w-4 h-4 accent-primary-500"
                                            />
                                            <span className="text-sm text-gray-300">Aplica al Décimo Tercer Mes</span>
                                        </label>
                                        <label className="inline-flex items-center gap-3 cursor-pointer">
                                            <input
                                                type="checkbox"
                                                checked={formData.aplicaAPrestaciones}
                                                onChange={(e) => setField('aplicaAPrestaciones', e.target.checked)}
                                                className="w-4 h-4 accent-primary-500"
                                            />
                                            <span className="text-sm text-gray-300">Aplica a Prestaciones Laborales</span>
                                        </label>
                                    </div>
                                </CollapsibleSection>
                            )}

                            {/* Botones del formulario */}
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
                                    {editingId ? 'Actualizar Deducción' : 'Crear Deducción'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Modal Confirmacion Desactivar */}
            {showConfirmDelete && deduccionToDelete && (
                <div className="fixed inset-0 bg-black bg-opacity-60 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-md w-full">
                        <div className="p-6">
                            <div className="w-12 h-12 bg-red-500/15 rounded-full flex items-center justify-center mx-auto mb-4">
                                <svg className="w-6 h-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                                </svg>
                            </div>
                            <h3 className="text-lg font-semibold text-gray-100 text-center mb-2">
                                Desactivar Deducción
                            </h3>
                            <p className="text-gray-400 text-center mb-6">
                                ¿Está seguro de que desea desactivar esta deducción? Esta acción marcará la deducción como inactiva.
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => {
                                        setShowConfirmDelete(false);
                                        setDeduccionToDelete(null);
                                    }}
                                    className="flex-1 px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
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

export default DeduccionesPage;
