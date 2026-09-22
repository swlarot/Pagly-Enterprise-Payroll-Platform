import { describe, it, expect } from 'vitest';
import { cuatrimestreDe } from './decimo';

describe('cuatrimestreDe', () => {
  it('la primera partida cierra el 15 de abril y empieza el 16 de diciembre anterior', () => {
    expect(cuatrimestreDe(2026, 4)).toEqual({ desde: '2025-12-16', hasta: '2026-04-15' });
  });
  it('la segunda va del 16 de abril al 15 de agosto', () => {
    expect(cuatrimestreDe(2026, 8)).toEqual({ desde: '2026-04-16', hasta: '2026-08-15' });
  });
  it('la tercera va del 16 de agosto al 15 de diciembre', () => {
    expect(cuatrimestreDe(2026, 12)).toEqual({ desde: '2026-08-16', hasta: '2026-12-15' });
  });
});
