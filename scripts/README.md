# `scripts/` — Utilidades del repo Planilla

Scripts auxiliares organizados por propósito. Lo que **no** es código de producción vive aquí.

## Estructura

| Carpeta | Propósito |
|---------|-----------|
| [`dev/`](./dev/) | Arranque y parada del entorno local (backend + frontend). |
| [`database/`](./database/) | Scripts SQL de limpieza / verificación de base de datos. **No ejecutar en producción.** |
| [`migrations-legacy/`](./migrations-legacy/) | Fixes históricos one-off ya aplicados al código. **No re-ejecutar.** |

## Convenciones

- Los scripts sueltos en la raíz de `scripts/` (ej. `cleanup-*.sql`, `run-cleanup.ps1`) están ignorados por `.gitignore` — son helpers locales que pueden contener credenciales o estado específico del entorno.
- Versionar un script nuevo: colocarlo en la subcarpeta correspondiente (no en la raíz de `scripts/`).
- Para scripts temporales de ayuda (prueba manual, limpieza puntual) usar la raíz de `scripts/` y dejarlos untracked.

## Notas de seguridad

- Antes de ejecutar cualquier script SQL de `database/`, verificar la variable `ConnectionStrings__DefaultConnection` apunta al entorno correcto.
- Los fixes en `migrations-legacy/` modifican el código fuente (no la DB) y ya están reflejados en los archivos. Volver a ejecutarlos puede corromper el código actual.
