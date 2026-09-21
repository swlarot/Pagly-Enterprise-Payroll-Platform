import { describe, it, expect } from 'vitest';
import { finDePeriodo, siguienteInicio, inicioSugerido, TIPO_PERIODO } from './periodos';

describe('finDePeriodo', () => {
  it('quincenal: 1 → 15 y 16 → fin de mes', () => {
    expect(finDePeriodo('2026-01-01', TIPO_PERIODO.Quincenal)).toBe('2026-01-15');
    expect(finDePeriodo('2026-01-16', TIPO_PERIODO.Quincenal)).toBe('2026-01-31');
    expect(finDePeriodo('2026-02-16', TIPO_PERIODO.Quincenal)).toBe('2026-02-28');
    expect(finDePeriodo('2028-02-16', TIPO_PERIODO.Quincenal)).toBe('2028-02-29');
  });
  it('quincenal: un inicio raro (día 10) cierra el 15 igual', () => {
    expect(finDePeriodo('2026-03-10', TIPO_PERIODO.Quincenal)).toBe('2026-03-15');
  });
  it('semanal: +6 días, cruzando mes', () => {
    expect(finDePeriodo('2026-01-29', TIPO_PERIODO.Semanal)).toBe('2026-02-04');
  });
  it('bisemanal: +13 días', () => {
    expect(finDePeriodo('2026-01-01', TIPO_PERIODO.Bisemanal)).toBe('2026-01-14');
    expect(finDePeriodo('2026-12-25', TIPO_PERIODO.Bisemanal)).toBe('2027-01-07');
  });
  it('mensual: último día del mes', () => {
    expect(finDePeriodo('2026-02-01', TIPO_PERIODO.Mensual)).toBe('2026-02-28');
    expect(finDePeriodo('2026-04-05', TIPO_PERIODO.Mensual)).toBe('2026-04-30');
  });
  it('fecha inválida → null', () => {
    expect(finDePeriodo('', TIPO_PERIODO.Quincenal)).toBeNull();
    expect(finDePeriodo('2026-02-30', TIPO_PERIODO.Quincenal)).toBeNull();
  });
});

describe('siguienteInicio', () => {
  it('el día después del fin', () => {
    expect(siguienteInicio('2026-01-15')).toBe('2026-01-16');
    expect(siguienteInicio('2026-01-31')).toBe('2026-02-01');
    expect(siguienteInicio('2026-12-31')).toBe('2027-01-01');
  });
});

describe('inicioSugerido', () => {
  it('sin planillas en el mes: el día 1', () => {
    expect(inicioSugerido(2026, 3, TIPO_PERIODO.Quincenal, [])).toBe('2026-03-01');
  });
  it('con la 1.ª quincena: el 16', () => {
    expect(inicioSugerido(2026, 3, TIPO_PERIODO.Quincenal, [
      { periodEndDate: '2026-03-15T00:00:00', payPeriodType: 2 },
    ])).toBe('2026-03-16');
  });
  it('ignora planillas de otro tipo', () => {
    expect(inicioSugerido(2026, 3, TIPO_PERIODO.Quincenal, [
      { periodEndDate: '2026-03-07T00:00:00', payPeriodType: 0 },
    ])).toBe('2026-03-01');
  });
});
