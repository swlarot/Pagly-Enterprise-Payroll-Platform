# 🔧 Plan Maestro de Refactorización - Vorluno Planilla

**Versión**: 1.0  
**Fecha**: 30 de enero de 2026  
**Estado**: Plan de Ejecución  

---

## 📋 RESUMEN EJECUTIVO

Este plan aborda 14 problemas críticos identificados en el sistema Vorluno Planilla, organizados en 4 niveles de prioridad con fases incrementales. El objetivo es estabilizar el sistema, corregir la arquitectura multi-tenant y preparar el camino para funcionalidades avanzadas.

### Problemas Identificados (Clasificados por Severidad)

| # | Problema | Severidad | Nivel |
|---|----------|-----------|-------|
| 1 | Errores de API (JSON parsing) en páginas principales | 🔴 Crítico | Infraestructura |
| 2 | Redirección incorrecta post-login | 🔴 Crítico | Infraestructura |
| 3 | Propietario de Tenant no se guarda correctamente | 🔴 Crítico | Panel Admin |
| 4 | Páginas en blanco (Préstamos, Deducciones, etc.) | 🟠 Alto | Dashboard |
| 5 | Gestión de Usuarios mal ubicada | 🟠 Alto | Panel Admin |
| 6 | Selección de empresa multi-tenant faltante | 🟠 Alto | Infraestructura |
| 7 | Sistema de Roles personalizado incompleto | 🟡 Medio | Dashboard |
| 8 | "Uso del Plan" innecesario en Dashboard | 🟢 Bajo | Dashboard |
| 9 | Configuración mal estructurada | 🟡 Medio | Dashboard |
| 10 | Audit Log mal ubicado | 🟡 Medio | Panel Admin |
| 11 | Invitación de usuarios exclusiva de Admin | 🟡 Medio | Panel Admin |
| 12 | Endpoints backend faltantes para gestión de usuarios | 🟠 Alto | Infraestructura |
| 13 | Falta validación de límites de plan | 🟡 Medio | Infraestructura |
| 14 | Cross-tenant access no validado en tests | 🟠 Alto | Infraestructura |

---

## 🏗️ NIVEL 1: INFRAESTRUCTURA (Fases 1.1 - 1.4)

> **Objetivo**: Estabilizar la base del sistema antes de tocar UI.

### Fase 1.1: Diagnóstico y Corrección de API Errors

**Síntoma**: Errores `"Unexpected token '<', "<!doctype "... is not valid JSON"` en múltiples páginas.

**Causa Probable**: 
- Rutas de API mal configuradas (SPA fallback devuelve HTML en lugar de 404)
- Middleware de autenticación redirigiendo a login page en lugar de 401
- CORS mal configurado
- Proxy de Vite no apuntando correctamente al backend

**Archivos a Revisar**:
```
📁 Backend
├── src/UI/Planilla.Web/Program.cs
│   └── Verificar orden de middleware: UseRouting → UseAuthentication → UseAuthorization → MapControllers → MapFallbackToFile
├── src/UI/Planilla.Web/Controllers/*.cs
│   └── Verificar que todos retornan JSON con [ApiController] y [Route("api/[controller]")]
└── src/Infrastructure/Planilla.Infrastructure/
    └── Verificar DbContext está registrado correctamente

📁 Frontend
├── ClientApp/vite.config.js
│   └── Verificar proxy: server.proxy['/api'] → target: 'https://localhost:7105'
├── ClientApp/src/services/api.js (o apiClient.js)
│   └── Verificar baseURL y headers de autenticación
└── ClientApp/src/contexts/AuthContext.jsx
    └── Verificar que token se envía en headers
```

**Checklist de Verificación**:
- [ ] `Program.cs` tiene `app.MapFallbackToFile("index.html")` AL FINAL del pipeline
- [ ] Controllers tienen `[ApiController]` attribute para respuestas consistentes
- [ ] Vite proxy configurado: `'/api': { target: 'https://localhost:7105', secure: false, changeOrigin: true }`
- [ ] Token JWT se incluye en header `Authorization: Bearer {token}`
- [ ] Middleware de auth retorna 401 JSON, NO redirect HTML

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior .NET Backend Developer + React DevOps especialista en debugging de SPA + API.

## CONTEXTO
El sistema Vorluno Planilla tiene errores de parsing JSON en múltiples páginas del dashboard.
Error: "Unexpected token '<', "<!doctype "... is not valid JSON"

