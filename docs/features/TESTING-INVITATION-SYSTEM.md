# Checklist de Pruebas: Sistema de Invitación de Usuarios

## Preparación
- [ ] Tener un tenant en plan **Free** (límite: 1 usuario)
- [ ] Tener un tenant en plan **Starter** (límite: 3 usuarios)
- [ ] Tener un tenant en plan **Professional** (límite: 10 usuarios)
- [ ] Tener un usuario SystemAdmin
- [ ] Tener un usuario Owner de un tenant
- [ ] Tener un usuario Admin de un tenant
- [ ] Tener un usuario Manager (sin permisos de invitación)

---

## Test Suite 1: Validación de Límites del Plan

### Test 1.1: Free Plan - Alcanzar Límite de Usuarios
**Endpoint**: `POST /api/tenant/invite`
**Tenant**: Plan Free (MaxUsers = 1)
**Precondición**: Tenant ya tiene 1 usuario activo (el Owner)

**Pasos:**
1. Login como Owner del tenant Free
2. Intentar invitar a `test@example.com`
3. **Resultado esperado**: Error 400 con mensaje:
   ```json
   {
     "error": "PLAN_LIMIT_REACHED",
     "message": "Has alcanzado el límite de 1 usuarios en tu plan Free..."
   }
   ```

**Criterio de aceptación**: ❌ La invitación debe ser rechazada

---

### Test 1.2: Starter Plan - Invitación dentro del límite
**Endpoint**: `POST /api/tenant/invite`
**Tenant**: Plan Starter (MaxUsers = 3)
**Precondición**: Tenant tiene 1 usuario activo (Owner)

**Pasos:**
1. Login como Owner del tenant Starter
2. Invitar a `user1@example.com` - ✅ Success
3. Invitar a `user2@example.com` - ✅ Success
4. Intentar invitar a `user3@example.com` - ❌ Error (límite alcanzado)

**Criterio de aceptación**: Las primeras 2 invitaciones exitosas, la 3ra rechazada

---

### Test 1.3: Invitaciones Pendientes cuentan para el límite
**Endpoint**: `POST /api/tenant/invite`
**Tenant**: Plan Starter (MaxUsers = 3)
**Precondición**: Tenant tiene 1 usuario activo + 2 invitaciones pendientes

**Pasos:**
1. Login como Owner
2. Intentar invitar a `newuser@example.com`
3. **Resultado esperado**: Error 400 - límite alcanzado

**Criterio de aceptación**: ❌ Invitación rechazada porque pendientes + activos = 3

---

## Test Suite 2: Validación de Email Duplicado

### Test 2.1: Usuario ya activo en el tenant
**Endpoint**: `POST /api/tenant/invite`
**Precondición**: `existing@example.com` ya es usuario activo en el tenant

**Pasos:**
1. Login como Owner
2. Intentar invitar a `existing@example.com`
3. **Resultado esperado**: Error 400 con mensaje:
   ```json
   {
     "error": "Este usuario ya está activo en el tenant"
   }
   ```

**Criterio de aceptación**: ❌ Invitación rechazada

---

### Test 2.2: Invitación pendiente duplicada
**Endpoint**: `POST /api/tenant/invite`
**Precondición**: Ya existe invitación pendiente para `pending@example.com`

**Pasos:**
1. Login como Owner
2. Intentar invitar nuevamente a `pending@example.com`
3. **Resultado esperado**: Error 400 con mensaje:
   ```json
   {
     "error": "Ya existe una invitación pendiente para este email"
   }
   ```

**Criterio de aceptación**: ❌ Invitación duplicada rechazada

---

## Test Suite 3: Usuarios en Múltiples Tenants

### Test 3.1: Invitar mismo email a 2 tenants diferentes
**Endpoint**: `POST /api/tenant/invite`

**Pasos:**
1. Login como Owner de **Tenant A**
2. Invitar a `multitenantuser@example.com` con rol Admin - ✅ Success
3. Aceptar invitación (crear cuenta)
4. Login como Owner de **Tenant B**
5. Invitar a `multitenantuser@example.com` con rol Manager - ✅ Success
6. Aceptar invitación (usuario ya existe, solo agregar TenantUser)

**Criterio de aceptación**:
- ✅ Usuario existe en ambos tenants
- ✅ En Tenant A tiene rol Admin
- ✅ En Tenant B tiene rol Manager
- ✅ Al hacer login, puede seleccionar el tenant

---

## Test Suite 4: Autorización de Endpoints

