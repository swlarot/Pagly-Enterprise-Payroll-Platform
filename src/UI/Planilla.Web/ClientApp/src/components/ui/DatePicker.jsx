import React, { useEffect, useId, useMemo, useRef, useState } from 'react';
import { Calendar as CalendarIcon, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { diasDelMes, formatear, parsear, NOMBRES_MES } from '../../utils/periodos';

// ============================================================
// Calendario propio para fechas de calendario (yyyy-MM-dd), sin zona horaria.
// Sustituye al <input type="date"> nativo, que cada navegador pinta distinto
// y no cabe en la paleta. Se escribe dd/MM/yyyy o se elige en la cuadrícula.
//
// Teclado: ↑↓←→ mueven el día, PageUp/PageDown el mes, Enter elige, Esc cierra.
// ============================================================

const DIAS = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
const pad = (n) => String(n).padStart(2, '0');
const aTexto = (iso) => { const p = parsear(iso); return p ? `${pad(p.d)}/${pad(p.m)}/${p.y}` : ''; };
const deTexto = (t) => {
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(t.trim());
  if (!m) return null;
  const iso = formatear(Number(m[3]), Number(m[2]), Number(m[1]));
  return parsear(iso) ? iso : null;
};
const hoyIso = () => { const h = new Date(); return formatear(h.getFullYear(), h.getMonth() + 1, h.getDate()); };
const sumar = (iso, dias) => {
  const p = parsear(iso); if (!p) return iso;
  const t = new Date(Date.UTC(p.y, p.m - 1, p.d + dias));
  return formatear(t.getUTCFullYear(), t.getUTCMonth() + 1, t.getUTCDate());
};
const sumarMeses = (iso, n) => {
  const p = parsear(iso); if (!p) return iso;
  const t = new Date(Date.UTC(p.y, p.m - 1 + n, 1));
  const y = t.getUTCFullYear(), m = t.getUTCMonth() + 1;
  return formatear(y, m, Math.min(p.d, diasDelMes(y, m)));
};

export default function DatePicker({ id, value, onChange, min, max, placeholder = 'dd/mm/aaaa', className = '', limpiable = false, ariaLabel }) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const [abierto, setAbierto] = useState(false);
  const [texto, setTexto] = useState(aTexto(value));
  const [foco, setFoco] = useState(value || hoyIso()); // día resaltado en la cuadrícula
  const raiz = useRef(null);

  useEffect(() => { setTexto(aTexto(value)); if (value) setFoco(value); }, [value]);

  // Cerrar al hacer clic fuera.
  useEffect(() => {
    if (!abierto) return;
    const fuera = (e) => { if (raiz.current && !raiz.current.contains(e.target)) setAbierto(false); };
    document.addEventListener('mousedown', fuera);
    return () => document.removeEventListener('mousedown', fuera);
  }, [abierto]);

  const permitido = (iso) => (!min || iso >= min) && (!max || iso <= max);
  const elegir = (iso) => { if (!permitido(iso)) return; onChange(iso); setAbierto(false); };

  const confirmarTexto = () => {
    if (texto.trim() === '') { if (limpiable) onChange(''); else setTexto(aTexto(value)); return; }
    const iso = deTexto(texto);
    if (iso && permitido(iso)) { onChange(iso); } else { setTexto(aTexto(value)); }
  };

  const tecla = (e) => {
    if (e.key === 'Escape') { setAbierto(false); return; }
    if (e.key === 'Enter') { e.preventDefault(); if (abierto) elegir(foco); else confirmarTexto(); return; }
    if (!abierto && (e.key === 'ArrowDown' || e.key === ' ')) { e.preventDefault(); setAbierto(true); return; }
    if (!abierto) return;
    const mov = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
    if (mov) { e.preventDefault(); setFoco(f => sumar(f, mov)); return; }
    if (e.key === 'PageUp') { e.preventDefault(); setFoco(f => sumarMeses(f, -1)); }
    if (e.key === 'PageDown') { e.preventDefault(); setFoco(f => sumarMeses(f, 1)); }
  };

  const f = parsear(foco) ?? parsear(hoyIso());
  const celdas = useMemo(() => {
    const primero = new Date(Date.UTC(f.y, f.m - 1, 1)).getUTCDay(); // 0 = domingo
    const desplazamiento = (primero + 6) % 7; // lunes primero
    const n = diasDelMes(f.y, f.m);
    const lista = Array.from({ length: desplazamiento }, () => null);
    for (let d = 1; d <= n; d++) lista.push(formatear(f.y, f.m, d));
    while (lista.length % 7) lista.push(null);
    return lista;
  }, [f.y, f.m]);

  const hoy = hoyIso();

  return (
    <div ref={raiz} className={`relative ${className}`}>
      <div className="relative">
        <input
          id={inputId}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          aria-label={ariaLabel}
          aria-haspopup="dialog"
          aria-expanded={abierto}
          value={texto}
          placeholder={placeholder}
          onChange={e => setTexto(e.target.value)}
          onBlur={confirmarTexto}
          onFocus={() => setAbierto(true)}
          onClick={() => setAbierto(true)}
          onKeyDown={tecla}
          className="w-full bg-navy-800 border border-navy-600 text-gray-100 rounded-lg pl-2.5 pr-8 py-1.5 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary-500 placeholder:text-gray-600"
        />
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none">
          <CalendarIcon className="w-4 h-4" />
        </span>
      </div>

      {abierto && (
        <div role="dialog" aria-label="Elegir fecha" className="absolute z-30 mt-1 w-[252px] bg-navy-900 border border-navy-600 rounded-lg shadow-xl p-2 select-none">
          <div className="flex items-center justify-between mb-1">
            <button type="button" onMouseDown={e => e.preventDefault()} onClick={() => setFoco(sumarMeses(foco, -1))} aria-label="Mes anterior" className="p-1 rounded hover:bg-navy-700 text-gray-300"><ChevronLeft className="w-4 h-4" /></button>
            <span className="text-sm font-medium text-gray-100">{NOMBRES_MES[f.m - 1]} {f.y}</span>
            <button type="button" onMouseDown={e => e.preventDefault()} onClick={() => setFoco(sumarMeses(foco, 1))} aria-label="Mes siguiente" className="p-1 rounded hover:bg-navy-700 text-gray-300"><ChevronRight className="w-4 h-4" /></button>
          </div>
          <div className="grid grid-cols-7 text-center text-[10px] text-gray-500 mb-0.5">
            {DIAS.map((d, i) => <span key={i} className="py-0.5">{d}</span>)}
          </div>
          <div className="grid grid-cols-7 gap-0.5">
            {celdas.map((iso, i) => {
              if (!iso) return <span key={i} />;
              const elegido = iso === value;
              const enFoco = iso === foco;
              const ok = permitido(iso);
              return (
                <button
                  key={iso}
                  type="button"
                  tabIndex={-1}
                  disabled={!ok}
                  onMouseDown={e => e.preventDefault()}
                  onClick={() => elegir(iso)}
                  className={`h-8 rounded text-sm font-mono ${
                    elegido ? 'bg-primary-600 text-white'
                    : enFoco ? 'bg-navy-700 text-white ring-1 ring-primary-500'
                    : ok ? 'text-gray-200 hover:bg-navy-700'
                    : 'text-gray-700 cursor-not-allowed'
                  } ${iso === hoy && !elegido ? 'underline underline-offset-2' : ''}`}
                >
                  {Number(iso.slice(8, 10))}
                </button>
              );
            })}
          </div>
          <div className="flex items-center justify-between mt-1.5 pt-1.5 border-t border-navy-700">
            <button type="button" onMouseDown={e => e.preventDefault()} onClick={() => { setFoco(hoy); elegir(hoy); }} className="text-xs text-gray-400 hover:text-gray-100">Hoy</button>
            {limpiable && value && (
              <button type="button" onMouseDown={e => e.preventDefault()} onClick={() => { onChange(''); setAbierto(false); }} className="text-xs text-gray-400 hover:text-gray-100 inline-flex items-center gap-1"><X className="w-3 h-3" /> Quitar</button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
