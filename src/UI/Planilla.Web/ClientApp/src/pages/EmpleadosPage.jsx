import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { formatDate, toDateInputValue } from '../utils/date';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { LinkUserModal } from '../components/empleados/LinkUserModal';
import { DeleteEmployeeModal } from '../components/empleados/DeleteEmployeeModal';
import { formatBalboas } from '../utils/currency';
import { PAY_PERIOD_CONFIG, CSS_RISK_OPTIONS } from '../constants/payroll';

const EmpleadosPage = () => {
    // Auth context for permissions
    const { canWrite, canDelete, isReadOnly } = useAuth();

    // State management
    const [empleados, setEmpleados] = useState([]);
    const [departamentos, setDepartamentos] = useState([]);
    const [posiciones, setPosiciones] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState('active'); // 'all', 'active', 'inactive', 'deleted', 'includeDeleted'
    const [showModal, setShowModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [empleadoToDelete, setEmpleadoToDelete] = useState(null);
    const [editingId, setEditingId] = useState(null);
    const [showLinkUserModal, setShowLinkUserModal] = useState(false);
    const [empleadoToLink, setEmpleadoToLink] = useState(null);
    const [showSaldoInicialModal, setShowSaldoInicialModal] = useState(false);
    const [empleadoSaldoInicial, setEmpleadoSaldoInicial] = useState(null);
    const [saldoInicialData, setSaldoInicialData] = useState(null);
    const [saldoInicialForm, setSaldoInicialForm] = useState({
        fechaCorte: '', diasVacacionesAcumulados: 0, diasVacacionesTomados: 0,
        montoDecimoAcumulado: 0, notas: ''
    });
    const [savingSaldoInicial, setSavingSaldoInicial] = useState(false);
    // B5: historial salarial del empleado en edición (read-only)
    const [historialSalarial, setHistorialSalarial] = useState([]);
    const [loadingHistorial, setLoadingHistorial] = useState(false);
    const [formData, setFormData] = useState({
        nombre: '',
        apellido: '',
        numeroIdentificacion: '',
        email: '',
        salarioBase: '',
        fechaContratacion: '',
        departamentoId: '',
        posicionId: '',
        payPeriodType: 2,       // Quincenal por defecto
        hoursPerWeek: 48,
        hoursPerPeriod: 104,
        // Campos CSS e ISR
        yearsCotized: 0,
        averageSalaryLast10Years: '',
        dependents: 0,
        cssRiskPercentage: 0.56,
        tipoContrato: 0,
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

    // Auto-calcular hoursPerPeriod cuando cambia payPeriodType o hoursPerWeek
    useEffect(() => {
        const config = PAY_PERIOD_CONFIG[formData.payPeriodType];
        if (config && formData.hoursPerWeek) {
            const calculatedHours = Math.round(parseFloat(formData.hoursPerWeek) * config.weekMultiplier);
            setFormData(prev => ({ ...prev, hoursPerPeriod: calculatedHours }));
        }
    }, [formData.payPeriodType, formData.hoursPerWeek]);

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

    // Filter employees based on search term and status
    const filteredEmpleados = empleados.filter(emp => {
        // Text search filter
        const matchesSearch =
            (emp.nombre ?? '').toLowerCase().includes(searchTerm.toLowerCase()) ||
            (emp.apellido ?? '').toLowerCase().includes(searchTerm.toLowerCase()) ||
            (emp.numeroIdentificacion ?? '').includes(searchTerm);

        if (!matchesSearch) return false;

        // Status filter
        switch (statusFilter) {
            case 'all':
                return !emp.isDeleted;
            case 'active':
                return emp.estaActivo && !emp.isDeleted;
            case 'inactive':
                return !emp.estaActivo && !emp.isDeleted;
            case 'deleted':
                return emp.isDeleted;
            case 'includeDeleted':
                return true;
            default:
                return !emp.isDeleted;
        }
    });

    const activeEmpleados = empleados.filter(emp => emp.estaActivo);
    const totalNomina = activeEmpleados.reduce((sum, emp) => sum + emp.salarioBase, 0);

    // Handle form submission (Create/Update)
    const handleSubmit = async (e) => {
        e.preventDefault();

        try {
            const empleadoEnEdicion = editingId ? empleados.find(emp => emp.id === editingId) : null;
            const tieneUsuarioVinculado = empleadoEnEdicion?.usuarioVinculadoEmail ?? empleadoEnEdicion?.tieneAccesoSistema;

            const payload = {
                nombre: formData.nombre,
                apellido: formData.apellido,
                salarioBase: parseFloat(formData.salarioBase),
                departamentoId: formData.departamentoId ? parseInt(formData.departamentoId) : null,
                posicionId: formData.posicionId ? parseInt(formData.posicionId) : null,
                payPeriodType: parseInt(formData.payPeriodType),
                tipoContrato: parseInt(formData.tipoContrato),
                hoursPerWeek: parseFloat(formData.hoursPerWeek),
                hoursPerPeriod: parseFloat(formData.hoursPerPeriod),
                // Campos CSS e ISR
                yearsCotized: parseInt(formData.yearsCotized) || 0,
                averageSalaryLast10Years: parseFloat(formData.averageSalaryLast10Years) || 0,
                dependents: parseInt(formData.dependents) || 0,
                cssRiskPercentage: parseFloat(formData.cssRiskPercentage) || 0.56,
                ...(editingId ? {
                    estaActivo: true,
                    // Fecha de contratación editable al actualizar
                    ...(formData.fechaContratacion ? { fechaContratacion: formData.fechaContratacion } : {})
                } : {
                    numeroIdentificacion: formData.numeroIdentificacion,
                    fechaContratacion: formData.fechaContratacion || new Date().toISOString().split('T')[0],
                    email: formData.email || null
                })
            };
            if (editingId && !tieneUsuarioVinculado)
                payload.email = formData.email || null;

            if (editingId) {
                await api.put(`/api/empleados/${editingId}`, payload);
                toast.success('Empleado actualizado exitosamente');
            } else {
                await api.post('/api/empleados', payload);
                toast.success('Empleado creado exitosamente');
            }

            // Refresh the list
            await fetchEmpleados();

            // Reset form and close modal
            resetForm();

        } catch (err) {
            toast.error(err.message || 'Error al guardar empleado');
        }
    };

    // Handle edit — pre-carga TODOS los campos del empleado existente
    // B5: carga el historial salarial del empleado para mostrarlo en el modal de edición
    const loadHistorialSalarial = async (empleadoId) => {
        try {
            setLoadingHistorial(true);
            const data = await api.get(`/api/empleados/${empleadoId}/historial-salarial`);
            setHistorialSalarial(Array.isArray(data) ? data : []);
        } catch (error) {
            // No bloquea la edición si falla; solo no muestra el historial.
            setHistorialSalarial([]);
        } finally {
            setLoadingHistorial(false);
        }
    };

    const handleEdit = (empleado) => {
        setHistorialSalarial([]);
        loadHistorialSalarial(empleado.id);
        setFormData({
            nombre: empleado.nombre,
            apellido: empleado.apellido,
            numeroIdentificacion: empleado.numeroIdentificacion,
            email: empleado.usuarioVinculadoEmail ?? empleado.email ?? '',
            salarioBase: empleado.salarioBase.toString(),
            fechaContratacion: empleado.fechaContratacion
                ? toDateInputValue(empleado.fechaContratacion)
                : '',
            departamentoId: empleado.departamentoId ? empleado.departamentoId.toString() : '',
            posicionId: empleado.posicionId ? empleado.posicionId.toString() : '',
            // payPeriodTypeValue es el int del enum enviado por el backend
            payPeriodType: empleado.payPeriodTypeValue ?? empleado.payPeriodType ?? 2,
            hoursPerWeek: empleado.hoursPerWeek ?? 48,
            hoursPerPeriod: empleado.hoursPerPeriod ?? 104,
            // Campos CSS e ISR — usar los valores del empleado con fallback a defaults
            yearsCotized: empleado.yearsCotized ?? 0,
            averageSalaryLast10Years: empleado.averageSalaryLast10Years
                ? empleado.averageSalaryLast10Years.toString()
                : '',
            dependents: empleado.dependents ?? 0,
            cssRiskPercentage: empleado.cssRiskPercentage ?? 0.56,
            tipoContrato: empleado.tipoContrato ?? 0,
        });
        setEditingId(empleado.id);
        setShowModal(true);
    };

    // Handle delete
    const handleDeleteClick = (empleado) => {
        setEmpleadoToDelete(empleado);
        setShowDeleteModal(true);
    };

    const handleDeleteSuccess = async () => {
        await fetchEmpleados();
        setEmpleadoToDelete(null);
    };

    // Handle reactivate
    const handleReactivate = async (empleado) => {
        if (!window.confirm(`¿Está seguro de reactivar a ${empleado.nombre} ${empleado.apellido}?`)) {
            return;
        }

        try {
            await api.post(`/api/empleados/${empleado.id}/reactivate`);
            await fetchEmpleados();
            toast.success('Empleado reactivado exitosamente');
        } catch (err) {
            toast.error(err.message || 'Error al reactivar empleado');
        }
    };

    // Vincular usuario a empleado
    const handleLinkUser = (empleado) => {
        setEmpleadoToLink(empleado);
        setShowLinkUserModal(true);
    };

    const handleLinkUserSuccess = async () => {
        await fetchEmpleados();
        setShowLinkUserModal(false);
        setEmpleadoToLink(null);
    };

    const handleSaldoInicial = async (empleado) => {
        setEmpleadoSaldoInicial(empleado);
        try {
            const data = await api.get(`/api/empleados/${empleado.id}/saldo-inicial`);
            setSaldoInicialData(data);
            setSaldoInicialForm({
                fechaCorte: data.fechaCorte ? data.fechaCorte.split('T')[0] : '',
                diasVacacionesAcumulados: data.diasVacacionesAcumulados || 0,
                diasVacacionesTomados: data.diasVacacionesTomados || 0,
                montoDecimoAcumulado: data.montoDecimoAcumulado || 0,
                notas: data.notas || ''
            });
        } catch {
            setSaldoInicialData(null);
            setSaldoInicialForm({ fechaCorte: '', diasVacacionesAcumulados: 0, diasVacacionesTomados: 0, montoDecimoAcumulado: 0, notas: '' });
        }
        setShowSaldoInicialModal(true);
    };

    const handleSaveSaldoInicial = async (e) => {
        e.preventDefault();
        if (!saldoInicialForm.fechaCorte) { toast.error('La fecha de corte es obligatoria'); return; }
        setSavingSaldoInicial(true);
        try {
            await api.post(`/api/empleados/${empleadoSaldoInicial.id}/saldo-inicial`, {
                fechaCorte: saldoInicialForm.fechaCorte,
                diasVacacionesAcumulados: Number(saldoInicialForm.diasVacacionesAcumulados),
                diasVacacionesTomados: Number(saldoInicialForm.diasVacacionesTomados),
                montoDecimoAcumulado: Number(saldoInicialForm.montoDecimoAcumulado),
                notas: saldoInicialForm.notas || null
            });
            toast.success('Saldos iniciales guardados correctamente');
            setShowSaldoInicialModal(false);
        } catch (err) {
            toast.error(err.message || 'Error al guardar saldos');
        } finally {
            setSavingSaldoInicial(false);
        }
    };

    // Desvincular usuario de empleado
    const handleUnlinkUser = async (empleado) => {
        if (!window.confirm(`¿Está seguro de desvincular el usuario de ${empleado.nombre} ${empleado.apellido}?`)) {
            return;
        }

        try {
            const response = await fetch(`/api/empleados/${empleado.id}/desvincular-usuario`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Error al desvincular usuario');
            }

            await fetchEmpleados();
            toast.success('Usuario desvinculado exitosamente');
        } catch (err) {
            toast.error(err.message || 'Error al desvincular usuario');
        }
    };

    // Reset form
    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setHistorialSalarial([]);
        setFormData({
            nombre: '',
            apellido: '',
            numeroIdentificacion: '',
            email: '',
            salarioBase: '',
            fechaContratacion: '',
            departamentoId: '',
            posicionId: '',
            payPeriodType: 2,
            hoursPerWeek: 48,
            hoursPerPeriod: 104,
            yearsCotized: 0,
            averageSalaryLast10Years: '',
            dependents: 0,
            cssRiskPercentage: 0.56,
            tipoContrato: 0,
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
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando empleados...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Action Bar + Stats bar compacta */}
            <div className="flex flex-col gap-4">
                {/* Fila superior: botón + stats */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    {canWrite() && (
                        <button
                            onClick={openNewModal}
                            className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                        >
                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                            </svg>
                            Agregar Empleado
                        </button>
                    )}

                    {/* Stats bar compacta */}
                    <div className="flex items-center gap-3 flex-wrap">
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-navy-800 border border-navy-700 rounded-lg">
                            <div className="w-2 h-2 rounded-full bg-primary-400" />
                            <span className="text-sm text-gray-300 font-medium">
                                {empleados.length} <span className="text-gray-500 font-normal">registrados</span>
                            </span>
                        </div>
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-navy-800 border border-navy-700 rounded-lg">
                            <div className="w-2 h-2 rounded-full bg-green-400" />
                            <span className="text-sm text-gray-300 font-medium">
                                {activeEmpleados.length} <span className="text-gray-500 font-normal">activos</span>
                            </span>
                        </div>
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-navy-800 border border-navy-700 rounded-lg">
                            <div className="w-2 h-2 rounded-full bg-red-400" />
                            <span className="text-sm text-gray-300 font-medium">
                                {empleados.filter(e => !e.estaActivo && !e.isDeleted).length} <span className="text-gray-500 font-normal">inactivos</span>
                            </span>
                        </div>
                        <div className="flex items-center gap-2 px-3 py-1.5 bg-navy-800 border border-navy-700 rounded-lg">
                            <div className="w-2 h-2 rounded-full bg-purple-400" />
                            <span className="text-sm font-mono text-gray-300 font-medium">
                                {formatBalboas(totalNomina)} <span className="text-gray-500 font-normal font-sans">nómina est.</span>
                            </span>
                        </div>
                    </div>
                </div>

                {/* Fila inferior: filtros + buscador */}
                <div className="flex flex-col sm:flex-row gap-3">
                    {/* Status Filter */}
                    <select
                        value={statusFilter}
                        onChange={(e) => setStatusFilter(e.target.value)}
                        className="px-4 py-2.5 bg-navy-900 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                    >
                        <option value="active">Solo Activos</option>
                        <option value="all">Todos (sin eliminados)</option>
                        <option value="inactive">Solo Inactivos</option>
                        <option value="deleted">Solo Eliminados</option>
                        <option value="includeDeleted">Incluir Eliminados</option>
                    </select>

                    {/* Search Bar */}
                    <div className="relative flex-1 sm:max-w-sm">
                        <input
                            type="text"
                            placeholder="Buscar por nombre, apellido o cédula..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="w-full pl-10 pr-4 py-2.5 bg-navy-900 border border-navy-600 rounded-lg text-gray-100 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                        />
                        <svg className="w-5 h-5 text-gray-400 absolute left-3 top-1/2 transform -translate-y-1/2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                        </svg>
                    </div>
                </div>
            </div>

            {/* Employee Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold font-display text-gray-100">
                        Lista de Empleados
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredEmpleados.length} {filteredEmpleados.length === 1 ? 'empleado' : 'empleados'})
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-950 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Nombre</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Identificación</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Email / Acceso</th>
                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Salario Base</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Tasa/h</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Fecha Contratación</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Departamento</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Posición</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {filteredEmpleados.map((empleado) => {
                                // Color de avatar consistente basado en el primer char del nombre
                                const avatarColors = [
                                    'bg-gradient-to-br from-primary-600 to-primary-500',
                                    'bg-gradient-to-br from-blue-600 to-blue-500',
                                    'bg-gradient-to-br from-purple-600 to-purple-500',
                                    'bg-gradient-to-br from-amber-600 to-amber-500',
                                    'bg-gradient-to-br from-red-600 to-red-500',
                                ];
                                const avatarColor = avatarColors[(empleado.nombre?.charCodeAt(0) || 0) % 5];
                                return (
                                <tr key={empleado.id} className="border-l-2 border-transparent hover:border-primary-500 hover:bg-navy-800/50 transition-all">
                                    {/* Nombre + Avatar */}
                                    <td className="px-4 py-3">
                                        <div className="flex items-center gap-3">
                                            <div className={`w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0 ${avatarColor}`}>
                                                {empleado.nombre?.[0]?.toUpperCase()}{empleado.apellido?.[0]?.toUpperCase()}
                                            </div>
                                            <div>
                                                <p className="text-sm font-semibold text-gray-200">{empleado.nombre} {empleado.apellido}</p>
                                                <p className="text-xs text-gray-500">{empleado.usuarioVinculadoEmail ?? empleado.email ?? `ID: ${empleado.id}`}</p>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="py-3 px-4 text-sm text-gray-300">{empleado.numeroIdentificacion}</td>
                                    <td className="py-3 px-4">
                                        {(empleado.usuarioVinculadoEmail ?? empleado.email) ? (
                                            <div className="flex flex-col gap-1">
                                                <span className="text-sm text-gray-300">{empleado.usuarioVinculadoEmail ?? empleado.email}</span>
                                                {empleado.tieneAccesoSistema ? (
                                                    <span className="inline-flex items-center w-fit px-2 py-0.5 rounded text-xs font-medium bg-green-500/15 text-green-400">
                                                        {empleado.rolSistema || 'Employee'}
                                                    </span>
                                                ) : (
                                                    <span className="inline-flex items-center w-fit px-2 py-0.5 rounded text-xs font-medium bg-navy-700 text-gray-400">
                                                        Sin acceso
                                                    </span>
                                                )}
                                            </div>
                                        ) : (
                                            <span className="text-sm text-gray-500">—</span>
                                        )}
                                    </td>
                                    {/* Salario con font-mono y prefijo B/. */}
                                    <td className="py-3 px-4 text-right">
                                        <span className="text-sm font-mono text-gray-200">
                                            B/. {Number(empleado.salarioBase || 0).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                        </span>
                                    </td>
                                    {/* Tasa por hora — B/. en lugar de USD */}
                                    <td className="py-3 px-4 text-sm font-mono text-gray-300">
                                        {(() => {
                                            // Usar hourlyRate del backend si está disponible y > 0
                                            if (empleado.hourlyRate && empleado.hourlyRate > 0) {
                                                return `B/. ${Number(empleado.hourlyRate).toLocaleString('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
                                            }
                                            // Fallback: calcular usando la fórmula mensual
                                            const hoursPerWeek = empleado.hoursPerWeek || 48;
                                            const weeksPerMonth = 52 / 12; // 4.3333...
                                            const hoursPerMonth = hoursPerWeek * weeksPerMonth;
                                            const rate = hoursPerMonth > 0 ? empleado.salarioBase / hoursPerMonth : 0;
                                            return `B/. ${Number(rate).toLocaleString('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
                                        })()}
                                    </td>
                                    <td className="py-3 px-4 text-sm text-gray-500">
                                        {formatDate(empleado.fechaContratacion)}
                                    </td>
                                    <td className="py-3 px-4 text-sm text-gray-300">
                                        {empleado.departamentoNombre || <span className="text-gray-500">-</span>}
                                    </td>
                                    <td className="py-3 px-4 text-sm text-gray-300">
                                        {empleado.posicionNombre || <span className="text-gray-500">-</span>}
                                    </td>
                                    {/* Estado con punto de color */}
                                    <td className="py-3 px-4">
                                        <div className="flex items-center gap-2">
                                            <div className={`w-2 h-2 rounded-full flex-shrink-0 ${
                                                empleado.isDeleted ? 'bg-gray-500' : empleado.estaActivo ? 'bg-green-400' : 'bg-red-400'
                                            }`} />
                                            <span className={`text-sm font-medium ${
                                                empleado.isDeleted ? 'text-gray-400' : empleado.estaActivo ? 'text-green-400' : 'text-red-400'
                                            }`}>
                                                {empleado.isDeleted ? 'Eliminado' : empleado.estaActivo ? 'Activo' : 'Inactivo'}
                                            </span>
                                        </div>
                                    </td>
                                    {/* Acciones: botones de icono compactos */}
                                    <td className="py-3 px-4">
                                        {!isReadOnly() ? (
                                            <div className="flex items-center gap-1">
                                                {canWrite() && (
                                                    <>
                                                        {/* Editar */}
                                                        <button
                                                            onClick={() => handleEdit(empleado)}
                                                            title="Editar"
                                                            className="p-1.5 text-gray-400 hover:text-primary-400 hover:bg-primary-500/10 rounded-lg transition-all"
                                                        >
                                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                            </svg>
                                                        </button>
                                                        {/* Vincular / Desvincular usuario */}
                                                        {empleado.userId ? (
                                                            <button
                                                                onClick={() => handleUnlinkUser(empleado)}
                                                                title="Desvincular usuario"
                                                                className="p-1.5 text-gray-400 hover:text-amber-400 hover:bg-amber-500/10 rounded-lg transition-all"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                                                                </svg>
                                                            </button>
                                                        ) : !(empleado.usuarioVinculadoEmail ?? empleado.email) ? (
                                                            <button
                                                                onClick={() => handleLinkUser(empleado)}
                                                                title="Vincular usuario existente"
                                                                className="p-1.5 text-gray-400 hover:text-green-400 hover:bg-green-500/10 rounded-lg transition-all"
                                                            >
                                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                                                                </svg>
                                                            </button>
                                                        ) : null}
                                                    </>
                                                )}
                                                {/* Saldos Iniciales */}
                                                {canWrite() && !empleado.isDeleted && (
                                                    <button
                                                        onClick={() => handleSaldoInicial(empleado)}
                                                        title="Saldos Iniciales (migración)"
                                                        className="p-1.5 text-gray-400 hover:text-yellow-400 hover:bg-yellow-500/10 rounded-lg transition-all"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" />
                                                        </svg>
                                                    </button>
                                                )}
                                                {/* Reactivar / Eliminar */}
                                                {empleado.isDeleted ? (
                                                    canDelete() && (
                                                        <button
                                                            onClick={() => handleReactivate(empleado)}
                                                            title="Reactivar"
                                                            className="p-1.5 text-gray-400 hover:text-green-400 hover:bg-green-500/10 rounded-lg transition-all"
                                                        >
                                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                                                            </svg>
                                                        </button>
                                                    )
                                                ) : canDelete() && (
                                                    <button
                                                        onClick={() => handleDeleteClick(empleado)}
                                                        title="Eliminar"
                                                        className="p-1.5 text-gray-400 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-all"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                                        </svg>
                                                    </button>
                                                )}
                                            </div>
                                        ) : (
                                            <span className="text-sm text-gray-500 italic">Solo lectura</span>
                                        )}
                                    </td>
                                </tr>
                                );
                            })}
                        </tbody>
                    </table>

                    {/* Empty State */}
                    {filteredEmpleados.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">
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
                                    className="mt-4 inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
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

            {/* Add/Edit Employee Modal - Portal a body para que el overlay cubra todo el viewport */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        {/* Modal Header */}
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold font-display text-gray-100">
                                {editingId ? 'Editar Empleado' : 'Nuevo Empleado'}
                            </h3>
                            <button
                                onClick={resetForm}
                                className="text-gray-400 hover:text-gray-200 transition-colors"
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
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Nombre <span className="text-red-400">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.nombre}
                                        onChange={(e) => setFormData({ ...formData, nombre: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                        placeholder="Ej: Juan"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Apellido <span className="text-red-400">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.apellido}
                                        onChange={(e) => setFormData({ ...formData, apellido: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                        placeholder="Ej: Pérez"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Número de Identificación <span className="text-red-400">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required={!editingId}
                                        disabled={editingId}
                                        value={formData.numeroIdentificacion}
                                        onChange={(e) => setFormData({ ...formData, numeroIdentificacion: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent disabled:bg-navy-700 disabled:cursor-not-allowed"
                                        placeholder="Ej: 8-123-4567"
                                    />
                                    {editingId && (
                                        <p className="text-xs text-gray-500 mt-1">No se puede editar la identificación</p>
                                    )}
                                </div>

                                <div>
                                    {editingId && (() => {
                                        const empleadoEnEdicion = empleados.find(e => e.id === editingId);
                                        const tieneUsuarioVinculado = empleadoEnEdicion?.usuarioVinculadoEmail || empleadoEnEdicion?.tieneAccesoSistema;
                                        if (tieneUsuarioVinculado && empleadoEnEdicion?.usuarioVinculadoEmail) {
                                            return (
                                                <>
                                                    <label className="block text-sm font-medium text-gray-300 mb-2">Email (usuario vinculado)</label>
                                                    <p className="text-lg font-semibold text-gray-100 py-2">{empleadoEnEdicion.usuarioVinculadoEmail}</p>
                                                    <p className="text-xs text-gray-500 mt-1">El email lo define el usuario vinculado. Para cambiarlo, desvincula desde la tabla y vuelve a vincular.</p>
                                                </>
                                            );
                                        }
                                        return (
                                            <>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Email
                                                    <span className="text-gray-400 font-normal ml-2 text-xs">(opcional)</span>
                                                </label>
                                                <input
                                                    type="email"
                                                    value={formData.email}
                                                    onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                                    placeholder="empleado@empresa.com"
                                                />
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Si incluyes un email, se creará automáticamente acceso al sistema con rol "Employee".
                                                    El empleado recibirá un correo para configurar su contraseña.
                                                </p>
                                            </>
                                        );
                                    })()}
                                    {!editingId && (
                                        <>
                                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                                Email
                                                <span className="text-gray-400 font-normal ml-2 text-xs">(opcional)</span>
                                            </label>
                                            <input
                                                type="email"
                                                value={formData.email}
                                                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                                                className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                                placeholder="empleado@empresa.com"
                                            />
                                            <p className="text-xs text-gray-500 mt-1">
                                                Si incluyes un email, se creará automáticamente acceso al sistema con rol "Employee".
                                                El empleado recibirá un correo para configurar su contraseña.
                                            </p>
                                        </>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Salario Base (Mensual) <span className="text-red-400">*</span>
                                    </label>
                                    <div className="relative">
                                        <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 text-sm font-medium">B/.</span>
                                        <input
                                            type="number"
                                            step="0.01"
                                            min="0.01"
                                            required
                                            value={formData.salarioBase}
                                            onChange={(e) => setFormData({ ...formData, salarioBase: e.target.value })}
                                            className="w-full pl-10 pr-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                            placeholder="1500.00"
                                        />
                                    </div>
                                    <p className="text-xs text-gray-500 mt-1">
                                        El salario mensual del empleado (independiente del período de pago)
                                    </p>
                                </div>

                                {/* Fecha de Contratación - visible tanto en creación como en edición */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha de Contratación {!editingId && <span className="text-red-400">*</span>}
                                        {editingId && <span className="text-gray-400 font-normal ml-2 text-xs">(opcional — se mantiene si no se cambia)</span>}
                                    </label>
                                    <input
                                        type="date"
                                        required={!editingId}
                                        value={formData.fechaContratacion}
                                        onChange={(e) => setFormData({ ...formData, fechaContratacion: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                                    />
                                </div>

                                {/* Tipo de Contrato */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo de contrato <span className="text-red-400">*</span>
                                    </label>
                                    <select
                                        value={formData.tipoContrato}
                                        onChange={(e) => setFormData({ ...formData, tipoContrato: parseInt(e.target.value) })}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent">
                                        <option value={0}>Indefinido</option>
                                        <option value={1}>Definido</option>
                                        <option value={2}>Por Obra</option>
                                    </select>
                                    <p className="text-sm text-gray-400 mt-1">
                                        Definido / Por Obra calculan cesantía (Decreto 60/1995) en vez de prima de antigüedad.
                                    </p>
                                </div>
                                
                                {/* B5: Historial Salarial (solo en edición) */}
                                {editingId && (
                                    <div className="md:col-span-2">
                                        <div className="border border-navy-600 rounded-lg p-4 bg-navy-950/50">
                                            <h4 className="text-sm font-semibold text-emerald-400 mb-1">
                                                Historial Salarial
                                            </h4>
                                            <p className="text-xs text-gray-500 mb-3">
                                                Base de los promedios de prima (5 años, Art. 226) e indemnización (6 meses, Art. 149) en la liquidación.
                                            </p>
                                            {loadingHistorial ? (
                                                <p className="text-sm text-gray-400">Cargando…</p>
                                            ) : historialSalarial.length === 0 ? (
                                                <p className="text-sm text-gray-400">Sin registros de salario todavía.</p>
                                            ) : (
                                                <ul className="space-y-2">
                                                    {historialSalarial.map((h) => (
                                                        <li key={h.id} className="flex items-center justify-between text-sm border-b border-navy-700/50 pb-2 last:border-0">
                                                            <div>
                                                                <span className="text-gray-100 font-medium">{formatBalboas(h.salarioMensual)}</span>
                                                                {h.motivo && <span className="text-gray-500 ml-2">— {h.motivo}</span>}
                                                            </div>
                                                            <span className="text-gray-400">{formatDate(h.fechaVigencia)}</span>
                                                        </li>
                                                    ))}
                                                </ul>
                                            )}
                                        </div>
                                    </div>
                                )}

                                {/* Sección: Información de Pago */}
                                <div className="md:col-span-2">
                                    <div className="border border-navy-600 rounded-lg p-4 bg-navy-950/50">
                                        <h4 className="text-sm font-semibold text-emerald-400 mb-4 flex items-center gap-2">
                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                            </svg>
                                            Información de Pago
                                        </h4>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                            {/* Tipo de Período */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Tipo de Período <span className="text-red-400">*</span>
                                                </label>
                                                <select
                                                    value={formData.payPeriodType}
                                                    onChange={(e) => setFormData({ ...formData, payPeriodType: parseInt(e.target.value) })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                                                >
                                                    <option value={0}>Semanal (52 períodos/año)</option>
                                                    <option value={1}>Bisemanal (26 períodos/año)</option>
                                                    <option value={2}>Quincenal (24 períodos/año)</option>
                                                    <option value={3}>Mensual (12 períodos/año)</option>
                                                </select>
                                            </div>

                                            {/* Horas por Semana */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Horas / Semana
                                                </label>
                                                <input
                                                    type="number"
                                                    step="0.5"
                                                    min="1"
                                                    max="168"
                                                    value={formData.hoursPerWeek}
                                                    onChange={(e) => setFormData({ ...formData, hoursPerWeek: e.target.value })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                                                    placeholder="48"
                                                />
                                            </div>

                                            {/* Horas por Período (auto-calculado) */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Horas / Período
                                                    <span className="text-gray-500 font-normal ml-2 text-xs">(auto-calculado)</span>
                                                </label>
                                                <input
                                                    type="number"
                                                    step="0.5"
                                                    min="1"
                                                    value={formData.hoursPerPeriod}
                                                    onChange={(e) => setFormData({ ...formData, hoursPerPeriod: e.target.value })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                                                    placeholder="104"
                                                />
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Puede ajustarse manualmente si es necesario
                                                </p>
                                            </div>

                                            {/* Salario por Período (readonly) */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Salario por Período
                                                    <span className="text-gray-500 font-normal ml-2 text-xs">(calculado)</span>
                                                </label>
                                                <div className="w-full px-3 py-2 border border-navy-700 bg-navy-950 text-blue-400 rounded-lg font-mono font-semibold">
                                                    {formData.salarioBase && PAY_PERIOD_CONFIG[formData.payPeriodType]
                                                        ? formatBalboas(parseFloat(formData.salarioBase) * 12 / PAY_PERIOD_CONFIG[formData.payPeriodType].periodsPerYear)
                                                        : <span className="text-gray-600">—</span>
                                                    }
                                                </div>
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Salario mensual × 12 &divide; períodos al año
                                                </p>
                                            </div>

                                            {/* Tasa por Hora (readonly) */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Tasa por Hora
                                                    <span className="text-gray-500 font-normal ml-2 text-xs">(calculada)</span>
                                                </label>
                                                <div className="w-full px-3 py-2 border border-navy-700 bg-navy-950 text-emerald-400 rounded-lg font-mono font-semibold">
                                                    {formData.salarioBase && formData.hoursPerWeek && parseFloat(formData.hoursPerWeek) > 0
                                                        ? formatBalboas(parseFloat(formData.salarioBase) / (parseFloat(formData.hoursPerWeek) * 4.3333))
                                                        : <span className="text-gray-600">—</span>
                                                    }
                                                </div>
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Salario mensual &divide; (horas semanales × 4.3333)
                                                </p>
                                            </div>
                                        </div>

                                        {/* Info: Períodos/Año */}
                                        <div className="mt-3 flex items-center gap-2 text-xs text-gray-500">
                                            <svg className="w-3.5 h-3.5 text-emerald-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                            </svg>
                                            Período seleccionado: <span className="text-emerald-400 font-medium">
                                                {PAY_PERIOD_CONFIG[formData.payPeriodType]?.name}
                                            </span>
                                            &mdash; {PAY_PERIOD_CONFIG[formData.payPeriodType]?.periodsPerYear} períodos al año
                                        </div>
                                    </div>
                                </div>

                                {/* Sección: Configuración CSS e ISR */}
                                <div className="md:col-span-2">
                                    <div className="border border-navy-600 rounded-lg p-4 bg-navy-950/50">
                                        <h4 className="text-sm font-semibold text-amber-400 mb-4 flex items-center gap-2">
                                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                                            </svg>
                                            Configuración CSS e ISR
                                        </h4>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                            {/* Años Cotizados CSS */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Años Cotizados CSS
                                                </label>
                                                <input
                                                    type="number"
                                                    min="0"
                                                    max="50"
                                                    step="1"
                                                    value={formData.yearsCotized}
                                                    onChange={(e) => setFormData({ ...formData, yearsCotized: e.target.value })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent"
                                                    placeholder="0"
                                                />
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Años cotizados ante la CSS. Referencial: no afecta el cálculo de la cuota, que se aplica sobre el salario completo (Ley 51/2005, Art. 96).
                                                </p>
                                            </div>

                                            {/* Promedio Salarial últimos 10 años */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Promedio Salarial (últimos 10 años)
                                                </label>
                                                <div className="relative">
                                                    <span className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 text-sm font-medium">B/.</span>
                                                    <input
                                                        type="number"
                                                        step="0.01"
                                                        min="0"
                                                        value={formData.averageSalaryLast10Years}
                                                        onChange={(e) => setFormData({ ...formData, averageSalaryLast10Years: e.target.value })}
                                                        className="w-full pl-10 pr-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent"
                                                        placeholder="0.00"
                                                    />
                                                </div>
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Referencial para topes de pensión (Art. 193). No afecta el cálculo de la cuota mensual.
                                                </p>
                                            </div>

                                            {/* Dependientes ISR */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Dependientes (ISR)
                                                </label>
                                                <input
                                                    type="number"
                                                    min="0"
                                                    max="3"
                                                    step="1"
                                                    value={formData.dependents}
                                                    onChange={(e) => setFormData({ ...formData, dependents: e.target.value })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent"
                                                    placeholder="0"
                                                />
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Deducción ISR de B/. 800 al año por cada dependiente (sin límite legal)
                                                </p>
                                            </div>

                                            {/* Riesgo Profesional CSS */}
                                            <div>
                                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                                    Riesgo Profesional CSS
                                                </label>
                                                <select
                                                    value={formData.cssRiskPercentage}
                                                    onChange={(e) => setFormData({ ...formData, cssRiskPercentage: parseFloat(e.target.value) })}
                                                    className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent"
                                                >
                                                    {CSS_RISK_OPTIONS.map(opt => (
                                                        <option key={opt.value} value={opt.value}>
                                                            {opt.label}
                                                        </option>
                                                    ))}
                                                </select>
                                                <p className="text-xs text-gray-500 mt-1">
                                                    Tasa de riesgo profesional patronal según actividad económica
                                                </p>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Departamento
                                    </label>
                                    <select
                                        value={formData.departamentoId}
                                        onChange={handleDepartmentChange}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
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
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Posición
                                    </label>
                                    <select
                                        value={formData.posicionId}
                                        onChange={(e) => setFormData({ ...formData, posicionId: e.target.value })}
                                        disabled={!formData.departamentoId}
                                        className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent disabled:bg-navy-700 disabled:cursor-not-allowed"
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
                                        <p className="text-xs text-primary-400 mt-1">
                                            Rango salarial: {formatBalboas(getSelectedPositionSalaryRange().min)} - {formatBalboas(getSelectedPositionSalaryRange().max)}
                                        </p>
                                    )}
                                </div>
                            </div>

                            {/* Modal Footer */}
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
                                    className="px-4 py-2 bg-primary-600 hover:bg-primary-700 text-white rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                                >
                                    {editingId ? 'Actualizar Empleado' : 'Crear Empleado'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            {/* Delete Employee Modal (validación + confirmación) */}
            {showDeleteModal && empleadoToDelete && (
                <DeleteEmployeeModal
                    isOpen={showDeleteModal}
                    onClose={() => {
                        setShowDeleteModal(false);
                        setEmpleadoToDelete(null);
                    }}
                    empleadoId={empleadoToDelete.id}
                    empleadoNombre={`${empleadoToDelete.nombre} ${empleadoToDelete.apellido}`}
                    onSuccess={handleDeleteSuccess}
                />
            )}

            {/* Link User Modal */}
            {showLinkUserModal && empleadoToLink && (
                <LinkUserModal
                    empleadoId={empleadoToLink.id}
                    empleadoNombre={`${empleadoToLink.nombre} ${empleadoToLink.apellido}`}
                    isOpen={showLinkUserModal}
                    onClose={() => {
                        setShowLinkUserModal(false);
                        setEmpleadoToLink(null);
                    }}
                    onSuccess={handleLinkUserSuccess}
                />
            )}

            {/* Modal: Saldos Iniciales */}
            {showSaldoInicialModal && empleadoSaldoInicial && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
                    <div className="bg-navy-900 border border-navy-700 rounded-2xl shadow-2xl w-full max-w-lg">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
                            <div>
                                <h2 className="text-lg font-semibold text-gray-100">Saldos Iniciales</h2>
                                <p className="text-xs text-gray-400 mt-0.5">{empleadoSaldoInicial.nombre} {empleadoSaldoInicial.apellido} — saldos previos a la migración al sistema</p>
                            </div>
                            <button onClick={() => setShowSaldoInicialModal(false)} className="text-gray-400 hover:text-gray-200 transition-colors">✕</button>
                        </div>
                        <form onSubmit={handleSaveSaldoInicial} className="p-6 space-y-5">
                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-1">Fecha de Corte <span className="text-red-400">*</span></label>
                                <input type="date" value={saldoInicialForm.fechaCorte}
                                    onChange={e => setSaldoInicialForm(p => ({ ...p, fechaCorte: e.target.value }))}
                                    className="w-full px-3 py-2 bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                    required
                                />
                                <p className="text-xs text-gray-500 mt-1">Fecha desde la cual el sistema lleva el control</p>
                            </div>
                            <div className="border-t border-navy-700 pt-4">
                                <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Vacaciones</p>
                                <div className="grid grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-xs font-medium text-gray-400 mb-1">Días acumulados</label>
                                        <input type="number" min="0" step="0.5" value={saldoInicialForm.diasVacacionesAcumulados}
                                            onChange={e => setSaldoInicialForm(p => ({ ...p, diasVacacionesAcumulados: e.target.value }))}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-xs font-medium text-gray-400 mb-1">Días ya tomados</label>
                                        <input type="number" min="0" step="0.5" value={saldoInicialForm.diasVacacionesTomados}
                                            onChange={e => setSaldoInicialForm(p => ({ ...p, diasVacacionesTomados: e.target.value }))}
                                            className="w-full px-3 py-2 bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                        />
                                    </div>
                                </div>
                            </div>
                            <div className="border-t border-navy-700 pt-4">
                                <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Décimo Tercer Mes</p>
                                <div>
                                    <label className="block text-xs font-medium text-gray-400 mb-1">Monto acumulado (B/.)</label>
                                    <input type="number" min="0" step="0.01" value={saldoInicialForm.montoDecimoAcumulado}
                                        onChange={e => setSaldoInicialForm(p => ({ ...p, montoDecimoAcumulado: e.target.value }))}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm"
                                    />
                                    <p className="text-xs text-gray-500 mt-1">Monto generado antes de usar el sistema; se suma al primer cálculo de décimo</p>
                                </div>
                            </div>
                            <div>
                                <label className="block text-xs font-medium text-gray-400 mb-1">Notas</label>
                                <textarea rows={2} value={saldoInicialForm.notas}
                                    onChange={e => setSaldoInicialForm(p => ({ ...p, notas: e.target.value }))}
                                    className="w-full px-3 py-2 bg-navy-800 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm resize-none"
                                    placeholder="Contexto adicional sobre estos saldos..."
                                />
                            </div>
                            <div className="flex justify-end gap-3 pt-2 border-t border-navy-700">
                                <button type="button" onClick={() => setShowSaldoInicialModal(false)}
                                    className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors text-sm">
                                    Cancelar
                                </button>
                                <button type="submit" disabled={savingSaldoInicial}
                                    className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-white rounded-lg font-medium transition-colors text-sm disabled:opacity-50">
                                    {savingSaldoInicial ? 'Guardando...' : 'Guardar Saldos'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};

export default EmpleadosPage;
