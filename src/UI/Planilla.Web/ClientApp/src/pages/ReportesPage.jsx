import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';

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
            const data = await api.get('/api/payrollheaders');
            // Filtrar planillas calculadas, aprobadas o pagadas (status >= 1)
            const planillasValidas = data.filter(p => p.status >= 1);
            setPlanillas(planillasValidas);
        } catch (error) {
            toast.error(error.message || 'Error al cargar planillas');
        }
    };

    const descargarExcel = async (tipo) => {
        if (!selectedPlanilla) {
            toast('Seleccione una planilla primero', { icon: '⚠️' });
            return;
        }
        setLoading(true);
        try {
            const filename = `Reporte_${tipo.toUpperCase()}_${selectedPlanilla}_${new Date().toISOString().slice(0,10)}.xlsx`;
            await api.download(`/api/reportes/${tipo}/${selectedPlanilla}/excel`, filename);
            toast.success('Excel descargado correctamente');
        } catch (error) {
            toast.error(error.message || 'Error al descargar Excel');
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
            const filename = `Reporte_${tipo.toUpperCase()}_${selectedPlanilla}_${new Date().toISOString().slice(0,10)}.pdf`;
            await api.download(`/api/reportes/${tipo}/${selectedPlanilla}/pdf`, filename);
            toast.success('PDF descargado correctamente');
        } catch (error) {
            toast.error(error.message || 'Error al descargar PDF');
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
            const data = await api.get(`/api/reportes/${tipo}/${selectedPlanilla}`);
            setReporteData(data);
            setModalType(tipo);
            setModalOpen(true);
        } catch (error) {
            toast.error(error.message || 'Error al obtener reporte');
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

    // Componente de card activa con el nuevo diseño
    const ReporteCard = ({ title, description, icon, accentClass, tipo }) => (
        <div className="group bg-navy-900 border border-navy-700 hover:border-primary-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
            {/* Icono grande */}
            <div className={`w-12 h-12 bg-gradient-to-br from-primary-600/20 to-primary-500/10 border border-primary-500/20 rounded-xl flex items-center justify-center mb-4 ${accentClass}`}>
                {icon}
            </div>

            {/* Título y descripción */}
            <h3 className="text-base font-bold text-gray-100 font-display mb-1">{title}</h3>
            <p className="text-sm text-gray-500 mb-4 leading-relaxed">{description}</p>

            {/* Select planilla */}
            <select
                value={selectedPlanilla}
                onChange={(e) => setSelectedPlanilla(e.target.value)}
                className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 mb-4 text-sm"
            >
                <option value="">Seleccionar planilla...</option>
                {planillas.map(p => (
                    <option key={p.id} value={p.id}>
                        {p.payrollNumber} - {formatDate(p.periodStartDate)} a {formatDate(p.periodEndDate)}
                    </option>
                ))}
            </select>

            {/* Botones de acción */}
            <div className="flex items-center gap-2 pt-3 border-t border-navy-700">
                {/* Ver */}
                <button
                    onClick={() => verReporte(tipo)}
                    disabled={!selectedPlanilla || loading}
                    title="Ver reporte"
                    className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-navy-800 hover:bg-primary-600 border border-navy-700 hover:border-primary-600 rounded-lg text-xs font-medium text-gray-400 hover:text-white transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                    </svg>
                    Ver
                </button>
                {/* Excel */}
                <button
                    onClick={() => descargarExcel(tipo)}
                    disabled={!selectedPlanilla || loading}
                    title="Descargar Excel"
                    className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-navy-800 hover:bg-emerald-600 border border-navy-700 hover:border-emerald-600 rounded-lg text-xs font-medium text-gray-400 hover:text-white transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
                    </svg>
                    Excel
                </button>
                {/* PDF */}
                <button
                    onClick={() => descargarPdf(tipo)}
                    disabled={!selectedPlanilla || loading}
                    title="Descargar PDF"
                    className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-navy-800 hover:bg-red-600 border border-navy-700 hover:border-red-600 rounded-lg text-xs font-medium text-gray-400 hover:text-white transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"/>
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
                        <thead className="bg-navy-800">
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
                        <tbody className="divide-y divide-navy-700">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.baseCss)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.cssPatrono)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.riesgoProfesional)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium font-mono text-gray-100">{formatCurrency(emp.totalCss)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-4 py-3 text-sm text-amber-400" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSalarios)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalCssEmpleado)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalCssPatrono)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalRiesgo)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotal)}</td>
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
                        <thead className="bg-navy-800">
                            <tr>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Salario Bruto</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SE Empleado</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SE Patrono</th>
                                <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Total SE</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-navy-700">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.sePatrono)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium font-mono text-gray-100">{formatCurrency(emp.totalSe)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-4 py-3 text-sm text-amber-400" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSalarios)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSeEmpleado)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSePatrono)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotal)}</td>
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
                        <thead className="bg-navy-800">
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
                        <tbody className="divide-y divide-navy-700">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.cedula}</td>
                                    <td className="px-4 py-3 text-sm text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.ingresoAnualProyectado)}</td>
                                    <td className="px-4 py-3 text-sm text-right text-gray-300">{emp.dependientes}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.deduccionDependientes)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.baseGravable)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-mono text-gray-300">{formatCurrency(emp.isrAnual)}</td>
                                    <td className="px-4 py-3 text-sm text-right font-medium font-mono text-gray-100">{formatCurrency(emp.isrPeriodo)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-4 py-3 text-sm text-amber-400" colSpan="2">TOTALES</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalIngresos)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalDeducciones)}</td>
                                <td className="px-4 py-3 text-sm"></td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalIsrAnual)}</td>
                                <td className="px-4 py-3 text-sm text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalIsrPeriodo)}</td>
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
                        <thead className="bg-navy-800">
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
                        <tbody className="divide-y divide-navy-700">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-2 py-2 text-gray-300">{emp.cedula}</td>
                                    <td className="px-2 py-2 text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-2 py-2 text-gray-300">{emp.departamento || '-'}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.salarioBase)}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.horasExtra)}</td>
                                    <td className="px-2 py-2 text-right font-medium font-mono text-gray-100">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.isr)}</td>
                                    <td className="px-2 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.otrasDeducciones)}</td>
                                    <td className="px-2 py-2 text-right font-medium font-mono text-gray-100">{formatCurrency(emp.totalDeducciones)}</td>
                                    <td className="px-2 py-2 text-right font-bold font-mono text-green-400">{formatCurrency(emp.salarioNeto)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-2 py-2 text-amber-400" colSpan="3">TOTALES</td>
                                <td className="px-2 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totalBruto)}</td>
                                <td className="px-2 py-2"></td>
                                <td className="px-2 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totalBruto)}</td>
                                <td className="px-2 py-2" colSpan="4"></td>
                                <td className="px-2 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totalDeducciones)}</td>
                                <td className="px-2 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totalNeto)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        if (modalType === 'horas-extra') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full text-xs">
                        <thead className="bg-navy-800">
                            <tr>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Diurna</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Nocturna</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Dom/Fer</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Festivos</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Mixtas</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hrs Exceso</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total Hrs</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Monto Total</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-navy-700">
                            {reporteData.empleados?.map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-3 py-2 text-gray-300">{emp.numeroIdentificacion}</td>
                                    <td className="px-3 py-2 text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasDiurnas.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasNocturnas.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasDomingoFeriado.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasFestivos.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasMixtas.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{emp.horasExceso.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-medium font-mono text-gray-100">{emp.totalHoras.toFixed(2)}h</td>
                                    <td className="px-3 py-2 text-right font-bold font-mono text-green-400">{formatCurrency(emp.montoTotal)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-3 py-2 text-amber-400" colSpan="2">TOTALES</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasDiurnas.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasNocturnas.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasDomingoFeriado.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasFestivos.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasMixtas.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHorasExceso.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{reporteData.totales?.totalHoras.toFixed(2)}h</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalMonto)}</td>
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
            'planilla-detallada': 'Planilla Detallada Completa',
            'horas-extra': 'Reporte de Horas Extra'
        };
        return titles[modalType] || 'Reporte';
    };

    return (
        <div>
            {/* Header */}
            <div className="mb-6">
                <h2 className="text-2xl font-display font-bold text-gray-100">Reportes de Planilla</h2>
                <p className="text-gray-400 mt-1">Genere y descargue reportes oficiales para CSS, Seguro Educativo, ISR y más</p>
            </div>

            {/* Grid de cards — 2 columnas en desktop */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <ReporteCard
                    title="Planilla CSS"
                    description="Reporte para la Caja de Seguro Social con aportes CSS de empleados y empleador"
                    icon={
                        <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                        </svg>
                    }
                    accentClass="bg-gradient-to-br from-primary-600/20 to-primary-500/10 border-primary-500/20"
                    tipo="css"
                />

                <ReporteCard
                    title="Seguro Educativo"
                    description="Reporte de aportes al Seguro Educativo para la DGI"
                    icon={
                        <svg className="w-6 h-6 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path d="M12 14l9-5-9-5-9 5 9 5z" />
                            <path d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z" />
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l9-5-9-5-9 5 9 5zm0 0l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0112 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14zm-4 6v-7.5l4-2.222" />
                        </svg>
                    }
                    accentClass="bg-gradient-to-br from-purple-600/20 to-purple-500/10 border-purple-500/20"
                    tipo="seguro-educativo"
                />

                <ReporteCard
                    title="Impuesto sobre la Renta"
                    description="Reporte ISR para la DGI con proyección anual y retenciones"
                    icon={
                        <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                        </svg>
                    }
                    accentClass="bg-gradient-to-br from-amber-600/20 to-amber-500/10 border-amber-500/20"
                    tipo="isr"
                />

                <ReporteCard
                    title="Planilla Detallada"
                    description="Desglose completo de la planilla con todos los conceptos: salarios, deducciones, horas extra, etc."
                    icon={
                        <svg className="w-6 h-6 text-violet-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                    }
                    accentClass="bg-gradient-to-br from-violet-600/20 to-violet-500/10 border-violet-500/20"
                    tipo="planilla-detallada"
                />

                <ReporteCard
                    title="Horas Extra"
                    description="Reporte detallado de horas extra por tipo: diurna, nocturna, festivos, mixtas y exceso"
                    icon={
                        <svg className="w-6 h-6 text-orange-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                    }
                    accentClass="bg-gradient-to-br from-orange-600/20 to-orange-500/10 border-orange-500/20"
                    tipo="horas-extra"
                />

                {/* Banner Coming Soon — único, ocupa todo el ancho */}
                <div className="col-span-full mt-2 flex items-center gap-3 px-5 py-3.5 bg-navy-800/50 border border-navy-700/50 rounded-xl">
                    <div className="w-8 h-8 rounded-lg bg-navy-700 flex items-center justify-center flex-shrink-0">
                        <svg className="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z"/>
                        </svg>
                    </div>
                    <div>
                        <p className="text-sm font-medium text-gray-400">Más reportes en camino</p>
                        <p className="text-xs text-gray-600">Costos patronales, recibos de pago, reportes de CSS, análisis de tendencias y más</p>
                    </div>
                </div>
            </div>

            {/* Modal */}
            {modalOpen && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={() => setModalOpen(false)}>
                    <div className="bg-navy-900 rounded-2xl shadow-2xl shadow-black/30 max-w-6xl w-full max-h-[90vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
                        {/* Header */}
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
                            <h3 className="text-xl font-display font-bold text-gray-100">{getModalTitle()}</h3>
                            <button onClick={() => setModalOpen(false)} className="text-gray-400 hover:text-gray-200">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        {/* Subheader con info */}
                        <div className="px-6 py-3 bg-navy-800 border-b border-navy-700 grid grid-cols-4 gap-4 text-sm">
                            <div>
                                <span className="text-gray-500">Empresa:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.nombreEmpresa}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">RUC:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.ruc}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">Período:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.periodo}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">Generado:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.fechaGeneracion ? formatDate(reporteData.fechaGeneracion) : ''}</span>
                            </div>
                        </div>

                        {/* Body con tabla */}
                        <div className="flex-1 overflow-auto p-6">
                            {renderModalContent()}
                        </div>

                        {/* Footer con botones */}
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
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