### Test 4.1: SystemAdmin puede invitar desde Admin Panel
**Endpoint**: `POST /api/admin/tenants/{id}/users`
**Auth**: SystemAdmin

**Pasos:**
1. Login como SystemAdmin
2. Invitar a `newuser@example.com` en un tenant específico
3. **Resultado esperado**: 201 Created con datos del usuario

**Criterio de aceptación**: ✅ Invitación exitosa, usuario creado

---

### Test 4.2: Owner puede invitar desde Tenant Settings
**Endpoint**: `POST /api/tenant/invite`
**Auth**: Owner del tenant

**Pasos:**
1. Login como Owner
2. Invitar a `teammember@example.com`
3. **Resultado esperado**: 201 Created con token de invitación

**Criterio de aceptación**: ✅ Invitación exitosa

---

### Test 4.3: Admin puede invitar desde Tenant Settings
**Endpoint**: `POST /api/tenant/invite`
**Auth**: Admin del tenant

**Pasos:**
1. Login como Admin (no Owner)
2. Invitar a `newmember@example.com`
3. **Resultado esperado**: 201 Created

**Criterio de aceptación**: ✅ Admin tiene permisos de invitación

---

### Test 4.4: Manager NO puede invitar
**Endpoint**: `POST /api/tenant/invite`
**Auth**: Manager del tenant

**Pasos:**
1. Login como Manager
2. Intentar invitar a `test@example.com`
3. **Resultado esperado**: 403 Forbidden

**Criterio de aceptación**: ❌ Manager no tiene permisos

---

### Test 4.5: Accountant NO puede invitar
**Endpoint**: `POST /api/tenant/invite`
**Auth**: Accountant del tenant

**Pasos:**
1. Login como Accountant
2. Intentar invitar a `test@example.com`
3. **Resultado esperado**: 403 Forbidden

**Criterio de aceptación**: ❌ Accountant no tiene permisos

---

## Test Suite 5: Audit Log

### Test 5.1: Invitación registrada en audit log
**Endpoint**: `POST /api/tenant/invite` → `GET /api/tenant/audit`

**Pasos:**
1. Login como Owner
2. Invitar a `audittest@example.com`
3. Consultar audit log: `GET /api/tenant/audit?action=InviteCreated`
4. **Resultado esperado**: Debe aparecer un registro con:
   - Action: "InviteCreated"
   - EntityType: "TenantInvitation"
   - Metadata: {"InvitedEmail": "audittest@example.com", "Role": "Manager"}

**Criterio de aceptación**: ✅ Registro encontrado en audit log

---

### Test 5.2: Invitación aceptada registrada en audit log
**Endpoint**: `POST /api/auth/accept-invitation` → `GET /api/admin/tenants/{id}/audit`

**Pasos:**
1. Invitar usuario
2. Aceptar invitación
3. SystemAdmin consulta audit log del tenant
4. **Resultado esperado**: Debe aparecer registro con:
   - Action: "InviteAccepted"
   - EntityType: "TenantInvitation"

**Criterio de aceptación**: ✅ Aceptación registrada

---

## Test Suite 6: SystemAdmin vs Owner

### Test 6.1: SystemAdmin puede invitar a CUALQUIER tenant
**Endpoint**: `POST /api/admin/tenants/{id}/users`

**Pasos:**
1. Login como SystemAdmin
2. Obtener lista de tenants: `GET /api/admin/tenants`
3. Seleccionar un tenant al azar (TenantId = X)
4. Invitar usuario a ese tenant: `POST /api/admin/tenants/X/users`
5. **Resultado esperado**: ✅ Success

**Criterio de aceptación**: SystemAdmin no está limitado por TenantContext

---

### Test 6.2: Owner solo puede invitar a SU tenant
**Endpoint**: `POST /api/tenant/invite`

**Pasos:**
1. Login como Owner de Tenant A (TenantId = 1)
2. Intentar manipular request para invitar a Tenant B
   - (No es posible porque el endpoint usa TenantContext)
3. **Resultado esperado**: Invitación se crea en Tenant A (su tenant actual)

**Criterio de aceptación**: Owner respeta TenantContext automáticamente

---

## Test Suite 7: Validación de Estado de Suscripción

### Test 7.1: Suscripción PastDue no puede invitar
**Endpoint**: `POST /api/tenant/invite`
**Precondición**: Tenant tiene suscripción con Status = PastDue

**Pasos:**
1. Login como Owner
2. Intentar invitar usuario
3. **Resultado esperado**: Error 400 con mensaje:
   ```json
   {
     "error": "PLAN_LIMIT_REACHED",
     "message": "Tu suscripción tiene un pago pendiente. Por favor actualiza tu método de pago."
   }
   ```

