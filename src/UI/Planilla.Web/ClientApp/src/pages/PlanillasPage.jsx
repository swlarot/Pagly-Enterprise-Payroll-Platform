import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { SkeletonCard, SkeletonTable } from '../components/Skeleton';
import EmptyState from '../components/EmptyState';
import { api } from '../services/api';

const PlanillasPage = () => {
    const [planillas, setPlanillas] = useState([]);
    const [empleadosCount, setEmpleadosCount] = useState(0);
    const [loading, setLoading] = useState(true);
    const [processingAction, setProcessingAction] = useState(null);
    const [selectedPlanilla, setSelectedPlanilla] = useState(null);
    const [showNewModal, setShowNewModal] = useState(false);
    const [showDetailsModal, setShowDetailsModal] = useState(false);
    const [planillaDetails, setPlanillaDetails] = useState(null);
    const [formData, setFormData] = useState({
        payrollNumber: '',
        periodStartDate: '',
        periodEndDate: '',
        payDate: '',
        companyId: 1
    });

    useEffect(() => {
        fetchData();
    }, []);

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

            // Select first planilla by default
            if (enrichedPlanillas.length > 0 && !selectedPlanilla) {
                setSelectedPlanilla(enrichedPlanillas[0]);
            }

            // Fetch empleados count
            const empleadosData = await api.get('/api/empleados');
            setEmpleadosCount(empleadosData.filter(e => e.estaActivo).length);

        } catch (err) {
            toast.error(err.message || 'Error al cargar datos');
        } finally {
            setLoading(false);
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
            0: { label: 'Draft', bg: 'bg-yellow-100', text: 'text-yellow-800', dot: 'bg-yellow-600' },
            1: { label: 'Calculated', bg: 'bg-blue-100', text: 'text-blue-800', dot: 'bg-blue-600' },
            2: { label: 'Approved', bg: 'bg-green-100', text: 'text-green-800', dot: 'bg-green-600' },
            3: { label: 'Paid', bg: 'bg-emerald-100', text: 'text-emerald-800', dot: 'bg-emerald-600' },
            4: { label: 'Cancelled', bg: 'bg-red-100', text: 'text-red-800', dot: 'bg-red-600' }
        };
        const s = statuses[status] || statuses[0];
        return (
            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${s.bg} ${s.text}`}>
                <span className={`w-1.5 h-1.5 rounded-full mr-1.5 ${s.dot}`}></span>
                {s.label}
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
            periodStartDate: '',
            periodEndDate: '',
            payDate: '',
            companyId: 1
        });
        setShowNewModal(true);
    };

    const handleCreatePlanilla = async (e) => {
        e.preventDefault();

        // Validation
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
                    className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
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
                                setSelectedPlanilla(planilla); // Ya viene enriquecida del fetchData
                            }}
                            className="w-full px-3 py-2.5 bg-white border border-gray-300 rounded-lg text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
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
                    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                        <h3 className="text-sm font-medium text-gray-600 mb-2">Salarios Brutos</h3>
                        <p className="text-3xl font-bold text-gray-900">{formatCurrency(selectedPlanilla.totalGrossPay)}</p>
                        <p className="text-sm text-blue-600 mt-2">{empleadosCount} empleados</p>
                    </div>

                    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                        <h3 className="text-sm font-medium text-gray-600 mb-2">Aportes CSS</h3>
                        <p className="text-3xl font-bold text-gray-900">{formatCurrency(selectedPlanilla.totalEmployeeCss + selectedPlanilla.totalEmployerCss)}</p>
                        <p className="text-sm text-yellow-600 mt-2">Empleado + Patrono</p>
                    </div>

                    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                        <h3 className="text-sm font-medium text-gray-600 mb-2">Seguro Educativo</h3>
                        <p className="text-3xl font-bold text-gray-900">{formatCurrency(selectedPlanilla.totalEmployeeSe + selectedPlanilla.totalEmployerSe)}</p>
                        <p className="text-sm text-purple-600 mt-2">Empleado + Patrono</p>
                    </div>

                    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                        <h3 className="text-sm font-medium text-gray-600 mb-2">ISR Retenido</h3>
                        <p className="text-3xl font-bold text-gray-900">{formatCurrency(selectedPlanilla.totalIncomeTax)}</p>
                        <p className="text-sm text-red-600 mt-2">Según tabla DGI</p>
                    </div>
                </div>
            ) : (
                <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
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
                <div className="flex gap-3">
                    {selectedPlanilla.status === 0 && (
                        <button
                            onClick={handleCalculate}
                            disabled={processingAction === 'calculating'}
                            className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
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
                    )}

                    {selectedPlanilla.status === 1 && (
                        <>
                            <button
                                onClick={handleApprove}
                                disabled={processingAction === 'approving'}
                                className="inline-flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
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
                                className="inline-flex items-center gap-2 bg-yellow-600 hover:bg-yellow-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                                </svg>
                                Recalcular
                            </button>
                        </>
                    )}

                    {selectedPlanilla.status === 2 && (
                        <button
                            disabled
                            className="inline-flex items-center gap-2 bg-emerald-600 text-white px-4 py-2.5 rounded-lg font-medium shadow-sm opacity-50 cursor-not-allowed"
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
                            className="inline-flex items-center gap-2 bg-gray-600 hover:bg-gray-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-sm"
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

            {/* History Table */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h3 className="text-lg font-semibold text-gray-900">
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
                            <thead className="bg-gray-50 border-b border-gray-200">
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
                            <tbody className="bg-white divide-y divide-gray-200">
                                {planillas.map((planilla) => (
                                    <tr key={planilla.id} className="hover:bg-gray-50 transition-colors">
                                        <td className="py-4 px-6 text-sm font-medium text-gray-900">{planilla.payrollNumber}</td>
                                        <td className="py-4 px-6 text-sm text-gray-500">
                                            {new Date(planilla.periodStartDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short' })}
                                            {' - '}
                                            {new Date(planilla.periodEndDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}
                                        </td>
                                        <td className="py-4 px-6 text-sm text-gray-900">{planilla.employeeCount || empleadosCount}</td>
                                        <td className="py-4 px-6 text-sm font-medium text-gray-900">{formatCurrency(planilla.totalGrossPay)}</td>
                                        <td className="py-4 px-6 text-sm text-gray-900">{formatCurrency(planilla.totalDeductions)}</td>
                                        <td className="py-4 px-6 text-sm font-medium text-gray-900">{formatCurrency(planilla.totalNetPay)}</td>
                                        <td className="py-4 px-6">{getStatusBadge(planilla.status)}</td>
                                        <td className="py-4 px-6">
                                            <button
                                                onClick={() => viewDetails(planilla)}
                                                className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium text-sm"
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

            {/* New Planilla Modal */}
            {showNewModal && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
                            <h3 className="text-xl font-semibold text-gray-900">Nueva Planilla</h3>
                            <button
                                onClick={() => setShowNewModal(false)}
                                className="text-gray-400 hover:text-gray-600 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleCreatePlanilla} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Número de Planilla <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="text"
                                        required
                                        value={formData.payrollNumber}
                                        onChange={(e) => setFormData({ ...formData, payrollNumber: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                        placeholder="2025-001"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha Inicio Período <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.periodStartDate}
                                        onChange={(e) => setFormData({ ...formData, periodStartDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha Fin Período <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.periodEndDate}
                                        onChange={(e) => setFormData({ ...formData, periodEndDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                    />
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-700 mb-2">
                                        Fecha de Pago <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.payDate}
                                        onChange={(e) => setFormData({ ...formData, payDate: e.target.value })}
                                        className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                    />
                                </div>
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                                <button
                                    type="button"
                                    onClick={() => setShowNewModal(false)}
                                    className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors shadow-sm"
                                >
                                    Crear Planilla
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Details Modal */}
            {showDetailsModal && planillaDetails && (
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-xl shadow-2xl max-w-6xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between sticky top-0 bg-white">
                            <h3 className="text-xl font-semibold text-gray-900">Detalles de Planilla - {planillaDetails.payrollNumber}</h3>
                            <button
                                onClick={() => setShowDetailsModal(false)}
                                className="text-gray-400 hover:text-gray-600 transition-colors"
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
                                    <p className="text-sm text-gray-600">Período</p>
                                    <p className="font-medium text-gray-900">
                                        {new Date(planillaDetails.periodStartDate).toLocaleDateString('es-PA')}
                                        {' - '}
                                        {new Date(planillaDetails.periodEndDate).toLocaleDateString('es-PA')}
                                    </p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-600">Fecha de Pago</p>
                                    <p className="font-medium text-gray-900">
                                        {new Date(planillaDetails.payDate).toLocaleDateString('es-PA')}
                                    </p>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-600">Estado</p>
                                    <div className="mt-1">{getStatusBadge(planillaDetails.status)}</div>
                                </div>
                                <div>
                                    <p className="text-sm text-gray-600">Total Neto</p>
                                    <p className="font-bold text-lg text-gray-900">{formatCurrency(planillaDetails.totalNetPay)}</p>
                                </div>
                            </div>

                            {/* Details Table */}
                            {planillaDetails.details && planillaDetails.details.length > 0 ? (
                                <div className="overflow-x-auto">
                                    <table className="w-full">
                                        <thead className="bg-gray-50 border-b border-gray-200">
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
                                        <tbody className="bg-white divide-y divide-gray-200">
                                            {planillaDetails.details.map((detail) => (
                                                <tr key={detail.id} className="hover:bg-gray-50">
                                                    <td className="py-3 px-4 text-sm text-gray-900">{detail.empleado?.nombre} {detail.empleado?.apellido}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-900">{formatCurrency(detail.grossPay)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-900">{formatCurrency(detail.cssEmployee)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-900">{formatCurrency(detail.educationalInsuranceEmployee)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-900">{formatCurrency(detail.incomeTax)}</td>
                                                    <td className="py-3 px-4 text-sm text-right text-gray-900">{formatCurrency(detail.totalDeductions)}</td>
                                                    <td className="py-3 px-4 text-sm text-right font-medium text-gray-900">{formatCurrency(detail.netPay)}</td>
                                                </tr>
                                            ))}
                                        </tbody>
                                        <tfoot className="bg-gray-50 border-t-2 border-gray-300">
                                            <tr>
                                                <td className="py-3 px-4 text-sm font-bold text-gray-900">TOTALES</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.totalGrossPay)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.cssEmployee || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.educationalInsuranceEmployee || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.details?.reduce((sum, d) => sum + (d.incomeTax || 0), 0) || 0)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.totalDeductions)}</td>
                                                <td className="py-3 px-4 text-sm text-right font-bold text-gray-900">{formatCurrency(planillaDetails.totalNetPay)}</td>
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
