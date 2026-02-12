import React, { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import { SkeletonCard, SkeletonTable } from '../components/Skeleton';
import EmptyState from '../components/EmptyState';
import { api } from '../services/api';

// Configuración de tipos de período de pago
const PAY_PERIOD_CONFIG = {
    0: { name: 'Semanal', periodsPerYear: 52 },
    1: { name: 'Bisemanal', periodsPerYear: 26 },
    2: { name: 'Quincenal', periodsPerYear: 24 },
    3: { name: 'Mensual', periodsPerYear: 12 },
};

const PlanillasPage = () => {
    const [planillas, setPlanillas] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [empleadosCount, setEmpleadosCount] = useState(0);
    const [loading, setLoading] = useState(true);
    const [processingAction, setProcessingAction] = useState(null);
    const [selectedPlanilla, setSelectedPlanilla] = useState(null);
    const [showNewModal, setShowNewModal] = useState(false);
    const [showDetailsModal, setShowDetailsModal] = useState(false);
    const [planillaDetails, setPlanillaDetails] = useState(null);
    const [formData, setFormData] = useState({
        payrollNumber: '',
        payPeriodType: 2,
        periodStartDate: '',
        periodEndDate: '',
        payDate: '',
        companyId: 1
    });

    // Estado del panel de horas trabajadas
    const [showHoursPanel, setShowHoursPanel] = useState(false);
    const [employeeHours, setEmployeeHours] = useState([]);
    const [hoursLoading, setHoursLoading] = useState(false);
    // Ref para rastrear timers de debounce por empleadoId
    const debounceTimers = useRef({});
    const [ensureTaxConfigLoading, setEnsureTaxConfigLoading] = useState(false);
    const [importNovedadesLoading, setImportNovedadesLoading] = useState(false);
    const [showImportConfirmModal, setShowImportConfirmModal] = useState(false);
    const [importConfirmData, setImportConfirmData] = useState(null);

    useEffect(() => {
        fetchData();
    }, []);

    // Al cambiar la planilla seleccionada, cerrar panel de horas
    useEffect(() => {
        setShowHoursPanel(false);
    }, [selectedPlanilla?.id]);

    // Enriquece una planilla con totales calculados desde details
    const enrichPlanillaWithTotals = (planilla) => {
        if (!planilla.details || planilla.details.length === 0) {
            return {
                ...planilla,
                totalEmployeeCss: 0,
                totalEmployerCss: 0,
                totalEmployeeSe: 0,
                totalEmployerSe: 0,
                totalIncomeTax: 0
            };
        }

        const totals = planilla.details.reduce((acc, detail) => ({
            totalEmployeeCss: acc.totalEmployeeCss + (detail.cssEmployee || 0),
            totalEmployerCss: acc.totalEmployerCss + (detail.cssEmployer || 0),
            totalEmployeeSe: acc.totalEmployeeSe + (detail.educationalInsuranceEmployee || 0),
            totalEmployerSe: acc.totalEmployerSe + (detail.educationalInsuranceEmployer || 0),
            totalIncomeTax: acc.totalIncomeTax + (detail.incomeTax || 0)
        }), {
            totalEmployeeCss: 0,
            totalEmployerCss: 0,
            totalEmployeeSe: 0,
            totalEmployerSe: 0,
            totalIncomeTax: 0
        });

        return { ...planilla, ...totals };
    };

    const fetchData = async () => {
        try {
            setLoading(true);

            // Fetch planillas
            const planillasData = await api.get('/api/payrollheaders');

            // Enriquecer planillas con totales calculados
            const enrichedPlanillas = planillasData.map(enrichPlanillaWithTotals);
            setPlanillas(enrichedPlanillas);

            // Seleccionar primera planilla por defecto
            if (enrichedPlanillas.length > 0 && !selectedPlanilla) {
                setSelectedPlanilla(enrichedPlanillas[0]);
            }

            // Fetch empleados activos
            const empleadosData = await api.get('/api/empleados');
            const activos = empleadosData.filter(e => e.estaActivo);
            setEmpleados(activos);
            setEmpleadosCount(activos.length);

        } catch (err) {
            toast.error(err.message || 'Error al cargar datos');
        } finally {
            setLoading(false);
        }
    };

    // Normaliza campos numéricos de horas (evita "09" / "07" cuando la API devuelve strings)
    const normalizeHoursRow = (row) => {
        const n = (v) => (v != null && v !== '' && !Number.isNaN(Number(v))) ? Number(v) : 0;
        return {
            ...row,
            regularHours: n(row.regularHours),
            sundayHours: n(row.sundayHours),
            holidayHours: n(row.holidayHours),
            overtimeDayHours: n(row.overtimeDayHours),
            overtimeNightHours: n(row.overtimeNightHours),
            overtimeHolidayHours: n(row.overtimeHolidayHours),
            overtimeMixedHours: n(row.overtimeMixedHours),
            overtimeExcessHours: n(row.overtimeExcessHours),
            absenceHours: n(row.absenceHours)
        };
    };

    // Carga las horas trabajadas de la planilla seleccionada
    const fetchHours = async (planillaId) => {
        try {
            setHoursLoading(true);
            const data = await api.get(`/api/payrollheaders/${planillaId}/hours`);
            setEmployeeHours(Array.isArray(data) ? data.map(normalizeHoursRow) : data);
        } catch (err) {
            toast.error(err.message || 'Error al cargar horas trabajadas');
        } finally {
            setHoursLoading(false);
        }
    };

    // Abre o cierra el panel de horas
    const toggleHoursPanel = async () => {
        if (!showHoursPanel) {
            setShowHoursPanel(true);
            await fetchHours(selectedPlanilla.id);
        } else {
            setShowHoursPanel(false);
        }
    };

    // Crear/verificar configuración de impuestos (CSS, SE, ISR) si falta
    const handleEnsureTaxConfig = async () => {
        try {
            setEnsureTaxConfigLoading(true);
            await api.post('/api/payrollheaders/ensure-tax-config');
            toast.success('Configuración de planilla creada o verificada. Vuelve a intentar Calcular Planilla.');
        } catch (err) {
            toast.error(err.message || 'Error al crear configuración');
        } finally {
            setEnsureTaxConfigLoading(false);
        }
    };

    // Auto-llena horas regulares con defaults del backend
    const handleGenerateDefaultHours = async () => {
        try {
            setHoursLoading(true);
            await api.post(`/api/payrollheaders/${selectedPlanilla.id}/hours/generate-defaults`);
            toast.success('Horas regulares generadas exitosamente');
            await fetchHours(selectedPlanilla.id);
        } catch (err) {
            toast.error(err.message || 'Error al generar horas por defecto');
        } finally {
            setHoursLoading(false);
        }
    };

    // Importa horas extra y ausencias desde módulos separados
    const handleImportNovedades = async (mode = 'overwrite') => {
        try {
            setImportNovedadesLoading(true);
            const response = await api.post(`/api/payrollheaders/${selectedPlanilla.id}/hours/import-novedades?mode=${mode}`);
            
            if (response.requiresConfirmation) {
                setImportConfirmData({
                    employeesCount: response.employeesWithExistingValues,
                    message: response.message
                });
                setShowImportConfirmModal(true);
                return;
            }

            const summary = response.summary;
            toast.success(
                `Importadas ${(summary.totalOvertimeHours || 0).toFixed(1)} horas extra y ${(summary.absenceHours || 0).toFixed(1)} horas de ausencias de ${summary.employeesProcessed || 0} empleado(s)`,
                { duration: 5000 }
            );
            await fetchHours(selectedPlanilla.id);
            setShowImportConfirmModal(false);
        } catch (err) {
            toast.error(err.message || 'Error al importar novedades');
        } finally {
            setImportNovedadesLoading(false);
        }
    };

    // Actualiza un campo de horas en el estado local con debounce para guardar
    const handleHoursChange = (empleadoId, field, value) => {
        const numericValue = parseFloat(value) || 0;

        setEmployeeHours(prev =>
            prev.map(row =>
                row.empleadoId === empleadoId
                    ? { ...row, [field]: numericValue }
                    : row
            )
        );

        // Debounce: esperar 800ms antes de guardar para no saturar la API
        if (debounceTimers.current[empleadoId]) {
            clearTimeout(debounceTimers.current[empleadoId]);
        }

        debounceTimers.current[empleadoId] = setTimeout(() => {
            saveEmployeeHours(empleadoId);
        }, 800);
    };

    // Guarda las horas de un empleado en la API
    const saveEmployeeHours = async (empleadoId) => {
        const row = employeeHours.find(r => r.empleadoId === empleadoId);
        if (!row) return;

        try {
            await api.put(`/api/payrollheaders/${selectedPlanilla.id}/hours/${empleadoId}`, {
                empleadoId: row.empleadoId,
                regularHours: row.regularHours || 0,
                sundayHours: row.sundayHours || 0,
                holidayHours: row.holidayHours || 0,
                overtimeDayHours: row.overtimeDayHours || 0,
                overtimeNightHours: row.overtimeNightHours || 0,
                overtimeHolidayHours: row.overtimeHolidayHours || 0,
                overtimeMixedHours: row.overtimeMixedHours || 0,
                overtimeExcessHours: row.overtimeExcessHours || 0,
                absenceHours: row.absenceHours || 0
            });
        } catch (err) {
            toast.error(`Error al guardar horas de empleado: ${err.message}`);
        }
    };

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2
        }).format(amount || 0);
    };

    const getStatusBadge = (status) => {
        const statuses = {
            0: { label: 'Draft', bg: 'bg-amber-500/15', text: 'text-amber-400', dot: 'bg-amber-600' },
            1: { label: 'Calculated', bg: 'bg-blue-500/15', text: 'text-blue-400', dot: 'bg-blue-600' },
            2: { label: 'Approved', bg: 'bg-green-500/15', text: 'text-green-400', dot: 'bg-green-600' },
            3: { label: 'Paid', bg: 'bg-emerald-500/15', text: 'text-emerald-400', dot: 'bg-emerald-600' },
            4: { label: 'Cancelled', bg: 'bg-red-500/15', text: 'text-red-400', dot: 'bg-red-600' }
        };
        const s = statuses[status] || statuses[0];
        return (
            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${s.bg} ${s.text}`}>
                <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${s.dot}`}></span>
                {s.label}
            </span>
        );
    };

    // Badge para tipo de período
    const getPayPeriodBadge = (payPeriodType) => {
        const config = PAY_PERIOD_CONFIG[payPeriodType];
        if (!config) return null;
        return (
            <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-navy-700 text-gray-400 border border-navy-600 ml-1.5">
                {config.name}
            </span>
        );
    };

    const generatePayrollNumber = () => {
        const year = new Date().getFullYear();
        const nextNumber = planillas.length + 1;
        return `${year}-${String(nextNumber).padStart(3, '0')}`;
    };

    const openNewModal = () => {
        setFormData({
            payrollNumber: generatePayrollNumber(),
            payPeriodType: 2,
            periodStartDate: '',
            periodEndDate: '',
            payDate: '',
            companyId: 1
        });
        setShowNewModal(true);
    };

    const handleCreatePlanilla = async (e) => {
        e.preventDefault();

        // Validación
        const startDate = new Date(formData.periodStartDate);
        const endDate = new Date(formData.periodEndDate);
        const payDate = new Date(formData.payDate);

        if (endDate <= startDate) {
            toast.error('La fecha fin debe ser posterior a la fecha inicio');
            return;
        }

        if (payDate < endDate) {
            toast.error('La fecha de pago debe ser igual o posterior a la fecha fin del período');
            return;
        }

        try {
            await api.post('/api/payrollheaders', formData);

            toast.success('Planilla creada exitosamente');
            setShowNewModal(false);
            await fetchData();
        } catch (err) {
            toast.error(err.message || 'Error al crear planilla');
        }
    };

    const handleCalculate = async () => {
        if (!selectedPlanilla) return;

        try {
            setProcessingAction('calculating');

            const result = await api.post(`/api/payrollheaders/${selectedPlanilla.id}/calculate`);
            const employeeCount = result.details?.length || empleadosCount;

            toast.success(`Planilla calculada: ${employeeCount} empleados procesados`);

            await fetchData();
        } catch (err) {
            toast.error(err.message || 'Error al calcular planilla');
        } finally {
            setProcessingAction(null);
        }
    };

    const handleApprove = async () => {
        if (!selectedPlanilla) return;

        try {
            setProcessingAction('approving');

            await api.post(`/api/payrollheaders/${selectedPlanilla.id}/approve`);

            toast.success('Planilla aprobada exitosamente');
            await fetchData();
        } catch (err) {
            toast.error(err.message || 'Error al aprobar planilla');
        } finally {
            setProcessingAction(null);
        }
    };

    const viewDetails = async (planilla) => {
        try {
            const data = await api.get(`/api/payrollheaders/${planilla.id}`);
            setPlanillaDetails(data);
            setShowDetailsModal(true);
        } catch (err) {
            toast.error(err.message || 'Error al cargar detalles');
        }
    };

    if (loading) {
        return (
            <div className="space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                    <SkeletonCard />
                    <SkeletonCard />
                    <SkeletonCard />
                    <SkeletonCard />
                </div>
                <SkeletonTable rows={5} columns={7} />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Header Actions */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <button
                    onClick={openNewModal}
                    className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Nueva Planilla
                </button>

                {planillas.length > 0 && (
                    <div className="relative w-full sm:w-64">
                        <select
                            value={selectedPlanilla?.id || ''}
                            onChange={(e) => {
                                const planilla = planillas.find(p => p.id === parseInt(e.target.value));
                                setSelectedPlanilla(planilla);
                            }}
                            className="w-full px-3 py-2.5 bg-navy-900 border border-navy-600 rounded-lg text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                        >
                            {planillas.map(p => (
                                <option key={p.id} value={p.id}>
                                    {p.payrollNumber} - {new Date(p.periodStartDate).toLocaleDateString('es-PA', { month: 'short', year: 'numeric' })}
                                </option>
                            ))}
                        </select>
                    </div>
                )}
            </div>

            {/* Summary Cards */}
            {selectedPlanilla ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <h3 className="text-sm font-medium text-gray-400 mb-2">Salarios Brutos</h3>
                        <p className="text-3xl font-bold text-gray-100 font-mono">{formatCurrency(selectedPlanilla.totalGrossPay)}</p>
                        <p className="text-sm text-primary-400 mt-2">{empleadosCount} empleados</p>
                    </div>

                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <h3 className="text-sm font-medium text-gray-400 mb-2">Aportes CSS</h3>
                        <p className="text-3xl font-bold text-gray-100 font-mono">{formatCurrency(selectedPlanilla.totalEmployeeCss + selectedPlanilla.totalEmployerCss)}</p>
                        <p className="text-sm text-amber-400 mt-2">Empleado + Patrono</p>
                    </div>

                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <h3 className="text-sm font-medium text-gray-400 mb-2">Seguro Educativo</h3>
                        <p className="text-3xl font-bold text-gray-100 font-mono">{formatCurrency(selectedPlanilla.totalEmployeeSe + selectedPlanilla.totalEmployerSe)}</p>
                        <p className="text-sm text-purple-400 mt-2">Empleado + Patrono</p>
                    </div>

                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <h3 className="text-sm font-medium text-gray-400 mb-2">ISR Retenido</h3>
                        <p className="text-3xl font-bold text-gray-100 font-mono">{formatCurrency(selectedPlanilla.totalIncomeTax)}</p>
                        <p className="text-sm text-red-400 mt-2">Según tabla DGI</p>
                    </div>
                </div>
            ) : (
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <EmptyState
                        icon={
                            <svg className="w-16 h-16 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                            </svg>
                        }
                        title="Sin Datos de Planilla"
                        description="Selecciona una planilla o crea una nueva para ver los detalles"
                    />
                </div>
            )}

            {/* Action Buttons Based on Status */}
            {selectedPlanilla && (
                <div className="flex flex-wrap gap-3">
                    {selectedPlanilla.status === 0 && (
                        <>
                            <button
                                onClick={handleCalculate}
                                disabled={processingAction === 'calculating'}
                                className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {processingAction === 'calculating' ? (
                                    <>
                                        <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                                        Calculando...
                                    </>
                                ) : (
                                    <>
                                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                                        </svg>
                                        Calcular Planilla
                                    </>
                                )}
                            </button>

                            {/* Botón Gestionar Horas - disponible en Draft y Calculated */}
                            <button
                                onClick={toggleHoursPanel}
                                className={`inline-flex items-center gap-2 px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 ${
                                    showHoursPanel
                                        ? 'bg-red-600 hover:bg-red-700 text-white'
                                        : 'bg-emerald-600 hover:bg-emerald-700 text-white'
                                }`}
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                {showHoursPanel ? 'Cerrar Panel de Horas' : 'Gestionar Horas'}
                            </button>
                            <button
                                type="button"
                                onClick={handleEnsureTaxConfig}
                                disabled={ensureTaxConfigLoading}
                                className="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-emerald-400 transition-colors disabled:opacity-50"
                                title="Si Calcular Planilla falla por falta de configuración CSS/SE/ISR, haz clic aquí"
                            >
                                {ensureTaxConfigLoading ? (
                                    <div className="w-4 h-4 border-2 border-gray-400 border-t-transparent rounded-full animate-spin" />
                                ) : (
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                    </svg>
                                )}
                                Crear config. impuestos
                            </button>
                        </>
                    )}

                    {selectedPlanilla.status === 1 && (
                        <>
                            <button
                                onClick={handleApprove}
                                disabled={processingAction === 'approving'}
                                className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {processingAction === 'approving' ? (
                                    <>
                                        <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                                        Aprobando...
                                    </>
                                ) : (
                                    <>
                                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                                        </svg>
                                        Aprobar Planilla
                                    </>
                                )}
                            </button>
                            <button
                                onClick={handleCalculate}
                                disabled={processingAction === 'calculating'}
                                className="inline-flex items-center gap-2 bg-yellow-600 hover:bg-yellow-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                                </svg>
                                Recalcular
                            </button>
                            {/* Botón Gestionar Horas - también disponible en Calculated */}
                            <button
                                onClick={toggleHoursPanel}
                                className={`inline-flex items-center gap-2 px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 ${
                                    showHoursPanel
                                        ? 'bg-red-600 hover:bg-red-700 text-white'
                                        : 'bg-emerald-600 hover:bg-emerald-700 text-white'
                                }`}
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                {showHoursPanel ? 'Cerrar Horas' : 'Editar Horas'}
                            </button>
                        </>
                    )}

                    {selectedPlanilla.status === 2 && (
                        <button
                            disabled
                            className="inline-flex items-center gap-2 bg-emerald-600 text-white px-4 py-2.5 rounded-lg font-medium shadow-lg shadow-black/20 opacity-50 cursor-not-allowed"
                            title="Próximamente"
                        >
                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                            Procesar Pago (Próximamente)
                        </button>
                    )}

                    {(selectedPlanilla.status === 3 || selectedPlanilla.status === 4) && (
                        <button
                            onClick={() => viewDetails(selectedPlanilla)}
                            className="inline-flex items-center gap-2 bg-gray-600 hover:bg-gray-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                        >
                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                            </svg>
                            Ver Detalles
                        </button>
                    )}
                </div>
            )}

            {/* Panel de Horas Trabajadas - visible en Draft y Calculated */}
            {selectedPlanilla && (selectedPlanilla.status === 0 || selectedPlanilla.status === 1) && showHoursPanel && (
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-emerald-700/50 overflow-hidden">
                    <div className="px-6 py-4 border-b border-navy-700 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100">
                                Horas Trabajadas
                                <span className="ml-2 text-sm font-normal text-gray-500">
                                    — {selectedPlanilla.payrollNumber}
                                </span>
                                {selectedPlanilla.status === 1 && (
                                    <span className="ml-2 inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                        Calculada
                                    </span>
                                )}
                            </h3>
                            <p className="text-xs text-gray-500 mt-0.5">
                                Los cambios se guardan automáticamente al dejar de editar un campo
                                {selectedPlanilla.status === 1 && ' • Recuerda recalcular la planilla después de editar'}
                            </p>
                        </div>
                        <div className="flex gap-2">
                            <button
                                onClick={handleGenerateDefaultHours}
                                disabled={hoursLoading}
                                className="inline-flex items-center gap-2 bg-emerald-600 hover:bg-emerald-700 text-white px-3 py-2 rounded-lg text-sm font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {hoursLoading ? (
                                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                                ) : (
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                                    </svg>
                                )}
                                Auto-llenar Regulares
                            </button>
                            <button
                                onClick={() => handleImportNovedades('overwrite')}
                                disabled={importNovedadesLoading || hoursLoading}
                                className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-3 py-2 rounded-lg text-sm font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {importNovedadesLoading ? (
                                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                                ) : (
                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                                    </svg>
                                )}
                                Importar Novedades
                            </button>
                        </div>
                    </div>

                    {hoursLoading && employeeHours.length === 0 ? (
                        <div className="flex items-center justify-center py-12">
                            <div className="w-8 h-8 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
                        </div>
                    ) : employeeHours.length === 0 ? (
                        <div className="text-center py-10 text-gray-500">
                            <svg className="w-12 h-12 mx-auto mb-3 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <p className="font-medium text-gray-400">No hay registros de horas</p>
                            <p className="text-sm mt-1">Usa "Auto-llenar Regulares" para generar los registros con horas estándar</p>
                        </div>
                    ) : (
                        <div className="overflow-x-auto p-4">
                            <table className="w-full text-sm">
                                <colgroup>
                                    <col className="w-[min(200px,22%)]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                    <col className="w-[70px]" />
                                </colgroup>
                                <thead className="bg-navy-950 border-b-2 border-navy-600">
                                    <tr>
                                        <th className="text-left py-4 px-4 text-xs font-semibold text-gray-400 uppercase tracking-wider">Empleado</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas regulares">Regulares</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas domingo">Domingo</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas feriado">Feriado</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas extra diurnas">Extra Diurna</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas extra nocturnas">Extra Nocturna</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas extra en festivos nacionales">Extra Festivos</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas extra mixtas">Extra Mixtas</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas extra con exceso">Extra Exceso</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider" title="Horas de ausencia">Ausencias</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-navy-700/50">
                                    {employeeHours.map((row) => {
                                        const emp = empleados.find(e => e.id === row.empleadoId);
                                        const nombreCompleto = emp
                                            ? `${emp.nombre} ${emp.apellido}`
                                            : `Empleado #${row.empleadoId}`;
                                        const num = (v) => (v != null && v !== '' && !Number.isNaN(Number(v))) ? Number(v) : 0;
                                        return (
                                            <tr key={row.empleadoId} className="hover:bg-navy-800/50 transition-colors">
                                                <td className="py-3 px-4 text-gray-100 font-medium whitespace-nowrap overflow-hidden text-ellipsis">
                                                    {nombreCompleto}
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.regularHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'regularHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-navy-600 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.sundayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'sundayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-navy-600 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.holidayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'holidayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-navy-600 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeDayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeDayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-navy-600 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeNightHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeNightHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-navy-600 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeHolidayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeHolidayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-purple-600/50 rounded-md text-purple-300 text-sm text-center focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-purple-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeMixedHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeMixedHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-blue-600/50 rounded-md text-blue-300 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeExcessHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeExcessHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-orange-600/50 rounded-md text-orange-300 text-sm text-center focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.absenceHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'absenceHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-red-900/50 rounded-md text-red-300 text-sm text-center focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-red-500 transition-colors"
                                                    />
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            )}

            {/* Modal de confirmación para importar novedades */}
            {showImportConfirmModal && importConfirmData && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
                    <div className="bg-navy-900 rounded-xl shadow-xl border border-navy-700 max-w-md w-full p-6">
                        <h3 className="text-lg font-semibold text-gray-100 mb-4">Confirmar importación</h3>
                        <p className="text-gray-300 mb-6">{importConfirmData.message}</p>
                        <div className="flex gap-3">
                            <button
                                onClick={() => {
                                    setShowImportConfirmModal(false);
                                    handleImportNovedades('overwrite');
                                }}
                                className="flex-1 bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                Sobrescribir
                            </button>
                            <button
                                onClick={() => {
                                    setShowImportConfirmModal(false);
                                    handleImportNovedades('sum');
                                }}
                                className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                Sumar
                            </button>
                            <button
                                onClick={() => {
                                    setShowImportConfirmModal(false);
                                    setImportConfirmData(null);
                                }}
                                className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                Cancelar
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* History Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Historial de Planillas
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({planillas.length} {planillas.length === 1 ? 'planilla' : 'planillas'})
                        </span>
                    </h3>
                </div>

                {planillas.length === 0 ? (
                    <EmptyState
                        icon={
                            <svg className="w-16 h-16 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                            </svg>
                        }
                        title="No hay planillas creadas"
                        description="Crea una nueva planilla para comenzar el proceso de nómina"
                        action={
                            <button
                                onClick={openNewModal}
                                className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                </svg>
                                Nueva Planilla
                            </button>
                        }
                    />
                ) : (
                    <div className="overflow-x-auto">
                        <table className="w-full">
                            <thead className="bg-navy-950 border-b border-navy-700">
                                <tr>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">#Planilla</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Período</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Empleados</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Bruto</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Deducciones</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Neto</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Estado</th>
                                    <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase tracking-wider">Acciones</th>
                                </tr>
                            </thead>
                            <tbody className="bg-navy-900 divide-y divide-navy-700">
                                {planillas.map((planilla) => (
                                    <tr key={planilla.id} className="hover:bg-navy-800 transition-colors">
                                        <td className="py-4 px-6 text-sm font-medium text-gray-100">
                                            <div className="flex items-center flex-wrap gap-1">
                                                <span>{planilla.payrollNumber}</span>
                                                {planilla.payPeriodType !== undefined && planilla.payPeriodType !== null &&
                                                    getPayPeriodBadge(planilla.payPeriodType)
                                                }
                                            </div>
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-500">
                                            {new Date(planilla.periodStartDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short' })}
                                            {' - '}
                                            {new Date(planilla.periodEndDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-100">{planilla.employeeCount || empleadosCount}</td>
                                        <td className="py-4 px-6 text-sm font-medium text-gray-100 font-mono">{formatCurrency(planilla.totalGrossPay)}</td>
                                        <td className="py-4 px-6 text-sm text-gray-100 font-mono">{formatCurrency(planilla.totalDeductions)}</td>
                                        <td className="py-4 px-6 text-sm font-medium text-gray-100 font-mono">{formatCurrency(planilla.totalNetPay)}</td>
                                        <td className="py-4 px-6">{getStatusBadge(planilla.status)}</td>
                                        <td className="py-4 px-6">
                                            <button
                                                onClick={() => viewDetails(planilla)}
                                                className="inline-flex items-center gap-1 text-primary-400 hover:text-primary-300 font-medium text-sm"
                                            >
                                                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                                </svg>
                                                Ver
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>

            {/* Modal: Nueva Planilla */}
            {showNewModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">Nueva Planilla</h3>
                            <button
                                onClick={() => setShowNewModal(false)}
                                className="text-gray-400 hover:text-gray-300 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleCreatePlanilla} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                {/* Número de Planilla */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Número de Planilla <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.payrollNumber}
                                        onChange={(e) => setFormData({ ...formData, payrollNumber: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-navy-800 text-gray-100"
                                        placeholder="2025-001"
                                    />
                                </div>

                                {/* Tipo de Período */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo de Período <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        value={formData.payPeriodType}
                                        onChange={(e) => setFormData({ ...formData, payPeriodType: parseInt(e.target.value) })}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-navy-800 text-gray-100"
                                    >
                                        {Object.entries(PAY_PERIOD_CONFIG).map(([key, config]) => (
                                            <option key={key} value={key}>
                                                {config.name} — {config.periodsPerYear} períodos / año
                                            </option>
                                        ))}
                                    </select>
                                    <p className="mt-1 text-xs text-gray-500">
                                        Define la frecuencia de pago para esta planilla
                                    </p>
                                </div>

                                {/* Fecha Inicio */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha Inicio Período <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.periodStartDate}
                                        onChange={(e) => setFormData({ ...formData, periodStartDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-navy-800 text-gray-100"
                                    />
                                </div>

                                {/* Fecha Fin */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha Fin Período <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.periodEndDate}
                                        onChange={(e) => setFormData({ ...formData, periodEndDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-navy-800 text-gray-100"
                                    />
                                </div>

                                {/* Fecha de Pago */}
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha de Pago <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.payDate}
                                        onChange={(e) => setFormData({ ...formData, payDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-navy-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-navy-800 text-gray-100"
                                    />
                                </div>
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={() => setShowNewModal(false)}
                                    className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                                >
                                    Crear Planilla
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Modal: Detalles de Planilla */}
            {showDetailsModal && planillaDetails && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-6xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">
                                Detalles de Planilla — {planillaDetails.payrollNumber}
                            </h3>
                            <button
                                onClick={() => setShowDetailsModal(false)}
                                className="text-gray-400 hover:text-gray-300 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <div className="p-6">
                            {/* Header Info */}
                            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
                                <div>
                                    <p className="text-sm text-gray-400">Período</p>
                                    <p className="font-medium text-gray-100">
                                        {new Date(planillaDetails.periodStartDate).toLocaleDateString('es-PA')}
                                        {' - '}
                                        {new Date(planillaDetails.periodEndDate).toLocaleDateString('es-PA')}
                                    </p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Fecha de Pago</p>
                                    <p className="font-medium text-gray-100">
                                        {new Date(planillaDetails.payDate).toLocaleDateString('es-PA')}
                                    </p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Estado</p>
                                    <div className="mt-1">{getStatusBadge(planillaDetails.status)}</div>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-400">Total Neto</p>
                                    <p className="font-bold text-lg text-gray-100 font-mono">{formatCurrency(planillaDetails.totalNetPay)}</p>
                                </div>
                            </div>

                            {/* Details Table */}
                            {planillaDetails.details && planillaDetails.details.length > 0 ? (
                                <div className="overflow-x-auto">
                                    <table className="w-full">
                                        <thead className="bg-navy-950 border-b border-navy-700">
                                            <tr>
                                                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">Bruto</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">CSS</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">SE</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">ISR</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">Total Ded.</th>
                                                <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase">Neto</th>
                                            </tr>
                                        </thead>
                                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                                            {planillaDetails.details.map((detail) => (
                                                <tr key={detail.id} className="hover:bg-navy-800">
                                                    <td className="py-3 px-4 text-sm text-gray-100">{detail.empleado?.nombre} {detail.empleado?.apellido}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.grossPay)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.cssEmployee)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.educationalInsuranceEmployee)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.incomeTax)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.totalDeductions)}</td>
                                                    <td className="py-3 px-4 text-sm text-right font-medium text-gray-100 font-mono">{formatCurrency(detail.netPay)}</td>
                                                </tr>
                                            ))}
                                        </tbody>
                                        <tfoot className="bg-navy-950 border-t-2 border-navy-600">
                                            <tr>
                                                <td className="py-3 px-4 text-sm font-bold text-gray-100">TOTALES</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalGrossPay)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.cssEmployee || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.educationalInsuranceEmployee || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.incomeTax || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalDeductions)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalNetPay)}</td>
                                            </tr>
                                        </tfoot>
                                    </table>
                                </div>
                            ) : (
                                <div className="text-center py-8 text-gray-500">
                                    No hay detalles de empleados para esta planilla
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default PlanillasPage;