Esto indica que el frontend recibe HTML (probablemente página de error o login) 
en lugar de JSON cuando hace requests a /api/*.

## TAREA EXACTA

### Paso 1: Diagnosticar en Backend
1. Abrir `src/UI/Planilla.Web/Program.cs`
2. Verificar el orden del middleware pipeline:
   - UseRouting() debe estar antes de UseAuthentication()
   - MapFallbackToFile("index.html") debe estar DESPUÉS de MapControllers()
3. Buscar cualquier middleware custom que pueda estar haciendo redirect HTML

### Paso 2: Verificar Controllers
1. Listar todos los controllers en `src/UI/Planilla.Web/Controllers/`
2. Verificar que TODOS tengan:
   - [ApiController] attribute
   - [Route("api/[controller]")] attribute
   - Retornen ActionResult<T> o IActionResult con JSON

### Paso 3: Verificar Vite Proxy
1. Abrir `src/UI/Planilla.Web/ClientApp/vite.config.js`
2. Verificar configuración del proxy:
```js
server: {
  proxy: {
    '/api': {
      target: 'https://localhost:7105', // o el puerto correcto
      secure: false,
      changeOrigin: true
    }
  }
}
```

### Paso 4: Verificar API Client
1. Buscar archivo de servicio API (api.js, apiClient.js, axios instance)
2. Verificar que incluye token en headers:
```js
const token = localStorage.getItem('token');
if (token) {
  headers['Authorization'] = `Bearer ${token}`;
}
```

### Paso 5: Crear endpoint de diagnóstico
Agregar a Program.cs ANTES del fallback:
```csharp
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
```

## FORMATO DE ENTREGA
1. Lista de archivos revisados con problemas encontrados
2. Diff de cada corrección necesaria
3. Comando para probar: `curl -v http://localhost:5173/api/health`
4. Checklist de verificación post-fix
```

---

### Fase 1.2: Corrección del Flujo de Login y Redirección

**Síntoma**: Al hacer login, redirige a `/system-admin/tenants/create` mostrando "Acceso Denegado".

**Causa Probable**:
- Lógica de redirección no diferencia entre roles (SystemAdmin vs TenantOwner vs Employee)
- No hay verificación de cantidad de tenants del usuario
- El flujo asume que todo usuario autenticado es SystemAdmin

**Archivos a Revisar**:
```
📁 Frontend
├── ClientApp/src/pages/LoginPage.jsx
│   └── Verificar lógica post-login: ¿hacia dónde redirige?
├── ClientApp/src/contexts/AuthContext.jsx
│   └── Verificar qué datos trae /api/auth/me (roles, tenants[])
├── ClientApp/src/App.jsx
│   └── Verificar rutas protegidas y lógica de redirect
└── ClientApp/src/components/ProtectedRoute.jsx (si existe)
    └── Verificar lógica de autorización

📁 Backend
├── Controllers/AuthController.cs
│   └── Verificar qué retorna /api/auth/login y /api/auth/me
└── Services/AuthService.cs (o similar)
    └── Verificar claims del JWT (role, tenantId, tenants[])
```

**Flujo Correcto de Login**:
```
[Usuario ingresa credenciales]
         ↓
[POST /api/auth/login]
         ↓
[Backend valida + genera JWT con claims]
         ↓
[Backend retorna: { token, user, tenants[] }]
         ↓
[Frontend evalúa tenants.length]
         ↓
    ┌────┴────┐
    │         │
[tenants=1]  [tenants>1]
    │         │
    ↓         ↓
[Dashboard]  [TenantSelector Page]
    │         │
    │    [Usuario selecciona tenant]
    │         │
    └────┬────┘
         ↓
[Guardar tenantId seleccionado]
         ↓
[Redirigir a /dashboard]
```

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior Full-Stack Developer especialista en autenticación multi-tenant con JWT.

## CONTEXTO
Sistema SaaS multi-tenant Vorluno Planilla.
Problema: Al hacer login redirige incorrectamente a /system-admin/tenants/create 
y muestra "Acceso Denegado".

Un usuario puede pertenecer a múltiples empresas/tenants.

## TAREA EXACTA

### Backend - Modificar respuesta de login

1. Buscar `AuthController.cs` o equivalente
2. Modificar endpoint de login para retornar lista de tenants del usuario:

```csharp
// POST /api/auth/login
public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
{
    // ... validación existente ...
    
    var userTenants = await _tenantService.GetUserTenantsAsync(user.Id);
    
    return Ok(new LoginResponse
    {
        Token = token,
        User = new UserDto { Id = user.Id, Email = user.Email, Name = user.FullName },
        Tenants = userTenants.Select(t => new TenantSummaryDto 
        {
            Id = t.Id,
            Name = t.Name,
            Role = t.UserRole // Owner, Admin, Employee
        }).ToList(),
        RequiresTenantSelection = userTenants.Count > 1
    });
}
```

3. Crear endpoint para seleccionar tenant activo:
```csharp
// POST /api/auth/select-tenant
[Authorize]
public async Task<ActionResult> SelectTenant(Guid tenantId)
{
    var userId = User.GetUserId();
    var hasAccess = await _tenantService.UserHasAccessAsync(userId, tenantId);
    
    if (!hasAccess)
        return Forbid();
    
    // Generar nuevo token con tenantId en claims
    var newToken = await _tokenService.GenerateTokenWithTenant(userId, tenantId);
    
    return Ok(new { token = newToken });
}
```

### Frontend - Implementar flujo de selección

1. Crear `TenantSelectorPage.jsx`:
```jsx
// src/pages/TenantSelectorPage.jsx
export default function TenantSelectorPage() {
  const { user, tenants, selectTenant } = useAuth();
  const navigate = useNavigate();

  const handleSelect = async (tenantId) => {
    await selectTenant(tenantId);
    navigate('/dashboard');
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="max-w-md w-full space-y-8">
        <h2>Selecciona una empresa</h2>
        <div className="space-y-4">
          {tenants.map(tenant => (
            <button
              key={tenant.id}
              onClick={() => handleSelect(tenant.id)}
              className="w-full p-4 border rounded hover:bg-blue-50"
            >
              <div className="font-semibold">{tenant.name}</div>
              <div className="text-sm text-gray-500">{tenant.role}</div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
```

2. Modificar `AuthContext.jsx`:
```jsx
const login = async (email, password) => {
  const response = await api.post('/auth/login', { email, password });
  const { token, user, tenants, requiresTenantSelection } = response.data;
  
  localStorage.setItem('token', token);
  setUser(user);
  setTenants(tenants);
  
  return { requiresTenantSelection, tenants };
};

const selectTenant = async (tenantId) => {
  const response = await api.post('/auth/select-tenant', { tenantId });
  localStorage.setItem('token', response.data.token);
  // Reload user data with new tenant context
  await loadUser();
};
```

3. Modificar `LoginPage.jsx`:
```jsx
const handleSubmit = async (e) => {
  e.preventDefault();
  try {
    const { requiresTenantSelection } = await login(email, password);
    
    if (requiresTenantSelection) {
      navigate('/select-tenant');
    } else {
      navigate('/dashboard');
    }
  } catch (error) {
    setError(error.message);
  }
};
```

4. Agregar ruta en `App.jsx`:
```jsx
<Route path="/select-tenant" element={<TenantSelectorPage />} />
```

## FORMATO DE ENTREGA
1. Archivos backend modificados con diffs completos
2. Nuevos archivos frontend con código completo
3. Migraciones EF si se necesitan cambios en DB
4. Test manual: login con usuario que tiene 2+ tenants
```

---

### Fase 1.3: Endpoints de Gestión de Usuarios por Tenant

**Síntoma**: No existen los endpoints necesarios para gestionar usuarios dentro del Admin Panel.

**Endpoints Requeridos**:
```
GET    /api/admin/tenants/{id}/users        - Listar usuarios del tenant
POST   /api/admin/tenants/{id}/users        - Invitar usuario al tenant
PUT    /api/admin/tenants/{id}/users/{uid}  - Modificar rol/estado
DELETE /api/admin/tenants/{id}/users/{uid}  - Remover usuario del tenant
GET    /api/admin/tenants/{id}/audit        - Audit log del tenant
```

**Modelo de Datos Necesario**:
```
TenantUser (tabla pivote)
├── TenantId (FK)
├── UserId (FK)
├── Role (enum: Owner, Admin, Manager, Employee)
├── Status (enum: Active, Inactive, Pending)
├── InvitedAt
├── InvitedBy
├── JoinedAt
└── LastActivityAt
```

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior .NET Backend Developer especialista en Clean Architecture y multi-tenancy.

## CONTEXTO
Sistema SaaS multi-tenant Vorluno Planilla.
Necesitamos endpoints para que el SystemAdmin gestione usuarios dentro de cada tenant.

## TAREA EXACTA

### Paso 1: Crear/Verificar entidad TenantUser

Ubicación: `src/Core/Vorluno.Planilla.Domain/Entities/TenantUser.cs`

```csharp
public class TenantUser : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; }
    
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; }
    
    public TenantRole Role { get; set; } // Owner, Admin, Manager, Employee
    public TenantUserStatus Status { get; set; } // Active, Inactive, Pending
    
    public DateTime? InvitedAt { get; set; }
    public Guid? InvitedById { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public enum TenantRole { Owner = 1, Admin = 2, Manager = 3, Employee = 4 }
public enum TenantUserStatus { Pending = 0, Active = 1, Inactive = 2 }
```

### Paso 2: Crear DTOs

Ubicación: `src/Core/Vorluno.Planilla.Application/DTOs/TenantUserDto.cs`

```csharp
public record TenantUserDto(
    Guid UserId,
    string Email,
    string FullName,
    TenantRole Role,
    TenantUserStatus Status,
    DateTime? LastActivityAt
);

public record InviteUserRequest(string Email, TenantRole Role);

public record UpdateTenantUserRequest(TenantRole? Role, TenantUserStatus? Status);
```

### Paso 3: Crear Servicio

Ubicación: `src/Core/Vorluno.Planilla.Application/Services/TenantUserService.cs`

### Paso 4: Crear Controller

Ubicación: `src/UI/Planilla.Web/Controllers/Admin/TenantUsersController.cs`

```csharp
[ApiController]
[Route("api/admin/tenants/{tenantId}/users")]
[Authorize(Roles = "SystemAdmin")]
public class TenantUsersController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TenantUserDto>>> GetUsers(Guid tenantId)
    
    [HttpPost]
    public async Task<ActionResult<TenantUserDto>> InviteUser(Guid tenantId, InviteUserRequest request)
    
    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateUser(Guid tenantId, Guid userId, UpdateTenantUserRequest request)
    
    [HttpDelete("{userId}")]
    public async Task<ActionResult> RemoveUser(Guid tenantId, Guid userId)
}
```

### Paso 5: Migración EF Core

```bash
cd src/UI/Planilla.Web
dotnet ef migrations add AddTenantUserManagement --project ../../Infrastructure/Vorluno.Planilla.Infrastructure
```

## FORMATO DE ENTREGA
1. Archivos de entidad, DTO, servicio y controller completos
2. Comando de migración
3. Tests unitarios para el servicio
4. Ejemplo de request/response para cada endpoint
```

---

### Fase 1.4: Corrección del Propietario de Tenant

**Síntoma**: Al crear tenant, el propietario no se guarda y aparece "Sin propietario" en la lista.

**Causa Probable**:
- El formulario de creación de tenant envía datos del propietario pero no se procesan
- No se crea la relación TenantUser con Role = Owner
- El campo OwnerId del Tenant no se actualiza

**Archivos a Revisar**:
```
📁 Backend
├── Controllers/Admin/TenantsController.cs
│   └── POST /api/admin/tenants - ¿Recibe datos del owner?
├── Services/TenantService.cs
│   └── CreateTenantAsync - ¿Crea TenantUser con Role=Owner?
└── Entities/Tenant.cs
    └── ¿Tiene OwnerId y relación con User?

📁 Frontend
├── pages/admin/CreateTenantPage.jsx
│   └── ¿Envía ownerEmail, ownerName, ownerPassword?
└── services/tenantService.js
    └── ¿El request incluye datos del propietario?
```

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior .NET Backend Developer con experiencia en Identity y multi-tenancy.

## CONTEXTO
Al crear un tenant con datos del propietario (nombre, email, contraseña), 
el propietario no se asocia correctamente y aparece "Sin propietario" en la lista.

## TAREA EXACTA

### Paso 1: Verificar DTO de creación

Buscar el DTO que usa POST /api/admin/tenants:
```csharp
public record CreateTenantRequest
{
    public string Name { get; init; }
    public string Identifier { get; init; }
    public string Plan { get; init; }
    
    // Datos del propietario
    public string OwnerFullName { get; init; }
    public string OwnerEmail { get; init; }
    public string OwnerPassword { get; init; }
}
```

### Paso 2: Modificar servicio de creación

```csharp
public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request)
{
    // 1. Crear o buscar usuario
    var user = await _userManager.FindByEmailAsync(request.OwnerEmail);
    
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = request.OwnerEmail,
            Email = request.OwnerEmail,
            FullName = request.OwnerFullName,
            EmailConfirmed = true // Para desarrollo
        };
        
        var result = await _userManager.CreateAsync(user, request.OwnerPassword);
        if (!result.Succeeded)
            throw new ValidationException(result.Errors.First().Description);
    }
    
    // 2. Crear tenant
    var tenant = new Tenant
    {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Identifier = request.Identifier,
        Plan = Enum.Parse<SubscriptionPlan>(request.Plan),
        OwnerId = user.Id, // ← IMPORTANTE
        Status = TenantStatus.Trialing,
        CreatedAt = DateTime.UtcNow
    };
    
    _context.Tenants.Add(tenant);
    
    // 3. Crear relación TenantUser como Owner
    var tenantUser = new TenantUser
    {
        TenantId = tenant.Id,
        UserId = user.Id,
        Role = TenantRole.Owner,
        Status = TenantUserStatus.Active,
        JoinedAt = DateTime.UtcNow
    };
    
    _context.TenantUsers.Add(tenantUser);
    
    await _context.SaveChangesAsync();
    
    return MapToDto(tenant);
}
```

### Paso 3: Verificar que el frontend envía los datos

En el formulario de creación:
```jsx
const handleSubmit = async (e) => {
  e.preventDefault();
  
  const payload = {
    name: formData.name,
    identifier: formData.identifier,
    plan: formData.plan,
    ownerFullName: formData.ownerFullName,
    ownerEmail: formData.ownerEmail,
    ownerPassword: formData.ownerPassword
  };
  
  await api.post('/api/admin/tenants', payload);
};
```

### Paso 4: Modificar query de listado para incluir owner

```csharp
public async Task<List<TenantListDto>> GetTenantsAsync()
{
    return await _context.Tenants
        .Include(t => t.Owner) // ← Incluir owner
        .Select(t => new TenantListDto
        {
            Id = t.Id,
            Name = t.Name,
            OwnerName = t.Owner != null ? t.Owner.FullName : "Sin propietario",
            OwnerEmail = t.Owner != null ? t.Owner.Email : null,
            Plan = t.Plan.ToString(),
            Status = t.Status.ToString(),
            EmployeeCount = t.TenantUsers.Count
        })
        .ToListAsync();
}
```

## FORMATO DE ENTREGA
1. Diff de TenantService.cs
2. Diff del Controller si necesita cambios
3. Verificación de que Tenant tiene propiedad OwnerId y navegación Owner
4. Test: crear tenant → verificar en DB que OwnerId está seteado
```

---

## 🔧 NIVEL 2: PANEL ADMIN (Fases 2.1 - 2.3)

> **Objetivo**: Centralizar gestión de tenants y usuarios en el panel de administración.

### Fase 2.1: Mover Gestión de Usuarios al Admin Panel

**Cambios Requeridos**:

1. **Eliminar del menú tenant** (`AuthLayout.tsx`):
   - Remover sección "Administración" con "Usuarios" y "Audit Log"

2. **Agregar a TenantDetailsPage**:
   - Tab o sección "Usuarios del Tenant"
   - Tabla con: email, nombre, rol, estado, última actividad
   - Botón "Invitar Usuario" con modal
   - Acciones: cambiar rol, activar/desactivar

3. **Agregar sección Audit Log**:
   - Filtros por fecha, acción, usuario
   - Tabla con: fecha, usuario, acción, detalles

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior React Developer con experiencia en UIs de administración SaaS.

## CONTEXTO
Sistema Vorluno Planilla. La gestión de usuarios está mal ubicada en el menú del tenant.
Debe moverse al Panel de Admin (System Admin) dentro de TenantDetailsPage.

## TAREA EXACTA

### Paso 1: Eliminar del menú tenant

Archivo: `src/UI/Planilla.Web/ClientApp/src/layouts/AuthLayout.tsx`

Buscar y ELIMINAR la sección de "Administración" que contiene:
- Usuarios
- Audit Log

### Paso 2: Modificar TenantDetailsPage

Archivo: `src/UI/Planilla.Web/ClientApp/src/pages/admin/TenantDetailsPage.tsx`

Agregar dos nuevas secciones/tabs:

```tsx
import { useState, useEffect } from 'react';
import { useParams } from 'react-router-dom';
import { Users, Activity, Plus, Edit, Power } from 'lucide-react';

export default function TenantDetailsPage() {
  const { id } = useParams();
  const [activeTab, setActiveTab] = useState('info');
  const [tenant, setTenant] = useState(null);
  const [users, setUsers] = useState([]);
  const [auditLogs, setAuditLogs] = useState([]);
  const [showInviteModal, setShowInviteModal] = useState(false);

  useEffect(() => {
    loadTenant();
    if (activeTab === 'users') loadUsers();
    if (activeTab === 'audit') loadAuditLogs();
  }, [id, activeTab]);

  const loadUsers = async () => {
    const response = await fetch(`/api/admin/tenants/${id}/users`);
    setUsers(await response.json());
  };

  return (
    <div className="p-6">
      {/* Tabs */}
      <div className="border-b mb-6">
        <nav className="flex space-x-4">
          <TabButton active={activeTab === 'info'} onClick={() => setActiveTab('info')}>
            Información
          </TabButton>
          <TabButton active={activeTab === 'users'} onClick={() => setActiveTab('users')}>
            <Users className="w-4 h-4 mr-2" /> Usuarios
          </TabButton>
          <TabButton active={activeTab === 'audit'} onClick={() => setActiveTab('audit')}>
            <Activity className="w-4 h-4 mr-2" /> Audit Log
          </TabButton>
        </nav>
      </div>

      {/* Content */}
      {activeTab === 'info' && <TenantInfoSection tenant={tenant} />}
      {activeTab === 'users' && (
        <UsersSection 
          users={users} 
          onInvite={() => setShowInviteModal(true)}
          onRefresh={loadUsers}
        />
      )}
      {activeTab === 'audit' && <AuditLogSection logs={auditLogs} />}

      {/* Modal de invitación */}
      {showInviteModal && (
        <InviteUserModal
          tenantId={id}
          onClose={() => setShowInviteModal(false)}
          onSuccess={() => { loadUsers(); setShowInviteModal(false); }}
        />
      )}
    </div>
  );
}

