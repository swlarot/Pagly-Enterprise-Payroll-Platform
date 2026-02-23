import React, { useEffect, useRef, useState, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import {
  Search, LayoutDashboard, Users, Building2, Briefcase,
  ClipboardList, BarChart3, User, DollarSign, CreditCard,
  Minus, Clock, TreePalm, XCircle, Building, X, ArrowRight,
  Loader2, ChevronRight,
} from 'lucide-react';
import { api } from '../../services/api';

interface GlobalSearchProps {
  open: boolean;
  onClose: () => void;
}

interface Employee {
  id: number;
  nombre: string;
  apellido: string;
  numeroIdentificacion: string;
  email?: string;
  departamentoNombre?: string;
  posicionNombre?: string;
  estaActivo: boolean;
}

interface NavItem {
  label: string;
  path: string;
  icon: React.ReactNode;
  group: string;
  keywords?: string[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: <LayoutDashboard className="w-4 h-4" />, group: 'Navegación', keywords: ['inicio', 'home'] },
  { label: 'Empleados', path: '/empleados', icon: <Users className="w-4 h-4" />, group: 'Navegación', keywords: ['personal', 'staff'] },
  { label: 'Departamentos', path: '/departamentos', icon: <Building2 className="w-4 h-4" />, group: 'Navegación' },
  { label: 'Posiciones', path: '/posiciones', icon: <Briefcase className="w-4 h-4" />, group: 'Navegación', keywords: ['cargos', 'puestos'] },
  { label: 'Mi Perfil', path: '/mi-perfil', icon: <User className="w-4 h-4" />, group: 'Navegación' },
  { label: 'Planillas', path: '/planillas', icon: <ClipboardList className="w-4 h-4" />, group: 'Módulos', keywords: ['nomina', 'nómina', 'pago'] },
  { label: 'Reportes', path: '/reportes', icon: <BarChart3 className="w-4 h-4" />, group: 'Módulos', keywords: ['css', 'informes', 'estadísticas'] },
  { label: 'Anticipos', path: '/anticipos', icon: <DollarSign className="w-4 h-4" />, group: 'Novedades' },
  { label: 'Préstamos', path: '/prestamos', icon: <CreditCard className="w-4 h-4" />, group: 'Novedades' },
  { label: 'Deducciones', path: '/deducciones', icon: <Minus className="w-4 h-4" />, group: 'Novedades' },
  { label: 'Acreedores', path: '/acreedores', icon: <Building className="w-4 h-4" />, group: 'Novedades' },
  { label: 'Horas Extra', path: '/horas-extra', icon: <Clock className="w-4 h-4" />, group: 'Asistencia' },
  { label: 'Ausencias', path: '/ausencias', icon: <XCircle className="w-4 h-4" />, group: 'Asistencia' },
  { label: 'Vacaciones', path: '/vacaciones', icon: <TreePalm className="w-4 h-4" />, group: 'Asistencia' },
];

// Ítems rápidos que siempre se muestran cuando no hay query
const QUICK_ITEMS = NAV_ITEMS.filter(i =>
  ['/dashboard', '/empleados', '/planillas', '/reportes'].includes(i.path)
);

export function GlobalSearch({ open, onClose }: GlobalSearchProps) {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const [query, setQuery] = useState('');
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [allEmployees, setAllEmployees] = useState<Employee[]>([]);
  const [employeesLoaded, setEmployeesLoaded] = useState(false);

  // Cargar empleados una vez al abrir (para búsqueda local instantánea)
  useEffect(() => {
    if (open && !employeesLoaded) {
      setIsLoading(true);
      api.get<Employee[]>('/api/empleados')
        .then(data => {
          setAllEmployees(data);
          setEmployeesLoaded(true);
        })
        .catch(() => {})
        .finally(() => setIsLoading(false));
    }
  }, [open, employeesLoaded]);

  // Focus input al abrir
  useEffect(() => {
    if (open) {
      setTimeout(() => inputRef.current?.focus(), 50);
      setQuery('');
      setSelectedIndex(0);
    }
  }, [open]);

  // Filtrar empleados localmente al cambiar query
  useEffect(() => {
    if (!query.trim()) {
      setEmployees([]);
      return;
    }
    const q = query.toLowerCase().trim();
    const filtered = allEmployees.filter(e =>
      `${e.nombre} ${e.apellido}`.toLowerCase().includes(q) ||
      e.numeroIdentificacion?.toLowerCase().includes(q) ||
      e.email?.toLowerCase().includes(q) ||
      e.departamentoNombre?.toLowerCase().includes(q) ||
      e.posicionNombre?.toLowerCase().includes(q)
    ).slice(0, 5);
    setEmployees(filtered);
    setSelectedIndex(0);
  }, [query, allEmployees]);

  // Filtrar ítems de navegación
  const filteredNav = query.trim()
    ? NAV_ITEMS.filter(item => {
        const q = query.toLowerCase();
        return (
          item.label.toLowerCase().includes(q) ||
          item.group.toLowerCase().includes(q) ||
          item.keywords?.some(k => k.includes(q))
        );
      }).slice(0, 4)
    : QUICK_ITEMS;

  // Lista plana de todos los ítems seleccionables
  const allItems = [
    ...filteredNav.map(n => ({ type: 'nav' as const, item: n })),
    ...employees.map(e => ({ type: 'emp' as const, item: e })),
  ];

  const handleSelect = useCallback((index: number) => {
    const selected = allItems[index];
    if (!selected) return;
    if (selected.type === 'nav') {
      navigate(selected.item.path);
    } else {
      navigate(`/empleados`);
    }
    onClose();
  }, [allItems, navigate, onClose]);

  // Navegación con teclado
  useEffect(() => {
    if (!open) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { onClose(); return; }
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        setSelectedIndex(i => Math.min(i + 1, allItems.length - 1));
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault();
        setSelectedIndex(i => Math.max(i - 1, 0));
      }
      if (e.key === 'Enter') {
        e.preventDefault();
        handleSelect(selectedIndex);
      }
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [open, selectedIndex, allItems.length, handleSelect, onClose]);

  // Scroll al ítem seleccionado
  useEffect(() => {
    const el = listRef.current?.querySelector(`[data-index="${selectedIndex}"]`);
    el?.scrollIntoView({ block: 'nearest' });
  }, [selectedIndex]);

  if (!open) return null;

  // Agrupar ítems de nav por grupo (solo cuando no hay query, para mostrar separadores)
  const showGroupLabels = !query.trim();

  const content = (
    <>
      {/* Overlay - en body para cubrir todo el viewport */}
      <div
        className="fixed inset-0 z-[100] bg-black bg-opacity-60 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="fixed inset-0 z-[101] flex items-start justify-center pt-[12vh] px-4 pointer-events-none">
        <div
          className="w-full max-w-[580px] bg-[#0d1b2e] border border-white/[0.08] rounded-2xl shadow-2xl shadow-black/60 overflow-hidden pointer-events-auto"
          onClick={e => e.stopPropagation()}
        >
          {/* Input */}
          <div className="flex items-center gap-3 px-4 py-3.5 border-b border-white/[0.06]">
            {isLoading
              ? <Loader2 className="w-5 h-5 text-gray-500 flex-shrink-0 animate-spin" />
              : <Search className="w-5 h-5 text-gray-500 flex-shrink-0" />
            }
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder="Buscar módulos, empleados, cédulas..."
              className="flex-1 bg-transparent text-[15px] text-white placeholder-gray-600 outline-none"
            />
            {query && (
              <button
                onClick={() => setQuery('')}
                className="p-1 rounded-md text-gray-600 hover:text-gray-300 hover:bg-white/[0.06] transition-colors"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
            <kbd className="text-[11px] text-gray-600 bg-white/[0.06] border border-white/[0.08] px-2 py-1 rounded-md">
              ESC
            </kbd>
          </div>

          {/* Resultados */}
          <div ref={listRef} className="max-h-[400px] overflow-y-auto py-2">

            {/* Sin resultados */}
            {query.trim() && filteredNav.length === 0 && employees.length === 0 && !isLoading && (
              <div className="px-4 py-8 text-center">
                <p className="text-[13px] text-gray-600">Sin resultados para <span className="text-gray-400">"{query}"</span></p>
              </div>
            )}

            {/* Sección navegación */}
            {filteredNav.length > 0 && (
              <div>
                {showGroupLabels && (
                  <p className="px-4 pt-1 pb-1.5 text-[10.5px] font-semibold tracking-widest uppercase text-gray-600">
                    Accesos rápidos
                  </p>
                )}
                {!showGroupLabels && (
                  <p className="px-4 pt-1 pb-1.5 text-[10.5px] font-semibold tracking-widest uppercase text-gray-600">
                    Módulos
                  </p>
                )}
                {filteredNav.map((navItem, i) => {
                  const globalIndex = i;
                  const isSelected = selectedIndex === globalIndex;
                  return (
                    <button
                      key={navItem.path}
                      data-index={globalIndex}
                      onClick={() => handleSelect(globalIndex)}
                      onMouseEnter={() => setSelectedIndex(globalIndex)}
                      className={`w-full flex items-center gap-3 px-4 py-2.5 transition-colors text-left ${
                        isSelected ? 'bg-white/[0.06]' : 'hover:bg-white/[0.03]'
                      }`}
                    >
                      <span className={`flex-shrink-0 ${isSelected ? 'text-primary-400' : 'text-gray-500'}`}>
                        {navItem.icon}
                      </span>
                      <span className={`flex-1 text-[13.5px] font-medium ${isSelected ? 'text-white' : 'text-gray-300'}`}>
                        {navItem.label}
                      </span>
                      <span className="text-[11px] text-gray-600 bg-white/[0.04] px-2 py-0.5 rounded">
                        {navItem.group}
                      </span>
                      {isSelected && <ChevronRight className="w-3.5 h-3.5 text-gray-500 flex-shrink-0" />}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Sección empleados */}
            {employees.length > 0 && (
              <div className={filteredNav.length > 0 ? 'mt-1 pt-1 border-t border-white/[0.05]' : ''}>
                <p className="px-4 pt-2 pb-1.5 text-[10.5px] font-semibold tracking-widest uppercase text-gray-600">
                  Empleados
                </p>
                {employees.map((emp, i) => {
                  const globalIndex = filteredNav.length + i;
                  const isSelected = selectedIndex === globalIndex;
                  const fullName = `${emp.nombre} ${emp.apellido}`;
                  return (
                    <button
                      key={emp.id}
                      data-index={globalIndex}
                      onClick={() => handleSelect(globalIndex)}
                      onMouseEnter={() => setSelectedIndex(globalIndex)}
                      className={`w-full flex items-center gap-3 px-4 py-2.5 transition-colors text-left ${
                        isSelected ? 'bg-white/[0.06]' : 'hover:bg-white/[0.03]'
                      }`}
                    >
                      {/* Avatar inicial */}
                      <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-blue-500/30 to-indigo-600/30 border border-white/[0.08] flex items-center justify-center flex-shrink-0">
                        <span className="text-[11px] font-bold text-blue-300">
                          {emp.nombre[0]}{emp.apellido[0]}
                        </span>
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className={`text-[13.5px] font-medium truncate ${isSelected ? 'text-white' : 'text-gray-300'}`}>
                          {fullName}
                        </p>
                        <p className="text-[11px] text-gray-600 truncate">
                          {emp.numeroIdentificacion}
                          {emp.posicionNombre ? ` · ${emp.posicionNombre}` : ''}
                        </p>
                      </div>
                      {!emp.estaActivo && (
                        <span className="text-[10px] text-amber-500 bg-amber-500/10 px-2 py-0.5 rounded-full flex-shrink-0">
                          Inactivo
                        </span>
                      )}
                      {isSelected && <ArrowRight className="w-3.5 h-3.5 text-gray-500 flex-shrink-0" />}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Ver todos los empleados — solo cuando buscando y hay resultados */}
            {query.trim() && employees.length > 0 && (
              <button
                onClick={() => { navigate('/empleados'); onClose(); }}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 mt-1 border-t border-white/[0.05] text-[12px] text-gray-500 hover:text-gray-300 hover:bg-white/[0.03] transition-colors"
              >
                <span>Ver todos los empleados</span>
                <ArrowRight className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Footer con atajos */}
          <div className="flex items-center gap-4 px-4 py-2.5 border-t border-white/[0.05] bg-white/[0.02]">
            <div className="flex items-center gap-1.5 text-[11px] text-gray-600">
              <kbd className="bg-white/[0.06] border border-white/[0.08] px-1.5 py-0.5 rounded text-[10px]">↑↓</kbd>
              <span>Navegar</span>
            </div>
            <div className="flex items-center gap-1.5 text-[11px] text-gray-600">
              <kbd className="bg-white/[0.06] border border-white/[0.08] px-1.5 py-0.5 rounded text-[10px]">↵</kbd>
              <span>Abrir</span>
            </div>
            <div className="flex items-center gap-1.5 text-[11px] text-gray-600">
              <kbd className="bg-white/[0.06] border border-white/[0.08] px-1.5 py-0.5 rounded text-[10px]">ESC</kbd>
              <span>Cerrar</span>
            </div>
          </div>
        </div>
      </div>
    </>
  );

  return createPortal(content, document.body);
}
