import React, { useState, useEffect, useRef, useCallback } from 'react';
import { api } from '../services/api';

// Colores de badge por tipo de acreedor
const TIPO_BADGE = {
    1: { label: 'Persona Natural', className: 'bg-green-500/15 text-green-400' },
    2: { label: 'Banco', className: 'bg-blue-500/15 text-blue-400' },
    3: { label: 'Cooperativa', className: 'bg-teal-500/15 text-teal-400' },
    4: { label: 'Sindicato', className: 'bg-orange-500/15 text-orange-400' },
    5: { label: 'Aseguradora', className: 'bg-purple-500/15 text-purple-400' },
    6: { label: 'Ent. Gubernamental', className: 'bg-gray-500/15 text-gray-400' },
    99: { label: 'Otro', className: 'bg-gray-500/15 text-gray-400' },
};

/**
 * Combobox reutilizable para seleccionar un acreedor del catálogo.
 *
 * Props:
 *   value      {number|null}  — id del acreedor seleccionado
 *   onChange   {function}     — (acreedorId, acreedorData|null) => void
 *   disabled   {boolean}
 */
export default function AcreedorCombobox({ value, onChange, disabled = false }) {
    const [inputText, setInputText] = useState('');
    const [results, setResults] = useState([]);
    const [isOpen, setIsOpen] = useState(false);
    const [isSearching, setIsSearching] = useState(false);
    const [selectedAcreedor, setSelectedAcreedor] = useState(null);

    const debounceRef = useRef(null);
    const containerRef = useRef(null);

    // Cuando el value externo cambia (p.ej. al editar), cargar el acreedor
    useEffect(() => {
        if (!value) {
            setSelectedAcreedor(null);
            setInputText('');
            return;
        }
        // Si ya tenemos el acreedor cargado con el mismo id, no volvemos a buscar
        if (selectedAcreedor && selectedAcreedor.id === value) return;

        // Cargar datos del acreedor por id
        api.get(`/api/acreedores/${value}`)
            .then(data => {
                setSelectedAcreedor(data);
                setInputText(data.nombre);
            })
            .catch(() => {
                // Si falla, limpiar
                setSelectedAcreedor(null);
                setInputText('');
            });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [value]);

    // Cerrar dropdown al hacer clic fuera
    useEffect(() => {
        const handleClickOutside = (e) => {
            if (containerRef.current && !containerRef.current.contains(e.target)) {
                setIsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    // Búsqueda con debounce de 300 ms
    const search = useCallback((text) => {
        if (debounceRef.current) clearTimeout(debounceRef.current);

        if (!text || text.trim().length < 1) {
            setResults([]);
            setIsOpen(false);
            return;
        }

        debounceRef.current = setTimeout(async () => {
            try {
                setIsSearching(true);
                const data = await api.get(`/api/acreedores/buscar?q=${encodeURIComponent(text.trim())}`);
                setResults(Array.isArray(data) ? data : []);
                setIsOpen(true);
            } catch {
                setResults([]);
            } finally {
                setIsSearching(false);
            }
        }, 300);
    }, []);

    const handleInputChange = (e) => {
        const text = e.target.value;
        setInputText(text);
        // Si el usuario empieza a escribir mientras hay uno seleccionado, limpiar selección
        if (selectedAcreedor) {
            setSelectedAcreedor(null);
            onChange(null, null);
        }
        search(text);
    };

    const handleSelect = (acreedor) => {
        setSelectedAcreedor(acreedor);
        setInputText(acreedor.nombre);
        setIsOpen(false);
        setResults([]);
        onChange(acreedor.id, acreedor);
    };

    const handleClear = () => {
        setSelectedAcreedor(null);
        setInputText('');
        setResults([]);
        setIsOpen(false);
        onChange(null, null);
    };

    const tipoBadge = selectedAcreedor
        ? TIPO_BADGE[selectedAcreedor.tipoAcreedor] || TIPO_BADGE[99]
        : null;

    return (
        <div ref={containerRef} className="relative">
            {/* Campo de texto con botón limpiar */}
            <div className="relative">
                {/* Icono lupa */}
                <span className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none">
                    {isSearching ? (
                        <svg className="w-4 h-4 text-gray-500 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                        </svg>
                    ) : (
                        <svg className="w-4 h-4 text-gray-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                        </svg>
                    )}
                </span>

                <input
                    type="text"
                    value={inputText}
                    onChange={handleInputChange}
                    onFocus={() => { if (results.length > 0) setIsOpen(true); }}
                    disabled={disabled}
                    placeholder="Buscar acreedor por nombre o identificación..."
                    className={`w-full pl-9 pr-8 py-2 bg-navy-800 border border-navy-600 text-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 text-sm ${
                        disabled ? 'opacity-50 cursor-not-allowed' : ''
                    } ${selectedAcreedor ? 'border-primary-500/50' : ''}`}
                />

                {/* Botón limpiar — visible solo cuando hay texto */}
                {inputText && !disabled && (
                    <button
                        type="button"
                        onClick={handleClear}
                        className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300 transition-colors"
                        title="Limpiar selección"
                    >
                        <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                )}
            </div>

            {/* Acreedor seleccionado — chip de confirmación */}
            {selectedAcreedor && (
                <div className="mt-1.5 flex items-center gap-2 px-2 py-1 bg-primary-500/10 border border-primary-500/30 rounded-lg">
                    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium ${tipoBadge?.className}`}>
                        {tipoBadge?.label}
                    </span>
                    <span className="text-xs text-gray-200 font-medium truncate">{selectedAcreedor.nombre}</span>
                    {selectedAcreedor.identificacion && (
                        <span className="text-[10px] text-gray-500 ml-auto flex-shrink-0">
                            {selectedAcreedor.tipoIdentificacion || 'ID'}: {selectedAcreedor.identificacion}
                        </span>
                    )}
                </div>
            )}

            {/* Mensaje cuando no hay nada seleccionado y no hay texto */}
            {!selectedAcreedor && !inputText && (
                <p className="mt-1 text-[11px] text-gray-600 px-1">Sin acreedor vinculado — escribe para buscar en el catálogo</p>
            )}

            {/* Dropdown de resultados */}
            {isOpen && results.length > 0 && (
                <div className="absolute z-50 w-full mt-1 bg-navy-800 border border-navy-600 rounded-lg shadow-2xl shadow-black/40 max-h-60 overflow-y-auto">
                    {results.map(acreedor => {
                        const badge = TIPO_BADGE[acreedor.tipoAcreedor] || TIPO_BADGE[99];
                        return (
                            <button
                                key={acreedor.id}
                                type="button"
                                onClick={() => handleSelect(acreedor)}
                                className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-navy-700 transition-colors text-left"
                            >
                                <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium flex-shrink-0 ${badge.className}`}>
                                    {badge.label}
                                </span>
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm text-gray-100 font-medium truncate">{acreedor.nombre}</p>
                                    {acreedor.identificacion && (
                                        <p className="text-[11px] text-gray-500 truncate">
                                            {acreedor.tipoIdentificacion || 'ID'}: {acreedor.identificacion}
                                        </p>
                                    )}
                                </div>
                                {acreedor.banco && (
                                    <span className="text-[11px] text-gray-500 flex-shrink-0">{acreedor.banco}</span>
                                )}
                            </button>
                        );
                    })}
                </div>
            )}

            {/* Sin resultados */}
            {isOpen && !isSearching && results.length === 0 && inputText.trim().length >= 1 && (
                <div className="absolute z-50 w-full mt-1 bg-navy-800 border border-navy-600 rounded-lg shadow-2xl shadow-black/40 px-4 py-3">
                    <p className="text-sm text-gray-500 text-center">No se encontraron acreedores para "{inputText}"</p>
                </div>
            )}
        </div>
    );
}
