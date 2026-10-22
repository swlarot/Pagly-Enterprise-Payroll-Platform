import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

// ============================================================
// Enlaces a un mes y a una fila: /planillas?anio=2026&mes=10&abrir=5.
// Los usan los avisos y los correos de revisión para llevar a la persona
// justo a lo que tiene que mirar.
// ============================================================

/** Mes (y fila a abrir) que pide la URL, o null si no pide nada válido. */
export function leerEnlaceMes(search) {
  const p = new URLSearchParams(search ?? '');
  const anio = Number(p.get('anio'));
  const mes = Number(p.get('mes'));
  if (!Number.isInteger(anio) || anio < 2000 || anio > 2100 || !Number.isInteger(mes) || mes < 1 || mes > 12) return null;
  const abrir = Number(p.get('abrir'));
  return { anio, mes, abrir: Number.isInteger(abrir) && abrir > 0 ? abrir : null };
}

/**
 * Cuando la URL pide un mes (al entrar o al pulsar otro aviso estando ya en
 * la página), avisa a la página para que cambie de mes y abra la fila.
 */
export function useEnlaceMes(onIr) {
  const { search } = useLocation();
  const ir = useRef(onIr);
  ir.current = onIr;
  useEffect(() => {
    const destino = leerEnlaceMes(search);
    if (destino) ir.current(destino);
  }, [search]);
}
