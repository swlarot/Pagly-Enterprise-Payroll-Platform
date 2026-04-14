# `scripts/migrations-legacy/` — Fixes históricos (NO re-ejecutar)

Scripts PowerShell que se usaron **una sola vez** para migrar / normalizar el código del repo durante refactors anteriores. Sus cambios **ya están aplicados** en los archivos fuente.

> 🚫 **No re-ejecutar.** Correrlos de nuevo puede corromper código válido (p. ej. reemplazar `CompanyId` por `TenantId` en archivos donde ya se hizo, dobles ediciones, etc.).

Se conservan solo como referencia histórica y para entender cómo se hicieron migraciones masivas pasadas.

## Scripts

| Script | Refactor que aplicó |
|--------|---------------------|
| `convert-api-calls.ps1` | Migró llamadas al cliente API a un formato unificado. |
| `fix-pages-api.ps1` | Corrigió imports / rutas de páginas que usaban el cliente API directo. |
| `fix-controller-companyid.ps1` | Reemplazó `CompanyId` → `TenantId` en controllers durante la transición a multi-tenant. |
| `fix-remaining-controllers.ps1` | Segunda pasada del reemplazo anterior sobre controllers restantes. |
| `fix-tenant-security.ps1` | Añadió verificación de `TenantId` en queries de repositorios. |
| `inject-tenant-context.ps1` | Inyectó `ICurrentTenantService` en servicios que lo necesitaban. |
| `remove-redundant-where.ps1` | Limpió cláusulas `.Where(x => x.TenantId == ...)` que quedaron redundantes tras activar los global query filters. |

## Si necesitas reutilizarlos

1. Leer el script completo para entender qué busca / reemplaza.
2. Hacer un branch nuevo antes de correrlo.
3. Revisar el diff con cuidado — muchos de estos scripts fueron frágiles y necesitaron ajustes manuales posteriores.
