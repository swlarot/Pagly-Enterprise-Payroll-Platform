# Planilla - Sistema de Gestión de Planilla Empresarial

## Visión del Proyecto

Planilla es un **SaaS de Nómina y Planilla** especializado para empresas en Panamá, con cumplimiento total de la Ley 462 de la CSS, regulaciones laborales panameñas, y capacidades multi-tenant enterprise-grade.

## Stack Tecnológico

- **Backend**: .NET 9, ASP.NET Core Web API, Entity Framework Core
- **Frontend**: React 19 con Vite, Tailwind CSS, Lucide Icons
- **Base de Datos**: PostgreSQL 16+
- **Autenticación**: ASP.NET Core Identity + JWT Bearer Tokens
- **Pagos**: Stripe (suscripciones)
- **Arquitectura**: Clean Architecture (Domain/Application/Infrastructure/Web)
- **Hosting**: CapRover + DigitalOcean

## Arquitectura Multi-Tenant

```
┌─────────────────────────────────────────────────────────────┐
│                         Planilla SaaS                           │
├─────────────────────────────────────────────────────────────┤
│  Tenant A (Empresa ABC)    │  Tenant B (Empresa XYZ)        │
│  ┌───────────────────┐     │  ┌───────────────────┐         │
│  │ Empleados         │     │  │ Empleados         │         │
│  │ Planillas         │     │  │ Planillas         │         │
│  │ Reportes          │     │  │ Reportes          │         │
│  │ Configuración     │     │  │ Configuración     │         │
│  └───────────────────┘     │  └───────────────────┘         │
├─────────────────────────────────────────────────────────────┤
│                    Shared Infrastructure                     │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────────────┐    │
│  │ Auth    │ │ Billing │ │ Admin   │ │ Feature Flags   │    │
│  │ Service │ │ Service │ │ Portal  │ │ & Subscriptions │    │
│  └─────────┘ └─────────┘ └─────────┘ └─────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Estructura de Carpetas

```
src/
├── Core/
│   ├── Planilla.Domain/           # Entidades, Enums, Interfaces base
│   │   ├── Entities/
│   │   │   ├── Company.cs
│   │   │   ├── Tenant.cs
│   │   │   ├── Subscription.cs
│   │   │   ├── User.cs
│   │   │   ├── Employee.cs
│   │   │   ├── PayrollHeader.cs
│   │   │   └── ...
│   │   ├── Enums/
│   │   └── Interfaces/
│   │
│   └── Planilla.Application/      # DTOs, Services Interfaces, Use Cases
│       ├── DTOs/
│       ├── Interfaces/
│       └── Services/
│
├── Infrastructure/
│   └── Planilla.Infrastructure/   # EF Core, Repositorios, Servicios externos
│       ├── Data/
│       │   ├── ApplicationDbContext.cs
│       │   └── Configurations/
│       ├── Repositories/
│       ├── Services/
│       │   ├── StripeService.cs
│       │   ├── EmailService.cs
│       │   └── ...
│       └── Identity/
│
└── UI/
    └── Planilla.Web/              # API + React SPA
        ├── Controllers/
        ├── ClientApp/             # React 19 + Vite
        │   ├── src/
        │   │   ├── components/
        │   │   ├── pages/
        │   │   ├── hooks/
        │   │   ├── services/
        │   │   └── contexts/
        │   └── ...
        └── ...
