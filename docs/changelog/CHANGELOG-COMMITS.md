# Changelog de commits y cambios documentados

Este documento describe los **commits** realizados en la rama `master` (febrero 2025) y los **cambios que no tienen documentación específica** en otros archivos. Para la política de eliminación física, ver [POLITICA-ELIMINACION.md](../compliance/POLITICA-ELIMINACION.md).

---

## 1. Resumen de commits

| Hash      | Mensaje | Descripción breve |
|-----------|---------|-------------------|
| `d24ff45` | chore: permitir en .gitignore archivos .md y carpeta docs/ | El repositorio deja de ignorar `*.md` y la carpeta `docs/` para poder versionar documentación. |
| `38f70be` | docs: política de eliminación física y enlace en README | Se añade `docs/POLITICA-ELIMINACION.md` y un enlace en el README. |
| `7853cc6` | feat(backend): eliminación física (tenant, invitación, deducción, depto, posición, roles) y correcciones | Backend: eliminación física en los puntos indicados, correcciones de nulabilidad, claim de admin y validaciones (ver sección 2). |
| `b3214bf` | feat(frontend): roles, ConfirmModal TS, páginas tenant y empleados, tipos y vite-env | Frontend: migración de ConfirmModal a TypeScript, mejoras en roles y páginas de tenant/empleados, tipos y `vite-env.d.ts` (ver sección 3). |
| `17d77c8` | chore: Program, appsettings y wwwroot | Ajustes en `Program.cs`, `appsettings.json` y archivos estáticos en `wwwroot`. |

---

## 2. Cambios en backend (commit 7853cc6) sin documentación específica

Además de la **eliminación física** (documentada en [POLITICA-ELIMINACION.md](../compliance/POLITICA-ELIMINACION.md)), en el mismo commit se incluyeron las siguientes correcciones y mejoras.

### 2.1 Autenticación y JWT

- **Claim `is_system_admin` en usuarios de tenant:**  
  Al generar el JWT para usuarios de tenant (incluidos Owners), el claim `is_system_admin` se envía explícitamente como `"false"`. Así se evita que, por configuración o herencia, un usuario de tenant sea tratado como system admin y redirigido al panel de administración del sistema tras el login.
- **Lugar:** `AuthController.cs` en los métodos que generan token para tenant (p. ej. `GenerateJwtToken`, `RefreshToken`, `SelectTenant`). Los tokens de system admin siguen enviando `"true"` solo cuando se usa `GenerateSystemAdminJwtToken`.

### 2.2 Nulabilidad y advertencias del compilador (CS8602, CS8604)

- **AuthController:** Comprobaciones y uso de operadores de nulabilidad (`!`, `?.`, `??`) al acceder a `tenantUser.Tenant`, `tenantUser.Tenant.Subscription`, etc., en `GenerateJwtToken`, `GetCurrentUser`, `RefreshToken`, `SelectTenant`.
- **CustomRolesController:** Comprobación de `result.Value` antes de usar `result.Value.Id` en `CreateRole`.
- **EmpleadosController:** Uso de `?.` para `tenantUser.User?.Email` y `tenantUser.User?.NombreCompleto`.
- **AdminController:** Uso de `user.NombreCompleto ?? user.Email ?? "Usuario"` para el argumento `toName` en `SendInvitationEmailAsync`; comprobaciones tempranas con `string.IsNullOrWhiteSpace(userId)` en métodos que reciben `userId` por ruta (`DeleteUser`, `RemoveUserFromTenant`, `ReactivateUser`, `ReactivateUserInTenant`, `HardDeleteUser`) para devolver `400 Bad Request` cuando el valor es inválido.
- **CustomTenantRoleService:** Comprobaciones de nulos para `dto` y `dto.UserId`, y uso de `?.` y `??` para `permissions.ErrorMessage` y permisos.
- **Program.cs:** Eliminación del uso de `BuildServiceProvider().GetRequiredService<ILogger>()` en el bloque `catch` de la configuración de Stripe para cumplir con la recomendación ASP0000 (no usar `BuildServiceProvider` en tiempo de inicio).

### 2.3 DTOs y dominio

- Ajustes en DTOs y enums (p. ej. `EmpleadoDeletionDtos`, `EmpleadoDtos`, `AssignRoleToUserDto`, `CustomTenantRoleDto`, `TenantUserDto`, `SystemPermission`, `PlanLimits`) para alinearlos con los flujos de eliminación, roles y permisos. Los cambios son de forma y consistencia con el comportamiento descrito en POLITICA-ELIMINACION y en la documentación de roles/permisos.

