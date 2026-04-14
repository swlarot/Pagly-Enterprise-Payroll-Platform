# Pagly — Overview del proyecto

> Ficha global del repositorio Planilla (producto **Pagly**, empresa **Vorluno**).
> Generada el **2026-04-14** a partir de inspección directa del código fuente. Si encuentras desajustes con el código actual, actualiza este archivo.

---

## 1. Qué es Pagly

**Pagly** es un SaaS multi-tenant de **gestión de planilla (nómina) para Panamá y LATAM**. Automatiza cálculos regulatorios de Panamá (CSS, Seguro Educativo, ISR, décimo tercer mes, vacaciones, liquidaciones) y gestiona todo el ciclo de una nómina empresarial: empleados, departamentos, posiciones, préstamos, ausencias, horas extra, reportes y recibos de pago.

Incluye además una **API Platform B2B** (rate-limited, con API keys) para que integradores externos consuman el motor de cálculo de nómina panameña.

- **Empresa:** Vorluno — Panama City, Panama — [vorluno.dev](https://vorluno.dev)
- **Contacto:** contacto@vorluno.dev
- **Tagline Vorluno:** *Where code meets craft.*
- **Hermanos de producto:** CLAU (KYC), Core360 (ERP).

---

## 2. URLs y accesos

| Recurso                 | URL / ubicación                                                                                |
|-------------------------|------------------------------------------------------------------------------------------------|
| **Repositorio**         | `C:\Planilla` (branch principal: `master`)                                                     |
| **Backend local**       | `http://localhost:5039` (Swagger en `/swagger`)                                                |
| **Frontend local**      | `http://localhost:5173` (Vite dev server)                                                      |
| **DB local**            | PostgreSQL `localhost:5432` → `PlanillaDB` (ver `src/UI/Planilla.Web/appsettings.json:3`)      |
| **Deploy producción**   | CapRover sobre DigitalOcean (push a `master` → build automático)                               |
| **Health check**        | `GET /health` (PostgreSQL + MultiTenant)                                                       |
| **Linear team**         | `DEV` — team ID `9fa831fd-3c46-4efe-89b7-58bf01c2e2d4` (prefijo de commits: `DEV-#`)           |
| **Stripe**              | Configurado vía `Stripe:*` en `appsettings.json` — publishable/secret/webhook secret/prices    |
| **Email provider**      | Brevo (`sib_api_v3_sdk` 4.0.2) — `BrevoEmailService.cs`                                        |

> La URL pública de producción vive en el panel de CapRover — no está hardcoded en el repo.

---

## 3. Stack técnico real

### Backend
- **.NET 9** (`<TargetFramework>net9.0</TargetFramework>` en los 4 `.csproj` bajo `src/`)
- **ASP.NET Core Web API** + **ASP.NET Core Identity** (EF Core + JWT Bearer)
- **Entity Framework Core 9.0.2** con **Npgsql** (PostgreSQL provider)
- **AutoMapper 12.0.1** para DTOs ↔ entidades
- **Swashbuckle 9.0.1** (Swagger/OpenAPI)
- **QuestPDF 2024.3.0** para PDFs (recibos, liquidaciones) — requiere fuentes `fontconfig + fonts-liberation` en el container
- **ClosedXML 0.102.0** para exportaciones Excel
- **Stripe.net 50.1.0** para billing y webhooks
- **Brevo (`sib_api_v3_sdk` 4.0.2)** para envío de emails transaccionales

### Frontend
- **React 19.1** + **Vite 7** + **TypeScript 5.9**
- **React Router v7** (rutas SPA)
- **Tailwind CSS 3.4**
- **Recharts** (dashboards)
- **Lucide React** (iconos)
- **react-hot-toast** (notificaciones)
- **Context API** para estado global (sin Redux/Zustand)
- Output del build: `src/UI/Planilla.Web/wwwroot/` (servido por el backend en producción)

### Infraestructura
- **Docker multi-stage** (Node 20-alpine → dotnet/sdk:9.0 → dotnet/aspnet:9.0)
- **CapRover** + **DigitalOcean** (deploy por push a `master`)
- **PostgreSQL 16** (local y prod)
- **Puerto expuesto runtime:** 80

---

## 4. Arquitectura (Clean Architecture)

```
src/
├─ Core/
│  ├─ Planilla.Domain/         → Entidades, enums, interfaces de dominio, reglas de negocio
│  └─ Planilla.Application/    → DTOs, services portables (cálculos), orquestación
├─ Infrastructure/
│  └─ Planilla.Infrastructure/ → DbContext, repos, EF migrations, Stripe, Brevo, seeders
└─ UI/
   └─ Planilla.Web/            → Controllers + SPA React en ClientApp/
tests/
├─ Planilla.Application.Tests/    → xUnit (Application layer)
└─ Planilla.Web.IntegrationTests/ → Integration tests de API
```

**Flujo de dependencias:** `Web → Application + Infrastructure + Domain` · `Infrastructure → Application + Domain` · `Application → Domain` · `Domain → ∅`.

**Nombres reales de los `.csproj`** (usados por el Dockerfile):
- `Vorluno.Planilla.Domain.csproj`
- `Vorluno.Planilla.Application.csproj`
- `Vorluno.Planilla.Infrastructure.csproj`
- `Vorluno.Planilla.Web.csproj` (proyecto de arranque)

---

## 5. Modelo de dominio (resumen)

37 entidades en `src/Core/Planilla.Domain/Entities/`. Las más importantes:

| Grupo                  | Entidades clave                                                                                         |
|------------------------|---------------------------------------------------------------------------------------------------------|
| **Multi-tenant & auth** | `Tenant`, `AppUser` (IdentityUser), `TenantUser`, `CustomTenantRole`, `RolePermission`, `TenantInvitation`, `RefreshToken`, `AuditLogEntry` |
| **Billing**            | `Subscription`, `StripeWebhookEvent`, `ApiKey`                                                          |
| **Organización**       | `Empleado`, `Departamento`, `Posicion`, `SaldoInicialEmpleado`                                          |
| **Nómina**             | `PayrollHeader`, `PayrollDetail`, `PayrollEmployeeHours`, `PayrollTaxConfiguration`, `TaxBracket`, `ReciboDeSueldo`, `PlanillaDecimo` |
| **Conceptos**          | `HoraExtra`, `Ausencia`, `SolicitudVacaciones`, `SaldoVacaciones`, `Prestamo`, `PagoPrestamo`, `Anticipo`, `DeduccionFija` |
| **Liquidación / ops**  | `Liquidacion`, `DetalleDecimo`, `DeduccionAplicada`, `Acreedor`                                         |
| **API platform**       | `IdempotencyRecord` (24h TTL), `QuotaAlertSent`                                                         |

Enums relevantes en `Planilla.Domain/Enums/`:
`TenantRole`, `SystemPermission`, `SubscriptionPlan`, `SubscriptionStatus`, `PayrollStatus`, `PayPeriodType`, `TipoPlanilla`, `TipoHoraExtra`, `TipoAusencia`, `EstadoDecimo`, `EstadoLiquidacion`, `EstadoOrdenJudicial`, `EstadoPrestamo`, `EstadoVacaciones`, etc.

---

## 6. Sistema de roles y permisos (modelo actual)

> ⚠️ El esquema viejo `Owner > Admin > Manager > Accountant > Employee` **ya no aplica**. El sistema ahora usa roles custom por tenant con permisos granulares.

### Estructura

1. **Rol de sistema (`TenantRole` enum):**
   - `Owner (0)` → acceso total dentro del tenant.
   - `User (1)` → acceso definido por el `CustomTenantRole` asignado.

2. **Roles personalizados (`CustomTenantRole`)** — creados por el Owner desde UI. Campos: `TenantId`, `Name`, `Description`, `IsSystem`, `Color`, `DisplayOrder`. Ej: "Gerente RRHH", "Contador Senior".

3. **Permisos granulares (`SystemPermission`):** 29 strings en `src/Core/Planilla.Domain/Enums/SystemPermission.cs`. Se asignan vía `RolePermission` a cada `CustomTenantRole`.

### Categorías de permisos

| Categoría        | Ejemplos                                                                              |
|------------------|---------------------------------------------------------------------------------------|
| Empleados        | `employees.read`, `employees.create`, `employees.update`, `employees.delete`         |
| Auto-servicio    | `employee.view_self`, `payroll.view_self`, `vacations.request_self`, ...             |
| Estructura       | `departments.manage`, `positions.manage`                                              |
| Nómina           | `payroll.view`, `payroll.calculate`, `payroll.approve`, `payroll.delete`             |
| Conceptos        | `loans.manage`, `deductions.manage`, `overtime.manage`, `absences.manage`, ...       |
| Reportes         | `reports.view`, `reports.export`                                                      |
| Config           | `settings.taxes`, `settings.roles`, `settings.users`, `settings.billing`             |
| Auditoría        | `audit.view`, `dashboard.view`                                                        |

### Verificación

- **Backend:** atributos `[Authorize(Roles="Owner")]`, policies `RequireOwner` / `TenantManageUsers` / `RequireSystemAdmin` (ver `Program.cs:115-124`), y servicios como `ICustomTenantRoleService` para validar permisos por string.
- **Frontend:** `useAuth()` (`ClientApp/src/contexts/AuthContext.tsx`) expone `hasPermission(p)`, `hasRole(...)`, `canWrite()`, `canDelete()`, `isReadOnly()`, `canAccessFeature()`.

---

## 7. Multi-tenancy

- **Resolución del tenant actual:** `TenantContext` (`src/Infrastructure/Planilla.Infrastructure/Services/TenantContext.cs`) implementa `ITenantContext`. Lee claims del JWT: `tenant_id`, `tenant_role`, `is_system_admin`, `sub`, `email`.
- **Global query filters** en `ApplicationDbContext` filtran automáticamente por `TenantId` toda entidad que implemente `ITenantEntity` — seguridad crítica contra fugas cross-tenant.
- **Sin header custom:** el `TenantId` viaja dentro del JWT, no en un header aparte.

---

## 8. Planes y facturación

Definidos en `src/Core/Planilla.Domain/Models/PlanFeatures.cs:GetLimits()`.

| Plan           | Empleados | Usuarios | Empresas | Excel | PDF | API   | Email | Auditoría | Retención  | Precio/mes | API req/mes |
|----------------|-----------|----------|----------|-------|-----|-------|-------|-----------|------------|------------|-------------|
| **Free**       | 5         | 1        | 1        | ❌    | ❌  | ❌    | ❌    | ❌        | 90 días    | $0         | 0           |
| **Starter**    | 25        | 3        | 1        | ✅    | ❌  | ❌    | ✅    | ❌        | 365 días   | $29.99     | 0           |
| **Professional** | 100     | 10       | 3        | ✅    | ✅  | ✅    | ✅    | ✅        | 730 días   | $79.99     | 10,000      |
| **Enterprise** | ∞         | ∞        | ∞        | ✅    | ✅  | ✅    | ✅    | ✅        | ∞          | $199.99    | 100,000     |

Stripe:
- `Price IDs` configurados en `appsettings.json` (`Stripe:PriceId*`).
- Webhook handler: `StripeWebhookController` — maneja `customer.subscription.updated`, `customer.subscription.deleted`, `payment_intent.succeeded`.
- `StripeBillingService` (`Infrastructure/Services/StripeBillingService.cs`) crea sesiones de checkout y sincroniza suscripciones.

---

## 9. Compliance Panamá (motor de cálculo)

Servicios portables en `src/Core/Planilla.Application/Services/`:

| Servicio                                    | Calcula                                                              |
|---------------------------------------------|----------------------------------------------------------------------|
| `CssCalculationServicePortable`             | CSS empleado + empleador (Ley 462), seguro de riesgo, topes          |
| `EducationalInsuranceServicePortable`       | Seguro Educativo (0.75% empleado + 0.75% empleador)                  |
| `IncomeTaxCalculationServicePortable`       | ISR con brackets + deducción por dependientes ($800 c/u)             |
| `PayrollCalculationOrchestratorPortable`    | Orquestador que coordina CSS + SE + ISR en el cálculo de nómina      |
| `OvertimeFactorService`                     | Factores de horas extra (nocturna, dominical, feriado)               |
| `PanamaHolidayService`                      | Feriados oficiales de Panamá                                         |
| `DecimoCalculationService`                  | Décimo tercer mes (3 pagos anuales)                                  |
| `LiquidacionCalculationService`             | Prima de antigüedad, vacaciones no pagadas, décimos prorrateados     |
| `AsistenciaCalculationService`              | Deducciones por ausencias / faltas                                   |

Brackets de ISR: seed desde `docs/seeds/seed_tax_brackets_2025.json` → tabla `TaxBracket`. Configuración por tenant vive en `PayrollTaxConfiguration`.

---

## 10. Controllers / endpoints

25 controllers en `src/UI/Planilla.Web/Controllers/`. Áreas:

- **Auth / usuarios:** `AuthController`, `CustomRolesController`, `TenantController`.
- **Organización:** `EmpleadosController`, `DepartamentosController`, `PosicionesController`.
- **Nómina:** `PayrollHeadersController`, `HorasExtraController`, `VacacionesController`, `AusenciasController`, `PrestamosController`, `DeduccionesController`, `AnticiposController`, `DecimoController`, `AcreedoresController`, `LiquidacionesController`.
- **Reportes:** `ReportesController`.
- **Billing:** `BillingController`, `SubscriptionController`, `StripeWebhookController`.
- **Configuración:** `ConfiguracionController`.
- **System admin:** `AdminController`, `SystemUsersController`, `SystemApiUsageController`.
- **API Platform B2B (v1):** `V1/CalculatorController` con `ApiKey` auth scheme y rate-limiting.
- **API keys:** `ApiKeysController`.

Swagger disponible en desarrollo en `/swagger`.

---

## 11. Autenticación

- **JWT Bearer** (ASP.NET Core Identity) — configurado en `Program.cs:78-105`.
- Claims emitidos: `sub`, `email`, `tenant_id`, `tenant_role`, `is_system_admin`, `nombre_completo`.
- **Expiry:** 24 h (configurable vía `Jwt:ExpireHours`).
- **Refresh tokens** persistidos en tabla `RefreshToken`.
- **Password policy** relajada (mínimo 8 caracteres, sin requerir especiales).
- **API Platform B2B** usa esquema aparte: `ApiKeyAuthenticationHandler` (`src/UI/Planilla.Web/Authentication/`) con hash SHA256 y rate-limit por minuto.
- **Frontend:** tokens en `localStorage` (`auth_token`, `refresh_token`). Interceptor en `ClientApp/src/services/api.ts` hace refresh automático al recibir 401.

### Endpoints clave
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/accept-invite`
- `GET /api/auth/me`
- `POST /api/auth/admin/create-tenant` *(solo system admin)*

**No hay auto-registro público** — los tenants se crean desde `/system-admin/tenants/create`.

---

## 12. Frontend (SPA React)

Estructura de `src/UI/Planilla.Web/ClientApp/src/`:

```
pages/           → una página = un archivo (LoginPage, DashboardPage, RolesPage, etc.)
components/
  ui/            → Button, Card, Input, Select, Badge, Modal (primitives propias)
  layout/        → AuthLayout, SystemAdminLayout, MainLayout
  auth/          → ProtectedRoute, RoleGuard, SystemAdminRoute
  admin/         → paneles admin
  charts/        → Recharts
  empleados/     → CRUD empleados
  roles/         → gestión de roles custom
  tenant/        → selector de tenant
contexts/        → AuthContext (estado global)
services/        → api.ts (base), authService, employeeService, roleService, permissionService, ...
types/           → api.ts con tipos compartidos
hooks/           → useAuth, etc.
utils/, constants/, assets/
App.tsx          → router root
main.tsx         → entry point
```

Rutas públicas: `/login`, `/accept-invite`. Después requieren tenant seleccionado.

---

## 13. Deploy y operaciones

### Pipeline (3 stages en `Dockerfile`)
1. `node:20-alpine` → `npm ci` + `npm run build` (Vite `outDir: '../wwwroot'`).
2. `dotnet/sdk:9.0` → `dotnet restore` + `dotnet publish` (copia wwwroot dentro del build).
3. `dotnet/aspnet:9.0` → runtime en puerto 80, usuario no-root `appuser`, fuentes para QuestPDF. `Program.cs` aplica migraciones (`MigrateAsync`) al arrancar.

### Variables de entorno (producción — CapRover)
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpireHours`
- `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PriceId*`, `Stripe__SuccessUrl`, `Stripe__CancelUrl`
- `Brevo__ApiKey`, `Brevo__SenderEmail`, `Brevo__SenderName`
- `Cors__AllowedOrigins` (lista separada por coma)
- `ApiRateLimit__PerMinute` (default 60)
- `ASPNETCORE_ENVIRONMENT=Production`

### Rollback
Panel CapRover → App → Deployment → deploy de versión anterior (~30 s).

### Checklist antes de push a `master`
- `package-lock.json` commiteado si agregaste paquetes npm
- Migraciones EF Core commiteadas
- Nuevos `.csproj` agregados a `Planilla.sln`
- Servicios nuevos registrados en `Program.cs`

---

## 14. Testing

| Proyecto                              | Framework | Ubicación                                  |
|---------------------------------------|-----------|--------------------------------------------|
| `Vorluno.Planilla.Application.Tests`  | xUnit     | `tests/Planilla.Application.Tests/`        |
| `Planilla.Web.IntegrationTests`       | xUnit     | `tests/Planilla.Web.IntegrationTests/`     |

Frontend: no hay Vitest/Jest configurado actualmente.

---

## 15. Flujo de trabajo (Linear + Git)

1. Crear ticket Linear en team `DEV` con plantilla (Bug / Feature / Tech Task) — **obligatorio antes de tocar código**.
2. Mover a `In Progress`.
3. Commit con prefijo `DEV-#:` (ej. `DEV-93: chore: reorganizar documentación`).
4. Mover a `Done`.

**Reglas de commit:**
- Un commit por ticket.
- Sin `Co-Authored-By: Claude...`.
- Los títulos en Linear **sin** el prefijo `DEV-#` (Linear lo agrega).

---

## 16. Contactos y responsables

- **Empresa:** Vorluno Software (Panamá, UTC-5)
- **Owner / mantenedor:** Jose (`swlarot` en Git)
- **Web:** [vorluno.dev](https://vorluno.dev) · **Email:** contacto@vorluno.dev

---

## 17. Dónde seguir leyendo

- Índice navegable de toda la documentación → [docs/README.md](./README.md)
- Runbook de incidentes de nómina → [docs/runbooks/payday-down.md](./runbooks/payday-down.md)
- Arranque local rápido → [docs/onboarding/INICIO-RAPIDO.md](./onboarding/INICIO-RAPIDO.md)
- Motor de cálculo → [docs/payroll/payroll-calculations.md](./payroll/payroll-calculations.md)
- Roles y permisos (implementación detallada) → [docs/roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md](./roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md)
- Deploy CapRover → [docs/deploy/DEPLOY-CAPROVER.md](./deploy/DEPLOY-CAPROVER.md)
- API Platform B2B → [docs/api-platform/README.md](./api-platform/README.md)
