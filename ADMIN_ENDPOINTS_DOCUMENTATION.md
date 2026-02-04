# Documentación: Admin Panel Endpoints

## Resumen de Implementación

Se han creado exitosamente los endpoints del panel de administración para usuarios SystemAdmin en el sistema Planilla SaaS.

## Archivos Creados

### DTOs (C:\Planilla\src\Core\Planilla.Application\DTOs\Admin\)

1. **CreateTenantDto.cs** - DTO para crear un nuevo tenant con usuario owner
2. **AdminTenantDto.cs** - DTO detallado de tenant para el Admin Panel (incluye Owner y Usage)
3. **UpdateAdminTenantDto.cs** - DTO para actualizar información de un tenant
4. **SystemMetricsDto.cs** - DTO con métricas generales del sistema
5. **AdminTenantUserDto.cs** - DTO de usuarios de tenant para el Admin Panel

### Controller

**C:\Planilla\src\UI\Planilla.Web\Controllers\AdminController.cs** - Controlador principal con 7 endpoints

## Endpoints Implementados

### Seguridad

**IMPORTANTE**: Todos los endpoints verifican que `AppUser.IsSystemAdmin = true` mediante el método privado `IsSystemAdminAsync()`. Si el usuario no es SystemAdmin, retorna `403 Forbidden`.

Los SystemAdmins NO están limitados por TenantContext - pueden ver y gestionar todos los tenants del sistema.

---

### 1. GET /api/admin/tenants

Lista todos los tenants del sistema con información de suscripción y uso.

**Autenticación**: Bearer Token (SystemAdmin required)

**Respuesta exitosa (200 OK)**:
```json
[
  {
    "id": 1,
    "name": "Empresa ABC",
    "subdomain": "empresa-abc",
    "ruc": "155566-1-123456",
    "dv": "12",
    "address": "Calle 50, Panamá",
    "phone": "+507 1234-5678",
    "email": "contacto@abc.com",
    "createdAt": "2024-01-15T10:30:00Z",
    "isActive": true,
    "subscription": {
      "plan": 2,
      "planName": "Professional",
      "status": 1,
      "statusName": "Active",
      "trialEndsAt": null,
      "maxEmployees": 100,
      "maxUsers": 10,
      "maxCompanies": 3,
      "canExportExcel": true,
      "canExportPdf": true,
      "canUseApi": true,
      "monthlyPrice": 79.99
    },
    "owner": null,
    "usage": {
      "totalUsers": 5,
      "activeUsers": 5,
      "totalEmployees": 45,
      "activeEmployees": 42,
      "totalPayrolls": 12,
      "pendingInvitations": 2,
      "maxUsers": 10,
      "maxEmployees": 100,
      "userUsagePercentage": 50.0,
      "employeeUsagePercentage": 42.0
    }
  }
]
```

**Ejemplo cURL**:
```bash
curl -X GET http://localhost:5039/api/admin/tenants \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN"
```

---

### 2. GET /api/admin/tenants/{id}

Obtiene detalles completos de un tenant específico, incluyendo información del propietario.

**Autenticación**: Bearer Token (SystemAdmin required)

**Parámetros**:
- `id` (path): ID del tenant

**Respuesta exitosa (200 OK)**:
```json
{
  "id": 1,
  "name": "Empresa ABC",
  "subdomain": "empresa-abc",
  "ruc": "155566-1-123456",
  "dv": "12",
  "address": "Calle 50, Panamá",
  "phone": "+507 1234-5678",
  "email": "contacto@abc.com",
  "createdAt": "2024-01-15T10:30:00Z",
  "isActive": true,
  "subscription": {
    "plan": 2,
    "planName": "Professional",
    "status": 1,
    "statusName": "Active",
    "trialEndsAt": null,
    "maxEmployees": 100,
    "maxUsers": 10,
    "maxCompanies": 3,
    "canExportExcel": true,
    "canExportPdf": true,
    "canUseApi": true,
    "monthlyPrice": 79.99
  },
  "owner": {
    "userId": "abc123-def456-...",
    "email": "owner@abc.com",
    "fullName": "Juan Pérez",
    "joinedAt": "2024-01-15T10:35:00Z",
    "lastLoginAt": "2024-02-20T14:30:00Z"
  },
  "usage": {
    "totalUsers": 5,
    "activeUsers": 5,
    "totalEmployees": 45,
    "activeEmployees": 42,
    "totalPayrolls": 12,
    "pendingInvitations": 2,
    "maxUsers": 10,
    "maxEmployees": 100,
    "userUsagePercentage": 50.0,
    "employeeUsagePercentage": 42.0
  }
}
```

