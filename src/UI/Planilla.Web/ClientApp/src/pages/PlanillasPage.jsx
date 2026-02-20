import React, { useState, useEffect, useRef } from 'react';
import toast from 'react-hot-toast';
import {
    CheckCircle2,
    Circle,
    Calculator,
    CheckCheck,
    CreditCard,
    Banknote,
    Clock,
    Settings,
    Eye,
    RotateCcw,
    Plus,
    Zap,
    Upload,
    Save,
    AlertTriangle,
    X,
} from 'lucide-react';
import { SkeletonCard, SkeletonTable } from '../components/Skeleton';
import EmptyState from '../components/EmptyState';
import { Modal } from '../components/ui/Modal';
import { Button } from '../components/ui/Button';
import { api } from '../services/api';

// Configuración de tipos de período de pago
const PAY_PERIOD_CONFIG = {
    0: { name: 'Semanal', periodsPerYear: 52 },
    1: { name: 'Bisemanal', periodsPerYear: 26 },
    2: { name: 'Quincenal', periodsPerYear: 24 },
    3: { name: 'Mensual', periodsPerYear: 12 },
};

// Pasos del workflow de la planilla
const WORKFLOW_STEPS = [
    { status: 0, label: 'Borrador', icon: Circle },
    { status: 1, label: 'Calculado', icon: Calculator },
    { status: 2, label: 'Aprobado', icon: CheckCheck },
    { status: 3, label: 'Pagado', icon: Banknote },
];

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
    const [showApproveConfirmModal, setShowApproveConfirmModal] = useState(false);
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
    // Ref para rastrear si estamos abriendo el panel desde la tabla
    const openingFromTable = useRef(false);
    const [ensureTaxConfigLoading, setEnsureTaxConfigLoading] = useState(false);
    const [importNovedadesLoading, setImportNovedadesLoading] = useState(false);
    const [showImportConfirmModal, setShowImportConfirmModal] = useState(false);
    const [importConfirmData, setImportConfirmData] = useState(null);

    useEffect(() => {
        fetchData();
    }, []);

    // Al cambiar la planilla seleccionada, cerrar panel de horas solo si no se está abriendo desde la tabla
    useEffect(() => {
        // Si estamos abriendo desde la tabla, no cerrar el panel
        if (openingFromTable.current) {
            openingFromTable.current = false;
            return;
        }
        // Si cambiamos de planilla y el panel está abierto, cerrarlo
        if (showHoursPanel) {
            setShowHoursPanel(false);
        }
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

    // Guarda todas las horas pendientes y cierra el panel
    const handleSaveAndClose = async () => {
        try {
            // Limpiar todos los timers de debounce pendientes y guardar inmediatamente
            const empleadosConCambiosPendientes = new Set();

            Object.keys(debounceTimers.current).forEach(empleadoId => {
                if (debounceTimers.current[empleadoId]) {
                    clearTimeout(debounceTimers.current[empleadoId]);
                    delete debounceTimers.current[empleadoId];
                    empleadosConCambiosPendientes.add(parseInt(empleadoId));
                }
            });

            // Guardar solo las horas de empleados con cambios pendientes
            const savePromises = Array.from(empleadosConCambiosPendientes).map(empleadoId =>
                saveEmployeeHours(empleadoId)
            );

            // Si hay cambios pendientes, esperar a que se completen
            if (savePromises.length > 0) {
                await Promise.all(savePromises);
                toast.success('Horas guardadas correctamente');
            }

            // Cerrar el panel
            setShowHoursPanel(false);
        } catch (err) {
            toast.error(err.message || 'Error al guardar horas');
        }
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
        return 'B/. ' + new Intl.NumberFormat('es-PA', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount || 0);
    };

    const getStatusBadge = (status) => {
        const statuses = {
            0: { label: 'Borrador', bg: 'bg-amber-500/15', text: 'text-amber-400', dot: 'bg-amber-600' },
            1: { label: 'Calculado', bg: 'bg-blue-500/15', text: 'text-blue-400', dot: 'bg-blue-600' },
            2: { label: 'Aprobado', bg: 'bg-green-500/15', text: 'text-green-400', dot: 'bg-green-600' },
            3: { label: 'Pagado', bg: 'bg-emerald-500/15', text: 'text-emerald-400', dot: 'bg-emerald-600' },
            4: { label: 'Cancelado', bg: 'bg-red-500/15', text: 'text-red-400', dot: 'bg-red-600' }
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

    // handleApprove ahora abre el modal de confirmación
    const handleApprove = () => {
        setShowApproveConfirmModal(true);
    };

    // confirmedApprove ejecuta la llamada API real
    const confirmedApprove = async () => {
        if (!selectedPlanilla) return;

        try {
            setProcessingAction('approving');
            setShowApproveConfirmModal(false);

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

    // Formatea la etiqueta del selector de planilla
    const formatPlanillaOption = (planilla) => {
        const periodoLabel = PAY_PERIOD_CONFIG[planilla.payPeriodType]?.name || '';
        const fechaLabel = new Date(planilla.periodStartDate).toLocaleDateString('es-PA', { month: 'short', year: 'numeric' });
        return `${planilla.payrollNumber} — ${fechaLabel}${periodoLabel ? ` (${periodoLabel})` : ''}`;
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

    // Determina si la planilla tiene datos calculados
    const hasCalculatedData = selectedPlanilla && selectedPlanilla.status >= 1;

    return (
        <div className="space-y-6">

            {/* ==================== HEADER ==================== */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-gray-100">Planillas de Nómina</h1>
                    <p className="text-sm text-gray-500 mt-0.5">Gestiona y aprueba tus planillas de pago</p>
                </div>
                <Button
                    icon={Plus}
                    variant="success"
                    size="md"
                    onClick={openNewModal}
                >
                    Nueva Planilla
                </Button>
            </div>

            {/* Selector mejorado de planilla */}
            {planillas.length > 0 && (
                <div className="flex flex-col sm:flex-row sm:items-center gap-2">
                    <label className="text-sm font-medium text-gray-400 whitespace-nowrap">
                        Seleccionar planilla
                    </label>
                    <div className="relative w-full sm:w-80">
                        <select
                            value={selectedPlanilla?.id || ''}
                            onChange={(e) => {
                                const planilla = planillas.find(p => p.id === parseInt(e.target.value));
                                setSelectedPlanilla(planilla);
                            }}
                            className="w-full pl-3 pr-8 py-2.5 bg-navy-900 border border-navy-600 rounded-lg text-gray-100 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent appearance-none cursor-pointer"
                        >
                            {planillas.map(p => (
                                <option key={p.id} value={p.id}>
                                    {formatPlanillaOption(p)}
                                </option>
                            ))}
                        </select>
                        <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2.5">
                            <svg className="w-4 h-4 text-gray-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                            </svg>
                        </div>
                    </div>
                </div>
            )}

            {/* ==================== WORKFLOW STEPPER ==================== */}
            {selectedPlanilla && (
                <div className="bg-navy-900 border border-navy-700 rounded-xl p-5">
                    {/* Nombre y fechas */}
                    <div className="mb-5">
                        <div className="flex items-center gap-2 flex-wrap">
                            <h2 className="text-lg font-bold text-gray-100 font-display">
                                Planilla {selectedPlanilla.payrollNumber}
                            </h2>
                            {selectedPlanilla.payPeriodType !== undefined && selectedPlanilla.payPeriodType !== null &&
                                getPayPeriodBadge(selectedPlanilla.payPeriodType)
                            }
                            {selectedPlanilla.status === 4 && (
                                <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-red-500/15 text-red-400 border border-red-500/20 ml-1">
                                    Cancelada
                                </span>
                            )}
                        </div>
                        <p className="text-sm text-gray-400 mt-0.5">
                            Período: {new Date(selectedPlanilla.periodStartDate).toLocaleDateString('es-PA')} — {new Date(selectedPlanilla.periodEndDate).toLocaleDateString('es-PA')}
                            {selectedPlanilla.payDate && (
                                <span className="ml-3 text-gray-500">
                                    Fecha de pago: {new Date(selectedPlanilla.payDate).toLocaleDateString('es-PA')}
                                </span>
                            )}
                        </p>
                    </div>

                    {/* Stepper horizontal */}
                    <div className="flex items-center">
                        {WORKFLOW_STEPS.map((step, index) => {
                            const currentStatus = selectedPlanilla.status === 4 ? -1 : selectedPlanilla.status;
                            const isPast = currentStatus > step.status;
                            const isActive = currentStatus === step.status;
                            const isFuture = currentStatus < step.status;

                            return (
                                <React.Fragment key={step.status}>
                                    {/* Paso */}
                                    <div className="flex flex-col items-center gap-1.5 flex-shrink-0">
                                        <div className={`
                                            w-9 h-9 rounded-full flex items-center justify-center transition-all
                                            ${isPast ? 'bg-emerald-500/20 border-2 border-emerald-500 text-emerald-400' : ''}
                                            ${isActive ? 'bg-primary-600 border-2 border-primary-400 text-white ring-4 ring-primary-500/20' : ''}
                                            ${isFuture ? 'bg-navy-800 border-2 border-navy-600 text-gray-600' : ''}
                                        `}>
                                            {isPast ? (
                                                <CheckCircle2 className="w-5 h-5" />
                                            ) : (
                                                <step.icon className="w-4 h-4" />
                                            )}
                                        </div>
                                        <span className={`text-xs font-medium ${
                                            isPast ? 'text-emerald-400' :
                                            isActive ? 'text-primary-400' :
                                            'text-gray-600'
                                        }`}>
                                            {step.label}
                                        </span>
                                    </div>

                                    {/* Línea conectora */}
                                    {index < WORKFLOW_STEPS.length - 1 && (
                                        <div className={`flex-1 h-0.5 mx-2 rounded-full ${
                                            currentStatus > step.status ? 'bg-emerald-500' : 'bg-navy-700'
                                        }`} />
                                    )}
                                </React.Fragment>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* ==================== SUMMARY CARDS ==================== */}
            {selectedPlanilla ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
                    {/* Salario Bruto */}
                    <div className="bg-navy-900 rounded-xl border border-navy-700 p-5">
                        <p className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">Salario Bruto</p>
                        {hasCalculatedData ? (
                            <>
                                <p className="text-2xl font-bold text-gray-100 font-mono">{formatCurrency(selectedPlanilla.totalGrossPay)}</p>
                                <p className="text-xs text-gray-500 mt-1.5">
                                    {empleadosCount} empleados
                                    {empleadosCount > 0 && selectedPlanilla.totalGrossPay > 0 && (
                                        <span className="ml-1">
                                            • Promedio B/. {(selectedPlanilla.totalGrossPay / empleadosCount).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                        </span>
                                    )}
                                </p>
                            </>
                        ) : (
                            <>
                                <p className="text-2xl font-bold text-gray-600 font-mono" title="Calcula la planilla para ver los totales">—</p>
                                <p className="text-xs text-gray-600 mt-1.5">{empleadosCount} empleados</p>
                            </>
                        )}
                    </div>

                    {/* Neto a Pagar */}
                    <div className="bg-navy-900 rounded-xl border border-navy-700 p-5">
                        <p className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">Neto a Pagar</p>
                        {hasCalculatedData ? (
                            <>
                                <p className="text-2xl font-bold text-emerald-400 font-mono">{formatCurrency(selectedPlanilla.totalNetPay)}</p>
                                {selectedPlanilla.totalGrossPay > 0 && (
                                    <p className="text-xs text-gray-500 mt-1.5">
                                        Ahorro deducc: B/. {(selectedPlanilla.totalGrossPay - selectedPlanilla.totalNetPay).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                    </p>
                                )}
                            </>
                        ) : (
                            <>
                                <p className="text-2xl font-bold text-gray-600 font-mono" title="Calcula la planilla para ver los totales">—</p>
                                <p className="text-xs text-gray-600 mt-1.5">Pendiente de cálculo</p>
                            </>
                        )}
                    </div>

                    {/* Aportes CSS */}
                    <div className="bg-navy-900 rounded-xl border border-navy-700 p-5">
                        <p className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">Aportes CSS</p>
                        {hasCalculatedData ? (
                            <>
                                <p className="text-2xl font-bold text-amber-400 font-mono">
                                    {formatCurrency((selectedPlanilla.totalEmployeeCss || 0) + (selectedPlanilla.totalEmployerCss || 0))}
                                </p>
                                <div className="mt-1.5 space-y-0.5">
                                    <div className="flex justify-between text-xs text-gray-500">
                                        <span>Empleado:</span>
                                        <span className="font-mono">{formatCurrency(selectedPlanilla.totalEmployeeCss)}</span>
                                    </div>
                                    <div className="flex justify-between text-xs text-gray-500">
                                        <span>Patrono:</span>
                                        <span className="font-mono">{formatCurrency(selectedPlanilla.totalEmployerCss)}</span>
                                    </div>
                                </div>
                            </>
                        ) : (
                            <>
                                <p className="text-2xl font-bold text-gray-600 font-mono" title="Calcula la planilla para ver los totales">—</p>
                                <p className="text-xs text-gray-600 mt-1.5">Empleado + Patrono</p>
                            </>
                        )}
                    </div>

                    {/* Seguro Educativo */}
                    <div className="bg-navy-900 rounded-xl border border-navy-700 p-5">
                        <p className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">Seguro Educativo</p>
                        {hasCalculatedData ? (
                            <>
                                <p className="text-2xl font-bold text-purple-400 font-mono">
                                    {formatCurrency((selectedPlanilla.totalEmployeeSe || 0) + (selectedPlanilla.totalEmployerSe || 0))}
                                </p>
                                <div className="mt-1.5 space-y-0.5">
                                    <div className="flex justify-between text-xs text-gray-500">
                                        <span>Empleado:</span>
                                        <span className="font-mono">{formatCurrency(selectedPlanilla.totalEmployeeSe)}</span>
                                    </div>
                                    <div className="flex justify-between text-xs text-gray-500">
                                        <span>Patrono:</span>
                                        <span className="font-mono">{formatCurrency(selectedPlanilla.totalEmployerSe)}</span>
                                    </div>
                                </div>
                            </>
                        ) : (
                            <>
                                <p className="text-2xl font-bold text-gray-600 font-mono" title="Calcula la planilla para ver los totales">—</p>
                                <p className="text-xs text-gray-600 mt-1.5">Empleado + Patrono</p>
                            </>
                        )}
                    </div>

                    {/* ISR Retenido */}
                    <div className="bg-navy-900 rounded-xl border border-navy-700 p-5">
                        <p className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">ISR Retenido</p>
                        {hasCalculatedData ? (
                            <>
                                <p className="text-2xl font-bold text-red-400 font-mono">{formatCurrency(selectedPlanilla.totalIncomeTax)}</p>
                                <p className="text-xs text-gray-500 mt-1.5">Según tabla DGI</p>
                            </>
                        ) : (
                            <>
                                <p className="text-2xl font-bold text-gray-600 font-mono" title="Calcula la planilla para ver los totales">—</p>
                                <p className="text-xs text-gray-600 mt-1.5">Según tabla DGI</p>
                            </>
                        )}
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

            {/* ==================== PANEL DE ACCIONES ==================== */}
            {selectedPlanilla && (
                <div className="bg-navy-900 border border-navy-700 rounded-xl overflow-hidden">
                    {/* Header del panel sin badge de estado (ya está en el stepper) */}
                    <div className="px-5 py-3 bg-navy-950/60 border-b border-navy-700 flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full bg-primary-400 animate-pulse" />
                        <span className="text-sm font-semibold text-gray-300">
                            Acciones — Planilla {selectedPlanilla.payrollNumber}
                        </span>
                    </div>

                    {/* Botones de acción */}
                    <div className="p-5">

                        {/* ESTADO: Borrador */}
                        {selectedPlanilla.status === 0 && (
                            <div className="flex flex-wrap gap-3 items-center">
                                {/* Acción Principal */}
                                <button
                                    onClick={handleCalculate}
                                    disabled={processingAction === 'calculating'}
                                    className="group inline-flex items-center gap-3 bg-primary-600 hover:bg-primary-700 text-white px-6 py-3.5 rounded-xl font-semibold text-[15px] transition-all shadow-lg shadow-primary-900/40 disabled:opacity-50 disabled:cursor-not-allowed hover:-translate-y-0.5 hover:shadow-xl"
                                >
                                    {processingAction === 'calculating' ? (
                                        <>
                                            <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                                            Calculando...
                                        </>
                                    ) : (
                                        <>
                                            <Calculator className="w-5 h-5" />
                                            Calcular Planilla
                                        </>
                                    )}
                                </button>

                                {/* Separador visual */}
                                <div className="w-px h-8 bg-navy-700 hidden sm:block" />

                                {/* Herramientas secundarias */}
                                <button
                                    onClick={toggleHoursPanel}
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-emerald-500/50 text-gray-200 hover:text-emerald-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5"
                                >
                                    <Clock className="w-4 h-4" />
                                    {showHoursPanel ? 'Cerrar Horas' : 'Editar Horas'}
                                </button>

                                <button
                                    type="button"
                                    onClick={handleEnsureTaxConfig}
                                    disabled={ensureTaxConfigLoading}
                                    title="Si Calcular Planilla falla por falta de configuración CSS/SE/ISR, haz clic aquí"
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-amber-500/40 text-gray-300 hover:text-amber-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5 disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                    {ensureTaxConfigLoading ? (
                                        <div className="w-4 h-4 border-2 border-amber-400 border-t-transparent rounded-full animate-spin" />
                                    ) : (
                                        <Settings className="w-4 h-4" />
                                    )}
                                    Config. Impuestos
                                </button>

                                <button
                                    onClick={() => viewDetails(selectedPlanilla)}
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-primary-500/40 text-gray-400 hover:text-primary-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5"
                                >
                                    <Eye className="w-4 h-4" />
                                    Ver Detalles
                                </button>
                            </div>
                        )}

                        {/* ESTADO: Calculado */}
                        {selectedPlanilla.status === 1 && (
                            <div className="flex flex-wrap gap-3 items-center">
                                {/* Acción Principal */}
                                <button
                                    onClick={handleApprove}
                                    disabled={processingAction === 'approving'}
                                    className="inline-flex items-center gap-3 bg-green-600 hover:bg-green-700 text-white px-6 py-3.5 rounded-xl font-semibold text-[15px] transition-all shadow-lg shadow-green-900/40 disabled:opacity-50 disabled:cursor-not-allowed hover:-translate-y-0.5 hover:shadow-xl"
                                >
                                    {processingAction === 'approving' ? (
                                        <>
                                            <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                                            Aprobando...
                                        </>
                                    ) : (
                                        <>
                                            <CheckCheck className="w-5 h-5" />
                                            Aprobar Planilla
                                        </>
                                    )}
                                </button>

                                {/* Separador visual */}
                                <div className="w-px h-8 bg-navy-700 hidden sm:block" />

                                {/* Herramientas secundarias */}
                                <button
                                    onClick={handleCalculate}
                                    disabled={processingAction === 'calculating'}
                                    className="inline-flex items-center gap-2 bg-amber-600/20 hover:bg-amber-600/30 border border-amber-600/30 hover:border-amber-500/50 text-amber-400 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5 disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                    {processingAction === 'calculating' ? (
                                        <div className="w-4 h-4 border-2 border-amber-400 border-t-transparent rounded-full animate-spin" />
                                    ) : (
                                        <RotateCcw className="w-4 h-4" />
                                    )}
                                    Recalcular
                                </button>

                                <button
                                    onClick={toggleHoursPanel}
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-emerald-500/50 text-gray-200 hover:text-emerald-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5"
                                >
                                    <Clock className="w-4 h-4" />
                                    {showHoursPanel ? 'Cerrar Horas' : 'Editar Horas'}
                                </button>

                                <button
                                    onClick={() => viewDetails(selectedPlanilla)}
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-primary-500/40 text-gray-400 hover:text-primary-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5"
                                >
                                    <Eye className="w-4 h-4" />
                                    Ver Detalles
                                </button>
                            </div>
                        )}

                        {/* ESTADO: Aprobado */}
                        {selectedPlanilla.status === 2 && (
                            <div className="flex flex-wrap gap-3 items-center">
                                <button
                                    disabled
                                    className="inline-flex items-center gap-3 bg-emerald-600/20 text-emerald-400/50 px-6 py-3.5 rounded-xl font-semibold text-[15px] cursor-not-allowed border border-emerald-700/20"
                                >
                                    <CreditCard className="w-5 h-5" />
                                    Procesar Pago
                                    <span className="text-xs bg-emerald-500/10 px-2 py-0.5 rounded-full border border-emerald-500/20">Próximamente</span>
                                </button>
                                <button
                                    onClick={() => viewDetails(selectedPlanilla)}
                                    className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-primary-500/40 text-gray-200 hover:text-primary-300 px-4 py-2.5 rounded-xl font-medium text-sm transition-all hover:-translate-y-0.5"
                                >
                                    <Eye className="w-4 h-4" />
                                    Ver Detalles
                                </button>
                            </div>
                        )}

                        {/* ESTADO: Pagado o Cancelado */}
                        {(selectedPlanilla.status === 3 || selectedPlanilla.status === 4) && (
                            <button
                                onClick={() => viewDetails(selectedPlanilla)}
                                className="inline-flex items-center gap-2 bg-navy-800 hover:bg-navy-700 border border-navy-600 hover:border-primary-500/40 text-gray-200 hover:text-primary-300 px-6 py-3.5 rounded-xl font-semibold text-[15px] transition-all hover:-translate-y-0.5"
                            >
                                <Eye className="w-5 h-5" />
                                Ver Detalles Completos
                            </button>
                        )}
                    </div>

                    {/* Nota de ayuda contextual */}
                    {selectedPlanilla.status === 0 && (
                        <div className="px-5 pb-4">
                            <p className="text-xs text-gray-600 flex items-center gap-1.5">
                                <svg className="w-3.5 h-3.5 text-gray-600 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                Si "Calcular" falla por configuración faltante, usa "Config. Impuestos" primero e intenta de nuevo.
                            </p>
                        </div>
                    )}
                </div>
            )}

            {/* ==================== PANEL DE HORAS TRABAJADAS ==================== */}
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
                                    <Zap className="w-4 h-4" />
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
                                    <Upload className="w-4 h-4" />
                                )}
                                Importar Novedades
                            </button>
                            <button
                                onClick={handleSaveAndClose}
                                disabled={hoursLoading || importNovedadesLoading}
                                className="inline-flex items-center gap-2 bg-gray-600 hover:bg-gray-700 text-white px-3 py-2 rounded-lg text-sm font-medium transition-colors shadow-lg shadow-black/20 disabled:opacity-50 disabled:cursor-not-allowed"
                                title="Guardar cambios y cerrar el panel"
                            >
                                <Save className="w-4 h-4" />
                                Guardar y Cerrar
                            </button>
                        </div>
                    </div>

                    {hoursLoading && employeeHours.length === 0 ? (
                        <div className="flex items-center justify-center py-12">
                            <div className="w-8 h-8 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
                        </div>
                    ) : employeeHours.length === 0 ? (
                        <div className="text-center py-10 text-gray-500">
                            <Clock className="w-12 h-12 mx-auto mb-3 text-gray-600" />
                            <p className="font-medium text-gray-400">No hay registros de horas</p>
                            <p className="text-sm mt-1">Usa "Auto-llenar Regulares" para generar los registros con horas estándar</p>
                        </div>
                    ) : (
                        <div className="overflow-x-auto p-4">
                            {/* Leyenda de colores */}
                            <div className="flex flex-wrap items-center gap-4 mb-4 px-1 py-2 bg-navy-950/40 rounded-lg border border-navy-700">
                                <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Leyenda:</span>
                                <div className="flex items-center gap-1.5">
                                    <span className="w-3 h-3 rounded-sm bg-emerald-500/30 border border-emerald-400" />
                                    <span className="text-xs text-gray-400">Horas base</span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                    <span className="w-3 h-3 rounded-sm bg-orange-500/30 border border-orange-400" />
                                    <span className="text-xs text-gray-400">H. Extra</span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                    <span className="w-3 h-3 rounded-sm bg-purple-500/30 border border-purple-400" />
                                    <span className="text-xs text-gray-400">H. Extra especiales</span>
                                </div>
                                <div className="flex items-center gap-1.5">
                                    <span className="w-3 h-3 rounded-sm bg-red-500/30 border border-red-400" />
                                    <span className="text-xs text-gray-400">Ausencias</span>
                                </div>
                            </div>

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
                                <thead className="bg-navy-950 border-b-2 border-navy-600 sticky top-0 z-10">
                                    <tr>
                                        <th className="text-left py-4 px-4 text-xs font-semibold text-gray-400 uppercase tracking-wider">Empleado</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-emerald-500 uppercase tracking-wider" title="Horas regulares">Regulares</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-emerald-500 uppercase tracking-wider" title="Horas domingo">Domingo</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-emerald-500 uppercase tracking-wider" title="Horas feriado">Feriado</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-orange-400 uppercase tracking-wider" title="Horas extra diurnas">Extra Diurna</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-orange-400 uppercase tracking-wider" title="Horas extra nocturnas">Extra Nocturna</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-purple-400 uppercase tracking-wider" title="Horas extra en festivos nacionales">Extra Festivos</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-purple-400 uppercase tracking-wider" title="Horas extra mixtas">Extra Mixtas</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-purple-400 uppercase tracking-wider" title="Horas extra con exceso">Extra Exceso</th>
                                        <th className="text-center py-4 px-3 text-xs font-semibold text-red-400 uppercase tracking-wider" title="Horas de ausencia">Ausencias</th>
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
                                                {/* Horas base — verde esmeralda */}
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.regularHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'regularHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-emerald-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.sundayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'sundayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-emerald-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.holidayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'holidayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-emerald-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-colors"
                                                    />
                                                </td>
                                                {/* Horas extra diurna/nocturna — naranja */}
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeDayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeDayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-orange-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeNightHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeNightHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-orange-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-orange-500 transition-colors"
                                                    />
                                                </td>
                                                {/* Horas extra especiales — morado */}
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeHolidayHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeHolidayHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-purple-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-purple-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeMixedHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeMixedHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-purple-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-purple-500 transition-colors"
                                                    />
                                                </td>
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.overtimeExcessHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'overtimeExcessHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-purple-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-purple-500 transition-colors"
                                                    />
                                                </td>
                                                {/* Ausencias — rojo */}
                                                <td className="py-3 px-3 text-center align-middle">
                                                    <input
                                                        type="number"
                                                        min="0"
                                                        step="0.5"
                                                        value={num(row.absenceHours)}
                                                        onChange={(e) => handleHoursChange(row.empleadoId, 'absenceHours', e.target.value)}
                                                        className="w-full min-w-[70px] max-w-[70px] mx-auto block px-2 py-2 bg-navy-800 border border-red-300/40 rounded-md text-gray-100 text-sm text-center focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-red-500 transition-colors"
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

            {/* ==================== MODAL: CONFIRMAR IMPORTACIÓN ==================== */}
            <Modal
                isOpen={showImportConfirmModal}
                onClose={() => {
                    setShowImportConfirmModal(false);
                    setImportConfirmData(null);
                }}
                title="Confirmar importación"
                size="sm"
            >
                {importConfirmData && (
                    <div className="space-y-4">
                        <p className="text-gray-300">{importConfirmData.message}</p>
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
                )}
            </Modal>

            {/* ==================== MODAL: CONFIRMAR APROBACIÓN ==================== */}
            <Modal
                isOpen={showApproveConfirmModal}
                onClose={() => setShowApproveConfirmModal(false)}
                title="Confirmar Aprobación"
                size="sm"
            >
                <div className="space-y-4">
                    <div className="flex items-start gap-3 p-3 bg-amber-500/10 border border-amber-500/20 rounded-lg">
                        <AlertTriangle className="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" />
                        <p className="text-sm text-amber-300">
                            Esta acción es irreversible. Al aprobar, la planilla no podrá editarse.
                        </p>
                    </div>
                    {selectedPlanilla && (
                        <div className="bg-navy-800 rounded-lg p-4 border border-navy-600">
                            <p className="text-xs text-gray-500 mb-1">Total neto a pagar</p>
                            <p className="text-2xl font-bold text-emerald-400 font-mono">
                                {formatCurrency(selectedPlanilla.totalNetPay)}
                            </p>
                            <p className="text-xs text-gray-500 mt-1">
                                Planilla {selectedPlanilla.payrollNumber} — {empleadosCount} empleados
                            </p>
                        </div>
                    )}
                    <div className="flex gap-3 pt-2">
                        <button
                            onClick={() => setShowApproveConfirmModal(false)}
                            className="flex-1 bg-navy-700 hover:bg-navy-600 text-gray-200 px-4 py-2.5 rounded-lg font-medium transition-colors"
                        >
                            Cancelar
                        </button>
                        <button
                            onClick={confirmedApprove}
                            className="flex-1 bg-green-600 hover:bg-green-700 text-white px-4 py-2.5 rounded-lg font-semibold transition-colors shadow-lg shadow-green-900/40"
                        >
                            Confirmar Aprobación
                        </button>
                    </div>
                </div>
            </Modal>

            {/* ==================== HISTORIAL DE PLANILLAS ==================== */}
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
                                <Plus className="w-5 h-5" />
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
                                    <tr
                                        key={planilla.id}
                                        className={`border-l-2 transition-all cursor-pointer hover:bg-navy-800/50 ${
                                            selectedPlanilla?.id === planilla.id
                                                ? 'border-primary-500 bg-navy-800/30'
                                                : 'border-transparent hover:border-primary-500'
                                        }`}
                                        onClick={() => setSelectedPlanilla(planilla)}
                                    >
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
                                            {' — '}
                                            {new Date(planilla.periodEndDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-300">{planilla.employeeCount || empleadosCount}</td>
                                        {/* Montos con font-mono y prefijo B/. */}
                                        <td className="py-4 px-6 text-sm font-mono text-gray-200">
                                            B/. {Number(planilla.totalGrossPay || 0).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                        </td>
                                        <td className="py-4 px-6 text-sm font-mono text-gray-400">
                                            B/. {Number(planilla.totalDeductions || 0).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                        </td>
                                        <td className="py-4 px-6 text-sm font-mono font-semibold text-gray-200">
                                            B/. {Number(planilla.totalNetPay || 0).toLocaleString('es-PA', { minimumFractionDigits: 2 })}
                                        </td>
                                        {/* Badge de estado colorido en español */}
                                        <td className="py-4 px-6">
                                            <span className={`px-2 py-1 rounded-lg text-xs font-bold uppercase ${
                                                planilla.status === 0 ? 'bg-yellow-500/15 text-yellow-400' :
                                                planilla.status === 1 ? 'bg-blue-500/15 text-blue-400' :
                                                planilla.status === 2 ? 'bg-green-500/15 text-green-400' :
                                                planilla.status === 3 ? 'bg-emerald-500/15 text-emerald-400' :
                                                'bg-red-500/15 text-red-400'
                                            }`}>
                                                {['Borrador', 'Calculado', 'Aprobado', 'Pagado', 'Cancelado'][planilla.status] || 'Borrador'}
                                            </span>
                                        </td>
                                        {/* Acciones con texto */}
                                        <td className="py-4 px-6" onClick={(e) => e.stopPropagation()}>
                                            <div className="flex items-center gap-2">
                                                {(planilla.status === 0 || planilla.status === 1) && (
                                                    <button
                                                        onClick={async (e) => {
                                                            e.stopPropagation();
                                                            openingFromTable.current = true;
                                                            setSelectedPlanilla(planilla);
                                                            await fetchHours(planilla.id);
                                                            setShowHoursPanel(true);
                                                        }}
                                                        className="flex items-center gap-1.5 px-3 py-2 text-[13px] font-medium text-emerald-400 bg-emerald-500/10 hover:bg-emerald-500/20 border border-emerald-500/20 rounded-lg transition-all"
                                                    >
                                                        <Clock className="w-4 h-4" />
                                                        Horas
                                                    </button>
                                                )}
                                                <button
                                                    onClick={(e) => { e.stopPropagation(); viewDetails(planilla); }}
                                                    className="flex items-center gap-1.5 px-3 py-2 text-[13px] font-medium text-gray-300 bg-navy-800 hover:bg-primary-500/10 hover:text-primary-400 border border-navy-600 hover:border-primary-500/30 rounded-lg transition-all"
                                                >
                                                    <Eye className="w-4 h-4" />
                                                    Ver
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>

            {/* ==================== MODAL: NUEVA PLANILLA ==================== */}
            <Modal
                isOpen={showNewModal}
                onClose={() => setShowNewModal(false)}
                title="Nueva Planilla"
                size="lg"
            >
                <form onSubmit={handleCreatePlanilla}>
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
            </Modal>

            {/* ==================== MODAL: DETALLES DE PLANILLA ==================== */}
            <Modal
                isOpen={showDetailsModal}
                onClose={() => setShowDetailsModal(false)}
                title={planillaDetails ? `Detalles — ${planillaDetails.payrollNumber}` : 'Detalles de Planilla'}
                size="full"
            >
                {planillaDetails && (
                    <div>
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
                                            <th className="text-left py-3 px-3 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">Bruto</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">CSS</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">SE</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">ISR</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-red-400 uppercase">Pensión</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-orange-400 uppercase">Embargos</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-blue-400 uppercase">Volunt.</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">Total Ded.</th>
                                            <th className="text-right py-3 px-3 text-xs font-medium text-gray-500 uppercase">Neto</th>
                                        </tr>
                                    </thead>
                                    <tbody className="bg-navy-900 divide-y divide-navy-700">
                                        {planillaDetails.details.map((detail) => (
                                            <tr key={detail.id} className="hover:bg-navy-800">
                                                <td className="py-3 px-3 text-sm text-gray-100">{detail.empleado?.nombre} {detail.empleado?.apellido}</td>
                                                <td className="py-3 px-3 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.grossPay)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.cssEmployee)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.educationalInsuranceEmployee)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.incomeTax)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-red-400 font-mono">{formatCurrency(detail.pensionAlimenticia || 0)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-orange-400 font-mono">{formatCurrency(detail.embargos || 0)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-blue-400 font-mono">{formatCurrency(detail.deduccionesVoluntarias || 0)}</td>
                                                <td className="py-3 px-3 text-sm text-right text-gray-100 font-mono">{formatCurrency(detail.totalDeductions)}</td>
                                                <td className="py-3 px-3 text-sm text-right font-medium font-mono">
                                                    <span className="text-gray-100">{formatCurrency(detail.netPay)}</span>
                                                    {detail.tuvoLimitacionSalarioMinimo && (
                                                        <span className="ml-1 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-500/15 text-amber-400" title="Deducción limitada por salario mínimo">
                                                            SM
                                                        </span>
                                                    )}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                    <tfoot className="bg-navy-950 border-t-2 border-navy-600">
                                        <tr>
                                            <td className="py-3 px-3 text-sm font-bold text-gray-100">TOTALES</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalGrossPay)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.cssEmployee || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.educationalInsuranceEmployee || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.incomeTax || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-red-400 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.pensionAlimenticia || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-orange-400 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.embargos || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-blue-400 font-mono">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.deduccionesVoluntarias || 0), 0) || 0)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalDeductions)}</td>
                                            <td className="py-3 px-3 text-sm text-right font-bold text-gray-100 font-mono">{formatCurrency(planillaDetails.totalNetPay)}</td>
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
                )}
            </Modal>

        </div>
    );
};

export default PlanillasPage;
