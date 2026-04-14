# `scripts/database/` — Scripts SQL

Scripts de limpieza y utilidades sobre PostgreSQL.

> ⚠️ **Ninguno de estos scripts debe ejecutarse en producción.** Borran datos irrecuperablemente.

## Scripts

| Script | Qué hace |
|--------|----------|
| `clean-database.sql` | Limpieza completa: borra tenants, empleados, planillas, departamentos, posiciones, préstamos, deducciones, horas extra, usuarios no-system-admin, relaciones `TenantUser`, invitaciones, roles custom, audit logs. Resetea secuencias. |
| `cleanup_duplicates.sql` | Elimina registros duplicados puntuales (consultar el script para el detalle de tablas que toca). |
| `cleanup_users.sql` | Versión parcial: solo usuarios / `TenantUser`, preservando la estructura de tenants. |

## Qué se preserva

- Usuarios con `IsSystemAdmin = true`.
- Roles del sistema (`AspNetRoles`).
- Esquema de la base (tablas, columnas, índices, constraints).

## Uso

```powershell
# Con helper de PowerShell (solo existe localmente como scripts/run-cleanup.ps1, untracked)
./scripts/run-cleanup.ps1

# Directamente con psql
psql -h localhost -p 5432 -U postgres -d PlanillaDB -f scripts/database/clean-database.sql
```

## Después de limpiar

1. Verificar que sobrevive al menos un system admin:
   ```sql
   SELECT "Id", "Email", "NombreCompleto"
   FROM "AspNetUsers"
   WHERE "IsSystemAdmin" = true;
   ```
2. Crear tenants nuevos desde `/system-admin/tenants`.
3. Crear usuarios de tenant desde `/system-admin/users` y asignarlos a un tenant.

## Requisitos

- `psql` en el PATH.
- Permisos `DELETE` en la base.
- **Doble verificación de la variable `ConnectionStrings__DefaultConnection` antes de ejecutar.**

## Historial

- **2026-02-01** — scripts reorganizados al sistema simplificado (Owner / User). Se eliminaron los roles viejos (`Admin`, `Manager`, `Accountant`, `Employee`).