**Respuesta error (404 Not Found)**:
```json
{
  "error": "Tenant no encontrado"
}
```

**Ejemplo cURL**:
```bash
curl -X GET http://localhost:5039/api/admin/tenants/1 \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN"
```

---

### 3. POST /api/admin/tenants

Crea un nuevo tenant con usuario owner.

**Autenticación**: Bearer Token (SystemAdmin required)

**Body (JSON)**:
```json
{
  "name": "Nueva Empresa S.A.",
  "ownerEmail": "owner@nuevaempresa.com",
  "ownerPassword": "SecurePassword123!",
  "ownerFullName": "Carlos Rodríguez",
  "ruc": "155566-1-789012",
  "dv": "34",
  "address": "Avenida Balboa, Ciudad de Panamá",
  "phone": "+507 9876-5432",
  "companyEmail": "info@nuevaempresa.com"
}
```

**Campos requeridos**:
- `name`: Nombre de la empresa (máx 200 caracteres)
- `ownerEmail`: Email válido del propietario
- `ownerPassword`: Contraseña (entre 6 y 100 caracteres)

**Campos opcionales**:
- `ownerFullName`: Nombre completo del propietario
- `ruc`: RUC de Panamá
- `dv`: Dígito verificador
- `address`: Dirección física
- `phone`: Teléfono
- `companyEmail`: Email de contacto de la empresa

**Respuesta exitosa (201 Created)**:
```json
{
  "id": 5,
  "name": "Nueva Empresa S.A.",
  "subdomain": "nueva-empresa-sa",
  "ruc": "155566-1-789012",
  "dv": "34",
  "address": "Avenida Balboa, Ciudad de Panamá",
  "phone": "+507 9876-5432",
  "email": "info@nuevaempresa.com",
  "createdAt": "2024-02-20T15:00:00Z",
  "isActive": true,
  "subscription": {
    "plan": 2,
    "planName": "Professional",
    "status": 2,
    "statusName": "Trialing",
    "trialEndsAt": "2024-03-05T15:00:00Z",
    "maxEmployees": 100,
    "maxUsers": 10,
    "maxCompanies": 3,
    "canExportExcel": true,
    "canExportPdf": true,
    "canUseApi": true,
    "monthlyPrice": 79.99
  },
  "owner": {
    "userId": "xyz789-abc123-...",
    "email": "owner@nuevaempresa.com",
    "fullName": "Carlos Rodríguez",
    "joinedAt": "2024-02-20T15:00:00Z",
    "lastLoginAt": null
  },
  "usage": {
    "totalUsers": 1,
    "activeUsers": 1,
    "totalEmployees": 0,
    "activeEmployees": 0,
    "totalPayrolls": 0,
    "pendingInvitations": 0,
    "maxUsers": 10,
    "maxEmployees": 100,
    "userUsagePercentage": 10.0,
    "employeeUsagePercentage": 0.0
  }
}
```

**Respuesta error (400 Bad Request)**:
```json
{
  "error": "El email del propietario ya está registrado en el sistema"
}
```

**Notas**:
- Por defecto, crea el tenant con plan **Professional** en **Trial** de 14 días
- El subdomain se genera automáticamente del nombre de la empresa
- El usuario owner se crea con `EmailConfirmed = true`
- Se crea automáticamente la suscripción y la relación TenantUser

**Ejemplo cURL**:
```bash
curl -X POST http://localhost:5039/api/admin/tenants \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Nueva Empresa S.A.",
    "ownerEmail": "owner@nuevaempresa.com",
    "ownerPassword": "SecurePassword123!",
    "ownerFullName": "Carlos Rodríguez",
    "ruc": "155566-1-789012",
    "dv": "34",
    "address": "Avenida Balboa, Ciudad de Panamá",
    "phone": "+507 9876-5432",
    "companyEmail": "info@nuevaempresa.com"
  }'
```

