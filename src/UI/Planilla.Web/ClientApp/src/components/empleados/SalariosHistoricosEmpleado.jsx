import React, { useEffect, useState, useCallback } from 'react';
import { Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';
import CuadriculaMeses from '../importacion/CuadriculaMeses';

// ============================================================
// Salarios devengados por mes de un empleado (los últimos 60), dentro de
// su ficha. Los meses con planilla en Pagly se muestran en verde y no se
// editan; los demás se pueden escribir a mano y se guardan al salir de la
// celda. Es la misma cuadrícula de la importación.
// ============================================================

export default function SalariosHistoricosEmpleado({ empleadoId }) {
  const [meses, setMeses] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(null); // "anio-mes"

  const cargar = useCallback(async () => {
    try {
      setCargando(true);
      const data = await api.get(`/api/empleados/${empleadoId}/devengado-mensual`);
      setMeses((Array.isArray(data) ? data : []).map(m => ({
        anio: m.anio, mes: m.mes, monto: m.origen === 'SinDatos' ? null : m.total, origen: m.origen, editable: m.editable,
      })));
    } catch (e) {
      toast.error(e.message || 'No se pudieron cargar los salarios históricos');
    } finally {
      setCargando(false);
    }
  }, [empleadoId]);

  useEffect(() => { if (empleadoId) cargar(); }, [empleadoId, cargar]);

  // Se guarda al cambiar, con un pequeño retraso para no pegar por cada tecla.
  const timers = React.useRef({});
  const onChange = (anio, mes, monto) => {
    const clave = `${anio}-${mes}`;
    setMeses(prev => prev.map(m => (m.anio === anio && m.mes === mes) ? { ...m, monto } : m));
    clearTimeout(timers.current[clave]);
    timers.current[clave] = setTimeout(async () => {
      try {
        setGuardando(clave);
        if (monto == null) {
          await api.delete(`/api/empleados/${empleadoId}/devengado-mensual/${anio}/${mes}`).catch(e => {
            if (e.statusCode !== 404) throw e;
          });
          setMeses(prev => prev.map(m => (m.anio === anio && m.mes === mes) ? { ...m, origen: 'SinDatos' } : m));
        } else {
          await api.put(`/api/empleados/${empleadoId}/devengado-mensual/${anio}/${mes}`, { salario: monto });
          setMeses(prev => prev.map(m => (m.anio === anio && m.mes === mes) ? { ...m, origen: 'Manual' } : m));
        }
      } catch (e) {
        toast.error(e.message || 'No se pudo guardar el mes');
        cargar();
      } finally {
        setGuardando(null);
      }
    }, 600);
  };

  if (!empleadoId) return null;

  return (
    <div className="border border-navy-600 rounded-lg p-4 bg-navy-950/50">
      <div className="flex items-baseline justify-between mb-1">
        <h4 className="text-sm font-semibold text-emerald-400">Salarios devengados por mes</h4>
        {guardando && <span className="text-xs text-primary-300 flex items-center gap-1"><Loader2 className="w-3 h-3 animate-spin" /> guardando…</span>}
      </div>
      <p className="text-xs text-gray-500 mb-3">
        Los últimos 60 meses. Con ellos se calculan la prima de antigüedad, la indemnización, las vacaciones y el décimo.
        Los meses en <span className="text-primary-300">verde (P)</span> salen de las planillas de Pagly y no se editan; los demás puedes escribirlos aquí.
      </p>
      {cargando ? (
        <div className="h-24 flex items-center justify-center"><Loader2 className="w-5 h-5 animate-spin text-primary-500" /></div>
      ) : (
        <CuadriculaMeses meses={meses} onChange={onChange} />
      )}
    </div>
  );
}
