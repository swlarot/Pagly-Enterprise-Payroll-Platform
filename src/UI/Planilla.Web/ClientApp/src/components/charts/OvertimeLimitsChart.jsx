import React from 'react';

/**
 * Componente de gráfico de barras de progreso para límites legales de horas extra
 * @param {Object} props
 * @param {number} props.horasDelDia - Horas extra trabajadas hoy
 * @param {number} props.horasDeLaSemana - Horas extra trabajadas esta semana
 * @param {number} props.limiteDiario - Límite diario (3h)
 * @param {number} props.limiteSemanal - Límite semanal (9h)
 */
export default function OvertimeLimitsChart({ 
    horasDelDia = 0, 
    horasDeLaSemana = 0, 
    limiteDiario = 3, 
    limiteSemanal = 9 
}) {
    const porcentajeDia = Math.min(100, (horasDelDia / limiteDiario) * 100);
    const porcentajeSemana = Math.min(100, (horasDeLaSemana / limiteSemanal) * 100);
    const horasDisponiblesDia = Math.max(0, limiteDiario - horasDelDia);
    const horasDisponiblesSemana = Math.max(0, limiteSemanal - horasDeLaSemana);

    // Determinar color según porcentaje
    const getColor = (porcentaje) => {
        if (porcentaje >= 90) return 'bg-red-500';
        if (porcentaje >= 70) return 'bg-yellow-500';
        return 'bg-green-500';
    };

    const getTextColor = (porcentaje) => {
        if (porcentaje >= 90) return 'text-red-400';
        if (porcentaje >= 70) return 'text-yellow-400';
        return 'text-green-400';
    };

    return (
        <div className="space-y-4">
            {/* Límite Diario */}
            <div>
                <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium text-gray-300">Límite Diario (3h)</span>
                    <span className={`text-sm font-bold ${getTextColor(porcentajeDia)}`}>
                        {horasDelDia.toFixed(2)}h / {limiteDiario}h
                    </span>
                </div>
                <div className="w-full bg-navy-800 rounded-full h-3 overflow-hidden">
                    <div
                        className={`h-full transition-all duration-300 ${getColor(porcentajeDia)}`}
                        style={{ width: `${porcentajeDia}%` }}
                    />
                </div>
                {horasDisponiblesDia > 0 && (
                    <p className="text-xs text-gray-400 mt-1">
                        Quedan {horasDisponiblesDia.toFixed(2)}h disponibles hoy
                    </p>
                )}
                {horasDisponiblesDia <= 0 && horasDelDia > 0 && (
                    <p className="text-xs text-red-400 mt-1">
                        ⚠️ Límite diario excedido
                    </p>
                )}
            </div>

            {/* Límite Semanal */}
            <div>
                <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium text-gray-300">Límite Semanal (9h)</span>
                    <span className={`text-sm font-bold ${getTextColor(porcentajeSemana)}`}>
                        {horasDeLaSemana.toFixed(2)}h / {limiteSemanal}h
                    </span>
                </div>
                <div className="w-full bg-navy-800 rounded-full h-3 overflow-hidden">
                    <div
                        className={`h-full transition-all duration-300 ${getColor(porcentajeSemana)}`}
                        style={{ width: `${porcentajeSemana}%` }}
                    />
                </div>
                {horasDisponiblesSemana > 0 && (
                    <p className="text-xs text-gray-400 mt-1">
                        Quedan {horasDisponiblesSemana.toFixed(2)}h disponibles esta semana
                    </p>
                )}
                {horasDisponiblesSemana <= 0 && horasDeLaSemana > 0 && (
                    <p className="text-xs text-red-400 mt-1">
                        ⚠️ Límite semanal excedido
                    </p>
                )}
            </div>
        </div>
    );
}
