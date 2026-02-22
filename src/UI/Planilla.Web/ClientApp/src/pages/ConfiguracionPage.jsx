import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../contexts/AuthContext';
import { UsageDashboard } from '../components/UsageDashboard';
import { TenantRole } from '../types/api';
import { api } from '../services/api';
import { Save, Building2, Loader2 } from 'lucide-react';

const ConfiguracionPage = () => {
    const { hasRole } = useAuth();
    const [activeTab, setActiveTab] = useState('tasas');
    const [taxConfig, setTaxConfig] = useState(null);
    const [taxConfigLoading, setTaxConfigLoading] = useState(false);
    const [ensureTaxConfigLoading, setEnsureTaxConfigLoading] = useState(false);

    // Salario Mínimo state
    const [salarioMinimo, setSalarioMinimo] = useState('');
    const [actividadEconomica, setActividadEconomica] = useState('');
    const [salarioMinimoLoading, setSalarioMinimoLoading] = useState(false);
    const [salarioMinimoSaving, setSalarioMinimoSaving] = useState(false);

    const fetchTaxConfig = async () => {
        setTaxConfigLoading(true);
        try {
            const data = await api.get('/api/configuracion/tax-config');
            setTaxConfig(data);
        } catch (err) {
            if (err.statusCode === 404) setTaxConfig(null);
            else toast.error(err.message || 'Error al cargar configuración');
        } finally {
            setTaxConfigLoading(false);
        }
    };

    const fetchSalarioMinimo = async () => {
        setSalarioMinimoLoading(true);
        try {
            const data = await api.get('/api/configuracion/salario-minimo');
            setSalarioMinimo(data.salarioMinimoLegal?.toString() || '700');
            setActividadEconomica(data.actividadEconomica || '');
        } catch (err) {
            if (err.statusCode === 404) {
                toast.error('No existe configuración de planilla. Cree una primero en la pestaña Tasas CSS/SE.');
            } else {
                toast.error(err.message || 'Error al cargar salario mínimo');
            }
        } finally {
            setSalarioMinimoLoading(false);
        }
    };

    const handleSaveSalarioMinimo = async (e) => {
        e.preventDefault();
        const valor = parseFloat(salarioMinimo);
        if (isNaN(valor) || valor <= 0) {
            toast.error('El salario mínimo debe ser mayor a cero');
            return;
        }
        try {
            setSalarioMinimoSaving(true);
            await api.put('/api/configuracion/salario-minimo', {
                salarioMinimoLegal: valor,
                actividadEconomica: actividadEconomica || null,
            });
            toast.success('Salario mínimo actualizado correctamente');
        } catch (err) {
            toast.error(err.message || 'Error al guardar');
        } finally {
            setSalarioMinimoSaving(false);
        }
    };

    useEffect(() => {
        if (activeTab === 'tasas') fetchTaxConfig();
        if (activeTab === 'salario-minimo') fetchSalarioMinimo();
    }, [activeTab]);

    const handleEnsureTaxConfig = async () => {
        setEnsureTaxConfigLoading(true);
        try {
            await api.post('/api/configuracion/ensure-tax-config');
            toast.success('Configuración creada correctamente');
            await fetchTaxConfig();
        } catch (err) {
            toast.error(err.message || 'Error al crear configuración');
        } finally {
            setEnsureTaxConfigLoading(false);
        }
    };

    // Filtrar tabs según rol del usuario
    // Validación defensiva: si hasRole es undefined, visible será false por defecto
    const allTabs = [
        { id: 'tasas', label: 'Tasas CSS/SE', icon: 'M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z', visible: true },
        { id: 'isr', label: 'Tabla ISR', icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z', visible: true },
        { id: 'salario-minimo', label: 'Salario Mínimo', icon: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z', visible: true },
        { id: 'audit', label: 'Audit Log', icon: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z', visible: hasRole ? hasRole(TenantRole.Owner) : false },
        { id: 'plan', label: 'Uso del Plan', icon: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z', visible: true },
        { id: 'soporte', label: 'Soporte', icon: 'M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192l-3.536 3.536M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-5 0a4 4 0 11-8 0 4 4 0 018 0z', visible: true }
    ];

    const tabs = allTabs.filter(tab => tab.visible);

    return (
        <div className="space-y-6">
            {/* Tab Navigation — estilo pill */}
            <div className="flex gap-1 p-1 bg-navy-800 rounded-xl border border-navy-700 w-fit flex-wrap">
                {tabs.map(tab => (
                    <button
                        key={tab.id}
                        onClick={() => setActiveTab(tab.id)}
                        className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                            activeTab === tab.id
                                ? 'bg-primary-600 text-white shadow-md'
                                : 'text-gray-400 hover:text-gray-200 hover:bg-navy-700'
                        }`}
                    >
                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d={tab.icon} />
                        </svg>
                        <span>{tab.label}</span>
                    </button>
                ))}
            </div>

            {/* Tab Content */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="p-6">
                    {/* Tab: Tasas CSS/SE */}
                    {activeTab === 'tasas' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-4">Tasas de CSS y Seguro Educativo</h3>
                            <div className="mb-4">
                                <div className="inline-flex items-center px-3 py-1.5 bg-primary-500/10 border border-primary-500 rounded-lg">
                                    <svg className="w-4 h-4 text-primary-400 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span className="text-sm text-primary-400 font-medium">Tasas según Ley 462 de Panamá</span>
                                </div>
                            </div>

                            {taxConfigLoading ? (
                                <div className="flex items-center justify-center py-12">
                                    <div className="w-8 h-8 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" />
                                </div>
                            ) : !taxConfig ? (
                                <div className="py-8 px-6 bg-navy-950 rounded-xl border border-amber-500/30 text-center">
                                    <p className="text-gray-300 mb-2">No hay configuración de impuestos (CSS, SE, ISR) para tu empresa.</p>
                                    <p className="text-sm text-gray-500 mb-6">Sin esta configuración no podrás calcular planillas. Crea una con los valores por defecto de la Ley 462.</p>
                                    <button
                                        type="button"
                                        onClick={handleEnsureTaxConfig}
                                        disabled={ensureTaxConfigLoading}
                                        className="inline-flex items-center gap-2 bg-emerald-600 hover:bg-emerald-700 text-white px-5 py-2.5 rounded-lg font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                    >
                                        {ensureTaxConfigLoading ? (
                                            <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                                        ) : (
                                            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                                            </svg>
                                        )}
                                        Crear configuración por defecto (Ley 462)
                                    </button>
                                </div>
                            ) : (
                                <>
                                    <div className="overflow-x-auto">
                                        {/* Encabezado CSS — acento azul */}
                                        <div className="flex items-center gap-2 mb-3">
                                            <div className="w-2 h-6 rounded-full bg-blue-500"/>
                                            <h4 className="text-sm font-bold text-blue-400 uppercase tracking-wider">CSS — Caja de Seguro Social</h4>
                                        </div>
                                        <table className="w-full mb-6">
                                            <thead className="bg-navy-950 border-b border-navy-700">
                                                <tr>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Concepto</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Empleado</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Patrono</th>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Observaciones</th>
                                                </tr>
                                            </thead>
                                            <tbody className="bg-navy-900 divide-y divide-navy-700">
                                                <tr>
                                                    <td className="py-3 px-4 text-sm font-medium text-gray-100">CSS (Caja de Seguro Social)</td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                                            {Number(taxConfig.cssEmployeeRate).toFixed(2)}%
                                                        </span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                                            {Number(taxConfig.cssEmployerBaseRate).toFixed(2)}%
                                                        </span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-gray-500">Topes: B/.{Number(taxConfig.cssMaxContributionBaseStandard).toFixed(0)} / B/.{Number(taxConfig.cssMaxContributionBaseIntermediate).toFixed(0)} / B/.{Number(taxConfig.cssMaxContributionBaseHigh).toFixed(0)}</td>
                                                </tr>
                                            </tbody>
                                        </table>

                                        {/* Encabezado Seguro Educativo — acento violeta */}
                                        <div className="flex items-center gap-2 mb-3">
                                            <div className="w-2 h-6 rounded-full bg-purple-500"/>
                                            <h4 className="text-sm font-bold text-purple-400 uppercase tracking-wider">SE — Seguro Educativo</h4>
                                        </div>
                                        <table className="w-full mb-6">
                                            <thead className="bg-navy-950 border-b border-navy-700">
                                                <tr>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Concepto</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Empleado</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Patrono</th>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Observaciones</th>
                                                </tr>
                                            </thead>
                                            <tbody className="bg-navy-900 divide-y divide-navy-700">
                                                <tr>
                                                    <td className="py-3 px-4 text-sm font-medium text-gray-100">Seguro Educativo</td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-500/15 text-purple-400">
                                                            {Number(taxConfig.educationalInsuranceEmployeeRate).toFixed(2)}%
                                                        </span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-500/15 text-purple-400">
                                                            {Number(taxConfig.educationalInsuranceEmployerRate).toFixed(2)}%
                                                        </span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-gray-500">Sin tope máximo</td>
                                                </tr>
                                            </tbody>
                                        </table>

                                        {/* Encabezado Riesgos Profesionales — acento naranja */}
                                        <div className="flex items-center gap-2 mb-3">
                                            <div className="w-2 h-6 rounded-full bg-amber-500"/>
                                            <h4 className="text-sm font-bold text-amber-400 uppercase tracking-wider">RP — Riesgos Profesionales</h4>
                                        </div>
                                        <table className="w-full">
                                            <thead className="bg-navy-950 border-b border-navy-700">
                                                <tr>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Concepto</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Empleado</th>
                                                    <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa Patrono</th>
                                                    <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Observaciones</th>
                                                </tr>
                                            </thead>
                                            <tbody className="bg-navy-900 divide-y divide-navy-700">
                                                <tr>
                                                    <td className="py-3 px-4 text-sm font-medium text-gray-100">Riesgo Profesional</td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-navy-700 text-gray-300">-</span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-center">
                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-500/15 text-amber-400">
                                                            {Number(taxConfig.cssRiskRateLow).toFixed(2)}% - {Number(taxConfig.cssRiskRateHigh).toFixed(2)}%
                                                        </span>
                                                    </td>
                                                    <td className="py-3 px-4 text-sm text-gray-500">Según tipo de actividad</td>
                                                </tr>
                                            </tbody>
                                        </table>
                                    </div>
                                    <div className="mt-4 text-xs text-gray-500">
                                        Vigente desde {new Date(taxConfig.effectiveStartDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}
                                        {taxConfig.effectiveEndDate ? ` hasta ${new Date(taxConfig.effectiveEndDate).toLocaleDateString('es-PA', { day: '2-digit', month: 'short', year: 'numeric' })}` : ' (actual)'}.
                                    </div>
                                    <div className="mt-6 p-4 bg-navy-950 rounded-lg border border-navy-700">
                                        <h4 className="text-sm font-semibold text-gray-100 mb-2">Notas:</h4>
                                        <ul className="text-sm text-gray-400 space-y-1 list-disc list-inside">
                                            <li>Topes CSS según años cotizados y salario promedio (configuración actual en uso)</li>
                                            <li>Deducción ISR por dependiente: B/.{Number(taxConfig.dependentDeductionAmount).toFixed(0)} (máx. {taxConfig.maxDependents} dependientes)</li>
                                        </ul>
                                    </div>
                                </>
                            )}
                        </div>
                    )}

                    {/* Tab: Tabla ISR */}
                    {activeTab === 'isr' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-4">Tabla de Impuesto Sobre la Renta (ISR)</h3>
                            <div className="mb-4">
                                <div className="inline-flex items-center px-3 py-1.5 bg-green-500/15 border border-green-400 rounded-lg">
                                    <svg className="w-4 h-4 text-green-400 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span className="text-sm text-green-400 font-medium">Según DGI Panamá - Año fiscal {new Date().getFullYear()}</span>
                                </div>
                            </div>

                            <div className="overflow-x-auto">
                                <table className="w-full">
                                    <thead className="bg-navy-950 border-b border-navy-700">
                                        <tr>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Rango de Ingreso Anual</th>
                                            <th className="text-center py-3 px-4 text-sm font-medium text-gray-300">Tasa</th>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-300">Descripción</th>
                                        </tr>
                                    </thead>
                                    <tbody className="bg-navy-900 divide-y divide-navy-700">
                                        <tr className="bg-green-500/5">
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">B/.0 - B/.11,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-500/15 text-green-400">
                                                    Exento (0%)
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">No paga impuesto</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">B/.11,001 - B/.50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-amber-500/15 text-amber-400">
                                                    15%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">Sobre el exceso de B/.11,000</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">Más de B/.50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-red-500/15 text-red-400">
                                                    25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">Sobre el exceso de B/.50,000</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="p-4 bg-primary-500/10 rounded-lg border border-primary-500">
                                    <h4 className="text-sm font-semibold text-primary-300 mb-2">Ejemplo de Cálculo:</h4>
                                    <div className="text-sm text-primary-400 space-y-1">
                                        <p>Salario anual: <strong className="font-mono">B/.30,000</strong></p>
                                        <p>Exento: <span className="font-mono">B/.11,000</span> (0%)</p>
                                        <p>Gravable: <span className="font-mono">B/.19,000</span> × 15% = <strong className="font-mono">B/.2,850</strong></p>
                                        <p className="pt-2 border-t border-primary-400">ISR anual total: <strong className="font-mono">B/.2,850</strong></p>
                                    </div>
                                </div>

                                <div className="p-4 bg-navy-950 rounded-lg border border-navy-700">
                                    <h4 className="text-sm font-semibold text-gray-100 mb-2">Deducciones Permitidas:</h4>
                                    <ul className="text-sm text-gray-400 space-y-1 list-disc list-inside">
                                        <li>Gastos educativos: <span className="font-mono">hasta B/.5,000/año</span></li>
                                        <li>Intereses hipotecarios: <span className="font-mono">hasta B/.15,000/año</span></li>
                                        <li>Dependientes: <span className="font-mono">B/.800/año</span> por dependiente</li>
                                        <li>Aportes jubilatorios voluntarios</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Tab: Salario Mínimo */}
                    {activeTab === 'salario-minimo' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-2">Salario Mínimo Legal</h3>
                            <p className="text-sm text-gray-400 mb-6">
                                Configure el salario mínimo legal vigente. Este valor protege el salario mínimo inembargable al aplicar deducciones.
                            </p>

                            {salarioMinimoLoading ? (
                                <div className="flex items-center justify-center py-12">
                                    <Loader2 className="w-8 h-8 animate-spin text-primary-500" />
                                </div>
                            ) : (
                                <form onSubmit={handleSaveSalarioMinimo} className="max-w-lg space-y-5">
                                    <div>
                                        <label htmlFor="salarioMinimo" className="block text-sm font-medium text-gray-300 mb-1">
                                            Salario Mínimo Mensual (B/.)
                                        </label>
                                        <div className="relative rounded-lg">
                                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                                <span className="text-gray-500 text-sm font-medium">B/.</span>
                                            </div>
                                            <input
                                                type="number"
                                                id="salarioMinimo"
                                                step="0.01"
                                                min="0.01"
                                                value={salarioMinimo}
                                                onChange={(e) => setSalarioMinimo(e.target.value)}
                                                className="block w-full pl-10 pr-4 py-2.5 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-sm"
                                                placeholder="700.00"
                                                required
                                            />
                                        </div>
                                        <p className="mt-1 text-xs text-gray-500">
                                            Salario mínimo legal mensual vigente según actividad económica (Decreto Ejecutivo).
                                        </p>
                                    </div>

                                    <div>
                                        <label htmlFor="actividadEconomica" className="block text-sm font-medium text-gray-300 mb-1">
                                            Actividad Económica
                                        </label>
                                        <div className="relative rounded-lg">
                                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                                <Building2 className="h-5 w-5 text-gray-500" />
                                            </div>
                                            <input
                                                type="text"
                                                id="actividadEconomica"
                                                value={actividadEconomica}
                                                onChange={(e) => setActividadEconomica(e.target.value)}
                                                className="block w-full pl-10 pr-4 py-2.5 bg-navy-800 border border-navy-600 text-gray-100 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-sm"
                                                placeholder="Ej: Comercio al por menor"
                                                maxLength={300}
                                            />
                                        </div>
                                        <p className="mt-1 text-xs text-gray-500">
                                            Descripción de la actividad económica principal de la empresa (opcional).
                                        </p>
                                    </div>

                                    <div className="p-4 bg-amber-500/10 border border-amber-500/30 rounded-lg">
                                        <h4 className="text-sm font-semibold text-amber-300 mb-2">Importante</h4>
                                        <ul className="text-xs text-amber-400/80 list-disc list-inside space-y-1">
                                            <li>El salario mínimo se usa para calcular el piso inembargable en deducciones.</li>
                                            <li>Las pensiones alimenticias por orden judicial SI pueden reducir por debajo del salario mínimo.</li>
                                            <li>Los embargos judiciales y deducciones voluntarias solo aplican sobre el excedente.</li>
                                            <li>El monto se prorratea automáticamente según el período de pago.</li>
                                        </ul>
                                    </div>

                                    <div className="flex justify-end">
                                        <button
                                            type="submit"
                                            disabled={salarioMinimoSaving}
                                            className="inline-flex items-center gap-2 px-5 py-2.5 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                        >
                                            {salarioMinimoSaving ? (
                                                <Loader2 className="w-4 h-4 animate-spin" />
                                            ) : (
                                                <Save className="w-4 h-4" />
                                            )}
                                            Guardar
                                        </button>
                                    </div>
                                </form>
                            )}
                        </div>
                    )}

                    {/* Tab: Audit Log */}
                    {activeTab === 'audit' && hasRole(TenantRole.Owner) && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-4">Registro de Actividades (Audit Log)</h3>
                            <p className="text-sm text-gray-400 mb-6">
                                Consulta el historial completo de acciones realizadas en el sistema para auditoría y seguridad.
                            </p>

                            <Link
                                to="/audit"
                                className="inline-flex items-center px-6 py-3 bg-purple-600 hover:bg-purple-700 text-white font-medium rounded-lg shadow-lg shadow-black/20 transition-colors"
                            >
                                <svg className="w-5 h-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                </svg>
                                Ver Audit Log
                            </Link>

                            <div className="mt-8 p-4 bg-purple-500/15 rounded-lg border border-purple-400">
                                <h4 className="text-sm font-semibold text-purple-300 mb-2">Eventos Registrados:</h4>
                                <ul className="text-sm text-purple-400 space-y-1 list-disc list-inside">
                                    <li>Creación, modificación y eliminación de empleados</li>
                                    <li>Cálculo y aprobación de planillas</li>
                                    <li>Cambios en configuración de la empresa</li>
                                    <li>Inicio y cierre de sesión de usuarios</li>
                                    <li>Invitaciones enviadas y aceptadas</li>
                                </ul>
                            </div>
                        </div>
                    )}

                    {/* Tab: Uso del Plan */}
                    {activeTab === 'plan' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-4">Uso del Plan de Suscripción</h3>
                            <p className="text-sm text-gray-400 mb-6">
                                Monitorea el uso de tu plan actual y conoce los límites disponibles para tu empresa.
                            </p>

                            <UsageDashboard />
                        </div>
                    )}

                    {/* Tab: Soporte */}
                    {activeTab === 'soporte' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-100 mb-4">Soporte y Contacto</h3>
                            <p className="text-sm text-gray-400 mb-6">
                                ¿Necesitas ayuda? Estamos aquí para asistirte con cualquier duda o problema técnico.
                            </p>

                            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                                {/* Email de Soporte */}
                                <div className="group bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 p-6 hover:-translate-y-0.5 transition-all cursor-pointer">
                                    <div className="flex items-center gap-4 mb-4">
                                        <div className="w-12 h-12 bg-primary-500/10 rounded-lg flex items-center justify-center">
                                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h4 className="font-semibold text-gray-100">Email de Soporte</h4>
                                            <p className="text-sm text-gray-500">Respuesta en 24 horas</p>
                                        </div>
                                    </div>
                                    <a
                                        href="mailto:contacto@vorluno.dev"
                                        className="inline-flex items-center px-4 py-2 bg-primary-500/10 hover:bg-primary-500/15 text-primary-300 font-medium rounded-lg transition-colors"
                                    >
                                        contacto@vorluno.dev
                                    </a>
                                </div>

                                {/* Sitio Web */}
                                <div className="group bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 p-6 hover:-translate-y-0.5 transition-all cursor-pointer">
                                    <div className="flex items-center gap-4 mb-4">
                                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h4 className="font-semibold text-gray-100">Sitio Web</h4>
                                            <p className="text-sm text-gray-500">Conoce más sobre nosotros</p>
                                        </div>
                                    </div>
                                    <a
                                        href="https://vorluno.dev"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="inline-flex items-center px-4 py-2 bg-green-500/15 hover:bg-green-500/20 text-green-300 font-medium rounded-lg transition-colors"
                                    >
                                        vorluno.dev
                                        <svg className="w-4 h-4 ml-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                                        </svg>
                                    </a>
                                </div>

                                {/* Conecta en LinkedIn */}
                                <div className="group bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 p-6 hover:-translate-y-0.5 transition-all cursor-pointer">
                                    <div className="flex items-center gap-4 mb-4">
                                        <div className="w-12 h-12 bg-[#0a66c2]/20 rounded-lg flex items-center justify-center">
                                            <svg className="w-6 h-6 text-[#0a66c2]" viewBox="0 0 24 24" fill="currentColor">
                                                <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z" />
                                            </svg>
                                        </div>
                                        <div>
                                            <h4 className="font-semibold text-gray-100">Conecta en LinkedIn</h4>
                                            <p className="text-sm text-gray-500">Sígueme para novedades y soporte</p>
                                        </div>
                                    </div>
                                    <div
                                        className="badge-base LI-profile-badge inline-block"
                                        data-locale="es_ES"
                                        data-size="medium"
                                        data-theme="dark"
                                        data-type="VERTICAL"
                                        data-vanity="josé-gonzález-323b74248"
                                        data-version="v1"
                                    >
                                        <a
                                            className="badge-base__link LI-simple-link"
                                            href="https://pa.linkedin.com/in/jos%C3%A9-gonz%C3%A1lez-323b74248?trk=profile-badge"
                                            target="_blank"
                                            rel="noopener noreferrer"
                                        >
                                            José González
                                        </a>
                                    </div>
                                </div>
                            </div>

                            {/* Recursos Adicionales */}
                            <div className="mt-6 p-6 bg-gradient-to-r from-primary-500/10 to-purple-500/10 rounded-xl border border-primary-500">
                                <h4 className="text-sm font-semibold text-gray-100 mb-3">Recursos Útiles</h4>
                                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                    <div className="group bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 hover:-translate-y-0.5 transition-all cursor-pointer">
                                        <h5 className="font-medium text-gray-100 mb-1">Documentación</h5>
                                        <p className="text-xs text-gray-400">Guías y tutoriales</p>
                                    </div>
                                    <div className="group bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 hover:-translate-y-0.5 transition-all cursor-pointer">
                                        <h5 className="font-medium text-gray-100 mb-1">FAQ</h5>
                                        <p className="text-xs text-gray-400">Preguntas frecuentes</p>
                                    </div>
                                    <div className="group bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20 border border-navy-700 hover:border-primary-500/40 hover:-translate-y-0.5 transition-all cursor-pointer">
                                        <h5 className="font-medium text-gray-100 mb-1">Actualizaciones</h5>
                                        <p className="text-xs text-gray-400">Nuevas funciones</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                </div>
            </div>
        </div>
    );
};

export default ConfiguracionPage;
