import React, { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { api } from '../../services/api';

// ============================================================
// Opción de la empresa: exigir que otra persona confirme los cambios de
// salario y las bajas antes de aplicarlos. Solo la ve el dueño.
// ============================================================

export default function ConfirmacionCambios() {
    const [activo, setActivo] = useState(null);
    const [guardando, setGuardando] = useState(false);

    useEffect(() => {
        api.get('/api/cambios-pendientes/opcion')
            .then(r => setActivo(!!r.activo))
            .catch(() => setActivo(false));
    }, []);

    const cambiar = async (valor) => {
        setGuardando(true);
        try {
            const r = await api.put('/api/cambios-pendientes/opcion', { activo: valor });
            setActivo(!!r.activo);
            toast.success(valor
                ? 'Desde ahora los cambios de salario y las bajas esperan la confirmación de otra persona'
                : 'Los cambios de salario y las bajas vuelven a aplicarse al guardarlos');
        } catch (e) {
            toast.error(e.message || 'No se pudo guardar la opción');
        } finally {
            setGuardando(false);
        }
    };

    return (
        <div className="space-y-4 max-w-2xl">
            <div>
                <h3 className="text-lg font-semibold text-gray-100">Confirmación de cambios sensibles</h3>
                <p className="text-sm text-gray-400 mt-1">
                    Con esta opción, un cambio de salario o una baja no se aplican al guardarlos: quedan pendientes hasta
                    que otra persona con permiso los confirme o los rechace con su razón. Quien los pidió no puede
                    confirmarlos. Todo queda en el historial y en la auditoría.
                </p>
            </div>
            {activo == null ? (
                <Loader2 className="w-5 h-5 text-primary-400 animate-spin" aria-label="Cargando" />
            ) : (
                <label className="flex items-start gap-3 p-4 rounded-lg border border-navy-700 bg-navy-900 cursor-pointer">
                    <input type="checkbox" checked={activo} disabled={guardando} onChange={e => cambiar(e.target.checked)}
                        className="mt-0.5 accent-primary-600" />
                    <span className="text-sm text-gray-200">
                        Exigir que otra persona confirme los cambios de salario y las bajas
                        <span className="block text-xs text-gray-500 mt-0.5">Se avisa en la campana y por correo a quienes pueden confirmarlos.</span>
                    </span>
                    {guardando && <Loader2 className="w-4 h-4 text-primary-400 animate-spin ml-auto" />}
                </label>
            )}
        </div>
    );
}
