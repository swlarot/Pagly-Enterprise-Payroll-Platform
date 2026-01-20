import React, { useState, useEffect } from 'react';

const DashboardPage = () => {
    const [stats, setStats] = useState({
        totalEmpleados: 0,
        empleadosActivos: 0,
        ultimaPlanilla: null,
        aportesCss: 0,
        pendientes: 0
    });
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        fetchDashboardData();
    }, []);

    const fetchDashboardData = async () => {
        try {
            setLoading(true);
            setError(null);

            // Fetch empleados
            const empleadosRes = await fetch('/api/empleados');
            if (!empleadosRes.ok) throw new Error('Error al cargar empleados');
            const empleados = await empleadosRes.json();

            const activos = empleados.filter(e => e.estaActivo).length;

            // Fetch planillas (ordenadas por fecha descendente)
            const planillasRes = await fetch('/api/payrollheaders');
            if (!planillasRes.ok) throw new Error('Error al cargar planillas');
            const planillas = await planillasRes.json();

            // Última planilla (la más reciente)
            const ultimaPlanilla = planillas.length > 0 ? planillas[0] : null;

            // Planillas pendientes (Draft)
            const pendientes = planillas.filter(p => p.status === 0).length;

            setStats({
                totalEmpleados: empleados.length,
                empleadosActivos: activos,
                ultimaPlanilla: ultimaPlanilla,
                aportesCss: ultimaPlanilla ? ultimaPlanilla.totalEmployerCost : 0,
                pendientes: pendientes
            });
        } catch (err) {
            setError(err.message);
            console.error('Error fetching dashboard data:', err);
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
        const badges = {
            0: { text: 'BORRADOR', color: 'bg-yellow-100 text-yellow-800' },
            1: { text: 'CALCULADO', color: 'bg-blue-100 text-blue-800' },
            2: { text: 'APROBADO', color: 'bg-green-100 text-green-800' },
            3: { text: 'PAGADO', color: 'bg-emerald-100 text-emerald-800' },
            4: { text: 'CANCELADO', color: 'bg-red-100 text-red-800' }
        };
        const badge = badges[status] || badges[0];
        return <span className={`px-3 py-1 rounded-full text-xs font-semibold ${badge.color}`}>{badge.text}</span>;
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center h-64">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg">
                <p className="font-bold">Error</p>
                <p>{error}</p>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Stats Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                {/* Empleados Activos */}
                <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100 hover:shadow-md transition-shadow">
                    <div className="flex items-center justify-between">
                        <div className="flex-1">
                            <p className="text-sm font-medium text-gray-500">Empleados Activos</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{stats.empleadosActivos}</p>
                            <p className="text-xs text-gray-400 mt-1">de {stats.totalEmpleados} totales</p>
                        </div>
                        <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center">
                            <svg className="w-8 h-8 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                {/* Última Planilla */}
                <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100 hover:shadow-md transition-shadow">
                    <div className="flex items-center justify-between">
                        <div className="flex-1">
                            <p className="text-sm font-medium text-gray-500">Última Planilla</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">
                                {stats.ultimaPlanilla ? formatCurrency(stats.ultimaPlanilla.totalNetPay) : 'Sin planillas'}
                            </p>
                            <p className="text-xs text-gray-400 mt-1">
                                {stats.ultimaPlanilla
                                    ? new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')
                                    : 'No hay datos'}
                            </p>
                        </div>
                        <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
                            <svg className="w-8 h-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                {/* Aportes Patronales */}
                <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100 hover:shadow-md transition-shadow">
                    <div className="flex items-center justify-between">
                        <div className="flex-1">
                            <p className="text-sm font-medium text-gray-500">Aportes Patronales</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{formatCurrency(stats.aportesCss)}</p>
                            <p className="text-xs text-gray-400 mt-1">CSS + SE + Riesgo</p>
                        </div>
                        <div className="w-16 h-16 bg-amber-100 rounded-full flex items-center justify-center">
                            <svg className="w-8 h-8 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                            </svg>
                        </div>
                    </div>
                </div>

                {/* Planillas Pendientes */}
                <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100 hover:shadow-md transition-shadow">
                    <div className="flex items-center justify-between">
                        <div className="flex-1">
                            <p className="text-sm font-medium text-gray-500">Planillas Pendientes</p>
                            <p className="text-3xl font-bold text-gray-900 mt-2">{stats.pendientes}</p>
                            <p className="text-xs text-gray-400 mt-1">
                                {stats.pendientes > 0 ? 'Requieren cálculo' : 'Todo al día'}
                            </p>
                        </div>
                        <div className={`w-16 h-16 rounded-full flex items-center justify-center ${
                            stats.pendientes > 0 ? 'bg-red-100' : 'bg-gray-100'
                        }`}>
                            <svg className={`w-8 h-8 ${stats.pendientes > 0 ? 'text-red-600' : 'text-gray-400'}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>
            </div>

            {/* Resumen del Período */}
            {stats.ultimaPlanilla && (
                <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100">
                    <h3 className="text-lg font-semibold text-gray-900 mb-6">Resumen del Período</h3>
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                        <div className="text-center p-6 bg-gradient-to-br from-blue-50 to-blue-100 rounded-xl">
                            <div className="flex justify-center mb-3">
                                <div className="w-12 h-12 bg-blue-600 rounded-full flex items-center justify-center">
                                    <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                    </svg>
                                </div>
                            </div>
                            <p className="text-xs font-medium text-blue-600 uppercase mb-2">Total Salarios Brutos</p>
                            <p className="text-2xl font-bold text-blue-900">{formatCurrency(stats.ultimaPlanilla.totalGrossPay)}</p>
                        </div>

                        <div className="text-center p-6 bg-gradient-to-br from-red-50 to-red-100 rounded-xl">
                            <div className="flex justify-center mb-3">
                                <div className="w-12 h-12 bg-red-600 rounded-full flex items-center justify-center">
                                    <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 12H4" />
                                    </svg>
                                </div>
                            </div>
                            <p className="text-xs font-medium text-red-600 uppercase mb-2">Total Deducciones</p>
                            <p className="text-2xl font-bold text-red-900">- {formatCurrency(stats.ultimaPlanilla.totalDeductions)}</p>
                        </div>

                        <div className="text-center p-6 bg-gradient-to-br from-green-50 to-green-100 rounded-xl">
                            <div className="flex justify-center mb-3">
                                <div className="w-12 h-12 bg-green-600 rounded-full flex items-center justify-center">
                                    <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                    </svg>
                                </div>
                            </div>
                            <p className="text-xs font-medium text-green-600 uppercase mb-2">Total Neto</p>
                            <p className="text-2xl font-bold text-green-900">{formatCurrency(stats.ultimaPlanilla.totalNetPay)}</p>
                        </div>

                        <div className="text-center p-6 bg-gradient-to-br from-amber-50 to-amber-100 rounded-xl">
                            <div className="flex justify-center mb-3">
                                <div className="w-12 h-12 bg-amber-600 rounded-full flex items-center justify-center">
                                    <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                                    </svg>
                                </div>
                            </div>
                            <p className="text-xs font-medium text-amber-600 uppercase mb-2">Costo Patronal</p>
                            <p className="text-2xl font-bold text-amber-900">{formatCurrency(stats.ultimaPlanilla.totalEmployerCost)}</p>
                        </div>
                    </div>

                    <div className="mt-6 flex items-center justify-between p-4 bg-gray-50 rounded-lg">
                        <div className="flex items-center gap-3">
                            <span className="text-sm text-gray-600">Planilla #{stats.ultimaPlanilla.payrollNumber}</span>
                            <span className="text-gray-300">•</span>
                            {getStatusBadge(stats.ultimaPlanilla.status)}
                        </div>
                        <span className="text-sm text-gray-500">
                            {new Date(stats.ultimaPlanilla.periodStartDate).toLocaleDateString('es-PA')} - {new Date(stats.ultimaPlanilla.periodEndDate).toLocaleDateString('es-PA')}
                        </span>
                    </div>
                </div>
            )}

            {/* Estado del Sistema */}
            <div className="bg-white rounded-2xl shadow-sm p-6 border border-gray-100">
                <h3 className="text-lg font-semibold text-gray-900 mb-6">Estado del Sistema</h3>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="flex items-center justify-between p-4 bg-green-50 border border-green-200 rounded-xl">
                        <div className="flex items-center gap-3">
                            <div className="w-3 h-3 bg-green-500 rounded-full animate-pulse"></div>
                            <span className="text-sm font-medium text-green-900">Base de Datos</span>
                        </div>
                        <span className="text-xs font-semibold text-green-700 bg-green-200 px-3 py-1 rounded-full">CONECTADO</span>
                    </div>

                    <div className="flex items-center justify-between p-4 bg-green-50 border border-green-200 rounded-xl">
                        <div className="flex items-center gap-3">
                            <div className="w-3 h-3 bg-green-500 rounded-full animate-pulse"></div>
                            <span className="text-sm font-medium text-green-900">API Backend</span>
                        </div>
                        <span className="text-xs font-semibold text-green-700 bg-green-200 px-3 py-1 rounded-full">ACTIVO</span>
                    </div>

                    <div className="flex items-center justify-between p-4 bg-blue-50 border border-blue-200 rounded-xl">
                        <div className="flex items-center gap-3">
                            <div className="w-3 h-3 bg-blue-500 rounded-full"></div>
                            <span className="text-sm font-medium text-blue-900">Sincronización</span>
                        </div>
                        <span className="text-xs font-semibold text-blue-700 bg-blue-200 px-3 py-1 rounded-full">AHORA</span>
                    </div>
                </div>
            </div>

            {/* Acciones Rápidas */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <button
                    onClick={() => window.location.href = '/planillas'}
                    className="flex items-center justify-between p-6 bg-gradient-to-br from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white rounded-2xl shadow-lg hover:shadow-xl transition-all"
                >
                    <div className="text-left">
                        <p className="text-lg font-bold">Nueva Planilla</p>
                        <p className="text-sm text-blue-100">Crear período de pago</p>
                    </div>
                    <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                </button>

                <button
                    onClick={() => window.location.href = '/empleados'}
                    className="flex items-center justify-between p-6 bg-gradient-to-br from-green-600 to-green-500 hover:from-green-700 hover:to-green-600 text-white rounded-2xl shadow-lg hover:shadow-xl transition-all"
                >
                    <div className="text-left">
                        <p className="text-lg font-bold">Gestionar Empleados</p>
                        <p className="text-sm text-green-100">Ver y editar personal</p>
                    </div>
                    <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                    </svg>
                </button>

                <button
                    onClick={() => window.location.href = '/configuracion'}
                    className="flex items-center justify-between p-6 bg-gradient-to-br from-purple-600 to-purple-500 hover:from-purple-700 hover:to-purple-600 text-white rounded-2xl shadow-lg hover:shadow-xl transition-all"
                >
                    <div className="text-left">
                        <p className="text-lg font-bold">Configuración</p>
                        <p className="text-sm text-purple-100">Ajustes del sistema</p>
                    </div>
                    <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                </button>
            </div>
        </div>
    );
};

export default DashboardPage;
