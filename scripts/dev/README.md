# `scripts/dev/` — Arranque local

Helpers PowerShell para levantar el stack en desarrollo (Windows).

## Scripts

| Script | Qué hace |
|--------|----------|
| `iniciar-desarrollo.ps1` | Arranca backend (.NET en `:5039`) y frontend (Vite en `:5173`) en ventanas separadas. |
| `detener-desarrollo.ps1` | Mata los procesos del backend y frontend arrancados por el script anterior. |
| `verificar-puertos.ps1` | Comprueba que `5039` y `5173` estén libres antes de arrancar. |
| `start-dev.ps1` | Variante antigua del arranque; consultar el script para diferencias con `iniciar-desarrollo.ps1`. |

## Uso

```powershell
# Desde la raíz del repo
./scripts/dev/iniciar-desarrollo.ps1
```

Parar:
```powershell
./scripts/dev/detener-desarrollo.ps1
```

## Requisitos

- PowerShell 5.1+ (Windows) o PowerShell 7+ (cross-platform).
- .NET 9 SDK y Node.js 20+ instalados (ver `CLAUDE.md` raíz).
- PostgreSQL corriendo localmente con la base `PlanillaDB` creada.
