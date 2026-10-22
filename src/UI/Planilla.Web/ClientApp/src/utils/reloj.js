// Minutos a «2 h 05»; sin minutos, una raya.
export const minutosAHoras = (min) => (min > 0 ? `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')}` : '—');

// Días laborables como banderas: lunes = 1 … domingo = 64 (igual que el servidor).
export const DIAS_SEMANA = [
    { bit: 1, corto: 'L', nombre: 'lunes' },
    { bit: 2, corto: 'M', nombre: 'martes' },
    { bit: 4, corto: 'X', nombre: 'miércoles' },
    { bit: 8, corto: 'J', nombre: 'jueves' },
    { bit: 16, corto: 'V', nombre: 'viernes' },
    { bit: 32, corto: 'S', nombre: 'sábado' },
    { bit: 64, corto: 'D', nombre: 'domingo' },
];
