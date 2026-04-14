# Guía de Limpieza de Usuarios Problemáticos

## Problema

Al intentar eliminar usuarios del sistema desde el panel de administración, pueden aparecer errores 500 debido a:

1. **Restricciones de Foreign Key**: `TenantInvitation.CreatedByUserId` tiene `DeleteBehavior.Restrict`, lo que impide eliminar usuarios que crearon invitaciones (incluso si ya fueron aceptadas).
2. **Usuarios antiguos**: Usuarios creados antes de implementar la lógica de eliminación completa pueden tener referencias huérfanas.
3. **Datos inconsistentes**: Usuarios que fueron marcados como "inactivos" pero nunca eliminados físicamente.

## Solución Automática (Código)

El código en `AdminController.DeleteUser` ahora maneja correctamente:

- ✅ Elimina invitaciones NO aceptadas
- ✅ Actualiza `CreatedByUserId` en invitaciones ACEPTADAS a un SystemAdmin válido
- ✅ Desvincula empleados
- ✅ Elimina RefreshTokens
- ✅ Elimina TenantUsers
- ✅ Limpia registros de ASP.NET Identity

**Si aún así falla**, usa el script SQL manual de abajo.

## Solución Manual (Script SQL)

### Paso 1: Identificar los IDs de los usuarios

Ejecuta esta consulta para obtener los IDs de los usuarios problemáticos:

```sql
SELECT "Id", "Email", "UserName", "IsSystemAdmin", "IsDeleted" 
FROM "AspNetUsers" 
WHERE "Email" IN ('hatsukiminara@gmai.com', 'hatsukiminara@gmail.com', 'kmovil2014.km@gmail.com');
```

### Paso 2: Hacer Backup

**⚠️ IMPORTANTE**: Antes de ejecutar cualquier script de eliminación, haz un backup de la base de datos:

```bash
# PostgreSQL
pg_dump -U tu_usuario -d planilla_db > backup_antes_limpieza_$(date +%Y%m%d_%H%M%S).sql
```

### Paso 3: Ejecutar el Script

Abre `scripts/database/cleanup_users.sql` y:

1. Reemplaza `'USER_ID_AQUI'` con el ID real del usuario que quieres eliminar
2. Ejecuta el bloque `DO $$ ... END $$;` para cada usuario

O descomenta y ajusta los bloques de ejemplo al final del archivo con los IDs reales.

### Paso 4: Verificar

Después de ejecutar el script, verifica que los usuarios fueron eliminados:

```sql
SELECT "Id", "Email", "UserName", "IsSystemAdmin", "IsDeleted" 
FROM "AspNetUsers" 
ORDER BY "Email";
```

## Usuarios Específicos a Eliminar

Según el reporte del usuario, estos son los usuarios problemáticos:

1. **Carlos Tradra Peréz Pablo** - `hatsukiminara@gmai.com` (sin asignar)
   - ID probable: `b7d902a4-65a0-4f29-9161-c260f014751c`
   
2. **Carlos Tradra Peréz Pablo** - `hatsukiminara@gmail.com` (con tenant Vorluno)
   - ID probable: `527aa4ad-2318-4468-84b6-c950ff7500bd`
   
3. **Luis Montenegro** - `kmovil2014.km@gmail.com` (con tenant Vorluno)
   - ID probable: `60cea108-b62f-4239-bcfa-5503543bd882`

**Nota**: Los IDs pueden variar. Siempre verifica con la consulta del Paso 1 antes de ejecutar el script.

## Qué Hace el Script

El script SQL realiza las siguientes operaciones en orden:

1. **Desvincula empleados**: Pone `UserId = NULL` en la tabla `Empleados`
2. **Elimina RefreshTokens**: Borra todos los tokens de refresco del usuario
3. **Elimina TenantUsers**: Borra todas las membresías del usuario en tenants
4. **Elimina invitaciones pendientes**: Borra `TenantInvitations` no aceptadas
5. **Actualiza invitaciones aceptadas**: Cambia `CreatedByUserId` a un SystemAdmin válido
6. **Limpia Identity**: Elimina registros de `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`
7. **Elimina el usuario**: Finalmente borra el registro de `AspNetUsers`

## Prevención Futura

Para evitar este problema en el futuro:

1. ✅ El código ahora maneja correctamente las invitaciones aceptadas
2. ✅ Los logs detallados ayudan a diagnosticar problemas
3. ✅ Las validaciones previenen eliminar usuarios con datos críticos

Si encuentras usuarios que no se pueden eliminar, revisa los logs del servidor para ver exactamente qué está fallando.
