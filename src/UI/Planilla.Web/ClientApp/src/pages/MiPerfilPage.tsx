import { useEffect, useState } from 'react';
import { formatDate } from '../utils/date';
import { useAsyncLoad } from '../hooks/useAsyncLoad';
import { Card, CardHeader, CardBody } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { User, DollarSign, Calendar, FileText, Clock, Briefcase, ArrowLeft, Search } from 'lucide-react';
import { api } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { TenantRole } from '../types/api';
import type { EmpleadoDetalleDto, PlanillaMiPerfilDto, VacacionResumenDto } from '../types/api';
import { formatCurrency } from '../utils/currency';

type Empleado = EmpleadoDetalleDto;
type Planilla = PlanillaMiPerfilDto;
type Vacacion = VacacionResumenDto;

// ============================================================
// Utilidades compartidas
// ============================================================

const getStatusBadge = (status: string) => {
  const statusMap: Record<string, { label: string; variant: 'success' | 'warning' | 'danger' | 'info' }> = {
    Pendiente: { label: 'Pendiente', variant: 'warning' },
    Aprobada: { label: 'Aprobada', variant: 'success' },
    Rechazada: { label: 'Rechazada', variant: 'danger' },
    Cancelada: { label: 'Cancelada', variant: 'danger' },
  };
  const mapped = statusMap[status] || { label: status, variant: 'info' };
  return <Badge variant={mapped.variant}>{mapped.label}</Badge>;
};

const avatarColors = [
  'bg-gradient-to-br from-emerald-600 to-emerald-500',
  'bg-gradient-to-br from-blue-600 to-blue-500',
  'bg-gradient-to-br from-purple-600 to-purple-500',
  'bg-gradient-to-br from-amber-600 to-amber-500',
  'bg-gradient-to-br from-red-600 to-red-500',
];

// ============================================================
// Componente: Detalle de un empleado (perfil completo)
// ============================================================

interface EmployeeDetailProps {
  empleado: Empleado;
  isOwnerView: boolean;
  onBack?: () => void;
}

