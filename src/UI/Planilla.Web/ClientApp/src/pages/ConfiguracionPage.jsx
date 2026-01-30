import React, { useState } from 'react';
import toast from 'react-hot-toast';

const ConfiguracionPage = () => {
    const [activeTab, setActiveTab] = useState('empresa');
    const [companyData, setCompanyData] = useState({
        nombreEmpresa: 'Empresa Demo S.A.',
        ruc: '1234567-8-123456',
        direccion: 'Calle Principal, Ciudad de Panamá',
        telefono: '+507 6000-0000',
        email: 'contacto@empresademo.com'
    });

    const tabs = [
        { id: 'empresa', label: 'Empresa', icon: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4' },
        { id: 'tasas', label: 'Tasas CSS/SE', icon: 'M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z' },
        { id: 'isr', label: 'Tabla ISR', icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z' },
        { id: 'usuarios', label: 'Usuarios', icon: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z' }
    ];

    const handleSaveCompany = (e) => {
        e.preventDefault();
        toast('Funcionalidad de guardado próximamente disponible', { icon: 'ℹ️' });
    };

    return (
        <div className="space-y-6">
            {/* Tab Navigation */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="border-b border-gray-200">
                    <nav className="flex -mb-px">
                        {tabs.map(tab => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id)}
                                className={`group relative min-w-0 flex-1 overflow-hidden py-4 px-4 text-sm font-medium text-center hover:bg-gray-50 focus:z-10 ${
                                    activeTab === tab.id
                                        ? 'text-blue-600 border-b-2 border-blue-600'
                                        : 'text-gray-500 hover:text-gray-700'
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
                    {/* Tab: Empresa */}
                    {activeTab === 'empresa' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-900 mb-4">Datos de la Empresa</h3>
                            <form onSubmit={handleSaveCompany} className="space-y-4">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Nombre de la Empresa
                                        </label>
                                        <input
                                            type="text"
                                            value={companyData.nombreEmpresa}
                                            onChange={(e) => setCompanyData({ ...companyData, nombreEmpresa: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            RUC
                                        </label>
                                        <input
                                            type="text"
                                            value={companyData.ruc}
                                            onChange={(e) => setCompanyData({ ...companyData, ruc: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                            placeholder="1234567-8-123456"
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Teléfono
                                        </label>
                                        <input
                                            type="tel"
                                            value={companyData.telefono}
                                            onChange={(e) => setCompanyData({ ...companyData, telefono: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                            placeholder="+507 6000-0000"
                                        />
                                    </div>

                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Email
                                        </label>
                                        <input
                                            type="email"
                                            value={companyData.email}
                                            onChange={(e) => setCompanyData({ ...companyData, email: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                            placeholder="contacto@empresa.com"
                                        />
                                    </div>

                                    <div className="md:col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-2">
                                            Dirección
                                        </label>
                                        <textarea
                                            rows="3"
                                            value={companyData.direccion}
                                            onChange={(e) => setCompanyData({ ...companyData, direccion: e.target.value })}
                                            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                            placeholder="Calle Principal, Ciudad de Panamá"
                                        />
                                    </div>
                                </div>

                                <div className="flex justify-end pt-4">
                                    <button
                                        type="submit"
                                        className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors shadow-sm"
                                    >
                                        Guardar Cambios
                                    </button>
                                </div>
                            </form>
                        </div>
                    )}

                    {/* Tab: Tasas CSS/SE */}
                    {activeTab === 'tasas' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-900 mb-4">Tasas de CSS y Seguro Educativo</h3>
                            <div className="mb-4">
                                <div className="inline-flex items-center px-3 py-1.5 bg-blue-50 border border-blue-200 rounded-lg">
                                    <svg className="w-4 h-4 text-blue-600 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span className="text-sm text-blue-800 font-medium">Tasas según Ley 462 de Panamá</span>
                                </div>
                            </div>

                            <div className="overflow-x-auto">
                                <table className="w-full">
                                    <thead className="bg-gray-50 border-b border-gray-200">
                                        <tr>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-700">Concepto</th>
                                            <th className="text-center py-3 px-4 text-sm font-medium text-gray-700">Tasa Empleado</th>
                                            <th className="text-center py-3 px-4 text-sm font-medium text-gray-700">Tasa Patrono</th>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-700">Observaciones</th>
                                        </tr>
                                    </thead>
                                    <tbody className="bg-white divide-y divide-gray-200">
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">CSS (Caja de Seguro Social)</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                                                    9.75%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                                                    14.25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Topes escalonados según salario</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">Seguro Educativo</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-100 text-purple-800">
                                                    1.25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-purple-100 text-purple-800">
                                                    1.50%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Sin tope máximo</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">Riesgo Profesional</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                                                    -
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">
                                                    0.56% - 5.39%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-500">Según tipo de actividad</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div className="mt-6 p-4 bg-gray-50 rounded-lg border border-gray-200">
                                <h4 className="text-sm font-semibold text-gray-900 mb-2">Notas Importantes:</h4>
                                <ul className="text-sm text-gray-600 space-y-1 list-disc list-inside">
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
                            <h3 className="text-lg font-semibold text-gray-900 mb-4">Tabla de Impuesto Sobre la Renta (ISR)</h3>
                            <div className="mb-4">
                                <div className="inline-flex items-center px-3 py-1.5 bg-green-50 border border-green-200 rounded-lg">
                                    <svg className="w-4 h-4 text-green-600 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                    <span className="text-sm text-green-800 font-medium">Según DGI Panamá - Año fiscal 2025</span>
                                </div>
                            </div>

                            <div className="overflow-x-auto">
                                <table className="w-full">
                                    <thead className="bg-gray-50 border-b border-gray-200">
                                        <tr>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-700">Rango de Ingreso Anual</th>
                                            <th className="text-center py-3 px-4 text-sm font-medium text-gray-700">Tasa</th>
                                            <th className="text-left py-3 px-4 text-sm font-medium text-gray-700">Descripción</th>
                                        </tr>
                                    </thead>
                                    <tbody className="bg-white divide-y divide-gray-200">
                                        <tr className="bg-green-50/50">
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">$0 - $11,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-100 text-green-800">
                                                    Exento (0%)
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-600">No paga impuesto</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">$11,001 - $50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-yellow-100 text-yellow-800">
                                                    15%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-600">Sobre el exceso de $11,000</td>
                                        </tr>
                                        <tr>
                                            <td className="py-3 px-4 text-sm font-medium text-gray-900">Más de $50,000</td>
                                            <td className="py-3 px-4 text-sm text-center">
                                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-red-100 text-red-800">
                                                    25%
                                                </span>
                                            </td>
                                            <td className="py-3 px-4 text-sm text-gray-600">Sobre el exceso de $50,000</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="p-4 bg-blue-50 rounded-lg border border-blue-200">
                                    <h4 className="text-sm font-semibold text-blue-900 mb-2">Ejemplo de Cálculo:</h4>
                                    <div className="text-sm text-blue-800 space-y-1">
                                        <p>Salario anual: <strong>$30,000</strong></p>
                                        <p>Exento: $11,000 (0%)</p>
                                        <p>Gravable: $19,000 × 15% = <strong>$2,850</strong></p>
                                        <p className="pt-2 border-t border-blue-300">ISR anual total: <strong>$2,850</strong></p>
                                    </div>
                                </div>

                                <div className="p-4 bg-gray-50 rounded-lg border border-gray-200">
                                    <h4 className="text-sm font-semibold text-gray-900 mb-2">Deducciones Permitidas:</h4>
                                    <ul className="text-sm text-gray-600 space-y-1 list-disc list-inside">
                                        <li>Gastos educativos: hasta $5,000/año</li>
                                        <li>Intereses hipotecarios: hasta $15,000/año</li>
                                        <li>Dependientes: $800/año por dependiente</li>
                                        <li>Aportes jubilatorios voluntarios</li>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Tab: Usuarios */}
                    {activeTab === 'usuarios' && (
                        <div>
                            <h3 className="text-lg font-semibold text-gray-900 mb-4">Gestión de Usuarios y Permisos</h3>

                            <div className="mb-6 p-6 bg-blue-50 border border-blue-200 rounded-lg text-center">
                                <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-4">
                                    <svg className="w-8 h-8 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
                                    </svg>
                                </div>
                                <h4 className="text-lg font-semibold text-blue-900 mb-2">Gestión de Usuarios</h4>
                                <p className="text-blue-700 mb-4">Esta funcionalidad estará disponible próximamente</p>
                                <p className="text-sm text-blue-600">Podrás administrar usuarios, asignar roles y configurar permisos granulares</p>
                            </div>

                            <div>
                                <h4 className="text-sm font-semibold text-gray-900 mb-3">Roles del Sistema Disponibles:</h4>
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="p-4 bg-white border border-gray-200 rounded-lg hover:shadow-md transition-shadow">
                                        <div className="flex items-start gap-3">
                                            <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center flex-shrink-0">
                                                <svg className="w-5 h-5 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z" />
                                                </svg>
                                            </div>
                                            <div>
                                                <h5 className="font-semibold text-gray-900 mb-1">PayrollOperator</h5>
                                                <p className="text-sm text-gray-600">Puede crear y calcular planillas, gestionar empleados</p>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="p-4 bg-white border border-gray-200 rounded-lg hover:shadow-md transition-shadow">
                                        <div className="flex items-start gap-3">
                                            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center flex-shrink-0">
                                                <svg className="w-5 h-5 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                                                </svg>
                                            </div>
                                            <div>
                                                <h5 className="font-semibold text-gray-900 mb-1">PayrollAdmin</h5>
                                                <p className="text-sm text-gray-600">Puede aprobar planillas calculadas y gestionar configuraciones</p>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="p-4 bg-white border border-gray-200 rounded-lg hover:shadow-md transition-shadow">
                                        <div className="flex items-start gap-3">
                                            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center flex-shrink-0">
                                                <svg className="w-5 h-5 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                                                </svg>
                                            </div>
                                            <div>
                                                <h5 className="font-semibold text-gray-900 mb-1">FinanceManager</h5>
                                                <p className="text-sm text-gray-600">Puede procesar pagos, ver reportes financieros y auditoría</p>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="p-4 bg-white border border-gray-200 rounded-lg hover:shadow-md transition-shadow">
                                        <div className="flex items-start gap-3">
                                            <div className="w-10 h-10 bg-gray-100 rounded-lg flex items-center justify-center flex-shrink-0">
                                                <svg className="w-5 h-5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                                                </svg>
                                            </div>
                                            <div>
                                                <h5 className="font-semibold text-gray-900 mb-1">Viewer</h5>
                                                <p className="text-sm text-gray-600">Solo lectura - puede ver reportes sin modificar datos</p>
                                            </div>
                                        </div>
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