```

## Modelo de Datos SaaS

### Entidades Core (Multi-Tenant)

```csharp
// Tenant/Company - El inquilino principal
public class Tenant : BaseEntity
{
    public string Name { get; set; }
    public string Subdomain { get; set; }  // empresa.Planilla.cloud
    public string RUC { get; set; }
    public string DV { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Relaciones
    public int? SubscriptionId { get; set; }
    public Subscription Subscription { get; set; }
    public ICollection<Company> Companies { get; set; }
    public ICollection<TenantUser> Users { get; set; }
}

// Subscription - Plan de suscripción
public class Subscription : BaseEntity
{
    public int TenantId { get; set; }
    public SubscriptionPlan Plan { get; set; }  // Free, Starter, Professional, Enterprise
    public SubscriptionStatus Status { get; set; }  // Active, Canceled, PastDue, Trialing
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public string StripeCustomerId { get; set; }
    public string StripeSubscriptionId { get; set; }
    public decimal MonthlyPrice { get; set; }
    public int MaxEmployees { get; set; }
    public int MaxUsers { get; set; }
    public bool CanExportReports { get; set; }
    public bool HasApiAccess { get; set; }
    public bool HasPrioritySupport { get; set; }
}

// Planes de suscripción
public enum SubscriptionPlan
{
    Free = 0,           // 5 empleados, 1 usuario, reportes básicos
    Starter = 1,        // 25 empleados, 3 usuarios, Excel export
    Professional = 2,   // 100 empleados, 10 usuarios, PDF + Excel, API
    Enterprise = 3      // Ilimitado, usuarios ilimitados, soporte prioritario
}

// Usuario del Tenant
public class TenantUser : BaseEntity
{
    public int TenantId { get; set; }
    public string UserId { get; set; }  // ASP.NET Identity User ID
    public TenantRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    public Tenant Tenant { get; set; }
    public ApplicationUser User { get; set; }
}

// Roles dentro del Tenant
public enum TenantRole
{
    Owner = 0,          // Propietario - acceso total, puede eliminar tenant
    Admin = 1,          // Administrador - gestión completa excepto billing
    Manager = 2,        // Gerente - planillas, empleados, reportes
    Accountant = 3,     // Contador - solo reportes y consultas
    Employee = 4        // Empleado - solo ver su información
}
```

### Feature Flags por Plan

```csharp
public class PlanFeatures
{
    public static Dictionary<SubscriptionPlan, PlanLimits> Limits = new()
    {
        [SubscriptionPlan.Free] = new PlanLimits
        {
            MaxEmployees = 5,
            MaxUsers = 1,
            MaxCompanies = 1,
            CanExportExcel = false,
            CanExportPdf = false,
            CanUseApi = false,
            HasEmailNotifications = false,
            HasAuditLog = false,
            RetentionDays = 90,
            PricePerMonth = 0
        },
        [SubscriptionPlan.Starter] = new PlanLimits
        {
            MaxEmployees = 25,
            MaxUsers = 3,
            MaxCompanies = 1,
            CanExportExcel = true,
            CanExportPdf = false,
            CanUseApi = false,
            HasEmailNotifications = true,
            HasAuditLog = false,
            RetentionDays = 365,
            PricePerMonth = 29.99m
        },
        [SubscriptionPlan.Professional] = new PlanLimits
        {
            MaxEmployees = 100,
            MaxUsers = 10,
            MaxCompanies = 3,
            CanExportExcel = true,
            CanExportPdf = true,
            CanUseApi = true,
            HasEmailNotifications = true,
            HasAuditLog = true,
            RetentionDays = 730,  // 2 años
            PricePerMonth = 79.99m
        },
        [SubscriptionPlan.Enterprise] = new PlanLimits
        {
            MaxEmployees = int.MaxValue,
            MaxUsers = int.MaxValue,
            MaxCompanies = int.MaxValue,
            CanExportExcel = true,
            CanExportPdf = true,
            CanUseApi = true,
            HasEmailNotifications = true,
            HasAuditLog = true,
            RetentionDays = int.MaxValue,
            PricePerMonth = 199.99m  // O precio personalizado
        }
    };
}
```

## Patrones Obligatorios

### 1. Filtrado por Tenant (CRÍTICO)

**TODAS** las queries deben filtrar por TenantId:

```csharp
// En Repositories - SIEMPRE filtrar por TenantId
public async Task<List<Employee>> GetAllAsync()
{
    var tenantId = _currentTenantService.TenantId;
    return await _context.Employees
        .Where(e => e.TenantId == tenantId)
        .ToListAsync();
}

// En Controllers - obtener TenantId del token JWT
protected int GetCurrentTenantId()
{
    var claim = User.FindFirst("tenant_id");
    return int.Parse(claim?.Value ?? "0");
}
```

### 2. Verificación de Límites del Plan

```csharp
// Antes de crear empleados, verificar límite
public async Task<ActionResponse<Employee>> CreateEmployeeAsync(CreateEmployeeDto dto)
{
    var tenant = await _tenantService.GetCurrentTenantAsync();
    var currentCount = await _employeeRepo.CountAsync();
    var limit = PlanFeatures.Limits[tenant.Subscription.Plan].MaxEmployees;
    
    if (currentCount >= limit)
    {
        return ActionResponse<Employee>.Failure(
            $"Has alcanzado el límite de {limit} empleados en tu plan. Actualiza a un plan superior.");
    }
    
    // Crear empleado...
}
```

### 3. Autorización por Rol

```csharp
[Authorize(Roles = "Owner,Admin")]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteEmployee(int id) { }

[Authorize(Roles = "Owner,Admin,Manager")]
[HttpPost("calculate")]
public async Task<IActionResult> CalculatePayroll(int id) { }

[Authorize(Roles = "Owner,Admin,Manager,Accountant")]
[HttpGet("reports")]
public async Task<IActionResult> GetReports() { }
```

## Flujo de Onboarding

```
1. Usuario se registra (email + password)
   ↓
2. Nosotros creamos su empresa desde admin (nombre empresa, RUC)
   ↓
3. Se asigna plan Free automáticamente (14 días trial de Professional)
   ↓
4. Configura su empresa (tasas CSS, SE, ISR)
   ↓
5. Agrega empleados (hasta el límite del plan)
   ↓
6. Crea primera planilla
   ↓
7. Al terminar trial, decide: quedarse en Free o upgrade
```

## Integración Stripe

```csharp
// Webhook endpoints para Stripe
POST /api/webhooks/stripe

Eventos a manejar:
- customer.subscription.created
- customer.subscription.updated
- customer.subscription.deleted
- invoice.paid
- invoice.payment_failed
- customer.subscription.trial_will_end
```

## Endpoints API Principales

### Auth & Tenant
```
POST   /api/auth/register          # Registrar usuario + crear tenant
POST   /api/auth/login             # Login (devuelve JWT con tenant_id)
POST   /api/auth/refresh           # Refresh token
GET    /api/tenant                 # Info del tenant actual
PUT    /api/tenant                 # Actualizar tenant
POST   /api/tenant/invite          # Invitar usuario al tenant
DELETE /api/tenant/users/{id}      # Remover usuario del tenant
```

### Subscription
```
GET    /api/subscription           # Plan actual y uso
POST   /api/subscription/upgrade   # Upgrade plan (redirect a Stripe)
POST   /api/subscription/cancel    # Cancelar suscripción
GET    /api/subscription/invoices  # Historial de facturas
```

### Planilla (existentes + multi-tenant)
```
GET    /api/employees              # Filtrado por tenant automático
POST   /api/employees
GET    /api/payrollheaders
POST   /api/payrollheaders
POST   /api/payrollheaders/{id}/calculate
POST   /api/payrollheaders/{id}/approve
GET    /api/reports/css/{id}
GET    /api/reports/css/{id}/excel  # Solo si plan permite
GET    /api/reports/css/{id}/pdf    # Solo si plan permite
```

## Seguridad

### JWT Claims
```json
{
  "sub": "user-guid",
  "email": "usuario@empresa.com",
  "tenant_id": "123",
  "tenant_role": "Admin",
  "plan": "Professional",
  "exp": 1234567890
}
```

### Middleware de Tenant
```csharp
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantContext = context.RequestServices.GetRequiredService<ITenantContext>();
            await tenantContext.SetTenantAsync(int.Parse(tenantId));
        }
        
        await _next(context);
    }
}
```

## Testing

- **Unit Tests**: xUnit + Moq para servicios de dominio
- **Integration Tests**: WebApplicationFactory para API endpoints
- **Multi-tenant Tests**: Verificar aislamiento de datos entre tenants

## Convenciones de Código

### Backend

1. **Nunca** exponer entities directamente - usar DTOs
2. **Siempre** validar TenantId en operaciones de escritura
3. **Siempre** verificar límites del plan antes de crear recursos
4. **Usar** ActionResponse<T> para todas las respuestas de servicios
5. **Registrar** todos los servicios en Program.cs con el lifecycle correcto
6. **Auditar** operaciones críticas (cambios en planilla, eliminaciones)

### Frontend

**VER FRONTEND-RULES.md PARA REGLAS DETALLADAS**

#### Reglas Críticas de Frontend React

1. **SIEMPRE** exportar páginas con `export default function NombrePage()`
2. **SIEMPRE** registrar rutas en `App.tsx` (import + route)
3. **SIEMPRE** usar componentes UI de `components/ui/` antes de crear nuevos
4. **SIEMPRE** manejar estados de loading, error y empty
5. **SIEMPRE** usar `toast` para mensajes al usuario
6. **SIEMPRE** validar que la página carga en el navegador (no blanco)

#### Checklist al crear página React:
- [ ] Crear archivo en `src/pages/` con nombre PascalCase + Page
- [ ] Export default function
- [ ] Import en App.tsx
- [ ] Route en App.tsx con protección adecuada
- [ ] Layout aplicado (AuthLayout o SystemAdminLayout)
- [ ] npm run build exitoso
- [ ] Página funciona en navegador

#### Componentes UI Disponibles:
- `Button`, `Card`, `Input`, `Select`, `Badge`, `Modal` → `components/ui/`
- `AuthLayout`, `SystemAdminLayout` → `components/layout/`
- `ProtectedRoute`, `RoleGuard`, `SystemAdminRoute` → `components/auth/`

#### Estructura de página típica:
```tsx
import React, { useEffect, useState } from 'react';
import { Card, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

export default function MiPaginaPage() {
  const [data, setData] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => { loadData(); }, []);

  const loadData = async () => {
    try {
      setIsLoading(true);
      // API call
    } catch (error: any) {
      toast.error(error.message || 'Error');
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) return <Loader2 className="animate-spin" />;

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-900">Mi Página</h1>
      <Card><CardBody>{/* Contenido */}</CardBody></Card>
    </div>
  );
}
```

## Agentes Disponibles

El proyecto cuenta con agentes especializados en `/.claude/skills/user/`:

1. **Planilla-backend-architect** - Arquitectura backend, APIs, servicios
2. **Planilla-frontend-specialist** - React, UI/UX, componentes
3. **Planilla-payroll-architect** - Cálculos de planilla, leyes panameñas
4. **Planilla-functional-architect** - Procesos de negocio, flujos
5. **Planilla-docs-generator** - Documentación técnica y de usuario
6. **Planilla-uxui-designer** - Diseño de interfaces, sistema visual
7. **Planilla-ai-specialist** - Inteligencia artificial, predicciones
8. **Planilla-mobile-developer** - App móvil MAUI (futuro)

## Comandos Útiles

```bash
# Migraciones
dotnet ef migrations add NombreMigracion --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web

