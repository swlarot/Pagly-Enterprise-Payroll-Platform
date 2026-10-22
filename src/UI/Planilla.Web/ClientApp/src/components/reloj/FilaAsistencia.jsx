import React from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { minutosAHoras } from '../../utils/reloj';

// ============================================================
// Una persona en el mes: sus totales (días, llegadas tarde, ausencias,
// posibles horas extra) y, desplegada, cada día con su entrada, su salida
// y lo que Pagly ve contra el horario. Ahí mismo se cambia su horario.
// ============================================================

const ESTADO = {
    Completo: 'border-navy-600 text-gray-300',
    Tarde: 'border-amber-500/40 text-amber-200 bg-amber-500/10',
    'Sin salida': 'border-amber-500/40 text-amber-200',
    Ausente: 'border-red-500/40 text-red-200 bg-red-500/10',
    'Día libre trabajado': 'border-blue-500/40 text-blue-200',
    Libre: 'border-navy-800 text-gray-600',
    Feriado: 'border-navy-800 text-gray-500',
    Licencia: 'border-navy-700 text-gray-400',
    Vacaciones: 'border-navy-700 text-gray-400',
    'Ausencia registrada': 'border-navy-700 text-gray-400',
    'Sin datos': 'border-navy-800 text-gray-600',
};

const DIA = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];

export default function FilaAsistencia({ persona, horarios, abierta, onAbrir, onCambiarHorario, ocupada }) {
    return (
        <li className={`bg-navy-900 border rounded-xl overflow-hidden ${abierta ? 'border-navy-600' : 'border-navy-700'}`}>
            <button type="button" onClick={onAbrir} aria-expanded={abierta}
                className="w-full text-left px-4 py-3 flex flex-wrap items-center gap-x-5 gap-y-1 hover:bg-navy-800/60">
                {abierta ? <ChevronUp className="w-4 h-4 text-gray-500" /> : <ChevronDown className="w-4 h-4 text-gray-500" />}
                <span className="font-medium text-gray-100 min-w-[10rem]">{persona.nombre}</span>
                <span className="text-xs text-gray-500 font-mono">{persona.codigoReloj ? `n.º ${persona.codigoReloj}` : 'sin número'}</span>
                <span className="text-sm text-gray-400">{persona.diasTrabajados} {persona.diasTrabajados === 1 ? 'día' : 'días'}</span>
                {persona.tardanzas > 0 && <span className="text-sm text-amber-300">{persona.tardanzas} tarde ({persona.minutosTarde} min)</span>}
                {persona.ausencias > 0 && <span className="text-sm text-red-300">{persona.ausencias} {persona.ausencias === 1 ? 'ausencia' : 'ausencias'}</span>}
                {persona.sinSalida > 0 && <span className="text-sm text-amber-300">{persona.sinSalida} sin salida</span>}
                <span className="ml-auto text-sm text-gray-300">
                    Extra <span className="font-mono">{minutosAHoras(persona.minutosExtra)}</span>
                    {persona.horasExtraPorProponer > 0 && <span className="text-xs text-gray-500"> · {persona.horasExtraPorProponer} por proponer</span>}
                </span>
            </button>

            {abierta && (
                <div className="border-t border-navy-700 px-4 py-3 space-y-3">
                    <div className="flex flex-wrap items-center gap-2 text-sm">
                        <label htmlFor={`horario-${persona.empleadoId}`} className="text-gray-400">Horario</label>
                        <select id={`horario-${persona.empleadoId}`} disabled={ocupada}
                            value={persona.horarioAsignadoId ?? ''}
                            onChange={e => onCambiarHorario(e.target.value ? Number(e.target.value) : null)}
                            className="bg-navy-800 border border-navy-600 text-gray-100 rounded-md px-2 py-1 text-sm">
                            <option value="">El predeterminado{horarios.find(h => h.esPredeterminado) ? ` (${horarios.find(h => h.esPredeterminado).nombre})` : ''}</option>
                            {horarios.map(h => <option key={h.id} value={h.id}>{h.nombre}</option>)}
                        </select>
                    </div>
                    <ul className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-1.5">
                        {persona.dias.map(d => {
                            const f = new Date(d.fecha);
                            return (
                                <li key={d.fecha} className={`rounded-md border px-2 py-1.5 text-xs ${ESTADO[d.estado] ?? ESTADO.Completo}`}
                                    title={d.minutosTarde > 0 ? `${d.minutosTarde} min tarde` : d.minutosExtra > 0 ? `${d.minutosExtra} min después de su salida` : d.estado}>
                                    <span className="block text-gray-500">{DIA[f.getUTCDay()]} {f.getUTCDate()}</span>
                                    <span className="block">{d.entrada ? `${d.entrada}${d.salida ? `–${d.salida}` : ''}` : d.estado}</span>
                                    {d.marcas > 0 && d.estado !== 'Completo' && <span className="block text-[11px] opacity-80">{d.estado}</span>}
                                    {d.minutosExtra > 0 && <span className="block text-[11px] text-blue-200">+{d.minutosExtra} min{d.horaExtraPropuesta ? ' · propuesta' : ''}</span>}
                                </li>
                            );
                        })}
                    </ul>
                </div>
            )}
        </li>
    );
}
