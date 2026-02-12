import React from 'react';
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip, Legend } from 'recharts';

/**
 * Gráfico de torta mostrando distribución de costos por tipo de hora extra
 * @param {Object} props
 * @param {Array} props.data - Array de objetos con { name: string, value: number, color: string }
 * @param {string} props.title - Título del gráfico
 */
export default function OvertimeCostDistributionPieChart({ data = [], title = 'Distribución de Costos por Tipo' }) {
    if (!data || data.length === 0) {
        return (
            <div className="flex items-center justify-center h-64 text-gray-400">
                <p>No hay datos para mostrar</p>
            </div>
        );
    }

    const COLORS = [
        '#3B82F6', // Azul - Diurna
        '#8B5CF6', // Púrpura - Nocturna
        '#EC4899', // Rosa - Domingo/Feriado
        '#F59E0B', // Ámbar - Fiesta Nacional
        '#10B981', // Verde - Mixta
        '#EF4444', // Rojo - Exceso
    ];

    return (
        <div className="w-full">
            <h3 className="text-lg font-semibold text-gray-100 mb-4">{title}</h3>
            <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                    <Pie
                        data={data}
                        cx="50%"
                        cy="50%"
                        labelLine={false}
                        label={({ name, percent }) => `${name}: ${(percent * 100).toFixed(0)}%`}
                        outerRadius={80}
                        fill="#8884d8"
                        dataKey="value"
                    >
                        {data.map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.color || COLORS[index % COLORS.length]} />
                        ))}
                    </Pie>
                    <Tooltip 
                        contentStyle={{ 
                            backgroundColor: '#1F2937', 
                            border: '1px solid #374151',
                            borderRadius: '6px',
                            color: '#F3F4F6'
                        }}
                        formatter={(value) => `$${value.toFixed(2)}`}
                    />
                    <Legend 
                        wrapperStyle={{ color: '#9CA3AF' }}
                        formatter={(value) => value}
                    />
                </PieChart>
            </ResponsiveContainer>
        </div>
    );
}
