import React, { useState, useEffect, useCallback, lazy, Suspense } from 'react';
import { createPortal } from 'react-dom';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import ConfirmModal from '../components/ConfirmModal';

const OvertimeByTypeBarChart = lazy(() => import('../components/charts/OvertimeByTypeBarChart'));
const OvertimeCostDistributionPieChart = lazy(() => import('../components/charts/OvertimeCostDistributionPieChart'));
const OvertimeLimitsChart = lazy(() => import('../components/charts/OvertimeLimitsChart'));

const ChartFallback = () => (
    <div className="flex items-center justify-center py-8">
        <div className="w-6 h-6 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
    </div>
);

const HorasExtraPage = () => {
    // Auth context for permissions
    const { canWrite, canDelete, isReadOnly } = useAuth();

    // State management
    const [horasExtra, setHorasExtra] = useState([]);
    const [empleados, setEmpleados] = useState([]);
    const [tipos, setTipos] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [editingId, setEditingId] = useState(null);

    // Estados para eliminar hora extra
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [horaExtraToDelete, setHoraExtraToDelete] = useState(null);
    const [isDeleting, setIsDeleting] = useState(false);

    // Filters
    const [filters, setFilters] = useState({
        empleadoId: '',
        tipo: '',
        soloPendientes: false
    });

    // Form data
    const [formData, setFormData] = useState({
        empleadoId: '',
        fecha: new Date().toISOString().split('T')[0],
        tipoHoraExtra: '1',
        horaInicio: '09:00',
        horaFin: '10:00',
        motivo: ''
    });

    // Validación y límites
    const [limites, setLimites] = useState({
        horasDelDia: 0,
        horasDeLaSemana: 0,
        horasDisponiblesDia: 3,
        horasDisponiblesSemana: 9,
        porcentajeDia: 0,
        porcentajeSemana: 0,
        esExceso: false,
        mensaje: ''
    });

    const [festivoInfo, setFestivoInfo] = useState({
        esFestivo: false,
        nombreFestivo: null
    });

    const [sugerenciaTipo, setSugerenciaTipo] = useState(null);
    const [validando, setValidando] = useState(false);

    useEffect(() => {
        fetchHorasExtra();
        fetchEmpleados();
        fetchTipos();
    }, []);

    // Validar límites cuando cambian empleado, fecha o horas
    useEffect(() => {
        if (formData.empleadoId && formData.fecha && formData.horaInicio && formData.horaFin) {
            validarLimites();
        }
    }, [formData.empleadoId, formData.fecha, formData.horaInicio, formData.horaFin]);

    // Verificar si es festivo cuando cambia la fecha
    useEffect(() => {
        if (formData.fecha) {
            verificarFestivo();
        }
    }, [formData.fecha]);

    // Sugerir tipo cuando cambian fecha u horario
    useEffect(() => {
        if (formData.fecha && formData.horaInicio && formData.horaFin) {
            sugerirTipo();
        }
    }, [formData.fecha, formData.horaInicio, formData.horaFin]);

    const validarLimites = async () => {
        if (!formData.empleadoId || !formData.fecha) return;

        try {
            setValidando(true);
            const fecha = new Date(formData.fecha);
            const horaInicio = formData.horaInicio.split(':');
            const horaFin = formData.horaFin.split(':');
            const horasNuevas = calcularHoras(formData.horaInicio, formData.horaFin);

            const response = await api.get(
                `/api/horasextra/validar-limites?empleadoId=${formData.empleadoId}&fecha=${fecha.toISOString().split('T')[0]}&horasNuevas=${horasNuevas}`
            );

            setLimites({
                horasDelDia: response.horasDelDia || 0,
                horasDeLaSemana: response.horasDeLaSemana || 0,
                horasDisponiblesDia: response.horasDisponiblesDia || 3,
                horasDisponiblesSemana: response.horasDisponiblesSemana || 9,
                porcentajeDia: response.porcentajeDia || 0,
                porcentajeSemana: response.porcentajeSemana || 0,
                esExceso: response.esExceso || false,
                mensaje: response.mensaje || ''
            });
        } catch (err) {
            console.error('Error al validar límites:', err);
        } finally {
            setValidando(false);
        }
    };

    const verificarFestivo = async () => {
        if (!formData.fecha) return;

        try {
            const fecha = new Date(formData.fecha);
            const response = await api.get(
                `/api/horasextra/es-festivo?fecha=${fecha.toISOString().split('T')[0]}`
            );

            setFestivoInfo({
                esFestivo: response.esFestivo || false,
                nombreFestivo: response.nombreFestivo || null
            });
        } catch (err) {
            console.error('Error al verificar festivo:', err);
        }
    };

    const sugerirTipo = async () => {
        if (!formData.fecha || !formData.horaInicio || !formData.horaFin) return;

        try {
            const fecha = new Date(formData.fecha);
            const response = await api.get(
                `/api/horasextra/sugerir-tipo?fecha=${fecha.toISOString().split('T')[0]}&horaInicio=${formData.horaInicio}&horaFin=${formData.horaFin}`
            );

            setSugerenciaTipo(response);
        } catch (err) {
            console.error('Error al sugerir tipo:', err);
        }
    };

    const calcularHoras = (inicio, fin) => {
        const [hInicio, mInicio] = inicio.split(':').map(Number);
        const [hFin, mFin] = fin.split(':').map(Number);
        const inicioMinutos = hInicio * 60 + mInicio;
        const finMinutos = hFin * 60 + mFin;
        const diferencia = finMinutos - inicioMinutos;
        return Math.max(0, diferencia / 60);
    };

    const fetchHorasExtra = async () => {
        try {
            setLoading(true);
            const data = await api.get('/api/horasextra');
            setHorasExtra(data);
        } catch (err) {
            toast.error(`Error al cargar horas extra: ${err.message}`);
        } finally {
            setLoading(false);
        }
    };

    const fetchEmpleados = async () => {
        try {
            const data = await api.get('/api/empleados');
            setEmpleados(data.filter(e => e.estaActivo));
        } catch (err) {
            toast.error(`Error al cargar empleados: ${err.message}`);
        }
    };

    const fetchTipos = async () => {
        try {
            const data = await api.get('/api/horasextra/tipos');
            setTipos(data);
        } catch (err) {
            toast.error(`Error al cargar tipos: ${err.message}`);
        }
    };

    // Filter hours extra
    const filteredHorasExtra = horasExtra.filter(he => {
        if (filters.empleadoId && he.empleadoId !== parseInt(filters.empleadoId)) return false;
        if (filters.tipo && he.tipoHoraExtra !== parseInt(filters.tipo)) return false;
        if (filters.soloPendientes && he.estaAprobada) return false;
        return true;
    });

    // Calculate stats
    const pendientes = horasExtra.filter(h => !h.estaAprobada).length;
    const thisMonth = new Date();
    const horasEsteMes = horasExtra
        .filter(h => {
            const fecha = new Date(h.fecha);
            return fecha.getMonth() === thisMonth.getMonth() &&
                   fecha.getFullYear() === thisMonth.getFullYear();
        })
        .reduce((sum, h) => sum + h.cantidadHoras, 0);

    const montoEstimado = horasExtra
        .filter(h => !h.estaAprobada)
        .reduce((sum, h) => sum + (h.montoCalculado || 0), 0);

    // Función para obtener nombre corto y único de cada tipo
    const getTipoDisplayName = (tipoValor) => {
        const tipoMap = {
            1: 'Diurna',                    // TipoHoraExtra.Diurna
            2: 'Nocturna',                  // TipoHoraExtra.Nocturna
            3: 'Dom/Fer',                    // TipoHoraExtra.DomingoFeriado
            4: 'Noct. Dom/Fer',             // TipoHoraExtra.NocturnaDomingoFeriado
            5: 'Fiesta Diurna',              // TipoHoraExtra.FiestaNacionalDiurna
            6: 'Fiesta Nocturna',            // TipoHoraExtra.FiestaNacionalNocturna
            7: 'Mixta D-N',                  // TipoHoraExtra.MixtaDiurnaNocturna
            8: 'Mixta N-D'                   // TipoHoraExtra.MixtaNocturnaDiurna
        };
        return tipoMap[tipoValor] || 'Desconocido';
    };

    // Badge de tipo de hora extra con color semántico por tipo
    const getOvertimeTypeBadge = (tipoValor, tipoNombre) => {
        const colores = {
            1: 'bg-blue-500/15 text-blue-400 border-blue-500/20',        // Diurna
            2: 'bg-purple-500/15 text-purple-400 border-purple-500/20',   // Nocturna
            3: 'bg-amber-500/15 text-amber-400 border-amber-500/20',      // Dom/Feriado
            4: 'bg-red-500/15 text-red-400 border-red-500/20',            // Noct. Dom/Feriado
            5: 'bg-emerald-500/15 text-emerald-400 border-emerald-500/20', // Fiesta Diurna
            6: 'bg-pink-500/15 text-pink-400 border-pink-500/20',          // Fiesta Nocturna
            7: 'bg-cyan-500/15 text-cyan-400 border-cyan-500/20',          // Mixta D-N
            8: 'bg-orange-500/15 text-orange-400 border-orange-500/20',    // Mixta N-D
        };
        const color = colores[tipoValor] || 'bg-gray-500/15 text-gray-400 border-gray-500/20';
        const label = tipoNombre || getTipoDisplayName(tipoValor);
        return (
            <span className={`px-2 py-0.5 rounded-lg text-xs font-semibold border ${color}`}>
                {label}
            </span>
        );
    };

    const porTipo = tipos.map(tipo => ({
        ...tipo,
        cantidad: horasExtra.filter(h => h.tipoHoraExtra === tipo.valor).length,
        displayName: getTipoDisplayName(tipo.valor)
    }));

    // Preparar datos para gráficos
    const chartDataByType = porTipo.map(tipo => {
        const horasDelTipo = horasExtra.filter(h => h.tipoHoraExtra === tipo.valor);
        const totalHoras = horasDelTipo.reduce((sum, h) => sum + h.cantidadHoras, 0);
        const totalMonto = horasDelTipo.reduce((sum, h) => sum + (h.montoCalculado || 0), 0);
        return {
            tipo: tipo.displayName,
            horas: totalHoras,
            monto: totalMonto
        };
    }).filter(item => item.horas > 0);

    const pieChartData = chartDataByType.map((item, index) => {
        const colors = ['#3B82F6', '#8B5CF6', '#EC4899', '#F59E0B', '#10B981', '#EF4444'];
        return {
            name: item.tipo,
            value: item.monto,
            color: colors[index % colors.length]
        };
    });

    const formatCurrency = (amount) => {
        return 'B/. ' + new Intl.NumberFormat('es-PA', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount || 0);
    };

    const formatTime = (timeSpan) => {
        // timeSpan viene como "09:00:00" del backend
        return timeSpan.substring(0, 5);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        // Validar límites antes de enviar
        if (limites.esExceso) {
            const confirmar = window.confirm(
                `⚠️ Advertencia: ${limites.mensaje}\n\n¿Desea continuar de todas formas?`
            );
            if (!confirmar) {
                return;
            }
        }

        try {
            const payload = {
                empleadoId: parseInt(formData.empleadoId),
                fecha: formData.fecha,
                tipoHoraExtra: parseInt(formData.tipoHoraExtra),
                horaInicio: formData.horaInicio + ':00',
                horaFin: formData.horaFin + ':00',
                motivo: formData.motivo
            };

            if (editingId) {
                await api.put(`/api/horasextra/${editingId}`, payload);
            } else {
                await api.post('/api/horasextra', payload);
            }

            await fetchHorasExtra();
            toast.success(editingId ? 'Hora extra actualizada' : 'Hora extra registrada');
            resetForm();
        } catch (err) {
            toast.error(`Error: ${err.message}`);
        }
    };

    const handleAprobar = async (id) => {
        try {
            await api.post(`/api/horasextra/${id}/aprobar`, {});
            await fetchHorasExtra();
            toast.success('Hora extra aprobada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const handleRechazar = async (id) => {
        if (!window.confirm('¿Está seguro de rechazar esta hora extra?')) return;

        try {
            await api.post(`/api/horasextra/${id}/rechazar`, {});
            await fetchHorasExtra();
            toast.success('Hora extra rechazada');
        } catch (err) {
            toast.error(err.message);
        }
    };

    const handleDeleteHoraExtra = async () => {
        if (!horaExtraToDelete) return;
        try {
            setIsDeleting(true);
            await api.delete(`/api/horasextra/${horaExtraToDelete.id}`);
            toast.success('Hora extra eliminada');
            setShowDeleteModal(false);
            setHoraExtraToDelete(null);
            await fetchHorasExtra();
        } catch (error) {
            toast.error(error.message || 'Error al eliminar');
        } finally {
            setIsDeleting(false);
        }
    };

    const handleEdit = (horaExtra) => {
        setFormData({
            empleadoId: horaExtra.empleadoId.toString(),
            fecha: new Date(horaExtra.fecha).toISOString().split('T')[0],
            tipoHoraExtra: horaExtra.tipoHoraExtra.toString(),
            horaInicio: formatTime(horaExtra.horaInicio),
            horaFin: formatTime(horaExtra.horaFin),
            motivo: horaExtra.motivo
        });
        setEditingId(horaExtra.id);
        setShowModal(true);
    };

    const resetForm = () => {
        setShowModal(false);
        setEditingId(null);
        setFormData({
            empleadoId: '',
            fecha: new Date().toISOString().split('T')[0],
            tipoHoraExtra: '1',
            horaInicio: '09:00',
            horaFin: '10:00',
            motivo: ''
        });
        setLimites({
            horasDelDia: 0,
            horasDeLaSemana: 0,
            horasDisponiblesDia: 3,
            horasDisponiblesSemana: 9,
            porcentajeDia: 0,
            porcentajeSemana: 0,
            esExceso: false,
            mensaje: ''
        });
        setFestivoInfo({
            esFestivo: false,
            nombreFestivo: null
        });
        setSugerenciaTipo(null);
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="w-12 h-12 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Cargando horas extra...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Pendientes de Aprobar</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{pendientes}</p>
                            <div className="flex items-center gap-1 mt-2">
                                <svg className="w-3 h-3 text-amber-400" viewBox="0 0 12 12" fill="none">
                                    <path d="M2 9l3-3 2 2 3-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                                </svg>
                                <span className="text-xs text-gray-500">Requieren aprobación</span>
                            </div>
                        </div>
                        <div className="w-12 h-12 bg-amber-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Horas este Mes</p>
                            <p className="text-3xl font-bold text-gray-100 mt-2">{horasEsteMes.toFixed(1)}</p>
                            <div className="flex items-center gap-1 mt-2">
                                <svg className="w-3 h-3 text-primary-400" viewBox="0 0 12 12" fill="none">
                                    <path d="M2 9l3-3 2 2 3-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                                </svg>
                                <span className="text-xs text-gray-500">Este mes</span>
                            </div>
                        </div>
                        <div className="w-12 h-12 bg-primary-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm font-medium text-gray-400">Monto Estimado</p>
                            <p className="text-2xl font-bold font-mono text-gray-100 mt-2">{formatCurrency(montoEstimado)}</p>
                            <div className="flex items-center gap-1 mt-2">
                                <svg className="w-3 h-3 text-green-400" viewBox="0 0 12 12" fill="none">
                                    <path d="M2 9l3-3 2 2 3-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                                </svg>
                                <span className="text-xs text-gray-500">Pendientes de pago</span>
                            </div>
                        </div>
                        <div className="w-12 h-12 bg-green-500/15 rounded-lg flex items-center justify-center">
                            <svg className="w-6 h-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        </div>
                    </div>
                </div>

                <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                    <p className="text-sm font-medium text-gray-400 mb-3">Por Tipo</p>
                    <div className="space-y-2">
                        {porTipo.map((tipo, index) => (
                            <div key={tipo?.valor || `tipo-${index}`} className="flex items-center justify-between">
                                <span className="text-xs text-gray-400" title={tipo.nombre}>{tipo.displayName}</span>
                                <span className="px-2 py-0.5 bg-blue-500/15 text-blue-400 text-xs font-medium rounded-full">
                                    {tipo.cantidad}
                                </span>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* Gráficos */}
            {chartDataByType.length > 0 && (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <Suspense fallback={<ChartFallback />}>
                            <OvertimeByTypeBarChart
                                data={chartDataByType}
                                title="Horas Extra por Tipo"
                            />
                        </Suspense>
                    </div>
                    <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-6">
                        <Suspense fallback={<ChartFallback />}>
                            <OvertimeCostDistributionPieChart
                                data={pieChartData}
                                title="Distribución de Costos"
                            />
                        </Suspense>
                    </div>
                </div>
            )}

            {/* Filters and Action Bar */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 p-4">
                <div className="flex flex-col md:flex-row gap-4 items-start md:items-center justify-between">
                    <div className="flex flex-col sm:flex-row gap-3 flex-1">
                        <select
                            value={filters.empleadoId}
                            onChange={(e) => setFilters({ ...filters, empleadoId: e.target.value })}
                            className="px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                        >
                            <option value="">Todos los empleados</option>
                            {empleados.map(emp => (
                                <option key={emp.id} value={emp.id}>
                                    {emp.nombre} {emp.apellido}
                                </option>
                            ))}
                        </select>

                        <select
                            value={filters.tipo}
                            onChange={(e) => setFilters({ ...filters, tipo: e.target.value })}
                            className="px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                        >
                            <option value="">Todos los tipos</option>
                            {tipos.map(tipo => (
                                <option key={tipo.valor} value={tipo.valor}>
                                    {tipo.nombre}
                                </option>
                            ))}
                        </select>

                        <label className="flex items-center gap-2 px-3 py-2 bg-navy-950 rounded-lg cursor-pointer">
                            <input
                                type="checkbox"
                                checked={filters.soloPendientes}
                                onChange={(e) => setFilters({ ...filters, soloPendientes: e.target.checked })}
                                className="w-4 h-4 text-primary-600 rounded focus:ring-2 focus:ring-primary-500/20"
                            />
                            <span className="text-sm font-medium text-gray-300">Solo pendientes</span>
                        </label>
                    </div>

                    <button
                        onClick={() => setShowModal(true)}
                        className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2.5 rounded-lg font-medium transition-colors shadow-lg shadow-black/20"
                    >
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                        </svg>
                        Registrar Horas Extra
                    </button>
                </div>
            </div>

            {/* Table */}
            <div className="bg-navy-900 rounded-xl shadow-lg shadow-black/20 border border-navy-700 overflow-hidden">
                <div className="px-6 py-4 border-b border-navy-700">
                    <h3 className="text-lg font-semibold text-gray-100">
                        Horas Extra Registradas
                        <span className="ml-2 text-sm font-normal text-gray-500">
                            ({filteredHorasExtra.length} registros)
                        </span>
                    </h3>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-navy-950 border-b border-navy-700">
                            <tr>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Empleado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Fecha</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Tipo</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Horario</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Horas</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Monto</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Estado</th>
                                <th className="text-left py-3 px-6 text-xs font-medium text-gray-500 uppercase">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="bg-navy-900 divide-y divide-navy-700">
                            {filteredHorasExtra.map((he) => (
                                <tr key={he.id} className="hover:bg-navy-800 transition-colors">
                                    <td className="py-4 px-6 text-sm text-gray-100">{he.empleadoNombre}</td>
                                    <td className="py-4 px-6 text-sm text-gray-500">
                                        {new Date(he.fecha).toLocaleDateString('es-PA', {
                                            day: '2-digit',
                                            month: 'short',
                                            year: 'numeric'
                                        })}
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex flex-col gap-1">
                                            {getOvertimeTypeBadge(he.tipoHoraExtra, he.tipoNombre)}
                                            {he.esExceso && (
                                                <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-red-500/15 text-red-300 border border-red-500/20" title="Excede límite legal (>3h/día o >9h/semana)">
                                                    ⚠ Exceso
                                                </span>
                                            )}
                                        </div>
                                    </td>
                                    <td className="py-4 px-6 text-sm text-gray-100">
                                        {formatTime(he.horaInicio)} - {formatTime(he.horaFin)}
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium text-gray-100">
                                        {he.cantidadHoras.toFixed(2)}h
                                    </td>
                                    <td className="py-4 px-6 text-sm font-medium font-mono text-gray-100">
                                        <div className="flex flex-col">
                                            <span>{formatCurrency(he.montoCalculado)}</span>
                                            {he.esExceso && he.factorExceso && (
                                                <span className="text-xs text-gray-400 mt-0.5">
                                                    Factor: {he.factorMultiplicador.toFixed(2)}x
                                                </span>
                                            )}
                                        </div>
                                    </td>
                                    <td className="py-4 px-6">
                                        {he.estaAprobada ? (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-500/15 text-green-400">
                                                <span className="w-1.5 h-1.5 bg-green-400 rounded-full mr-1.5"></span>
                                                Aprobada
                                            </span>
                                        ) : (
                                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-500/15 text-amber-400">
                                                <span className="w-1.5 h-1.5 bg-amber-400 rounded-full mr-1.5"></span>
                                                Pendiente
                                            </span>
                                        )}
                                    </td>
                                    <td className="py-4 px-6">
                                        <div className="flex items-center gap-2">
                                            {!he.estaAprobada && (
                                                <>
                                                    <button
                                                        onClick={() => handleAprobar(he.id)}
                                                        className="inline-flex items-center gap-1 text-green-400 hover:text-green-300 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                                        </svg>
                                                        Aprobar
                                                    </button>
                                                    <button
                                                        onClick={() => handleEdit(he)}
                                                        className="inline-flex items-center gap-1 text-primary-400 hover:text-primary-300 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                                        </svg>
                                                        Editar
                                                    </button>
                                                    <button
                                                        onClick={() => handleRechazar(he.id)}
                                                        className="inline-flex items-center gap-1 text-red-400 hover:text-red-300 font-medium text-sm"
                                                    >
                                                        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                                        </svg>
                                                        Rechazar
                                                    </button>
                                                </>
                                            )}
                                            {he.estaAprobada && he.aprobadoPor && (
                                                <span className="text-xs text-gray-500">
                                                    Aprobado por {he.aprobadoPor}
                                                </span>
                                            )}
                                            {!he.planillaDetailId && (
                                                <button
                                                    onClick={() => { setHoraExtraToDelete(he); setShowDeleteModal(true); }}
                                                    className="p-1.5 rounded text-red-400 hover:text-red-300 hover:bg-red-500/10 transition-colors"
                                                    title="Eliminar hora extra"
                                                >
                                                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                                    </svg>
                                                </button>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>

                    {filteredHorasExtra.length === 0 && (
                        <div className="text-center py-12">
                            <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                            <h3 className="text-lg font-medium text-gray-100 mb-1">
                                No hay horas extra registradas
                            </h3>
                            <p className="text-gray-500 mb-4">
                                Comienza registrando la primera hora extra
                            </p>
                            <button
                                onClick={() => setShowModal(true)}
                                className="inline-flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white px-4 py-2 rounded-lg font-medium transition-colors"
                            >
                                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                                </svg>
                                Registrar Horas Extra
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Modal */}
            {showModal && createPortal(
                <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-navy-900 rounded-xl shadow-2xl shadow-black/30 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-navy-700 flex items-center justify-between sticky top-0 bg-navy-900">
                            <h3 className="text-xl font-semibold text-gray-100">
                                {editingId ? 'Editar Hora Extra' : 'Registrar Hora Extra'}
                            </h3>
                            <button
                                onClick={resetForm}
                                className="text-gray-400 hover:text-gray-200 transition-colors"
                            >
                                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        </div>

                        <form onSubmit={handleSubmit} className="p-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Empleado <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.empleadoId}
                                        onChange={(e) => setFormData({ ...formData, empleadoId: e.target.value })}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    >
                                        <option value="">Seleccionar empleado...</option>
                                        {empleados.map(emp => (
                                            <option key={emp.id} value={emp.id}>
                                                {emp.nombre} {emp.apellido} - {emp.posicionNombre || 'Sin posición'}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Fecha <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="date"
                                        required
                                        value={formData.fecha}
                                        onChange={(e) => setFormData({ ...formData, fecha: e.target.value })}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                    {festivoInfo.esFestivo && (
                                        <div className="mt-2 px-3 py-2 bg-purple-500/15 border border-purple-500/30 rounded-lg">
                                            <p className="text-sm text-purple-300">
                                                🎉 <strong>{festivoInfo.nombreFestivo}</strong> - Día festivo nacional
                                            </p>
                                        </div>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Tipo <span className="text-red-500">*</span>
                                    </label>
                                    <select
                                        required
                                        value={formData.tipoHoraExtra}
                                        onChange={(e) => setFormData({ ...formData, tipoHoraExtra: e.target.value })}
                                        className={`w-full px-3 py-2 bg-navy-800 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 ${
                                            sugerenciaTipo && parseInt(formData.tipoHoraExtra) !== sugerenciaTipo.tipoSugerido
                                                ? 'border-blue-500/50'
                                                : 'border-navy-600'
                                        } text-gray-200`}
                                    >
                                        {tipos.map((tipo, index) => (
                                            <option key={tipo?.valor || `tipo-${index}`} value={tipo?.valor}>
                                                {tipo.nombre}
                                            </option>
                                        ))}
                                    </select>
                                    {sugerenciaTipo && parseInt(formData.tipoHoraExtra) !== sugerenciaTipo.tipoSugerido && (
                                        <div className="mt-2 px-3 py-2 bg-blue-500/15 border border-blue-500/30 rounded-lg">
                                            <p className="text-sm text-blue-300">
                                                💡 Sugerencia: <strong>{sugerenciaTipo.nombreTipo}</strong> (Factor {sugerenciaTipo.factor}x)
                                            </p>
                                            <button
                                                type="button"
                                                onClick={() => setFormData({ ...formData, tipoHoraExtra: sugerenciaTipo.tipoSugerido.toString() })}
                                                className="mt-1 text-xs text-blue-400 hover:text-blue-300 underline"
                                            >
                                                Usar tipo sugerido
                                            </button>
                                        </div>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Hora Inicio <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="time"
                                        required
                                        value={formData.horaInicio}
                                        onChange={(e) => setFormData({ ...formData, horaInicio: e.target.value })}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Hora Fin <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="time"
                                        required
                                        value={formData.horaFin}
                                        onChange={(e) => setFormData({ ...formData, horaFin: e.target.value })}
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                    />
                                </div>

                                <div className="md:col-span-2">
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Motivo <span className="text-red-500">*</span>
                                    </label>
                                    <textarea
                                        required
                                        value={formData.motivo}
                                        onChange={(e) => setFormData({ ...formData, motivo: e.target.value })}
                                        rows="3"
                                        className="w-full px-3 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500"
                                        placeholder="Describir el motivo de las horas extra..."
                                    />
                                </div>

                                {/* Alertas de límites */}
                                {formData.empleadoId && formData.fecha && (
                                    <div className="md:col-span-2">
                                        <div className="bg-navy-800 rounded-lg p-4 border border-navy-700">
                                            <h4 className="text-sm font-semibold text-gray-300 mb-3">Límites Legales</h4>
                                            {validando ? (
                                                <div className="text-center py-4">
                                                    <div className="w-6 h-6 border-2 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
                                                    <p className="text-xs text-gray-400 mt-2">Validando límites...</p>
                                                </div>
                                            ) : (
                                                <>
                                                    <Suspense fallback={<ChartFallback />}>
                                                        <OvertimeLimitsChart
                                                            horasDelDia={limites.horasDelDia}
                                                            horasDeLaSemana={limites.horasDeLaSemana}
                                                            limiteDiario={3}
                                                            limiteSemanal={9}
                                                        />
                                                    </Suspense>
                                                    {limites.esExceso && (
                                                        <div className="mt-3 px-3 py-2 bg-red-500/15 border border-red-500/30 rounded-lg">
                                                            <p className="text-sm text-red-300">
                                                                ⚠️ <strong>Advertencia:</strong> {limites.mensaje}
                                                            </p>
                                                        </div>
                                                    )}
                                                    {limites.porcentajeDia >= 70 && limites.porcentajeDia < 90 && (
                                                        <div className="mt-3 px-3 py-2 bg-yellow-500/15 border border-yellow-500/30 rounded-lg">
                                                            <p className="text-sm text-yellow-300">
                                                                ⚠️ Se está acercando al límite diario
                                                            </p>
                                                        </div>
                                                    )}
                                                    {limites.porcentajeSemana >= 70 && limites.porcentajeSemana < 90 && (
                                                        <div className="mt-3 px-3 py-2 bg-yellow-500/15 border border-yellow-500/30 rounded-lg">
                                                            <p className="text-sm text-yellow-300">
                                                                ⚠️ Se está acercando al límite semanal
                                                            </p>
                                                        </div>
                                                    )}
                                                </>
                                            )}
                                        </div>
                                    </div>
                                )}
                            </div>

                            <div className="flex justify-end gap-3 pt-4 border-t border-navy-700">
                                <button
                                    type="button"
                                    onClick={resetForm}
                                    className="px-4 py-2 border border-navy-600 rounded-lg text-gray-300 hover:bg-navy-800 font-medium transition-colors"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className={`px-4 py-2 rounded-lg font-medium transition-colors shadow-lg shadow-black/20 ${
                                        limites.esExceso
                                            ? 'bg-red-600 hover:bg-red-700 text-white'
                                            : 'bg-primary-600 hover:bg-primary-700 text-white'
                                    }`}
                                >
                                    {editingId ? 'Actualizar' : 'Registrar'}
                                    {limites.esExceso && ' ⚠️'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>,
                document.body
            )}

            <ConfirmModal
                isOpen={showDeleteModal}
                onClose={() => { setShowDeleteModal(false); setHoraExtraToDelete(null); }}
                onConfirm={handleDeleteHoraExtra}
                title="Eliminar Hora Extra"
                message="¿Está seguro de eliminar esta hora extra? Esta acción no se puede deshacer."
                confirmText="Eliminar"
                variant="danger"
                isLoading={isDeleting}
            />
        </div>
    );
};

export default HorasExtraPage;