# Aplicar migraciones
dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web

# Ejecutar proyecto
dotnet run --project src/UI/Planilla.Web

# Frontend dev
cd src/UI/Planilla.Web/ClientApp && npm run dev
```

## Prioridades de Desarrollo

### Fase 1: Multi-Tenancy Base ✅ COMPLETADA
- [x] Entidades Tenant, Subscription, TenantUser
- [x] TenantMiddleware y filtrado automático
- [x] Migraciones de base de datos
- [x] Registro y login con creación de tenant

### Fase 2: Suscripciones ✅ COMPLETADA
- [x] Integración Stripe
- [x] Webhooks de pago
- [x] Portal de billing
- [x] Límites por plan

### Fase 3: Roles y Permisos ✅ COMPLETADA
- [x] Sistema de roles en tenant
- [x] Invitación de usuarios
- [x] Permisos granulares
- [x] Audit log

### Fase 4: Portal Admin
- [ ] Dashboard de métricas SaaS
- [ ] Gestión de tenants
- [ ] Reportes de uso
- [ ] Soporte integrado

---

## Deploy — CapRover + DigitalOcean

### Cómo funciona (LEER ANTES DE HACER PUSH)

**Todo push a `master` dispara un deploy automático en producción** via GitHub webhook → CapRover.

```
git push origin master  →  CapRover build Docker  →  Contenedor en prod
```

No se usa `caprover deploy` manualmente. El deploy es automático al pushear.

### Pipeline Dockerfile (3 stages)

```
Stage 1 (node:20-alpine):   npm ci + npm run build
                            vite outDir: '../wwwroot' → /app/wwwroot/