function EmployeeDetail({ empleado, isOwnerView, onBack }: EmployeeDetailProps) {
  const [planillas, setPlanillas] = useState<Planilla[]>([]);
  const [vacaciones, setVacaciones] = useState<Vacacion[]>([]);
  const { isLoading, run } = useAsyncLoad();

  useEffect(() => {
    loadEmployeeData();
  }, [empleado.id]);

  const loadEmployeeData = () => run(async () => {
    // Cargar planillas (con filtro por empleadoId si es Owner)
    const planillasUrl = isOwnerView
      ? `/api/payrollheaders?empleadoId=${empleado.id}`
      : '/api/payrollheaders';
    const planillasRes = await api.get<Planilla[]>(planillasUrl);

    // Filtrar detalles para mostrar solo los del empleado seleccionado
    const filteredPlanillas = planillasRes.map(p => ({
      ...p,
      details: p.details?.filter(d => d.empleadoId === empleado.id) || [],
    }));
    setPlanillas(filteredPlanillas.slice(0, 5));

    // Cargar vacaciones (con filtro por empleadoId si es Owner)
    const vacacionesUrl = isOwnerView
      ? `/api/vacaciones?empleadoId=${empleado.id}`
      : '/api/vacaciones';
    const vacacionesRes = await api.get<Vacacion[]>(vacacionesUrl);
    setVacaciones(vacacionesRes.slice(0, 5));
  }, 'Error al cargar datos del empleado');

  const initials = `${empleado.nombre?.[0] || ''}${empleado.apellido?.[0] || ''}`.toUpperCase();
  const color = avatarColors[(empleado.nombre?.charCodeAt(0) || 0) % avatarColors.length];

  return (
    <div className="space-y-6">
      {/* Header con botón Volver */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          {isOwnerView && onBack && (
            <button
              onClick={onBack}
              className="inline-flex items-center gap-2 text-gray-400 hover:text-gray-200 transition-colors"
            >
              <ArrowLeft className="w-5 h-5" />
              <span className="text-sm font-medium">Volver a la lista</span>
            </button>
          )}
          <div>
            <h1 className="text-3xl font-bold font-display text-gray-100">
              {isOwnerView ? `${empleado.nombre} ${empleado.apellido}` : 'Mi Perfil'}
            </h1>
            <p className="text-gray-400 mt-1">
              {isOwnerView ? 'Perfil del empleado' : 'Información personal y resumen de actividad'}
            </p>
          </div>
        </div>
      </div>

      {/* Información Personal */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className={`w-12 h-12 rounded-full flex items-center justify-center text-white text-sm font-bold ${color}`}>
              {initials}
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-100">Información Personal</h2>
              <p className="text-sm text-gray-400">
                {isOwnerView ? 'Datos del empleado' : 'Tus datos básicos como empleado'}
              </p>
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
              <p className="text-lg font-semibold text-gray-100">{empleado.email || '—'}</p>
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
              <label className="block text-sm font-medium text-gray-400 mb-1">Salario Base (Mensual)</label>
              <p className="text-lg font-semibold font-mono text-gray-100">{formatCurrency(empleado.salarioBase)}</p>
            </div>
            {empleado.hourlyRate != null && empleado.hourlyRate > 0 && (
              <div>
                <label className="block text-sm font-medium text-gray-400 mb-1">Tasa por Hora</label>
                <p className="text-lg font-semibold font-mono text-emerald-400">{formatCurrency(empleado.hourlyRate)}</p>
              </div>
            )}
          </div>
        </CardBody>
      </Card>

      {/* Tarjetas de Estadísticas */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Vacaciones Disponibles</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">—</p>
                <p className="text-sm text-gray-500 mt-1">próximamente</p>
              </div>
              <div className="w-12 h-12 bg-blue-500/15 rounded-full flex items-center justify-center">
                <Calendar className="w-6 h-6 text-primary-400" />
              </div>
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Horas Extra (Mes)</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">—</p>
                <p className="text-sm text-gray-500 mt-1">próximamente</p>
              </div>
              <div className="w-12 h-12 bg-green-500/15 rounded-full flex items-center justify-center">
                <Clock className="w-6 h-6 text-green-400" />
              </div>
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-400">Préstamos Activos</p>
                <p className="text-3xl font-bold text-gray-100 mt-2">—</p>
                <p className="text-sm text-gray-500 mt-1">próximamente</p>
              </div>
              <div className="w-12 h-12 bg-amber-500/15 rounded-full flex items-center justify-center">
                <DollarSign className="w-6 h-6 text-amber-400" />
              </div>
            </div>
          </CardBody>
        </Card>
      </div>

      {/* Últimas Planillas */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-purple-500/15 rounded-full flex items-center justify-center">
              <FileText className="w-5 h-5 text-purple-400" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-100">
                {isOwnerView ? 'Últimas Planillas' : 'Mis Últimas Planillas'}
              </h2>
              <p className="text-sm text-gray-400">Recibos de pago recientes</p>
            </div>
          </div>
        </CardHeader>
        <CardBody>
          {isLoading ? (
            <div className="flex justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600" />
            </div>
          ) : planillas.length === 0 ? (
            <p className="text-gray-400 text-center py-8">No hay planillas registradas</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-navy-700">
                <thead className="bg-navy-800">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">Período</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">Estado</th>
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-400 uppercase">Salario Neto</th>
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
                          : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>

      {/* Solicitudes de Vacaciones */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-blue-500/15 rounded-full flex items-center justify-center">
                <Briefcase className="w-5 h-5 text-primary-400" />
              </div>
              <div>
                <h2 className="text-xl font-semibold text-gray-100">
                  {isOwnerView ? 'Solicitudes de Vacaciones' : 'Mis Solicitudes de Vacaciones'}
                </h2>
                <p className="text-sm text-gray-400">Historial de solicitudes</p>
              </div>
            </div>
          </div>
        </CardHeader>
        <CardBody>
          {isLoading ? (
            <div className="flex justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600" />
            </div>
          ) : vacaciones.length === 0 ? (
            <p className="text-gray-400 text-center py-8">No hay solicitudes de vacaciones</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-navy-700">
                <thead className="bg-navy-800">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">Fecha Inicio</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">Fecha Fin</th>
                    <th className="px-6 py-3 text-center text-xs font-medium text-gray-400 uppercase">Días</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase">Estado</th>
                  </tr>
                </thead>
                <tbody className="bg-navy-900 divide-y divide-navy-700">
                  {vacaciones.map((vacacion) => (
                    <tr key={vacacion.id} className="hover:bg-navy-800">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-100">{formatDate(vacacion.fechaInicio)}</td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-100">{formatDate(vacacion.fechaFin)}</td>
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

// ============================================================
// Componente: Lista de empleados (vista Owner)
// ============================================================

interface EmployeeListProps {
  empleados: Empleado[];
  onSelectEmpleado: (empleado: Empleado) => void;
}

function EmployeeList({ empleados, onSelectEmpleado }: EmployeeListProps) {
  const [searchTerm, setSearchTerm] = useState('');

  const filtered = empleados.filter((e) => {
    const term = searchTerm.toLowerCase();
    return (
      e.nombre.toLowerCase().includes(term) ||
      e.apellido.toLowerCase().includes(term) ||
      e.numeroIdentificacion.toLowerCase().includes(term) ||
      (e.departamentoNombre || '').toLowerCase().includes(term) ||
      (e.posicionNombre || '').toLowerCase().includes(term)
    );
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold font-display text-gray-100">Perfiles de Empleados</h1>
          <p className="text-gray-400 mt-1">
            Consulta la información de todos los empleados ({empleados.length})
          </p>
        </div>
      </div>

      {/* Barra de búsqueda */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-500" />
        <input
          type="text"
          placeholder="Buscar por nombre, cédula, departamento o posición..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="w-full pl-10 pr-4 py-3 bg-navy-900 border border-navy-700 rounded-xl text-gray-200 placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 focus:border-primary-500 transition-all"
        />
      </div>

      {/* Tabla de empleados */}
      <Card>
        <CardBody>
          {filtered.length === 0 ? (
            <p className="text-gray-400 text-center py-12">
              {searchTerm ? 'No se encontraron empleados con ese criterio' : 'No hay empleados registrados'}
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-navy-700">
                <thead>
                  <tr>
                    <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Empleado
                    </th>
                    <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Cédula
                    </th>
                    <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Departamento
                    </th>
                    <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Posición
                    </th>
                    <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Salario Base
                    </th>
                    <th className="text-center py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Estado
                    </th>
                    <th className="text-center py-3 px-4 text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Acciones
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-navy-700/50">
                  {filtered.map((emp) => {
                    const initials = `${emp.nombre?.[0] || ''}${emp.apellido?.[0] || ''}`.toUpperCase();
                    const color = avatarColors[(emp.nombre?.charCodeAt(0) || 0) % avatarColors.length];
                    return (
                      <tr
                        key={emp.id}
                        className="hover:bg-navy-800/50 transition-all cursor-pointer"
                        onClick={() => onSelectEmpleado(emp)}
                      >
                        <td className="py-3 px-4">
                          <div className="flex items-center gap-3">
                            <div className={`w-9 h-9 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0 ${color}`}>
                              {initials}
                            </div>
                            <div>
                              <p className="text-sm font-semibold text-gray-200">
                                {emp.nombre} {emp.apellido}
                              </p>
                              <p className="text-xs text-gray-500">{emp.email || `ID: ${emp.id}`}</p>
                            </div>
                          </div>
                        </td>
                        <td className="py-3 px-4 text-sm text-gray-300">{emp.numeroIdentificacion}</td>
                        <td className="py-3 px-4 text-sm text-gray-300">
                          {emp.departamentoNombre || <span className="text-gray-600">—</span>}
                        </td>
                        <td className="py-3 px-4 text-sm text-gray-300">
                          {emp.posicionNombre || <span className="text-gray-600">—</span>}
                        </td>
                        <td className="py-3 px-4 text-sm text-right font-mono text-gray-200">
                          {formatCurrency(emp.salarioBase)}
                        </td>
                        <td className="py-3 px-4 text-center">
                          <div className="flex items-center justify-center gap-2">
                            <div className={`w-2 h-2 rounded-full flex-shrink-0 ${
                              emp.estaActivo !== false ? 'bg-green-400' : 'bg-red-400'
                            }`} />
                            <span className={`text-xs font-medium ${
                              emp.estaActivo !== false ? 'text-green-400' : 'text-red-400'
                            }`}>
                              {emp.estaActivo !== false ? 'Activo' : 'Inactivo'}
                            </span>
                          </div>
                        </td>
                        <td className="py-3 px-4 text-center">
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              onSelectEmpleado(emp);
                            }}
                            className="inline-flex items-center gap-1.5 text-primary-400 hover:text-primary-300 font-medium text-sm transition-colors"
                          >
                            <User className="w-4 h-4" />
                            Ver Perfil
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

// ============================================================
// Componente principal: MiPerfilPage
// ============================================================

export default function MiPerfilPage() {
  const { hasRole } = useAuth();
  const isOwner = hasRole(TenantRole.Owner);

  const { isLoading, run } = useAsyncLoad();
  const [empleados, setEmpleados] = useState<Empleado[]>([]);
  const [selectedEmpleado, setSelectedEmpleado] = useState<Empleado | null>(null);

  useEffect(() => {
    loadEmpleados();
  }, []);

  const loadEmpleados = () => run(async () => {
    const data = await api.get<Empleado[]>('/api/empleados');
    setEmpleados(data || []);

    // Si no es Owner, seleccionar automáticamente el primer (y único) empleado
    if (!isOwner && data && data.length > 0) {
      setSelectedEmpleado(data[0]);
    }
  }, 'Error al cargar información');

  // Loading
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-emerald-600" />
      </div>
    );
  }

  // --- MODO OWNER ---
  if (isOwner) {
    // Si hay un empleado seleccionado, mostrar su perfil completo
    if (selectedEmpleado) {
      return (
        <EmployeeDetail
          empleado={selectedEmpleado}
          isOwnerView={true}
          onBack={() => setSelectedEmpleado(null)}
        />
      );
    }

    // Si no, mostrar la lista de todos los empleados
    return (
      <EmployeeList
        empleados={empleados}
        onSelectEmpleado={setSelectedEmpleado}
      />
    );
  }

  // --- MODO EMPLEADO ---
  if (!selectedEmpleado) {
    return (
      <div className="text-center py-12">
        <User className="w-16 h-16 text-gray-600 mx-auto mb-4" />
        <p className="text-gray-400 text-lg">No se encontró información de tu perfil</p>
        <p className="text-gray-500 text-sm mt-2">
          Contacta a tu administrador para vincular tu usuario a un empleado
        </p>
      </div>
    );
  }

  return (
    <EmployeeDetail
      empleado={selectedEmpleado}
      isOwnerView={false}
    />
  );
}
