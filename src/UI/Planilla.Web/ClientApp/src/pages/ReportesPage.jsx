import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';

const ReportesPage = () => {

    // State management
    const [planillas, setPlanillas] = useState([]);
    const [selectedPlanilla, setSelectedPlanilla] = useState('');
    const [loading, setLoading] = useState(false);
    const [modalOpen, setModalOpen] = useState(false);
    const [modalType, setModalType] = useState('');
    const [reporteData, setReporteData] = useState(null);

    useEffect(() => {
        fetchPlanillas();
    }, []);

    const fetchPlanillas = async () => {
        try {
            const response = await fetch('/api/payrollheaders');
            if (!response.ok) throw new Error('Error al cargar planillas');
            const data = await response.json();
            // Filtrar planillas calculadas, aprobadas o pagadas (status >= 1)
            const planillasValidas = data.filter(p => p.status >= 1);
            setPlanillas(planillasValidas);
        } catch (error) {
            toast.error(error.message);
        }
    };

    const descargarExcel = async (tipo) => {
        if (!selectedPlanilla) {
            toast('Seleccione una planilla primero', { icon: '⚠️' });
            return;
        }
        setLoading(true);
        try {
            const response = await fetch(`/api/reportes/${tipo}/${selectedPlanilla}/excel`);
            if (!response.ok) throw new Error('Error al generar reporte');
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `Reporte_${tipo.toUpperCase()}_${selectedPlanilla}_${new Date().toISOString().slice(0,10)}.xlsx`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
            toast.success('Excel descargado correctamente');
        } catch (error) {
            toast.error(error.message);
        } finally {
            setLoading(false);
        }
    };

    const descargarPdf = async (tipo) => {
        if (!selectedPlanilla) {
            toast('Seleccione una planilla primero', { icon: '⚠️' });
            return;
        }
        setLoading(true);
        try {
            const response = await fetch(`/api/reportes/${tipo}/${selectedPlanilla}/pdf`);
            if (!response.ok) throw new Error('Error al generar reporte');
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `Reporte_${tipo.toUpperCase()}_${selectedPlanilla}_${new Date().toISOString().slice(0,10)}.pdf`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
            toast.success('PDF descargado correctamente');
        } catch (error) {
            toast.error(error.message);
        } finally {
            setLoading(false);
        }
    };

    const verReporte = async (tipo) => {
        if (!selectedPlanilla) {
            toast('Seleccione una planilla primero', { icon: '⚠️' });
            return;
        }
        setLoading(true);
        try {
            const response = await fetch(`/api/reportes/${tipo}/${selectedPlanilla}`);
            if (!response.ok) throw new Error('Error al obtener reporte');
            const data = await response.json();
            setReporteData(data);
            setModalType(tipo);
            setModalOpen(true);
        } catch (error) {
            toast.error(error.message);
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (amount) => {
        return new Intl.NumberFormat('es-PA', { style: 'currency', currency: 'USD' }).format(amount);
    };

    const formatDate = (dateString) => {
        return new Date(dateString).toLocaleDateString('es-PA');
    };

    const ReporteCard = ({ title, description, icon, color, borderColor, bgGradient, tipo, comingSoon }) => (
        <div className={`rounded-2xl shadow-sm hover:shadow-md transition-shadow p-6 ${bgGradient} border ${borderColor}`}>
            {/* Icono y título */}
            <div className="flex items-start gap-4 mb-4">
                <div className={`w-12 h-12 ${color} rounded-full flex items-center justify-center shadow-md`}>
                    {icon}
                </div>
                <div className="flex-1">
                    <h3 className="font-semibold text-lg text-gray-900 flex items-center gap-2">
                        {title}
                        {comingSoon && <span className="px-2 py-0.5 bg-yellow-100 text-yellow-700 text-xs rounded-full">Próximamente</span>}
                    </h3>
                    <p className="text-sm text-gray-600 mt-1">{description}</p>
                </div>
            </div>

            {/* Select planilla */}
            <select
                value={selectedPlanilla}
                onChange={(e) => setSelectedPlanilla(e.target.value)}
                disabled={comingSoon}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 mb-4 disabled:opacity-50"
            >
                <option value="">Seleccionar planilla...</option>
                {planillas.map(p => (
                    <option key={p.id} value={p.id}>
                        {p.payrollNumber} - {formatDate(p.periodStartDate)} a {formatDate(p.periodEndDate)}
                    </option>
                ))}
            </select>

            {/* Botones */}
            <div className="flex gap-2">
                <button
                    onClick={() => verReporte(tipo)}
                    disabled={!selectedPlanilla || loading || comingSoon}
                    className={`flex-1 flex items-center justify-center gap-2 px-3 py-1.5 text-sm font-medium text-white ${color} rounded-lg hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition-opacity`}
                >
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                    Ver
                </button>
                <button
                    onClick={() => descargarExcel(tipo)}
                    disabled={!selectedPlanilla || loading || comingSoon}
                    className="flex-1 flex items-center justify-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-green-600 rounded-lg hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    Excel
                </button>
                <button
                    onClick={() => descargarPdf(tipo)}
                    disabled={!selectedPlanilla || loading || comingSoon}
                    className="flex-1 flex items-center justify-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-red-600 rounded-lg hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                    </svg>
                    PDF
                </button>
            </div>
        </div>
    );

    const renderModalContent = () => {
        if (!reporteData) return null;

        if (modalType === 'css') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Salario Bruto</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Base CSS</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">CSS Empleado</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">CSS Patrono</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Riesgo Prof.</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Total</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-gray-50">
                                    <td className="px-4 py-3 text-sm">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.baseCss)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.cssPatrono)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.riesgoProfesional)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium">{formatCurrency(emp.totalCss)}</td>
                                </tr>
                            ))}
                            <tr className="bg-yellow-100 font-bold">
                                <td className="px-4 py-3 text-sm" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalSalarios)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalCssEmpleado)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalCssPatrono)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalRiesgo)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.granTotal)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        if (modalType === 'seguro-educativo') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Salario Bruto</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SE Empleado</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SE Patrono</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Total SE</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-gray-50">
                                    <td className="px-4 py-3 text-sm">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.sePatrono)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium">{formatCurrency(emp.totalSe)}</td>
                                </tr>
                            ))}
                            <tr className="bg-yellow-100 font-bold">
                                <td className="px-4 py-3 text-sm" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalSalarios)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalSeEmpleado)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalSePatrono)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.granTotal)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        if (modalType === 'isr') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Ingreso Anual</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Dependientes</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Deducción Dep.</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Base Gravable</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">ISR Anual</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">ISR Período</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-gray-50">
                                    <td className="px-4 py-3 text-sm">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.ingresoAnualProyectado)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{emp.dependientes}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.deduccionDependientes)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.baseGravable)}</td>
                                    <td className="px-4 py-3 text-sm text-right">{formatCurrency(emp.isrAnual)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium">{formatCurrency(emp.isrPeriodo)}</td>
                                </tr>
                            ))}
                            <tr className="bg-yellow-100 font-bold">
                                <td className="px-4 py-3 text-sm" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalIngresos)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalDeducciones)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalIsrAnual)}</td>
                                <td className="px-4 py-3 text-sm text-right">{formatCurrency(reporteData.totales?.totalIsrPeriodo)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        if (modalType === 'planilla-detallada') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full text-xs">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-2 py-2 text-left font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-2 py-2 text-left font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-2 py-2 text-left font-medium text-gray-500 uppercase">Depto</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Sal. Base</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Hrs Extra</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Bruto</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">CSS</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">SE</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">ISR</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Otras Ded.</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Total Ded.</th>
                                <th className="px-2 py-2 text-right font-medium text-gray-500 uppercase">Neto</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-gray-50">
                                    <td className="px-2 py-2">{emp.cedula}</td>
                                    <td className="px-2 py-2">{emp.nombreCompleto}</td>
                                    <td className="px-2 py-2">{emp.departamento || '-'}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.salarioBase)}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.horasExtra)}</td>
                                    <td className="px-2 py-2 text-right font-medium">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.isr)}</td>
                                    <td className="px-2 py-2 text-right">{formatCurrency(emp.otrasDeducciones)}</td>
                                    <td className="px-2 py-2 text-right font-medium">{formatCurrency(emp.totalDeducciones)}</td>
                                    <td className="px-2 py-2 text-right font-bold text-green-600">{formatCurrency(emp.salarioNeto)}</td>
                                </tr>
                            ))}
                            <tr className="bg-yellow-100 font-bold">
                                <td className="px-2 py-2" colSpan="3">TOTALES</td>
                                <td className="px-2 py-2 text-right">{formatCurrency(reporteData.totalBruto)}</td>
                                <td className="px-2 py-2"></td>
                                <td className="px-2 py-2 text-right">{formatCurrency(reporteData.totalBruto)}</td>
                                <td className="px-2 py-2" colSpan="4"></td>
                                <td className="px-2 py-2 text-right">{formatCurrency(reporteData.totalDeducciones)}</td>
                                <td className="px-2 py-2 text-right">{formatCurrency(reporteData.totalNeto)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        return null;
    };

    const getModalTitle = () => {
        const titles = {
            'css': 'Reporte Planilla CSS',
            'seguro-educativo': 'Reporte Seguro Educativo',
            'isr': 'Reporte Impuesto Sobre la Renta',
            'planilla-detallada': 'Planilla Detallada Completa'
        };
        return titles[modalType] || 'Reporte';
    };

    return (
        <div>
            {/* Header */}
            <div className="mb-6">
                <h2 className="text-2xl font-bold text-gray-900">Reportes de Planilla</h2>
                <p className="text-gray-600 mt-1">Genere y descargue reportes oficiales para CSS, Seguro Educativo, ISR y más</p>
            </div>

            {/* Grid de cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                <ReporteCard
                    title="Planilla CSS"
                    description="Reporte para la Caja de Seguro Social con aportes CSS de empleados y empleador"
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>}
                    color="bg-blue-600"
                    borderColor="border-blue-200"
                    bgGradient="bg-gradient-to-br from-blue-50 to-blue-100"
                    tipo="css"
                />

                <ReporteCard
                    title="Seguro Educativo"
                    description="Reporte de aportes al Seguro Educativo"
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path d="M12 14l9-5-9-5-9 5 9 5z" /><path d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l9-5-9-5-9 5 9 5zm0 0l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14zm-4 6v-7.5l4-2.222" /></svg>}
                    color="bg-green-600"
                    borderColor="border-green-200"
                    bgGradient="bg-gradient-to-br from-green-50 to-green-100"
                    tipo="seguro-educativo"
                />

                <ReporteCard
                    title="Impuesto sobre la Renta"
                    description="Reporte ISR para la DGI con proyección anual y retenciones"
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>}
                    color="bg-amber-600"
                    borderColor="border-amber-200"
                    bgGradient="bg-gradient-to-br from-amber-50 to-amber-100"
                    tipo="isr"
                />

                <ReporteCard
                    title="Planilla Detallada"
                    description="Desglose completo de la planilla con todos los conceptos: salarios, deducciones, horas extra, etc."
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>}
                    color="bg-purple-600"
                    borderColor="border-purple-200"
                    bgGradient="bg-gradient-to-br from-purple-50 to-purple-100"
                    tipo="planilla-detallada"
                />

                <ReporteCard
                    title="Costos Patronales"
                    description="Resumen de todos los aportes del empleador: CSS patronal, SE patronal, riesgo profesional"
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" /></svg>}
                    color="bg-red-600"
                    borderColor="border-red-200"
                    bgGradient="bg-gradient-to-br from-red-50 to-red-100"
                    tipo="css"
                    comingSoon
                />

                <ReporteCard
                    title="Recibos de Pago"
                    description="Generar recibos individuales de pago para cada empleado"
                    icon={<svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>}
                    color="bg-gray-600"
                    borderColor="border-gray-200"
                    bgGradient="bg-gradient-to-br from-gray-50 to-gray-100"
                    tipo="recibos"
                    comingSoon
                />
            </div>

            {/* Modal */}
            {modalOpen && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={() => setModalOpen(false)}>
                    <div className="bg-white rounded-2xl shadow-2xl max-w-6xl w-full max-h-[90vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
                        {/* Header */}
                        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
                            <h3 className="text-xl font-bold text-gray-900">{getModalTitle()}</h3>
                            <button onClick={() => setModalOpen(false)} className="text-gray-400 hover:text-gray-600">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        {/* Subheader con info */}
                        <div className="px-6 py-3 bg-gray-50 border-b border-gray-200 grid grid-cols-4 gap-4 text-sm">
                            <div>
                                <span className="text-gray-500">Empresa:</span>
                                <span className="ml-2 font-medium">{reporteData?.nombreEmpresa}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">RUC:</span>
                                <span className="ml-2 font-medium">{reporteData?.ruc}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">Período:</span>
                                <span className="ml-2 font-medium">{reporteData?.periodo}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">Generado:</span>
                                <span className="ml-2 font-medium">{reporteData?.fechaGeneracion ? formatDate(reporteData.fechaGeneracion) : ''}</span>
                            </div>
                        </div>

                        {/* Body con tabla */}
                        <div className="flex-1 overflow-auto p-6">
                            {renderModalContent()}
                        </div>

                        {/* Footer con botones */}
                        <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-3">
                            <button
                                onClick={() => descargarExcel(modalType)}
                                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 flex items-center gap-2"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                </svg>
                                Descargar Excel
                            </button>
                            <button
                                onClick={() => descargarPdf(modalType)}
                                className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 flex items-center gap-2"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                                </svg>
                                Descargar PDF
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default ReportesPage;
