import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../contexts/AuthContext';
import { UsageDashboard } from '../components/UsageDashboard';
import { TenantRole } from '../types/api';

const ConfiguracionPage = () => {
    const { hasRole } = useAuth();
    const [activeTab, setActiveTab] = useState('tasas');

    // Filtrar tabs según rol del usuario
    // Validación defensiva: si hasRole es undefined, visible será false por defecto
    const allTabs = [
        { id: 'tasas', label: 'Tasas CSS/SE', icon: 'M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z', visible: true },
        { id: 'isr', label: 'Tabla ISR', icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z', visible: true },
        { id: 'audit', label: 'Audit Log', icon: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z', visible: hasRole ? hasRole(TenantRole.Owner) : false },
        { id: 'plan', label: 'Uso del Plan', icon: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z', visible: true },
        { id: 'soporte', label: 'Soporte', icon: 'M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192l-3.536 3.536M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-5 0a4 4 0 11-8 0 4 4 0 018 0z', visible: true }
    ];

    const tabs = allTabs.filter(tab => tab.visible);

    return (
        <div className="space-y-6">
            {/* Tab Navigation */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="border-b border-navy-700">
                    <nav className="flex -mb-px">
                        {tabs.map(tab => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id)}
                                className={`group relative min-w-0 flex-1 overflow-hidden py-4 px-4 text-sm font-medium text-center hover:bg-navy-800 focus:z-10 ${
                                    activeTab === tab.id
                                        ? 'text-primary-400 border-b-2 border-primary-500'
                                        : 'text-gray-500 hover:text-gray-200'
                                }`}
                            >
                                <div className="flex items-center justify-center gap-2">
                                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d={tab.icon} />
                                    </svg>
                                    <span>{tab.label}</span>
                                </div>
                            </button>
                        ))}
                    </nav>
                </div>

                {/* Tab Content */}
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

                            <div className="overflow-x-auto">
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
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100">CSS (Caja de Seguro Social)</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                                    9.75%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-500/15 text-blue-400">
                                                    14.25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Topes escalonados según salario</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100">Seguro Educativo</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-500/15 text-purple-400">
                                                    1.25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-500/15 text-purple-400">
                                                    1.50%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Sin tope máximo</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100">Riesgo Profesional</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-navy-700 text-gray-300">
                                                    -
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-500/15 text-amber-400">
                                                    0.56% - 5.39%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Según tipo de actividad</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div className="mt-6 p-4 bg-navy-950 rounded-lg border border-navy-700">
                                <h4 className="text-sm font-semibold text-gray-100 mb-2">Notas Importantes:</h4>
                                <ul className="text-sm text-gray-400 space-y-1 list-disc list-inside">
                                    <li>Las tasas de CSS tienen topes escalonados: B/. 1,500 / 2,000 / 2,500 según salario</li>
                                    <li>El Seguro Educativo aplica sobre el salario total sin tope máximo</li>
                                    <li>El Riesgo Profesional varía según la actividad económica de la empresa</li>
                                </ul>
                            </div>
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
                                    <span className="text-sm text-green-400 font-medium">Según DGI Panamá - Año fiscal 2025</span>
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
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">$0 - $11,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-500/15 text-green-400">
                                                    Exento (0%)
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">No paga impuesto</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">$11,001 - $50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-amber-500/15 text-amber-400">
                                                    15%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">Sobre el exceso de $11,000</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-100 font-mono">Más de $50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-red-500/15 text-red-400">
                                                    25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-400">Sobre el exceso de $50,000</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="p-4 bg-primary-500/10 rounded-lg border border-primary-500">
                                    <h4 className="text-sm font-semibold text-primary-300 mb-2">Ejemplo de Cálculo:</h4>
                                    <div className="text-sm text-primary-400 space-y-1">
                                        <p>Salario anual: <strong className="font-mono">$30,000</strong></p>
                                        <p>Exento: <span className="font-mono">$11,000</span> (0%)</p>
                                        <p>Gravable: <span className="font-mono">$19,000</span> × 15% = <strong className="font-mono">$2,850</strong></p>
                                        <p className="pt-2 border-t border-primary-400">ISR anual total: <strong className="font-mono">$2,850</strong></p>
                                    </div>
                                </div>

                                <div className="p-4 bg-navy-950 rounded-lg border border-navy-700">
                                    <h4 className="text-sm font-semibold text-gray-100 mb-2">Deducciones Permitidas:</h4>
                                    <ul className="text-sm text-gray-400 space-y-1 list-disc list-inside">
                                        <li>Gastos educativos: <span className="font-mono">hasta $5,000/año</span></li>
                                        <li>Intereses hipotecarios: <span className="font-mono">hasta $15,000/año</span></li>
                                        <li>Dependientes: <span className="font-mono">$800/año</span> por dependiente</li>
                                        <li>Aportes jubilatorios voluntarios</li>
                                    </ul>
                                </div>
                            </div>
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

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                {/* Email de Soporte */}
                                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
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
                                        href="mailto:soporte@pagly.app"
                                        className="inline-flex items-center px-4 py-2 bg-primary-500/10 hover:bg-primary-500/15 text-primary-300 font-medium rounded-lg transition-colors"
                                    >
                                        soporte@pagly.app
                                    </a>
                                </div>

                                {/* Sitio Web */}
                                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
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
                                        href="https://pagly.app"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="inline-flex items-center px-4 py-2 bg-green-500/15 hover:bg-green-500/20 text-green-300 font-medium rounded-lg transition-colors"
                                    >
                                        pagly.app
                                        <svg className="w-4 h-4 ml-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                                        </svg>
                                    </a>
                                </div>
                            </div>

                            {/* Recursos Adicionales */}
                            <div className="mt-6 p-6 bg-gradient-to-r from-primary-500/10 to-purple-500/10 rounded-xl border border-primary-500">
                                <h4 className="text-sm font-semibold text-gray-100 mb-3">Recursos Útiles</h4>
                                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                    <div className="bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20">
                                        <h5 className="font-medium text-gray-100 mb-1">Documentación</h5>
                                        <p className="text-xs text-gray-400">Guías y tutoriales</p>
                                    </div>
                                    <div className="bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20">
                                        <h5 className="font-medium text-gray-100 mb-1">FAQ</h5>
                                        <p className="text-xs text-gray-400">Preguntas frecuentes</p>
                                    </div>
                                    <div className="bg-navy-900 p-4 rounded-lg shadow-lg shadow-black/20">
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