**Criterio de aceptación**: ❌ Invitación rechazada

---

### Test 7.2: Suscripción Canceled no puede invitar
**Endpoint**: `POST /api/tenant/invite`
**Precondición**: Tenant tiene suscripción con Status = Canceled

**Pasos:**
1. Login como Owner
2. Intentar invitar usuario
3. **Resultado esperado**: Error 400 con mensaje:
   ```json
   {
     "error": "PLAN_LIMIT_REACHED",
     "message": "Tu suscripción ha sido cancelada. Reactiva tu suscripción para continuar."
   }
   ```

**Criterio de aceptación**: ❌ Invitación rechazada

---

## Test Suite 8: Mensajes de Error Claros

### Test 8.1: Error indica plan siguiente recomendado
**Endpoint**: `POST /api/tenant/invite`
**Precondición**: Tenant en plan Starter (límite alcanzado)

**Pasos:**
1. Login como Owner
2. Intentar invitar usuario
3. **Resultado esperado**: Mensaje debe incluir plan recomendado:
   ```
   "Has alcanzado el límite de 3 usuarios en tu plan Starter.
    Actualiza a Professional (hasta 10 usuarios) para continuar."
   ```

**Criterio de aceptación**: ✅ Mensaje indica upgrade path

---

## Resumen de Criterios de Aceptación

| Test | Criterio | Estado |
|------|----------|--------|
| 1.1  | Límite Free respetado | ⬜ Pendiente |
| 1.2  | Límite Starter respetado | ⬜ Pendiente |
| 1.3  | Invitaciones pendientes cuentan | ⬜ Pendiente |
| 2.1  | Email duplicado rechazado | ⬜ Pendiente |
| 2.2  | Invitación pendiente duplicada rechazada | ⬜ Pendiente |
| 3.1  | Usuario en múltiples tenants | ⬜ Pendiente |
| 4.1  | SystemAdmin puede invitar | ⬜ Pendiente |
| 4.2  | Owner puede invitar | ⬜ Pendiente |
| 4.3  | Admin puede invitar | ⬜ Pendiente |
| 4.4  | Manager NO puede invitar | ⬜ Pendiente |
| 4.5  | Accountant NO puede invitar | ⬜ Pendiente |
| 5.1  | Invitación en audit log | ⬜ Pendiente |
| 5.2  | Aceptación en audit log | ⬜ Pendiente |
| 6.1  | SystemAdmin multi-tenant | ⬜ Pendiente |
| 6.2  | Owner respeta TenantContext | ⬜ Pendiente |
| 7.1  | PastDue rechazado | ⬜ Pendiente |
| 7.2  | Canceled rechazado | ⬜ Pendiente |
| 8.1  | Mensajes con upgrade path | ⬜ Pendiente |

---

## Ejecución de Pruebas

Para ejecutar estas pruebas:

1. **Crear datos de prueba**:
   ```sql
   -- Crear tenants con diferentes planes
   INSERT INTO Tenants (Name, Subdomain, IsActive, CreatedAt) VALUES
   ('Empresa Free', 'free-test', true, NOW()),
   ('Empresa Starter', 'starter-test', true, NOW()),
   ('Empresa Pro', 'pro-test', true, NOW());

   -- Crear suscripciones
   -- (Usar el endpoint POST /api/admin/tenants desde SystemAdmin)
   ```

2. **Usar Postman/Insomnia** para ejecutar las requests

3. **Marcar ✅** cada test que pase

4. **Documentar ❌** los tests que fallen con detalles del error

---

## Herramientas Recomendadas

- **Postman Collection**: Crear una colección con todos los endpoints
- **Newman**: Para ejecutar tests automatizados
- **SQL Scripts**: Para setup y teardown de datos de prueba

---

## Criterios de Éxito del Sistema

El sistema de invitaciones se considera **COMPLETO Y FUNCIONAL** si:

✅ Todos los tests de límites de plan pasan
✅ No se pueden crear usuarios duplicados en el mismo tenant
✅ Los usuarios pueden estar en múltiples tenants
✅ Solo SystemAdmin, Owner y Admin pueden invitar
✅ Todas las acciones se registran en audit log
✅ Los mensajes de error son claros y accionables
✅ El sistema respeta el estado de suscripción (PastDue, Canceled)

---

**Fecha de creación**: 2026-01-31
**Versión**: 1.0
**Responsable**: Backend Architect
