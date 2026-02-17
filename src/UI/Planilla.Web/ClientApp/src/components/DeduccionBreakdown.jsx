import React, { useState, useEffect } from 'react';
import {
  Loader2,
  AlertTriangle,
  Info,
  ShieldAlert,
  Scale,
  Heart,
  DollarSign,
  TrendingDown,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { Card, CardHeader, CardBody } from './ui/Card';
import { Badge } from './ui/Badge';
import { api } from '../services/api';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Formats a number as a Panama-locale currency string.
 * E.g. 1234.5 → "B/. 1,234.50"
 */
function formatCurrency(value) {
  if (value === null || value === undefined) return 'B/. —';
  return `B/. ${Number(value).toLocaleString('es-PA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

/**
 * Returns display label, Badge variant, and an icon component for each
 * deduction category string returned by the API.
 */
function getCategoryMeta(categoria) {
  switch (categoria) {
    case 'PensionAlimenticia':
      return {
        label: 'Pensión Alimenticia',
        variant: 'danger',
        Icon: Heart,
        rowClass: 'border-l-2 border-red-500/50',
      };
    case 'EmbargoJudicial':
      return {
        label: 'Embargo Judicial',
        variant: 'warning',
        Icon: Scale,
        rowClass: 'border-l-2 border-amber-500/50',
      };
    case 'Voluntaria':
      return {
        label: 'Voluntaria',
        variant: 'info',
        Icon: Heart,
        rowClass: 'border-l-2 border-blue-500/50',
      };
    case 'CSS':
      return {
        label: 'CSS',
        variant: 'success',
        Icon: ShieldAlert,
        rowClass: 'border-l-2 border-emerald-500/50',
      };
    case 'ISR':
      return {
        label: 'ISR',
        variant: 'success',
        Icon: DollarSign,
        rowClass: 'border-l-2 border-emerald-500/50',
      };
    case 'SE':
      return {
        label: 'Seguro Educativo',
        variant: 'success',
        Icon: DollarSign,
        rowClass: 'border-l-2 border-emerald-500/50',
      };
    default:
      return {
        label: categoria ?? 'Otra',
        variant: 'default',
        Icon: TrendingDown,
        rowClass: 'border-l-2 border-gray-500/30',
      };
  }
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

/** Summary card showing three KPIs across the top */
function SummaryBar({ salarioMinimoAplicado, tuvoLimitacion, montoLimitadoTotal }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
      {/* Salario Mínimo */}
      <div className="bg-navy-950 border border-navy-700 rounded-lg px-5 py-4">
        <p className="text-xs font-medium text-gray-400 uppercase tracking-wider mb-1">
          Salario Mínimo Aplicado
        </p>
        <p className="text-xl font-bold text-emerald-400">
          {formatCurrency(salarioMinimoAplicado)}
        </p>
        <p className="text-xs text-gray-500 mt-1">Piso inembargable del período</p>
      </div>

      {/* Limitación */}
      <div className="bg-navy-950 border border-navy-700 rounded-lg px-5 py-4">
        <p className="text-xs font-medium text-gray-400 uppercase tracking-wider mb-1">
          ¿Hubo Limitación?
        </p>
        <div className="mt-1">
          {tuvoLimitacion ? (
            <Badge variant="warning">
              <AlertTriangle className="w-3 h-3 mr-1 inline-block" />
              Sí — deducciones limitadas
            </Badge>
          ) : (
            <Badge variant="success">Sin limitaciones</Badge>
          )}
        </div>
        <p className="text-xs text-gray-500 mt-2">
          {tuvoLimitacion
            ? 'Una o más deducciones fueron reducidas'
            : 'Todas las deducciones se aplicaron en su totalidad'}
        </p>
      </div>

      {/* Monto Limitado Total */}
      <div className="bg-navy-950 border border-navy-700 rounded-lg px-5 py-4">
        <p className="text-xs font-medium text-gray-400 uppercase tracking-wider mb-1">
          Total Limitado
        </p>
        <p
          className={`text-xl font-bold ${
            montoLimitadoTotal > 0 ? 'text-amber-400' : 'text-gray-400'
          }`}
        >
          {formatCurrency(montoLimitadoTotal)}
        </p>
        <p className="text-xs text-gray-500 mt-1">
          Monto total que no pudo aplicarse
        </p>
      </div>
    </div>
  );
}

/** Alert banner shown when tuvoLimitacion is true */
function LimitacionAlert({ salarioMinimoAplicado }) {
  return (
    <div className="flex items-start gap-3 px-4 py-3 mb-6 bg-amber-500/10 border border-amber-500/30 rounded-lg text-amber-300">
      <Info className="w-5 h-5 flex-shrink-0 mt-0.5" />
      <div>
        <p className="font-semibold text-sm">Deducciones limitadas por salario mínimo</p>
        <p className="text-xs mt-1 text-amber-400/80 leading-relaxed">
          El Código de Trabajo panameño (Art.&nbsp;158) garantiza al trabajador recibir al
          menos el salario mínimo vigente luego de aplicar todas las deducciones. En este
          período el piso inembargable fue de&nbsp;
          <strong>{formatCurrency(salarioMinimoAplicado)}</strong>. Las deducciones que
          superaban dicho límite fueron reducidas automáticamente para respetar esta
          protección legal.
        </p>
      </div>
    </div>
  );
}

/** Visual floor indicator row inserted in the table */
function SalarioMinimoCue({ salarioMinimoAplicado }) {
  return (
    <tr>
      <td
        colSpan={8}
        className="px-4 py-2 bg-emerald-500/5 border-y border-emerald-500/30"
      >
        <div className="flex items-center gap-2 text-emerald-400 text-xs font-medium">
          <div className="flex-1 border-t border-dashed border-emerald-500/40" />
          <span className="whitespace-nowrap">
            Piso de Salario Mínimo — {formatCurrency(salarioMinimoAplicado)}
          </span>
          <div className="flex-1 border-t border-dashed border-emerald-500/40" />
        </div>
      </td>
    </tr>
  );
}

/** Single row in the deductions table */
function DeduccionRow({ deduccion, index }) {
  const { label, variant, Icon, rowClass } = getCategoryMeta(deduccion.categoria);
  const fueReducida = deduccion.montoLimitado > 0;

  return (
    <tr
      className={`
        ${rowClass}
        ${fueReducida ? 'bg-amber-500/5' : 'hover:bg-navy-800/50'}
        transition-colors
      `}
    >
      {/* # orden */}
      <td className="px-4 py-3 text-center text-sm text-gray-500">
        {deduccion.ordenAplicacion ?? index + 1}
      </td>

      {/* Categoría */}
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <Icon className="w-4 h-4 text-gray-400 flex-shrink-0" />
          <Badge variant={variant}>{label}</Badge>
        </div>
      </td>

      {/* Descripción */}
      <td className="px-4 py-3 text-sm text-gray-300">
        {deduccion.descripcion || '—'}
      </td>

      {/* Acreedor */}
      <td className="px-4 py-3 text-sm text-gray-400">
        {deduccion.nombreAcreedor || '—'}
      </td>

      {/* Monto Solicitado */}
      <td className="px-4 py-3 text-sm text-right text-gray-300 font-mono">
        {formatCurrency(deduccion.montoSolicitado)}
      </td>

      {/* Monto Aplicado */}
      <td className="px-4 py-3 text-sm text-right font-mono">
        <span
          className={
            fueReducida ? 'text-amber-400 font-semibold' : 'text-emerald-400'
          }
        >
          {formatCurrency(deduccion.montoAplicado)}
        </span>
      </td>

      {/* Monto Limitado */}
      <td className="px-4 py-3 text-sm text-right font-mono">
        {deduccion.montoLimitado > 0 ? (
          <span className="text-red-400 font-semibold">
            {formatCurrency(deduccion.montoLimitado)}
          </span>
        ) : (
          <span className="text-gray-600">—</span>
        )}
      </td>

      {/* Razón de Limitación */}
      <td className="px-4 py-3 text-xs text-gray-500 max-w-[180px]">
        {deduccion.razonLimitacion ? (
          <span className="text-amber-400/80">{deduccion.razonLimitacion}</span>
        ) : (
          '—'
        )}
      </td>
    </tr>
  );
}

/** Empty state when deducciones array is empty */
function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <TrendingDown className="w-12 h-12 text-gray-600 mb-4" />
      <p className="text-gray-400 font-medium">Sin deducciones en este detalle</p>
      <p className="text-gray-600 text-sm mt-1">
        No se encontraron deducciones aplicadas para este empleado en el período seleccionado.
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main Component
// ---------------------------------------------------------------------------

/**
 * DeduccionBreakdown
 *
 * Displays the full deduction breakdown for a single payroll detail.
 * Fetches from: GET /api/payrollheaders/{payrollHeaderId}/details/{detailId}/deducciones
 *
 * Props:
 *   payrollHeaderId  — ID of the parent payroll header
 *   detailId         — ID of the payroll detail line
 */
export default function DeduccionBreakdown({ payrollHeaderId, detailId }) {
  // Estado
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  // Carga inicial
  useEffect(() => {
    if (!payrollHeaderId || !detailId) return;
    loadDeducciones();
  }, [payrollHeaderId, detailId]);

  const loadDeducciones = async () => {
    try {
      setIsLoading(true);
      setError(null);

      const result = await api.get(
        `/api/payrollheaders/${payrollHeaderId}/details/${detailId}/deducciones`
      );
      setData(result);
    } catch (err) {
      const message = err?.message || 'Error al cargar el desglose de deducciones';
      setError(message);
      toast.error(message);
    } finally {
      setIsLoading(false);
    }
  };

  // ---- Renderizado de estados ----

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20">
        <Loader2 className="w-8 h-8 animate-spin text-emerald-500" />
        <span className="ml-3 text-gray-400 text-sm">Cargando deducciones...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-3 px-4 py-4 bg-red-500/10 border border-red-500/30 rounded-lg text-red-400">
        <AlertTriangle className="w-5 h-5 flex-shrink-0" />
        <div>
          <p className="font-medium text-sm">Error al cargar deducciones</p>
          <p className="text-xs mt-0.5 text-red-400/70">{error}</p>
        </div>
        <button
          onClick={loadDeducciones}
          className="ml-auto px-3 py-1.5 text-xs font-medium border border-red-500/40 rounded-md hover:bg-red-500/10 transition-colors"
        >
          Reintentar
        </button>
      </div>
    );
  }

  if (!data) return null;

  const { salarioMinimoAplicado, tuvoLimitacion, montoLimitadoTotal, deducciones } = data;
  const hayDeducciones = Array.isArray(deducciones) && deducciones.length > 0;

  // Índice donde insertar la línea de salario mínimo (antes de la primera deducción
  // cuyo saldo disponible antes sea menor o igual al salario mínimo aplicado)
  const floorInsertIndex = hayDeducciones
    ? deducciones.findIndex(
        (d) => d.saldoDisponibleAntes <= salarioMinimoAplicado * 1.05
      )
    : -1;

  return (
    <div className="space-y-2">
      {/* Alerta de limitación */}
      {tuvoLimitacion && (
        <LimitacionAlert salarioMinimoAplicado={salarioMinimoAplicado} />
      )}

      {/* Resumen de KPIs */}
      <SummaryBar
        salarioMinimoAplicado={salarioMinimoAplicado}
        tuvoLimitacion={tuvoLimitacion}
        montoLimitadoTotal={montoLimitadoTotal}
      />

      {/* Tabla de deducciones */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <h3 className="font-semibold text-gray-100 text-base">
                Detalle de Deducciones Aplicadas
              </h3>
              <p className="text-xs text-gray-500 mt-0.5">
                {hayDeducciones
                  ? `${deducciones.length} deducción${deducciones.length !== 1 ? 'es' : ''} en orden de aplicación`
                  : 'Sin deducciones registradas'}
              </p>
            </div>
            {hayDeducciones && (
              <Badge variant={tuvoLimitacion ? 'warning' : 'success'}>
                {tuvoLimitacion ? 'Con limitaciones' : 'Aplicación completa'}
              </Badge>
            )}
          </div>
        </CardHeader>

        <CardBody className="p-0">
          {!hayDeducciones ? (
            <div className="px-6 py-4">
              <EmptyState />
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                {/* Encabezados */}
                <thead>
                  <tr className="border-b border-navy-700 bg-navy-950/60">
                    <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider w-10">
                      #
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Categoría
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Descripción
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Acreedor
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Solicitado
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Aplicado
                    </th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Limitado
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      Razón
                    </th>
                  </tr>
                </thead>

                <tbody className="divide-y divide-navy-800">
                  {deducciones.map((ded, index) => (
                    <React.Fragment key={ded.id ?? index}>
                      {/* Línea de piso de salario mínimo cuando corresponde */}
                      {floorInsertIndex === index && (
                        <SalarioMinimoCue
                          salarioMinimoAplicado={salarioMinimoAplicado}
                        />
                      )}
                      <DeduccionRow deduccion={ded} index={index} />
                    </React.Fragment>
                  ))}

                  {/* Si nunca se insertó la cue (todas las deducciones estuvieron
                      por encima del piso), mostrarla al final */}
                  {floorInsertIndex === -1 && salarioMinimoAplicado > 0 && (
                    <SalarioMinimoCue
                      salarioMinimoAplicado={salarioMinimoAplicado}
                    />
                  )}
                </tbody>

                {/* Totales */}
                <tfoot>
                  <tr className="border-t-2 border-navy-600 bg-navy-950/80">
                    <td colSpan={4} className="px-4 py-3 text-xs text-gray-500 font-medium">
                      TOTALES
                    </td>
                    {/* Total Solicitado */}
                    <td className="px-4 py-3 text-right text-sm font-bold text-gray-200 font-mono">
                      {formatCurrency(
                        deducciones.reduce((acc, d) => acc + (d.montoSolicitado ?? 0), 0)
                      )}
                    </td>
                    {/* Total Aplicado */}
                    <td className="px-4 py-3 text-right text-sm font-bold text-emerald-400 font-mono">
                      {formatCurrency(
                        deducciones.reduce((acc, d) => acc + (d.montoAplicado ?? 0), 0)
                      )}
                    </td>
                    {/* Total Limitado */}
                    <td className="px-4 py-3 text-right text-sm font-bold font-mono">
                      <span className={montoLimitadoTotal > 0 ? 'text-amber-400' : 'text-gray-600'}>
                        {formatCurrency(montoLimitadoTotal)}
                      </span>
                    </td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </CardBody>
      </Card>

      {/* Leyenda de colores */}
      <div className="flex flex-wrap items-center gap-4 pt-1 pb-2 px-1">
        <span className="text-xs text-gray-600 font-medium uppercase tracking-wider">
          Leyenda:
        </span>
        {[
          { label: 'Pensión Alimenticia', variant: 'danger' },
          { label: 'Embargo Judicial', variant: 'warning' },
          { label: 'Voluntaria', variant: 'info' },
          { label: 'CSS / ISR / SE', variant: 'success' },
          { label: 'Otra', variant: 'default' },
        ].map(({ label, variant }) => (
          <div key={label} className="flex items-center gap-1.5">
            <Badge variant={variant}>{label}</Badge>
          </div>
        ))}
      </div>
    </div>
  );
}
