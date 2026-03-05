import React from 'react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';

/**
 * Gráfico de barras mostrando horas extra por tipo
 * @param {Object} props
 * @param {Array} props.data - Array de objetos con { tipo: string, horas: number, monto: number }
 * @param {string} props.title - Título del gráfico
 */
export default function OvertimeByTypeBarChart({ data = [], title = 'Horas Extra por Tipo' }) {
    if (!data || data.length === 0) {
        return (
            <div className="flex items-center justify-center h-64 text-gray-400">
                <p>No hay datos para mostrar</p>
            </div>
        );
    }

    return (
        <div className="w-full">
            <h3 className="text-lg font-semibold text-gray-100 mb-4">{title}</h3>
            <ResponsiveContainer width="100%" height={300}>
                <BarChart data={data} margin={{ top: 20, right: 30, left: 20, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                    <XAxis 
                        dataKey="tipo" 
                        stroke="#9CA3AF"
                        tick={{ fill: '#9CA3AF', fontSize: 12 }}
                        angle={-45}
                        textAnchor="end"
                        height={80}
                    />
                    <YAxis 
                        stroke="#9CA3AF"
                        tick={{ fill: '#9CA3AF', fontSize: 12 }}
                        label={{ value: 'Horas', angle: -90, position: 'insideLeft', fill: '#9CA3AF' }}
                    />
                    <Tooltip 
                        contentStyle={{ 
                            backgroundColor: '#1F2937', 
                            border: '1px solid #374151',
                            borderRadius: '6px',
                            color: '#F3F4F6'
                        }}
                        formatter={(value, name) => {
                            if (name === 'horas') {
                                return [`${value.toFixed(2)}h`, 'Horas'];
                            }
                            return [value, name];
                        }}
                    />
                    <Legend 
                        wrapperStyle={{ color: '#9CA3AF' }}
                        formatter={(value) => {
                            if (value === 'horas') return 'Horas';
                            if (value === 'monto') return 'Monto (B/.)';
                            return value;
                        }}
                    />
                    <Bar 
                        dataKey="horas" 
                        fill="#3B82F6" 
                        name="Horas"
                        radius={[4, 4, 0, 0]}
                    />
                </BarChart>
            </ResponsiveContainer>
        </div>
    );
}
