import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';

const MESES = [
    { value: 1, label: 'Enero' },
    { value: 2, label: 'Febrero' },
    { value: 3, label: 'Marzo' },
    { value: 4, label: 'Abril' },
    { value: 5, label: 'Mayo' },
    { value: 6, label: 'Junio' },
    { value: 7, label: 'Julio' },
    { value: 8, label: 'Agosto' },
    { value: 9, label: 'Septiembre' },
    { value: 10, label: 'Octubre' },
    { value: 11, label: 'Noviembre' },
    { value: 12, label: 'Diciembre' },
];

const CURRENT_YEAR = new Date().getFullYear();
const ANIOS = [CURRENT_YEAR - 1, CURRENT_YEAR, CURRENT_YEAR + 1];

const ReportesPage = () => {
    const [planillas, setPlanillas] = useState([]);
    const [loadingPlanillas, setLoadingPlanillas] = useState(true);

    // Selectors independientes por reporte
    const [selPlanillaRegular, setSelPlanillaRegular] = useState('');
    const [selSip, setSelSip] = useState('');
    const [selAcreedores, setSelAcreedores] = useState('');
    const [selComprobantes, setSelComprobantes] = useState('');
    const [selMes, setSelMes] = useState(new Date().getMonth() + 1);
    const [selAnio, setSelAnio] = useState(CURRENT_YEAR);

    const [loading, setLoading] = useState(false);
    const [modalOpen, setModalOpen] = useState(false);
    const [modalType, setModalType] = useState('');
    const [modalTitle, setModalTitle] = useState('');
    const [reporteData, setReporteData] = useState(null);

    useEffect(() => {
        fetchPlanillas();
    }, []);

    const fetchPlanillas = async () => {
        try {
            setLoadingPlanillas(true);
            const data = await api.get('/api/payrollheaders');
            const validas = (data || []).filter(p => p.status >= 1);
            setPlanillas(validas);
        } catch (error) {
            toast.error(error.message || 'Error al cargar planillas');
        } finally {
            setLoadingPlanillas(false);
        }
    };

    const formatCurrency = (amount) =>
        'B/. ' + new Intl.NumberFormat('es-PA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount || 0);

    const formatDate = (dateString) => {
        if (!dateString) return '';
        return new Date(dateString).toLocaleDateString('es-PA');
    };

    const formatHoras = (h) => (h || 0).toFixed(2) + 'h';

    // ── Acciones ──────────────────────────────────────────────────────────

    const verReporte = async (tipo, params = {}) => {
        setLoading(true);
        try {
            let url = '';
            let titulo = '';
            if (tipo === 'planilla-regular') {
                if (!selPlanillaRegular) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/planilla-regular/${selPlanillaRegular}`;
                titulo = 'Planilla Regular';
            } else if (tipo === 'sip') {
                if (!selSip) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/sip/${selSip}`;
                titulo = 'SIP — CSS Patronal';
            } else if (tipo === 'acreedores') {
                if (!selAcreedores) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/acreedores/${selAcreedores}`;
                titulo = 'Reporte de Acreedores';
            } else if (tipo === 'mensual') {
                url = `/api/reportes/mensual?mes=${selMes}&anio=${selAnio}`;
                titulo = `Reporte Mensual — ${MESES.find(m => m.value === selMes)?.label} ${selAnio}`;
            }
            const data = await api.get(url);
            setReporteData(data);
            setModalType(tipo);
            setModalTitle(titulo);
            setModalOpen(true);
        } catch (error) {
            toast.error(error.message || 'Error al obtener reporte');
        } finally {
            setLoading(false);
        }
    };

    const descargarExcel = async (tipo) => {
        setLoading(true);
        try {
            let url = '', filename = '';
            const fecha = new Date().toISOString().slice(0, 10);
            if (tipo === 'planilla-regular') {
                if (!selPlanillaRegular) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/planilla-regular/${selPlanillaRegular}/excel`;
                filename = `PlanillaRegular_${selPlanillaRegular}_${fecha}.xlsx`;
            } else if (tipo === 'sip') {
                if (!selSip) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/sip/${selSip}/excel`;
                filename = `SIP_${selSip}_${fecha}.xlsx`;
            } else if (tipo === 'acreedores') {
                if (!selAcreedores) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/acreedores/${selAcreedores}/excel`;
                filename = `Acreedores_${selAcreedores}_${fecha}.xlsx`;
            } else if (tipo === 'mensual') {
                url = `/api/reportes/mensual/excel?mes=${selMes}&anio=${selAnio}`;
                filename = `Mensual_${selMes}_${selAnio}_${fecha}.xlsx`;
            }
            await api.download(url, filename);
            toast.success('Excel descargado correctamente');
        } catch (error) {
            toast.error(error.message || 'Error al descargar Excel');
        } finally {
            setLoading(false);
        }
    };

    const descargarPdf = async (tipo) => {
        setLoading(true);
        try {
            let url = '', filename = '';
            const fecha = new Date().toISOString().slice(0, 10);
            if (tipo === 'planilla-regular') {
                if (!selPlanillaRegular) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/planilla-regular/${selPlanillaRegular}/pdf`;
                filename = `PlanillaRegular_${selPlanillaRegular}_${fecha}.pdf`;
            } else if (tipo === 'sip') {
                if (!selSip) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/sip/${selSip}/pdf`;
                filename = `SIP_${selSip}_${fecha}.pdf`;
            } else if (tipo === 'acreedores') {
                if (!selAcreedores) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/acreedores/${selAcreedores}/pdf`;
                filename = `Acreedores_${selAcreedores}_${fecha}.pdf`;
            } else if (tipo === 'comprobantes') {
                if (!selComprobantes) { toast('Seleccione una planilla', { icon: '⚠️' }); return; }
                url = `/api/reportes/comprobantes/${selComprobantes}/pdf`;
                filename = `Comprobantes_${selComprobantes}_${fecha}.pdf`;
            } else if (tipo === 'mensual') {
                url = `/api/reportes/mensual/pdf?mes=${selMes}&anio=${selAnio}`;
                filename = `Mensual_${selMes}_${selAnio}_${fecha}.pdf`;
            }
            await api.download(url, filename);
            toast.success('PDF descargado correctamente');
        } catch (error) {
            toast.error(error.message || 'Error al descargar PDF');
        } finally {
            setLoading(false);
        }
    };

    // ── Selectores ────────────────────────────────────────────────────────

    const PlanillaSelect = ({ value, onChange }) => (
        <select
            value={value}
            onChange={e => onChange(e.target.value)}
            className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 mb-4 text-sm"
        >
            <option value="">Seleccionar planilla...</option>
            {loadingPlanillas
                ? <option disabled>Cargando...</option>
                : planillas.map(p => (
                    <option key={p.id} value={p.id}>
                        {p.payrollNumber} — {formatDate(p.periodStartDate)} a {formatDate(p.periodEndDate)}
                    </option>
                ))
            }
        </select>
    );

    // ── Modal content ─────────────────────────────────────────────────────

    const renderModalContent = () => {
        if (!reporteData) return null;

        // Planilla Regular
        if (modalType === 'planilla-regular') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full text-xs">
                        <thead className="bg-navy-800">
                            <tr>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hs. Reg.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hs. Dom.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hs. Fer.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Hs. Extra</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Bruto</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">CSS</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">SE</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">ISR</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Acreed.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Neto</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-navy-700">
                            {(reporteData.empleados || []).map((emp, idx) => (
                                <tr key={idx} className={`hover:bg-navy-800 ${emp.tuvoLimitacion ? 'bg-orange-500/5' : ''}`}>
                                    <td className="px-3 py-2 text-gray-300">{emp.cedula}</td>
                                    <td className="px-3 py-2 text-gray-300">
                                        {emp.nombreCompleto}
                                        {emp.tuvoLimitacion && <span className="ml-1 text-orange-400 text-xs">⚠</span>}
                                    </td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-400">{formatHoras(emp.horasRegulares)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-400">{formatHoras(emp.horasDomingo)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-400">{formatHoras(emp.horasFeriado)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-400">{formatHoras(emp.horasExtra)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-100">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.isr)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.totalAcreedores)}</td>
                                    <td className="px-3 py-2 text-right font-bold font-mono text-green-400">{formatCurrency(emp.salarioNeto)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-3 py-2 text-amber-400" colSpan="6">TOTALES ({reporteData.totales?.totalEmpleados} empleados)</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalBruto)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalCss)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSe)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalIsr)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalAcreedores)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalNeto)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        // SIP
        if (modalType === 'sip') {
            return (
                <div className="overflow-x-auto">
                    <table className="w-full text-xs">
                        <thead className="bg-navy-800">
                            <tr>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Cédula</th>
                                <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Sal. Bruto</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Base CSS</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">CSS Emp.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">CSS Pat.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">SE Emp.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">SE Pat.</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Riesgo</th>
                                <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total SIP</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-navy-700">
                            {(reporteData.empleados || []).map((emp, idx) => (
                                <tr key={idx} className="hover:bg-navy-800">
                                    <td className="px-3 py-2 text-gray-300">{emp.cedula}</td>
                                    <td className="px-3 py-2 text-gray-300">{emp.nombreCompleto}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-100">{formatCurrency(emp.salarioBruto)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.baseCss)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.cssEmpleado)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.cssPatronal)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.seEmpleado)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.sePatronal)}</td>
                                    <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.riesgoProfesional)}</td>
                                    <td className="px-3 py-2 text-right font-bold font-mono text-blue-400">{formatCurrency(emp.totalSip)}</td>
                                </tr>
                            ))}
                            <tr className="bg-amber-500/15 font-bold">
                                <td className="px-3 py-2 text-amber-400" colSpan="2">TOTALES</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSalarios)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalBaseCss)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalCssEmpleado)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalCssPatronal)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSeEmpleado)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalSePatronal)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.totalRiesgo)}</td>
                                <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalSip)}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            );
        }

        // Acreedores
        if (modalType === 'acreedores') {
            const acreedores = reporteData.acreedores || [];
            return (
                <div className="space-y-6">
                    {/* Resumen rápido */}
                    <div className="grid grid-cols-2 gap-4 p-4 bg-navy-800 rounded-lg">
                        <div>
                            <span className="text-xs text-gray-500 uppercase">Total Acreedores</span>
                            <p className="text-lg font-bold text-gray-100">{reporteData.totalAcreedores}</p>
                        </div>
                        <div>
                            <span className="text-xs text-gray-500 uppercase">Gran Total</span>
                            <p className="text-lg font-bold text-green-400">{formatCurrency(reporteData.granTotal)}</p>
                        </div>
                    </div>

                    {acreedores.length === 0 ? (
                        <p className="text-gray-500 text-center py-8">No hay deducciones de acreedores en esta planilla.</p>
                    ) : (
                        acreedores.map((acreedor, idx) => (
                            <div key={idx} className="border border-navy-700 rounded-lg overflow-hidden">
                                {/* Cabecera acreedor */}
                                <div className="bg-navy-800 px-4 py-3 flex items-center justify-between">
                                    <div>
                                        <h4 className="font-bold text-gray-100">{acreedor.nombreAcreedor}</h4>
                                        <div className="flex gap-4 mt-1 text-xs text-gray-500">
                                            {acreedor.tipoAcreedor && <span>Tipo: {acreedor.tipoAcreedor}</span>}
                                            {acreedor.banco && <span>Banco: {acreedor.banco}</span>}
                                            {acreedor.numeroCuenta && <span>Cuenta: {acreedor.numeroCuenta}</span>}
                                            {acreedor.identificacion && <span>ID: {acreedor.identificacion}</span>}
                                        </div>
                                    </div>
                                    <div className="text-right">
                                        <p className="text-xs text-gray-500">{acreedor.cantidadEmpleados} empleado(s)</p>
                                        <p className="font-bold text-green-400">{formatCurrency(acreedor.totalATransferir)}</p>
                                    </div>
                                </div>
                                {/* Detalle empleados */}
                                <table className="w-full text-xs">
                                    <thead className="bg-navy-900">
                                        <tr>
                                            <th className="px-3 py-2 text-left text-gray-500 font-medium uppercase">Empleado</th>
                                            <th className="px-3 py-2 text-left text-gray-500 font-medium uppercase">Cédula</th>
                                            <th className="px-3 py-2 text-left text-gray-500 font-medium uppercase">Tipo</th>
                                            <th className="px-3 py-2 text-left text-gray-500 font-medium uppercase">Descripción</th>
                                            <th className="px-3 py-2 text-right text-gray-500 font-medium uppercase">Solicitado</th>
                                            <th className="px-3 py-2 text-right text-gray-500 font-medium uppercase">Aplicado</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-navy-700">
                                        {(acreedor.empleados || []).map((emp, eIdx) => (
                                            <tr key={eIdx} className={`hover:bg-navy-800 ${emp.fueLimitado ? 'bg-orange-500/5' : ''}`}>
                                                <td className="px-3 py-2 text-gray-300">{emp.nombreEmpleado}</td>
                                                <td className="px-3 py-2 text-gray-400">{emp.cedula}</td>
                                                <td className="px-3 py-2 text-gray-400">{emp.tipoDeduccion}</td>
                                                <td className="px-3 py-2 text-gray-400">{emp.descripcion}</td>
                                                <td className="px-3 py-2 text-right font-mono text-gray-400">{formatCurrency(emp.montoSolicitado)}</td>
                                                <td className="px-3 py-2 text-right font-mono text-gray-100">
                                                    {formatCurrency(emp.montoAplicado)}
                                                    {emp.fueLimitado && <span className="ml-1 text-orange-400">⚠</span>}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        ))
                    )}
                </div>
            );
        }

        // Mensual
        if (modalType === 'mensual') {
            return (
                <div className="space-y-4">
                    {/* Períodos incluidos */}
                    {(reporteData.periodosIncluidos || []).length > 0 && (
                        <div className="p-3 bg-navy-800 rounded-lg">
                            <span className="text-xs text-gray-500 uppercase">Períodos incluidos: </span>
                            <span className="text-sm text-gray-300">{reporteData.periodosIncluidos.join(', ')}</span>
                        </div>
                    )}

                    <div className="overflow-x-auto">
                        <table className="w-full text-xs">
                            <thead className="bg-navy-800">
                                <tr>
                                    <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Cédula</th>
                                    <th className="px-3 py-2 text-left font-medium text-gray-500 uppercase">Nombre</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total Bruto</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total CSS</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total SE</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total ISR</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Acreed.</th>
                                    <th className="px-3 py-2 text-right font-medium text-gray-500 uppercase">Total Neto</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-navy-700">
                                {(reporteData.empleados || []).map((emp, idx) => (
                                    <tr key={idx} className="hover:bg-navy-800">
                                        <td className="px-3 py-2 text-gray-300">{emp.cedula}</td>
                                        <td className="px-3 py-2 text-gray-300">{emp.nombreCompleto}</td>
                                        <td className="px-3 py-2 text-right font-mono text-gray-100">{formatCurrency(emp.totalBruto)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.totalCss)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.totalSe)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.totalIsr)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-gray-300">{formatCurrency(emp.totalAcreedores)}</td>
                                        <td className="px-3 py-2 text-right font-bold font-mono text-green-400">{formatCurrency(emp.totalNeto)}</td>
                                    </tr>
                                ))}
                                {reporteData.empleados?.length === 0 && (
                                    <tr>
                                        <td colSpan="8" className="px-3 py-8 text-center text-gray-500">
                                            No hay planillas calculadas para {MESES.find(m => m.value === selMes)?.label} {selAnio}
                                        </td>
                                    </tr>
                                )}
                                {(reporteData.empleados || []).length > 0 && (
                                    <tr className="bg-amber-500/15 font-bold">
                                        <td className="px-3 py-2 text-amber-400" colSpan="2">
                                            TOTALES ({reporteData.totales?.totalEmpleados} empleados)
                                        </td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalBruto)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalCss)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalSe)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalIsr)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalAcreedores)}</td>
                                        <td className="px-3 py-2 text-right font-mono text-amber-400">{formatCurrency(reporteData.totales?.granTotalNeto)}</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            );
        }

        return <p className="text-gray-500">Sin datos disponibles.</p>;
    };

    // ── Card components ───────────────────────────────────────────────────

    const AccionBtn = ({ onClick, disabled, colorClass, icon, label }) => (
        <button
            onClick={onClick}
            disabled={disabled || loading}
            className={`flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-navy-800 border border-navy-700 rounded-lg text-xs font-medium text-gray-400 transition-all disabled:opacity-50 disabled:cursor-not-allowed ${colorClass}`}
        >
            {icon}
            {label}
        </button>
    );

    const iconVer = <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>;
    const iconExcel = <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>;
    const iconPdf = <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" /></svg>;

    return (
        <div>
            {/* Header */}
            <div className="mb-6">
                <h2 className="text-2xl font-display font-bold text-gray-100">Reportes de Planilla</h2>
                <p className="text-gray-400 mt-1">Cinco reportes operativos: planilla, SIP, acreedores, comprobantes y consolidado mensual</p>
            </div>

            {/* Grid 2 + 2 + 1 */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">

                {/* ── Card 1: Planilla Regular ── */}
                <div className="bg-navy-900 border border-navy-700 hover:border-primary-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
                    <div className="w-12 h-12 bg-gradient-to-br from-primary-600/20 to-primary-500/10 border border-primary-500/20 rounded-xl flex items-center justify-center mb-4">
                        <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                    </div>
                    <h3 className="text-base font-bold text-gray-100 font-display mb-1">Planilla Regular</h3>
                    <p className="text-sm text-gray-500 mb-4 leading-relaxed">Borrador operativo del período con horas, salarios, CSS/SE e importes de acreedores por empleado</p>
                    <PlanillaSelect value={selPlanillaRegular} onChange={setSelPlanillaRegular} />
                    <div className="flex items-center gap-2 pt-3 border-t border-navy-700">
                        <AccionBtn onClick={() => verReporte('planilla-regular')} disabled={!selPlanillaRegular} colorClass="hover:bg-primary-600 hover:border-primary-600 hover:text-white" icon={iconVer} label="Ver" />
                        <AccionBtn onClick={() => descargarExcel('planilla-regular')} disabled={!selPlanillaRegular} colorClass="hover:bg-emerald-600 hover:border-emerald-600 hover:text-white" icon={iconExcel} label="Excel" />
                        <AccionBtn onClick={() => descargarPdf('planilla-regular')} disabled={!selPlanillaRegular} colorClass="hover:bg-red-600 hover:border-red-600 hover:text-white" icon={iconPdf} label="PDF" />
                    </div>
                </div>

                {/* ── Card 2: SIP ── */}
                <div className="bg-navy-900 border border-navy-700 hover:border-blue-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
                    <div className="w-12 h-12 bg-gradient-to-br from-blue-600/20 to-blue-500/10 border border-blue-500/20 rounded-xl flex items-center justify-center mb-4">
                        <svg className="w-6 h-6 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                        </svg>
                    </div>
                    <h3 className="text-base font-bold text-gray-100 font-display mb-1">SIP — CSS Patronal</h3>
                    <p className="text-sm text-gray-500 mb-4 leading-relaxed">Datos exactos para ingresar en la plataforma CSS: base, CSS emp/pat, SE emp/pat y riesgo profesional</p>
                    <PlanillaSelect value={selSip} onChange={setSelSip} />
                    <div className="flex items-center gap-2 pt-3 border-t border-navy-700">
                        <AccionBtn onClick={() => verReporte('sip')} disabled={!selSip} colorClass="hover:bg-primary-600 hover:border-primary-600 hover:text-white" icon={iconVer} label="Ver" />
                        <AccionBtn onClick={() => descargarExcel('sip')} disabled={!selSip} colorClass="hover:bg-emerald-600 hover:border-emerald-600 hover:text-white" icon={iconExcel} label="Excel" />
                        <AccionBtn onClick={() => descargarPdf('sip')} disabled={!selSip} colorClass="hover:bg-red-600 hover:border-red-600 hover:text-white" icon={iconPdf} label="PDF" />
                    </div>
                </div>

                {/* ── Card 3: Acreedores ── */}
                <div className="bg-navy-900 border border-navy-700 hover:border-emerald-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
                    <div className="w-12 h-12 bg-gradient-to-br from-emerald-600/20 to-emerald-500/10 border border-emerald-500/20 rounded-xl flex items-center justify-center mb-4">
                        <svg className="w-6 h-6 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 14v3m4-3v3m4-3v3M3 21h18M3 10h18M3 7l9-4 9 4M4 10h16v11H4V10z" />
                        </svg>
                    </div>
                    <h3 className="text-base font-bold text-gray-100 font-display mb-1">Acreedores</h3>
                    <p className="text-sm text-gray-500 mb-4 leading-relaxed">Lista de transferencias bancarias agrupadas por beneficiario con datos de cuenta para contabilidad</p>
                    <PlanillaSelect value={selAcreedores} onChange={setSelAcreedores} />
                    <div className="flex items-center gap-2 pt-3 border-t border-navy-700">
                        <AccionBtn onClick={() => verReporte('acreedores')} disabled={!selAcreedores} colorClass="hover:bg-primary-600 hover:border-primary-600 hover:text-white" icon={iconVer} label="Ver" />
                        <AccionBtn onClick={() => descargarExcel('acreedores')} disabled={!selAcreedores} colorClass="hover:bg-emerald-600 hover:border-emerald-600 hover:text-white" icon={iconExcel} label="Excel" />
                        <AccionBtn onClick={() => descargarPdf('acreedores')} disabled={!selAcreedores} colorClass="hover:bg-red-600 hover:border-red-600 hover:text-white" icon={iconPdf} label="PDF" />
                    </div>
                </div>

                {/* ── Card 4: Comprobantes ── */}
                <div className="bg-navy-900 border border-navy-700 hover:border-amber-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
                    <div className="w-12 h-12 bg-gradient-to-br from-amber-600/20 to-amber-500/10 border border-amber-500/20 rounded-xl flex items-center justify-center mb-4">
                        <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                        </svg>
                    </div>
                    <h3 className="text-base font-bold text-gray-100 font-display mb-1">Comprobantes de Pago</h3>
                    <p className="text-sm text-gray-500 mb-4 leading-relaxed">Recibos de sueldo individuales por empleado en PDF — un comprobante por página, con reserva de décimo referencial</p>
                    <PlanillaSelect value={selComprobantes} onChange={setSelComprobantes} />
                    <div className="flex items-center gap-2 pt-3 border-t border-navy-700">
                        <div className="flex-1 flex items-center gap-1.5 px-3 py-2 bg-navy-800/50 border border-navy-700/50 rounded-lg text-xs text-gray-600 italic">
                            Solo PDF (formato recibo vertical)
                        </div>
                        <AccionBtn onClick={() => descargarPdf('comprobantes')} disabled={!selComprobantes} colorClass="hover:bg-red-600 hover:border-red-600 hover:text-white" icon={iconPdf} label="PDF" />
                    </div>
                </div>

                {/* ── Card 5: Mensual (ancho completo) ── */}
                <div className="md:col-span-2 bg-navy-900 border border-navy-700 hover:border-purple-500/40 rounded-xl p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30">
                    <div className="flex items-start gap-4">
                        <div className="w-12 h-12 bg-gradient-to-br from-purple-600/20 to-purple-500/10 border border-purple-500/20 rounded-xl flex items-center justify-center flex-shrink-0">
                            <svg className="w-6 h-6 text-purple-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                        </div>
                        <div className="flex-1">
                            <h3 className="text-base font-bold text-gray-100 font-display mb-1">Reporte Mensual</h3>
                            <p className="text-sm text-gray-500 mb-4 leading-relaxed">Consolida todas las planillas del mes seleccionado: suma brutos, deducciones CSS/SE/ISR, acreedores y neto por empleado</p>
                            <div className="flex items-end gap-4">
                                {/* Selectores mes + año */}
                                <div className="flex gap-3 flex-1">
                                    <div className="flex-1">
                                        <label className="block text-xs text-gray-500 mb-1">Mes</label>
                                        <select
                                            value={selMes}
                                            onChange={e => setSelMes(parseInt(e.target.value))}
                                            className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-purple-500 focus:border-purple-500 text-sm"
                                        >
                                            {MESES.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
                                        </select>
                                    </div>
                                    <div className="w-28">
                                        <label className="block text-xs text-gray-500 mb-1">Año</label>
                                        <select
                                            value={selAnio}
                                            onChange={e => setSelAnio(parseInt(e.target.value))}
                                            className="w-full px-3 py-2 border border-navy-600 bg-navy-800 text-gray-100 rounded-lg focus:ring-2 focus:ring-purple-500 focus:border-purple-500 text-sm"
                                        >
                                            {ANIOS.map(a => <option key={a} value={a}>{a}</option>)}
                                        </select>
                                    </div>
                                </div>
                                {/* Botones */}
                                <div className="flex items-center gap-2">
                                    <AccionBtn onClick={() => verReporte('mensual')} colorClass="hover:bg-primary-600 hover:border-primary-600 hover:text-white" icon={iconVer} label="Ver" />
                                    <AccionBtn onClick={() => descargarExcel('mensual')} colorClass="hover:bg-emerald-600 hover:border-emerald-600 hover:text-white" icon={iconExcel} label="Excel" />
                                    <AccionBtn onClick={() => descargarPdf('mensual')} colorClass="hover:bg-red-600 hover:border-red-600 hover:text-white" icon={iconPdf} label="PDF" />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

            </div>

            {/* Modal */}
            {modalOpen && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4" onClick={() => setModalOpen(false)}>
                    <div className="bg-navy-900 rounded-2xl shadow-2xl shadow-black/30 max-w-6xl w-full max-h-[90vh] flex flex-col" onClick={e => e.stopPropagation()}>
                        {/* Header */}
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between">
                            <h3 className="text-xl font-display font-bold text-gray-100">{modalTitle}</h3>
                            <button onClick={() => setModalOpen(false)} className="text-gray-400 hover:text-gray-200">
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        {/* Subheader info */}
                        <div className="px-6 py-3 bg-navy-800 border-b border-navy-700 flex flex-wrap gap-4 text-sm">
                            <div>
                                <span className="text-gray-500">Empresa:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.nombreEmpresa}</span>
                            </div>
                            <div>
                                <span className="text-gray-500">RUC:</span>
                                <span className="ml-2 font-medium text-gray-300">{reporteData?.ruc}</span>
                            </div>
                            {reporteData?.periodo && (
                                <div>
                                    <span className="text-gray-500">Período:</span>
                                    <span className="ml-2 font-medium text-gray-300">{reporteData.periodo}</span>
                                </div>
                            )}
                            {reporteData?.nombreMes && (
                                <div>
                                    <span className="text-gray-500">Mes:</span>
                                    <span className="ml-2 font-medium text-gray-300">{reporteData.nombreMes} {reporteData.anio}</span>
                                </div>
                            )}
                        </div>

                        {/* Body */}
                        <div className="flex-1 overflow-auto p-6">
                            {renderModalContent()}
                        </div>

                        {/* Footer */}
                        <div className="px-6 py-4 border-t border-navy-700 flex justify-end gap-3">
                            {modalType !== 'comprobantes' && modalType !== 'acreedores-pdf-only' && (
                                <button
                                    onClick={() => descargarExcel(modalType)}
                                    disabled={loading}
                                    className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 flex items-center gap-2 disabled:opacity-50"
                                >
                                    {iconExcel} Descargar Excel
                                </button>
                            )}
                            <button
                                onClick={() => descargarPdf(modalType)}
                                disabled={loading}
                                className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 flex items-center gap-2 disabled:opacity-50"
                            >
                                {iconPdf} Descargar PDF
                            </button>
                        </div>
                    </div>
                </div>,
                document.body
            )}
        </div>
    );
};

export default ReportesPage;
