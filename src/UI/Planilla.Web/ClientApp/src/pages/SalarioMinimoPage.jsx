import React, { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { api } from '../services/api';
import { Loader2, DollarSign, Save, Building2 } from 'lucide-react';

export default function SalarioMinimoPage() {
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [salarioMinimo, setSalarioMinimo] = useState('');
    const [actividadEconomica, setActividadEconomica] = useState('');
    const [configId, setConfigId] = useState(null);

    useEffect(() => {
        loadSalarioMinimo();
    }, []);

    const loadSalarioMinimo = async () => {
        try {
            setIsLoading(true);
            const data = await api.get('/api/configuracion/salario-minimo');
            setSalarioMinimo(data.salarioMinimoLegal?.toString() || '700');
            setActividadEconomica(data.actividadEconomica || '');
            setConfigId(data.configId);
        } catch (err) {
            if (err.statusCode === 404) {
                toast.error('No existe configuracion de planilla. Vaya a Configuracion y cree una primero.');
            } else {
                toast.error(err.message || 'Error al cargar salario minimo');
            }
        } finally {
            setIsLoading(false);
        }
    };

    const handleSave = async (e) => {
        e.preventDefault();
        const valor = parseFloat(salarioMinimo);
        if (isNaN(valor) || valor <= 0) {
            toast.error('El salario minimo debe ser mayor a cero');
            return;
        }

        try {
            setIsSaving(true);
            await api.put('/api/configuracion/salario-minimo', {
                salarioMinimoLegal: valor,
                actividadEconomica: actividadEconomica || null,
            });
            toast.success('Salario minimo actualizado correctamente');
        } catch (err) {
            toast.error(err.message || 'Error al guardar');
        } finally {
            setIsSaving(false);
        }
    };

    if (isLoading) {
        return (
            <div className="flex items-center justify-center h-64">
                <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-3xl font-bold text-gray-900">Salario Minimo Legal</h1>
                <p className="mt-1 text-sm text-gray-500">
                    Configure el salario minimo legal vigente para su empresa. Este valor se usa para proteger
                    el salario minimo inembargable al aplicar deducciones.
                </p>
            </div>

            <form onSubmit={handleSave} className="max-w-lg">
                <div className="bg-white shadow rounded-lg p-6 space-y-5">
                    <div>
                        <label htmlFor="salarioMinimo" className="block text-sm font-medium text-gray-700">
                            Salario Minimo Mensual (B/.)
                        </label>
                        <div className="mt-1 relative rounded-md shadow-sm">
                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                <DollarSign className="h-5 w-5 text-gray-400" />
                            </div>
                            <input
                                type="number"
                                id="salarioMinimo"
                                step="0.01"
                                min="0.01"
                                value={salarioMinimo}
                                onChange={(e) => setSalarioMinimo(e.target.value)}
                                className="block w-full pl-10 pr-4 py-2 border border-gray-300 rounded-md focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                                placeholder="700.00"
                                required
                            />
                        </div>
                        <p className="mt-1 text-xs text-gray-500">
                            Salario minimo legal mensual vigente segun actividad economica (Decreto Ejecutivo).
                        </p>
                    </div>

                    <div>
                        <label htmlFor="actividadEconomica" className="block text-sm font-medium text-gray-700">
                            Actividad Economica
                        </label>
                        <div className="mt-1 relative rounded-md shadow-sm">
                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                <Building2 className="h-5 w-5 text-gray-400" />
                            </div>
                            <input
                                type="text"
                                id="actividadEconomica"
                                value={actividadEconomica}
                                onChange={(e) => setActividadEconomica(e.target.value)}
                                className="block w-full pl-10 pr-4 py-2 border border-gray-300 rounded-md focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                                placeholder="Ej: Comercio al por menor"
                                maxLength={300}
                            />
                        </div>
                        <p className="mt-1 text-xs text-gray-500">
                            Descripcion de la actividad economica principal de la empresa (opcional).
                        </p>
                    </div>

                    <div className="bg-amber-50 border border-amber-200 rounded-md p-4">
                        <h4 className="text-sm font-medium text-amber-800">Importante</h4>
                        <ul className="mt-1 text-xs text-amber-700 list-disc list-inside space-y-1">
                            <li>El salario minimo se usa para calcular el piso inembargable en deducciones.</li>
                            <li>Las pensiones alimenticias por orden judicial SI pueden reducir por debajo del salario minimo.</li>
                            <li>Los embargos judiciales y deducciones voluntarias solo aplican sobre el excedente del salario minimo.</li>
                            <li>El monto se prorratea automaticamente segun el periodo de pago (semanal, quincenal, etc.).</li>
                        </ul>
                    </div>

                    <div className="flex justify-end">
                        <button
                            type="submit"
                            disabled={isSaving}
                            className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
                        >
                            {isSaving ? (
                                <Loader2 className="w-4 h-4 animate-spin" />
                            ) : (
                                <Save className="w-4 h-4" />
                            )}
                            Guardar
                        </button>
                    </div>
                </div>
            </form>
        </div>
    );
}