function UsersSection({ users, onInvite, onRefresh }) {
  return (
    <div>
      <div className="flex justify-between mb-4">
        <h3 className="text-lg font-semibold">Usuarios del Tenant</h3>
        <button 
          onClick={onInvite}
          className="flex items-center px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          <Plus className="w-4 h-4 mr-2" /> Invitar Usuario
        </button>
      </div>
      
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Rol</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Estado</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Última Actividad</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Acciones</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-200">
          {users.map(user => (
            <tr key={user.userId}>
              <td className="px-6 py-4 whitespace-nowrap">{user.email}</td>
              <td className="px-6 py-4 whitespace-nowrap">{user.fullName}</td>
              <td className="px-6 py-4 whitespace-nowrap">
                <RoleBadge role={user.role} />
              </td>
              <td className="px-6 py-4 whitespace-nowrap">
                <StatusBadge status={user.status} />
              </td>
              <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                {user.lastActivityAt ? formatDate(user.lastActivityAt) : 'Nunca'}
              </td>
              <td className="px-6 py-4 whitespace-nowrap text-right">
                <button className="text-blue-600 hover:text-blue-900 mr-3">
                  <Edit className="w-4 h-4" />
                </button>
                <button className="text-gray-600 hover:text-gray-900">
                  <Power className="w-4 h-4" />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

## FORMATO DE ENTREGA
1. Diff de AuthLayout.tsx (líneas eliminadas)
2. Código completo de TenantDetailsPage.tsx modificado
3. Componentes auxiliares: InviteUserModal, RoleBadge, StatusBadge
4. Verificación visual: screenshot o descripción del resultado
```

---

### Fase 2.2: Audit Log por Tenant

**Endpoint Requerido**:
```
GET /api/admin/tenants/{id}/audit?page=1&pageSize=50&from=&to=&action=
```

**Modelo de Audit Log**:
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; }
    public string Action { get; set; } // Create, Update, Delete, Login, etc.
    public string EntityType { get; set; } // Employee, Payroll, etc.
    public Guid? EntityId { get; set; }
    public string OldValues { get; set; } // JSON
    public string NewValues { get; set; } // JSON
    public string IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

### Fase 2.3: Validación de Invitación de Usuarios

**Reglas de Negocio**:
1. Solo SystemAdmin puede invitar usuarios desde Admin Panel
2. El propietario del tenant puede invitar desde Configuración (limitado)
3. Verificar límites del plan antes de invitar
4. Un usuario puede pertenecer a múltiples tenants con diferentes roles

---

## 📊 NIVEL 3: DASHBOARD (Fases 3.1 - 3.4)

> **Objetivo**: Corregir páginas rotas y simplificar configuración.

### Fase 3.1: Corregir Páginas en Blanco

**Páginas Afectadas**:
- `/prestamos` → `PrestamosPage.jsx`
- `/deducciones` → `DeduccionesPage.jsx`
- `/anticipos` → `AnticiposPage.jsx`
- `/horas-extra` → `HorasExtraPage.jsx`
- `/ausencias` → `AusenciasPage.jsx`
- `/vacaciones` → `VacacionesPage.jsx`

**Diagnóstico**:
1. ¿El componente existe y está exportado correctamente?
2. ¿La ruta está registrada en App.jsx?
3. ¿El componente hace fetch a un endpoint que existe?
4. ¿Hay error en el componente que no se está mostrando?

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior React Developer con experiencia en debugging de SPAs.

## CONTEXTO
6 páginas del dashboard muestran contenido en blanco:
- PrestamosPage, DeduccionesPage, AnticiposPage
- HorasExtraPage, AusenciasPage, VacacionesPage

## TAREA EXACTA

### Paso 1: Verificar existencia de archivos
```bash
ls -la src/UI/Planilla.Web/ClientApp/src/pages/
```

### Paso 2: Para CADA página, verificar:

1. **Export default**:
```jsx
// ❌ Malo
export function PrestamosPage() {}

// ✅ Bueno
export default function PrestamosPage() {}
```

2. **Registro en rutas** (App.jsx):
```jsx
<Route path="/prestamos" element={<PrestamosPage />} />
```

3. **Import en App.jsx**:
```jsx
import PrestamosPage from './pages/PrestamosPage';
```

4. **Try-catch en fetch**:
```jsx
useEffect(() => {
  const loadData = async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/prestamos');
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      setPrestamos(data);
    } catch (error) {
      console.error('Error:', error);
      setError(error.message);
    } finally {
      setLoading(false);
    }
  };
  loadData();
}, []);
```

5. **Render condicional**:
```jsx
if (loading) return <div>Cargando...</div>;
if (error) return <div className="text-red-600">Error: {error}</div>;
if (!data || data.length === 0) return <div>No hay datos</div>;

return <Table data={data} />;
```

### Paso 3: Verificar endpoints backend

Para cada página, verificar que el endpoint existe:
- GET /api/prestamos
- GET /api/deducciones
- GET /api/anticipos
- GET /api/horasextra
- GET /api/ausencias
- GET /api/vacaciones

Si no existe, crear controller básico.

## FORMATO DE ENTREGA
1. Lista de archivos que existen/faltan
2. Diff de correcciones por archivo
3. Controllers faltantes con código básico
4. Checklist de verificación post-fix
```

---

### Fase 3.2: Eliminar "Uso del Plan" del Dashboard

**Ubicación**: Componente en el Dashboard principal

**Acción**: Eliminar completamente o mover a página de Billing dedicada

---

### Fase 3.3: Reestructurar Configuración del Tenant

**Estructura Actual** (incorrecta):
```
Configuración/
├── Empresa (❌ eliminar)
├── Usuarios (❌ mover a Admin Panel)
└── ???
```

**Estructura Nueva** (correcta):
```
Configuración/
├── Roles y Permisos
│   ├── Lista de roles (Owner puede crear nuevos)
│   ├── Botón "Nuevo Rol"
│   └── Para cada rol: botón "Permisos" → Modal de selección
└── Asignación de Roles
    ├── Lista de usuarios del tenant (solo lectura)
    └── Dropdown para asignar rol a cada usuario
```

**Nota Importante**: El propietario NO puede agregar usuarios manualmente, SOLO puede:
1. Crear roles personalizados
2. Definir permisos de cada rol
3. Asignar roles a usuarios existentes (los que el SystemAdmin invitó)

---

### Fase 3.4: Sistema de Roles Personalizados

**Modelo de Datos**:
```csharp
public class TenantRole
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } // "Gerente de RRHH", "Contador", etc.
    public bool IsSystem { get; set; } // true para Owner, Admin, Employee
    public DateTime CreatedAt { get; set; }
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } // "employees.read", "payroll.calculate", etc.
}
```

**Permisos Disponibles**:
```
employees.read          - Ver empleados
employees.create        - Crear empleados
employees.update        - Modificar empleados
employees.delete        - Eliminar empleados

departments.manage      - Gestionar departamentos
positions.manage        - Gestionar posiciones

payroll.view            - Ver planillas
payroll.calculate       - Calcular planillas
payroll.approve         - Aprobar planillas

loans.manage            - Gestionar préstamos
deductions.manage       - Gestionar deducciones
overtime.manage         - Gestionar horas extra
absences.manage         - Gestionar ausencias
vacations.manage        - Gestionar vacaciones

reports.view            - Ver reportes
reports.export          - Exportar reportes

settings.roles          - Gestionar roles (solo Owner)
```

**Prompt para Claude Code CLI**:
```
═══════════════════════════════════════════════════════════════
🎯 PROMPT PARA: Claude Code CLI
═══════════════════════════════════════════════════════════════

## ROL
Actúa como Senior Full-Stack Developer especialista en RBAC (Role-Based Access Control).

## CONTEXTO
Sistema Vorluno Planilla necesita un sistema de roles personalizados donde:
- El Owner del tenant puede crear roles con nombre personalizado
- Puede asignar permisos granulares a cada rol
- Puede asignar roles a usuarios existentes
- NO puede agregar usuarios (solo el SystemAdmin puede)

## TAREA EXACTA

### Backend

1. Crear entidades:
   - TenantRole (roles personalizados por tenant)
   - RolePermission (permisos asignados a cada rol)

2. Crear endpoints:
   - GET /api/tenants/me/roles - Listar roles del tenant
   - POST /api/tenants/me/roles - Crear rol
   - PUT /api/tenants/me/roles/{id} - Modificar rol
   - DELETE /api/tenants/me/roles/{id} - Eliminar rol (si no es system)
   - GET /api/tenants/me/roles/{id}/permissions - Obtener permisos
   - PUT /api/tenants/me/roles/{id}/permissions - Actualizar permisos
   - PUT /api/tenants/me/users/{userId}/role - Asignar rol a usuario

3. Crear middleware de autorización:
```csharp
[RequirePermission("employees.create")]
public async Task<ActionResult> CreateEmployee(...)
```

### Frontend

1. Crear RolesPage.tsx en Configuración:
   - Lista de roles con acciones
   - Modal para crear/editar rol
   - Modal de permisos con checkboxes agrupados

2. Crear RolePermissionsModal.tsx:
   - Permisos agrupados por módulo (Empleados, Planilla, etc.)
   - Checkboxes para cada permiso
   - Botón guardar

## FORMATO DE ENTREGA
1. Entidades con migraciones
2. Servicios con lógica de negocio
3. Controllers con autorización
4. Componentes React completos
5. Seeds para roles del sistema (Owner, Admin, Employee)
```

---

## 🎁 NIVEL 4: OPCIONALES (Fases 4.1 - 4.3)

> **Objetivo**: Mejoras no críticas que pueden hacerse después.

### Fase 4.1: Mejorar UX del Selector de Empresa

- Agregar logo/avatar de empresa
- Mostrar última actividad
- Recordar última empresa seleccionada

### Fase 4.2: Dashboard de Actividad Reciente

- Widget con últimas acciones del usuario
- Accesos directos a tareas pendientes

### Fase 4.3: Notificaciones de Invitación

- Email al usuario invitado
- Notificación in-app cuando es aceptada

---

## ✅ CHECKLIST MAESTRO DE VALIDACIÓN

### Después de Fase 1 (Infraestructura)
- [ ] `curl /api/health` retorna JSON
- [ ] Login con usuario de 1 tenant → va directo a dashboard
- [ ] Login con usuario de 2+ tenants → muestra selector
- [ ] Al crear tenant, owner aparece correctamente
- [ ] Endpoints de gestión de usuarios funcionan

### Después de Fase 2 (Admin Panel)
- [ ] TenantDetailsPage tiene tab de Usuarios
- [ ] Se puede invitar usuario desde Admin Panel
- [ ] Se puede cambiar rol/estado de usuario
- [ ] Audit Log muestra acciones del tenant

### Después de Fase 3 (Dashboard)
- [ ] Todas las páginas cargan sin error
- [ ] No hay "Uso del Plan" en dashboard
- [ ] Configuración solo muestra Roles y Permisos
- [ ] Owner puede crear roles y asignar permisos

### Después de Fase 4 (Opcionales)
- [ ] Selector de empresa tiene UX mejorada
- [ ] Dashboard muestra actividad reciente

---

## 📂 ARCHIVOS CLAVE A REVISAR

```
📁 BACKEND
├── Program.cs                           → Middleware pipeline
├── Controllers/AuthController.cs        → Login/Logout/SelectTenant
├── Controllers/Admin/TenantsController  → CRUD tenants
├── Controllers/Admin/TenantUsersController → Gestión usuarios (crear)
├── Services/TenantService.cs            → Lógica de tenants
├── Services/AuthService.cs              → Autenticación
└── Entities/
    ├── Tenant.cs                        → Debe tener OwnerId
    ├── TenantUser.cs                    → Relación usuario-tenant
    └── TenantRole.cs                    → Roles personalizados (crear)

📁 FRONTEND
├── App.jsx                              → Rutas
├── layouts/AuthLayout.tsx               → Menú del tenant (limpiar)
├── contexts/AuthContext.jsx             → Estado de auth
├── pages/
│   ├── LoginPage.jsx                    → Flujo de login
│   ├── TenantSelectorPage.jsx           → Selector de empresa (crear)
│   ├── admin/TenantDetailsPage.tsx      → Agregar gestión usuarios
│   └── config/RolesPage.tsx             → Sistema de roles (crear)
└── services/
    ├── api.js                           → Cliente HTTP
    └── tenantService.js                 → Llamadas a tenant API
```

---

## 🚀 ORDEN DE EJECUCIÓN RECOMENDADO

1. **Día 1-2**: Fase 1.1 (API Errors) + Fase 1.2 (Login Flow)
2. **Día 3**: Fase 1.3 (Endpoints Users) + Fase 1.4 (Owner Fix)
3. **Día 4**: Fase 2.1 (Mover Users al Admin)
4. **Día 5**: Fase 3.1 (Páginas en blanco) + Fase 3.2 (Cleanup Dashboard)
5. **Día 6-7**: Fase 3.3 + 3.4 (Sistema de Roles)
6. **Semana 2**: Fases opcionales

---

**Nota Final**: Este plan asume que tienes acceso al código fuente. Cada fase incluye un prompt listo para usar con Claude Code CLI. Ejecuta las fases en orden para evitar dependencias rotas.