Stage 2 (dotnet/sdk:9.0):   dotnet restore + dotnet publish --no-restore
                            copia /app/wwwroot → src/UI/Planilla.Web/wwwroot
Stage 3 (dotnet/aspnet:9.0): runtime en puerto 80
                            startup: Program.cs ejecuta MigrateAsync() automáticamente
```

### Archivos CRÍTICOS — no modificar sin entender el impacto

| Archivo | Qué rompe si se cambia mal |
|---------|---------------------------|
| `Dockerfile` | Todo el build |
| `captain-definition` | CapRover no sabe qué desplegar |
| `vite.config.js` → `outDir: '../wwwroot'` | Frontend no llega al contenedor |
| `Vorluno.Planilla.Web.csproj` (nombre) | `dotnet publish` del Dockerfile falla |
| `Planilla.sln` | `dotnet restore` no encuentra proyectos nuevos |
| `package-lock.json` | `npm ci` falla si no está commiteado |

### Reglas obligatorias antes de hacer push

1. **Si agregaste dependencias npm**: commitear `package-lock.json` (Dockerfile usa `npm ci`, no `npm install`)
2. **Si creaste nuevas migraciones EF Core**: deben estar en `Migrations/` y commiteadas — se aplican al arrancar en producción
3. **Si creaste un nuevo proyecto .csproj**: agregarlo al `Planilla.sln` o dotnet restore falla en Docker
4. **Si registraste nuevos servicios**: verificar que estén en `Program.cs` o la app crashea al arrancar
5. **NUNCA** hardcodear connection strings, JWT keys o API keys en código — van en variables de entorno de CapRover
6. **NUNCA** cambiar `outDir` en `vite.config.js` sin actualizar el Dockerfile simultáneamente

### Verificación post-deploy

```
GET /health      → {"status":"Healthy",...}   (PostgreSQL + MultiTenant checks)
GET /api/health  → {"status":"healthy",...}   (check rápido)
```

Buscar en logs de CapRover: `"Migraciones aplicadas correctamente"`

### Rollback

Panel CapRover → App → Deployment tab → click Deploy en versión anterior (~30 segundos).
O: `git revert HEAD && git push origin master`

### Variables de entorno en CapRover (App Config)

```
ConnectionStrings__DefaultConnection
Jwt__Key  /  Jwt__Issuer  /  Jwt__Audience
ASPNETCORE_ENVIRONMENT = Production
Stripe__PublishableKey / Stripe__SecretKey / Stripe__WebhookSecret  (opcional)
```

> Para diagnóstico detallado de fallos de deploy, usar el skill `/deploy-caprover`.

---

**IMPORTANTE**: Este documento es la fuente de verdad para el proyecto Planilla. Todos los agentes deben seguir estas convenciones y patrones.