---

### 4. PUT /api/admin/tenants/{id}

Actualiza información de un tenant existente.

**Autenticación**: Bearer Token (SystemAdmin required)

**Parámetros**:
- `id` (path): ID del tenant

**Body (JSON)** - Todos los campos son opcionales:
```json
{
  "name": "Empresa ABC Actualizada",
  "ruc": "155566-1-999999",
  "dv": "99",
  "address": "Nueva dirección, Panamá",
  "phone": "+507 1111-2222",
  "email": "nuevo@abc.com",
  "isActive": true
}
```

**Respuesta exitosa (200 OK)**: Retorna el tenant actualizado (mismo formato que GET /api/admin/tenants/{id})

**Respuesta error (404 Not Found)**:
```json
{
  "error": "Tenant no encontrado"
}
```

**Ejemplo cURL**:
```bash
curl -X PUT http://localhost:5039/api/admin/tenants/1 \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Empresa ABC Actualizada",
    "isActive": true
  }'
```

---

### 5. DELETE /api/admin/tenants/{id}

Desactiva un tenant (soft delete). No elimina físicamente los datos, solo marca `IsActive = false`.

**Autenticación**: Bearer Token (SystemAdmin required)

**Parámetros**:
- `id` (path): ID del tenant

**Respuesta exitosa (200 OK)**:
```json
{
  "success": true,
  "message": "Tenant desactivado exitosamente"
}
```

**Respuesta error (404 Not Found)**:
```json
{
  "error": "Tenant no encontrado"
}
```

**Ejemplo cURL**:
```bash
curl -X DELETE http://localhost:5039/api/admin/tenants/1 \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN"
```

---

### 6. GET /api/admin/metrics

Obtiene métricas generales del sistema.

**Autenticación**: Bearer Token (SystemAdmin required)

**Respuesta exitosa (200 OK)**:
```json
{
  "totalTenants": 15,
  "activeTenants": 12,
  "inactiveTenants": 3,
  "totalUsers": 78,
  "totalEmployees": 542,
  "totalPayrolls": 234,
  "tenantsByPlan": {
    "Free": 3,
    "Starter": 5,
    "Professional": 6,
    "Enterprise": 1
  },
  "tenantsBySubscriptionStatus": {
    "Active": 10,
    "Trialing": 2,
    "Canceled": 1,
    "PastDue": 2
  },
  "tenantsLast30Days": 4,
  "tenantsLast7Days": 1,
  "trialingTenants": 2,
  "lastUpdated": "2024-02-20T15:30:00Z"
}
```

**Ejemplo cURL**:
```bash
curl -X GET http://localhost:5039/api/admin/metrics \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN"
```

---

### 7. GET /api/admin/tenants/{id}/users

Lista todos los usuarios de un tenant específico.

**Autenticación**: Bearer Token (SystemAdmin required)

**Parámetros**:
- `id` (path): ID del tenant

**Respuesta exitosa (200 OK)**:
```json
[
  {
    "id": 1,
    "userId": "abc123-def456-...",
    "email": "owner@abc.com",
    "fullName": "Juan Pérez",
    "role": 0,
    "roleName": "Owner",
    "isActive": true,
    "joinedAt": "2024-01-15T10:35:00Z",
    "lastLoginAt": "2024-02-20T14:30:00Z",
    "isPendingInvitation": false,
    "invitationExpiresAt": null
  },
  {
    "id": 2,
    "userId": "xyz789-abc123-...",
    "email": "admin@abc.com",
    "fullName": "María González",
    "role": 1,
    "roleName": "Admin",
    "isActive": true,
    "joinedAt": "2024-01-20T09:00:00Z",
    "lastLoginAt": "2024-02-19T16:45:00Z",
    "isPendingInvitation": false,
    "invitationExpiresAt": null
  },
  {
    "id": 3,
    "userId": "",
    "email": "nuevo@abc.com",
    "fullName": null,
    "role": 2,
    "roleName": "Manager",
    "isActive": false,
    "joinedAt": "2024-02-18T11:20:00Z",
    "lastLoginAt": null,
    "isPendingInvitation": true,
    "invitationExpiresAt": "2024-02-25T11:20:00Z"
  }
]
```