### 2.4 Servicios de infraestructura

- **PlanLimitService** y **PlanUsageService:** Recuentos de empleados filtrados por `!e.IsDeleted` para respetar la política de “no contar eliminados” (coherente con [POLITICA-ELIMINACION.md](../compliance/POLITICA-ELIMINACION.md)).
- **EmployeeDeletionValidationService** y **CustomRolesSeeder:** Ajustes para validación de eliminación de empleados y datos iniciales de roles, alineados con los nuevos flujos.

---

## 3. Cambios en frontend (commit b3214bf) sin documentación específica

### 3.1 Componentes

- **ConfirmModal:** Migración de `ConfirmModal.jsx` a `ConfirmModal.tsx` (TypeScript). El componente se usa en flujos de confirmación (p. ej. eliminar, revocar). El archivo `.jsx` fue eliminado.
- **Roles:** Actualizaciones en `RoleCard.tsx`, `RolePermissionsModal.tsx`, `RolesTab.tsx`, `UsersManagementTab.tsx` para integrar permisos, eliminación de roles y gestión de usuarios del tenant de forma coherente con el backend (eliminación física, roles personalizados).

### 3.2 Páginas

- **EmpleadosPage.jsx:** Ajustes para listado y acciones de empleados (incluyendo filtro de eliminados en la API).
- **RolesPage.tsx:** Página de roles del tenant y permisos.
- **TenantDetailsPage.tsx, TenantSelectorPage.tsx, TenantsManagementPage.tsx:** Ajustes para gestión de tenants, selector de tenant y lista de tenants, alineados con la API y con la eliminación/revocación documentada en POLITICA-ELIMINACION.

### 3.3 Servicios y tipos

- **roleService.ts:** Llamadas a la API de roles personalizados y permisos.
- **api.ts (tipos):** Unificación de interfaces (p. ej. `AuditLogDto`) con campos opcionales donde aplica, para evitar conflictos de tipado.
- **vite-env.d.ts:**
  - Referencia a `vite/client` para tipado de `import.meta.env`.
  - Declaración `declare module '*.jsx'` para que los imports de componentes `.jsx` tengan tipo (export default como `ComponentType`).

### 3.4 App y rutas

- **App.tsx:** Eliminación de import no usado de `React`, y corrección de imports de páginas `.jsx` (con extensión y, si aplica, declaración en `vite-env.d.ts`).

---

## 4. Cambios en .gitignore (commit d24ff45)

- **Eliminado:** La regla que ignoraba todos los `*.md` y la excepción `!README.md`.
- **Eliminado:** La regla que ignoraba la carpeta `docs/`.
- **Resultado:** Todos los archivos `.md` y el contenido de `docs/` pueden versionarse y subirse al repositorio. Siguen ignorados, entre otros: `*.log`, `test-*.ps1`, `RESUMEN-*.txt`, `bin/`, `obj/`, `.vs/`, `.vscode/`, `.claude/`, `scripts/`.

---

## 5. Configuración y estáticos (commit 17d77c8)

- **Program.cs:** Cambios puntuales de configuración o arreglos de inicio (p. ej. el mencionado en 2.2 para Stripe).
- **appsettings.json:** Ajustes de configuración del proyecto (sin documentación específica de cada clave).
- **wwwroot/app.css, wwwroot/app.js:** Archivos estáticos servidos por la aplicación; actualizaciones de estilos o scripts de soporte.

Para detalles de despliegue o variables de entorno, ver la documentación de configuración del proyecto o del entorno (p. ej. README, guías de despliegue).

---

## 6. Referencia cruzada con otra documentación

| Tema | Dónde está documentado |
|------|-------------------------|
| Política de eliminación física (hard delete) | [POLITICA-ELIMINACION.md](../compliance/POLITICA-ELIMINACION.md) |
| Roles y permisos del tenant | [SISTEMA-ROLES-PERMISOS.md](../roles-permisos/SISTEMA-ROLES-PERMISOS.md), [ROLES-PERMISOS-IMPLEMENTATION.md](../roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md), `docs/implementation/` |
| Commits y cambios sin doc específica | Este documento (CHANGELOG-COMMITS.md) |

---

*Última actualización: febrero 2025.*
