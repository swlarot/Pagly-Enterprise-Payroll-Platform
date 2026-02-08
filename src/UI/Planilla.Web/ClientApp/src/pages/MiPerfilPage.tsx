import React, { useEffect, useState } from 'react';
import { Card, CardHeader, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { User, DollarSign, Calendar, FileText, Clock, Briefcase } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

interface Empleado {
  id: number;
  nombre: string;
  apellido: string;
  email: string;
  salarioBase: number;
  fechaContratacion: string;
  numeroIdentificacion: string;
  departamentoNombre?: string;
  posicionNombre?: string;
}

interface Planilla {
  id: number;
  periodStartDate: string;
  periodEndDate: string;
  status: string;
  netPay?: number;
  details: Array<{
    netPay: number;
    grossPay: number;
  }>;
}

interface Vacacion {
  id: number;
  fechaInicio: string;
  fechaFin: string;
  diasSolicitados: number;
  estado: string;
}

export default function MiPerfilPage() {
  const { user } = useAuth();
  const [isLoading, setIsLoading] = useState(true);
  const [empleado, setEmpleado] = useState<Empleado | null>(null);
  const [planillas, setPlanillas] = useState<Planilla[]>([]);
  const [vacaciones, setVacaciones] = useState<Vacacion[]>([]);
  const [estadisticas, setEstadisticas] = useState({
    diasVacacionesDisponibles: 0,
    horasExtraMes: 0,
    prestamosActivos: 0,
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setIsLoading(true);

      // Cargar información del empleado (backend devuelve solo el empleado vinculado)
      const empleadosRes = await api.get<Empleado[]>('/api/empleados');
      if (empleadosRes && empleadosRes.length > 0) {
        setEmpleado(empleadosRes[0]);
      }

      // Cargar últimas 5 planillas
      const planillasRes = await api.get<Planilla[]>('/api/payrollheaders');
      setPlanillas(planillasRes.slice(0, 5));

      // Cargar solicitudes de vacaciones
      const vacacionesRes = await api.get<Vacacion[]>('/api/vacaciones');
      setVacaciones(vacacionesRes.slice(0, 5));

      // TODO: Cargar estadísticas adicionales (días de vacaciones, horas extra, etc.)
      // Por ahora, valores de ejemplo
      setEstadisticas({
        diasVacacionesDisponibles: 15,
        horasExtraMes: 8,
        prestamosActivos: 0,
      });

    } catch (error: any) {
      console.error('Error cargando datos del perfil:', error);
      toast.error(error.message || 'Error al cargar tu información');
    } finally {
      setIsLoading(false);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('es-PA', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('es-PA', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
    });
  };

  const getStatusBadge = (status: string) => {
    const statusMap: Record<string, { label: string; variant: 'success' | 'warning' | 'danger' | 'info' }> = {
      'Pendiente': { label: 'Pendiente', variant: 'warning' },
      'Aprobada': { label: 'Aprobada', variant: 'success' },
      'Rechazada': { label: 'Rechazada', variant: 'danger' },
      'Cancelada': { label: 'Cancelada', variant: 'danger' },
    };
    const mapped = statusMap[status] || { label: status, variant: 'info' };
    return <Badge variant={mapped.variant}>{mapped.label}</Badge>;
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-emerald-600"></div>
      </div>
    );
  }

  if (!empleado) {
    return (
      <div className="text-center py-12">
        <p className="text-gray-400">No se encontró información de tu perfil</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold font-display text-gray-100">Mi Perfil</h1>
          <p className="text-gray-400 mt-1">Información personal y resumen de actividad</p>
        </div>
      </div>

      {/* Información Personal */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 bg-emerald-500/15 rounded-full flex items-center justify-center">
              <User className="w-6 h-6 text-emerald-400" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-100">Información Personal</h2>
              <p className="text-sm text-gray-400">Tus datos básicos como empleado</p>
            </div>
          </div>
        </CardHeader>
        <CardBody>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-1">Nombre Completo</label>
              <p className="text-lg font-semibold text-gray-100">
                {empleado.nombre} {empleado.apellido}
              </p>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-1">Cédula</label>
              <p className="text-lg font-semibold text-gray-100">{empleado.numeroIdentificacion}</p>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-1">Email</label>
              <p className="text-lg font-semibold text-gray-100">{empleado.email}</p>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-1">Fecha de Contratación</label>
              <p className="text-lg font-semibold text-gray-100">{formatDate(empleado.fechaContratacion)}</p>
            </div>
            {empleado.departamentoNombre && (
              <div>
                <label className="block text-sm font-medium text-gray-400 mb-1">Departamento</label>
                <p className="text-lg font-semibold text-gray-100">{empleado.departamentoNombre}</p>
              </div>
            )}
            {empleado.posicionNombre && (
              <div>
                <label className="block text-sm font-medium text-gray-400 mb-1">Posición</label>
                <p className="text-lg font-semibold text-gray-100">{empleado.posicionNombre}</p>
              </div>
            )}
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-1">Salario Base</label>
              <p className="text-lg font-semibold font-mono text-gray-100">{formatCurrency(empleado.salarioBase)}</p>
            </div>
          </div>
        </CardBody>
      </Card>

      {/* Tarjetas de Estadísticas */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Vacaciones Disponibles */}
        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Vacaciones Disponibles</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">
                  {estadisticas.diasVacacionesDisponibles}
                </p>
                <p className="text-sm text-gray-400 mt-1">días</p>
              </div>
              <div className="w-12 h-12 bg-blue-500/15 rounded-full flex items-center justify-center">
                <Calendar className="w-6 h-6 text-primary-400" />
              </div>
            </div>
          </CardBody>
        </Card>

        {/* Horas Extra del Mes */}
        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Horas Extra (Mes)</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">{estadisticas.horasExtraMes}</p>
                <p className="text-sm text-gray-400 mt-1">horas</p>
              </div>
              <div className="w-12 h-12 bg-green-500/15 rounded-full flex items-center justify-center">
                <Clock className="w-6 h-6 text-green-400" />
              </div>
            </div>
          </CardBody>
        </Card>

        {/* Préstamos Activos */}
        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Préstamos Activos</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">{estadisticas.prestamosActivos}</p>
                <p className="text-sm text-gray-400 mt-1">activos</p>
              </div>
              <div className="w-12 h-12 bg-amber-500/15 rounded-full flex items-center justify-center">
                <DollarSign className="w-6 h-6 text-amber-400" />
              </div>
            </div>
          </CardBody>
        </Card>
      </div>

      {/* Mis Últimas Planillas */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-purple-500/15 rounded-full flex items-center justify-center">
                <FileText className="w-5 h-5 text-purple-400" />
              </div>
              <div>
                <h2 className="text-xl font-semibold text-gray-100">Mis Últimas Planillas</h2>
                <p className="text-sm text-gray-400">Recibos de pago recientes</p>
              </div>
            </div>
          </div>
        </CardHeader>
        <CardBody>
          {planillas.length === 0 ? (
            <p className="text-gray-400 text-center py-8">No tienes planillas registradas</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-navy-700">
                <thead className="bg-navy-800">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">
                      Período
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">
                      Estado
                    </th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-400 uppercase">
                      Salario Neto
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-navy-900 divide-y divide-navy-700">
                  {planillas.map((planilla) => (
                    <tr key={planilla.id} className="hover:bg-navy-800">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-100">
                        {formatDate(planilla.periodStartDate)} - {formatDate(planilla.periodEndDate)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <Badge variant="success">{planilla.status}</Badge>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold font-mono text-right text-gray-100">
                        {planilla.details && planilla.details.length > 0
                          ? formatCurrency(planilla.details[0].netPay)
                          : '-'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>

      {/* Mis Solicitudes de Vacaciones */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-blue-500/15 rounded-full flex items-center justify-center">
                <Briefcase className="w-5 h-5 text-primary-400" />
              </div>
              <div>
                <h2 className="text-xl font-semibold text-gray-100">Mis Solicitudes de Vacaciones</h2>
                <p className="text-sm text-gray-400">Historial de solicitudes</p>
              </div>
            </div>
            <Button
              variant="primary"
              onClick={() => toast.info('Funcionalidad próximamente disponible')}
            >
              Solicitar Vacaciones
            </Button>
          </div>
        </CardHeader>
        <CardBody>
          {vacaciones.length === 0 ? (
            <p className="text-gray-400 text-center py-8">No tienes solicitudes de vacaciones</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-navy-700">
                <thead className="bg-navy-800">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">
                      Fecha Inicio
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">
                      Fecha Fin
                    </th>
                    <th className="px-6 py-3 text-center text-xs font-medium text-gray-400 uppercase">
                      Días
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">
                      Estado
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-navy-900 divide-y divide-navy-700">
                  {vacaciones.map((vacacion) => (
                    <tr key={vacacion.id} className="hover:bg-navy-800">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-100">
                        {formatDate(vacacion.fechaInicio)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-100">
                        {formatDate(vacacion.fechaFin)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-center font-semibold text-gray-100">
                        {vacacion.diasSolicitados}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">{getStatusBadge(vacacion.estado)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