**Respuesta error (404 Not Found)**:
```json
{
  "error": "Tenant no encontrado"
}
```

**Ejemplo cURL**:
```bash
curl -X GET http://localhost:5039/api/admin/tenants/1/users \
  -H "Authorization: Bearer YOUR_SYSTEM_ADMIN_TOKEN"
```

---

## Cómo Probar los Endpoints

### 1. Obtener Token de SystemAdmin

Primero, debes hacer login con uno de los usuarios SystemAdmin existentes:

```bash
curl -X POST http://localhost:5039/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "gjoseluisgonzalez507@gmail.com",
    "password": "YOUR_PASSWORD"
  }'
```

O con el otro usuario SystemAdmin:

```bash
curl -X POST http://localhost:5039/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "contacto@vorluno.dev",
    "password": "YOUR_PASSWORD"
  }'
```

**Respuesta**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "abc123...",
  "expiresAt": "2024-02-21T15:30:00Z",
  "user": { ... },
  "tenant": { ... },
  "subscription": { ... }
}
```

Guarda el valor de `token` para usarlo en los siguientes requests.

### 2. Usar el Token en los Requests

Agrega el header `Authorization: Bearer {token}` en todas las peticiones:

```bash
curl -X GET http://localhost:5039/api/admin/tenants \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 3. Ejemplos de Uso Común

**Crear un nuevo tenant**:
```bash
curl -X POST http://localhost:5039/api/admin/tenants \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Prueba Empresa",
    "ownerEmail": "owner@prueba.com",
    "ownerPassword": "Password123!",
    "ownerFullName": "Test Owner",
    "ruc": "12345-1-67890",
    "dv": "11"
  }'
```

**Ver métricas del sistema**:
```bash
curl -X GET http://localhost:5039/api/admin/metrics \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Actualizar un tenant**:
```bash
curl -X PUT http://localhost:5039/api/admin/tenants/1 \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "phone": "+507 9999-9999",
    "address": "Nueva dirección actualizada"
  }'
```

---

## Seguridad y Validaciones

1. **Verificación de SystemAdmin**: Todos los endpoints verifican `AppUser.IsSystemAdmin = true` antes de ejecutar cualquier lógica.

2. **Sin límites de TenantContext**: Los SystemAdmins pueden acceder a información de cualquier tenant, sin restricciones multi-tenant.

3. **Validaciones de entrada**:
   - Emails válidos
   - Longitudes de campos según las restricciones del modelo
   - Contraseñas con mínimo 6 caracteres

4. **Soft Delete**: El endpoint DELETE solo desactiva tenants, no los elimina físicamente.

5. **Logging**: Todas las operaciones críticas se registran en logs con el UserId del SystemAdmin que ejecutó la acción.

6. **Transacciones**: La creación de tenants usa transacciones de base de datos para garantizar consistencia.

---

## Testing con Postman

Puedes importar la siguiente colección en Postman para facilitar el testing:

1. Crea una nueva colección "Admin Panel Endpoints"
2. Crea una variable de entorno `baseUrl` con valor `http://localhost:5039`
3. Crea una variable de entorno `adminToken` para almacenar el token
4. Agrega cada endpoint con sus respectivos ejemplos

**Autenticación automática**:
En la configuración de la colección, agrega en "Authorization":
- Type: Bearer Token
- Token: `{{adminToken}}`

---

## Próximos Pasos

1. Implementar endpoint para cambiar el plan de suscripción de un tenant
2. Implementar endpoint para extender período de prueba
3. Agregar endpoint para obtener logs de auditoría a nivel sistema
4. Crear dashboard frontend para visualizar estas métricas
5. Implementar notificaciones por email cuando un SystemAdmin crea un tenant

---

## Notas Técnicas

- **Framework**: ASP.NET Core 9 con Entity Framework Core
- **Base de datos**: PostgreSQL con multi-tenancy
- **Autenticación**: ASP.NET Core Identity + JWT Bearer
- **Arquitectura**: Clean Architecture (Domain/Application/Infrastructure/Web)
- **ORM**: Entity Framework Core con Query Filters para multi-tenancy

---

Fecha de creación: 2024-02-20
Versión: 1.0.0
