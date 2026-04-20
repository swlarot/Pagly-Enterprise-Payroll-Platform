<div align="center">

<img src="https://raw.githubusercontent.com/vorluno/vorluno/main/BANNER-GITHUB.png" alt="Vorluno" width="100%">

# 💼 Pagly — Vorluno Planilla

### SaaS multi-tenant de gestión de nómina para Panamá

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![EF Core](https://img.shields.io/badge/EF_Core-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://docs.microsoft.com/en-us/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-CapRover-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://caprover.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)](#-licencia)

**Cumplimiento total con la legislación laboral panameña (CSS Ley 462, Seguro Educativo, ISR, décimo, liquidaciones)**

[Documentación](./docs/README.md) · [Overview del proyecto](./docs/OVERVIEW.md) · [Arranque local](./docs/onboarding/INICIO-RAPIDO.md) · [API Platform B2B](./docs/api-platform/README.md)

</div>

---

## 📋 Resumen

**Pagly** (nombre de código interno: `Vorluno.Planilla`) es el producto SaaS de nómina de [Vorluno Software](https://vorluno.dev), diseñado para empresas que operan en Panamá y necesitan automatizar el ciclo completo de planilla cumpliendo con todas las regulaciones locales.

Incluye además una **API Platform B2B** (rate-limited, API keys, idempotency) para que integradores externos consuman el motor de cálculo de nómina panameña.

> **📘 Ficha completa del proyecto** (stack, arquitectura, dominio, roles, compliance, deploy): [`docs/OVERVIEW.md`](./docs/OVERVIEW.md)

---

## ✨ Características principales

### 💰 Motor de cálculo (Panamá)
- **CSS (Ley 462)** — 9.75% empleado / 13.25% patrono (sube a 14.25% feb 2027, 15.25% mar 2029) con topes y segmentos.
- **Seguro Educativo** — 1.25% empleado / 1.50% patrono (sin tope máximo).
- **ISR** — brackets progresivos con deducción por dependientes ($800/c/u).
- **Riesgo Profesional** — configurable por categoría (0.56% — 5.39%).
- **Décimo tercer mes** — 3 pagos anuales (abril, agosto, diciembre) con CSS/SE sobre el monto.
- **Liquidaciones** — prima de antigüedad, vacaciones no pagadas, décimos prorrateados.
- **Horas extra** — 8 tipos (diurna, nocturna, dominical, feriado, etc.) con factores configurables.

### 👥 Gestión de empleados
- Expedientes completos (datos laborales, fiscales, CSS, contacto).
- Jerarquía de departamentos y posiciones.
- Historial salarial y de contratos.
- Ausencias, vacaciones, préstamos, anticipos, deducciones recurrentes.

### 📊 Flujo de nómina
- Estados: `Draft → Calculated → Approved → Paid`.
- Préstamos con amortización automática y prorrateo por quincena.
- Solicitudes de vacaciones con aprobación y cálculo de saldos.
- Auditoría completa (`CreatedBy`, `ModifiedBy`, timestamps).

### 📈 Reportes y exportaciones
- Recibos de sueldo en PDF (QuestPDF) con formato compacto banco 2×2.
- Reportes CSS regulatorios.
- Declaraciones ISR.
- Exportaciones Excel (ClosedXML).

### 🏢 Multi-tenant nativo
- Aislamiento por `TenantId` con **global query filters** de EF Core (seguridad crítica).
- Sistema de **roles custom por tenant** con 29 permisos granulares (`SystemPermission`).
- Invitaciones por email (Brevo) con plantillas.
- Planes con límites y upgrades vía Stripe (Free / Starter / Professional / Enterprise).

### 🔌 API Platform B2B
- Endpoints `/v1/*` con autenticación por API key (SHA256).
- Rate limiting por minuto + quotas mensuales por plan.
- Idempotency keys (TTL 24 h) para reintentos seguros.
- Quota alerts automáticas al 80% / 100% de uso.

---

## 🏗️ Arquitectura

Clean Architecture con 4 capas y dependencias unidireccionales:

```
src/
├─ Core/
│  ├─ Planilla.Domain/         # Entidades, enums, interfaces (0 dependencias)
│  └─ Planilla.Application/    # DTOs, servicios portables (cálculos), interfaces
├─ Infrastructure/
│  └─ Planilla.Infrastructure/ # EF Core, repositorios, Stripe, Brevo, seeders
└─ UI/
   └─ Planilla.Web/            # Controllers + SPA React (ClientApp/)
tests/
├─ Planilla.Application.Tests/       # xUnit
└─ Planilla.Web.IntegrationTests/    # xUnit (API)
```

**Flujo:** `Web → Application + Infrastructure + Domain` · `Infrastructure → Application + Domain` · `Application → Domain` · `Domain → ∅`.

| Capa | Responsabilidad |
|------|-----------------|
| **Domain** | Entidades, enums, value objects, interfaces de dominio. |
| **Application** | DTOs, interfaces de servicios, servicios portables de cálculo (CSS, SE, ISR). |
| **Infrastructure** | `DbContext`, migraciones EF Core, repos, Stripe, Brevo, seeders. |
| **Web** | REST API, autenticación, middleware, hosting del SPA. |

---

## 🛠️ Stack técnico

### Backend
- **.NET 9** (ASP.NET Core Web API + Identity + JWT Bearer)
- **Entity Framework Core 9.0.2** con **Npgsql** (PostgreSQL)
- **AutoMapper 12** · **Swashbuckle 9** (Swagger)
- **QuestPDF 2024.3** (recibos PDF) · **ClosedXML 0.102** (Excel)
- **Stripe.net 50.1** (billing/webhooks) · **Brevo** (`sib_api_v3_sdk` 4.0.2, email)
- **xUnit** + **Moq** + **FluentAssertions** (tests)

### Frontend
- **React 19.1** + **Vite 7** + **TypeScript 5.9**
- **React Router v7** · **Tailwind CSS 3.4**
- **Recharts** (dashboards) · **Lucide React** (icons) · **react-hot-toast**
- Estado global: Context API (sin Redux/Zustand)

### Infraestructura
- **PostgreSQL 16** (local y prod)
- **Docker multi-stage** (node:20-alpine → dotnet/sdk:9.0 → dotnet/aspnet:9.0)
- **CapRover** sobre **DigitalOcean** (push a `master` dispara deploy automático)

---

## 🚀 Arranque local

### Prerrequisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 16](https://www.postgresql.org/download/) corriendo en `localhost:5432`
- Base `PlanillaDB` creada (o ajustar la cadena de conexión)

### Setup rápido

```bash
# 1. Clonar repo
git clone https://github.com/vorluno/Vorluno-Planilla.git
cd Vorluno-Planilla

# 2. Configurar cadena de conexión en src/UI/Planilla.Web/appsettings.json
#    (o usar variable de entorno ConnectionStrings__DefaultConnection)

# 3. Aplicar migraciones (Program.cs las aplica automáticamente al arrancar,
#    pero también se pueden correr manualmente)
dotnet ef database update \
  --project src/Infrastructure/Planilla.Infrastructure \
  --startup-project src/UI/Planilla.Web

# 4. Instalar dependencias frontend
cd src/UI/Planilla.Web/ClientApp && npm install && cd -

# 5a. Arrancar backend (puerto 5039)
dotnet run --project src/UI/Planilla.Web

# 5b. En otra terminal, arrancar frontend (puerto 5173)
cd src/UI/Planilla.Web/ClientApp && npm run dev
```

### Helpers PowerShell (Windows)

```powershell
# Arrancar backend + frontend en ventanas separadas
./scripts/dev/iniciar-desarrollo.ps1

# Verificar puertos libres
./scripts/dev/verificar-puertos.ps1

# Detener
./scripts/dev/detener-desarrollo.ps1
```

Detalle completo en [`docs/onboarding/INICIO-RAPIDO.md`](./docs/onboarding/INICIO-RAPIDO.md) y [`docs/onboarding/COMO-INICIAR-DESARROLLO.md`](./docs/onboarding/COMO-INICIAR-DESARROLLO.md).

### Accesos locales

| Recurso | URL |
|---------|-----|
| API | `http://localhost:5039` |
| Swagger | `http://localhost:5039/swagger` |
| Frontend (dev) | `http://localhost:5173` |
| Health check | `http://localhost:5039/health` |

> **Nota:** no existe auto-registro público. Los tenants y usuarios se crean desde `/system-admin/tenants/create` por un system admin.

---

## 🚢 Deploy

El push a `master` dispara build automático en CapRover vía GitHub webhook. El pipeline Docker aplica migraciones EF Core al arrancar (`Program.cs:MigrateAsync`).

**Variables de entorno en producción** (CapRover → App Configs):

```
ConnectionStrings__DefaultConnection
Jwt__Key / Jwt__Issuer / Jwt__Audience / Jwt__ExpireHours
Stripe__SecretKey / Stripe__WebhookSecret / Stripe__PriceId* / Stripe__SuccessUrl / Stripe__CancelUrl
Brevo__ApiKey / Brevo__SenderEmail / Brevo__SenderName
Cors__AllowedOrigins      (lista separada por comas)
ApiRateLimit__PerMinute   (default: 60)
ASPNETCORE_ENVIRONMENT=Production
```

**Rollback:** CapRover → App → Deployment → deploy de versión anterior (~30 s).

Guías completas:
- [`docs/deploy/DEPLOY-CAPROVER.md`](./docs/deploy/DEPLOY-CAPROVER.md) — variables, health check, troubleshooting.
- [`docs/deploy/DEPLOY-PAGLY-CLAU.md`](./docs/deploy/DEPLOY-PAGLY-CLAU.md) — DNS en cPanel, GitHub webhook, dominio `pagly.clau.com.pa`.

---

## 🔐 Autenticación y autorización

- **JWT Bearer** (24 h) + **Refresh tokens** persistidos en DB.
- **Claims emitidos:** `sub`, `email`, `tenant_id`, `tenant_role`, `is_system_admin`, `nombre_completo`.
- **`TenantRole` enum:** `Owner (0)` / `User (1)`.
- **Roles custom por tenant:** `CustomTenantRole` + `RolePermission` + 29 `SystemPermission` granulares.
- **API Platform B2B:** esquema separado `ApiKey` con hash SHA256 y rate-limit.

El frontend expone `hasPermission(p)`, `hasRole(...)`, `canWrite()`, `canDelete()` desde `useAuth()`.

Detalle: [`docs/roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md`](./docs/roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md).

---

## 💳 Planes y facturación

Límites definidos en `src/Core/Planilla.Domain/Models/PlanFeatures.cs`:

| Plan | Empleados | Usuarios | Empresas | Excel | PDF | API | Precio/mes | API req/mes |
|------|-----------|----------|----------|-------|-----|-----|-----------:|------------:|
| **Free** | 5 | 1 | 1 | ❌ | ❌ | ❌ | $0 | 0 |
| **Starter** | 25 | 3 | 1 | ✅ | ❌ | ❌ | $29.99 | 0 |
| **Professional** | 100 | 10 | 3 | ✅ | ✅ | ✅ | $79.99 | 10.000 |
| **Enterprise** | ∞ | ∞ | ∞ | ✅ | ✅ | ✅ | $199.99 | 100.000 |

Webhooks Stripe manejados por `StripeWebhookController`: `customer.subscription.created/updated/deleted`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.trial_will_end`.

---

## 📝 Convenciones de desarrollo

### Reglas duras (no negociables)
1. **Nunca hardcodear tasas/tramos** — todo viene de `PayrollTaxConfiguration` o `TaxBracket`.
2. **Sin fallbacks silenciosos** — configuración faltante lanza `InvalidOperationException`.
3. **Nunca borrar datos a ciegas** — soft deletes (`IsActive`/`DeletedAt`) por defecto; hard delete solo donde la [política](./docs/compliance/POLITICA-ELIMINACION.md) lo permite.
4. **Auditar siempre** — `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`.
5. **Transacciones para multi-tabla** — usar `UnitOfWork`.
6. **`DbContext` solo en Infrastructure** — nunca en Domain ni Application.
7. **Multi-tenancy:** toda entidad `ITenantEntity` filtra por `TenantId` automáticamente vía global query filters. Validar siempre en código nuevo.

### Naming

| Tipo | Sufijo | Ejemplo |
|------|--------|---------|
| Transfer DTO | `Dto` | `EmpleadoDto` |
| Create/Update DTO | `Request` | `CreateEmpleadoRequest` |
| Resultado de cálculo | `Result` | `PayrollCalculationResult` |

### Flujo Git + Linear

1. Crear ticket en Linear (team `DEV`) con la plantilla correspondiente (Bug / Feature / Tech Task) **antes de tocar código**.
2. Mover a `In Progress`.
3. Commits con prefijo `DEV-#:` (ej. `DEV-93: chore: reorganizar documentación`).
4. Un commit por ticket. Sin `Co-Authored-By`. Títulos en Linear **sin** prefijo `DEV-#`.

Ver `CLAUDE.md` para detalles completos de convenciones del equipo.

---

## 📚 Documentación

Toda la documentación técnica y operativa vive en [`docs/`](./docs/README.md), organizada por módulo:

- **[docs/README.md](./docs/README.md)** — índice navegable con sección "Recientes".
- **[docs/OVERVIEW.md](./docs/OVERVIEW.md)** — ficha global del proyecto.
- **[docs/onboarding/](./docs/onboarding/)** — arranque local, setup de entorno.
- **[docs/architecture/](./docs/architecture/)** — diseño, convenciones frontend, plan de refactor.
- **[docs/payroll/](./docs/payroll/)** — motor de cálculo, fixes CSS/ISR, décimos.
- **[docs/compliance/](./docs/compliance/)** — regulación Panamá + política de eliminación de datos.
- **[docs/multi-tenant/](./docs/multi-tenant/)** — implementación multi-tenant.
- **[docs/roles-permisos/](./docs/roles-permisos/)** — sistema de roles custom.
- **[docs/api-platform/](./docs/api-platform/)** — quickstart + roadmap API B2B.
- **[docs/integrations/](./docs/integrations/)** — Stripe, Brevo, SMTP.
- **[docs/deploy/](./docs/deploy/)** — CapRover, dominio, build fixes.
- **[docs/security/](./docs/security/)** — auth, hardening.
- **[docs/qa/](./docs/qa/)** — pruebas, OWASP ZAP, bugs.
- **[docs/runbooks/](./docs/runbooks/)** — incidentes (ej. payday down).
- **[docs/changelog/](./docs/changelog/)** — historial de cambios.

---

## 🗺️ Roadmap

Hecho ✅ · En progreso 🚧 · Próximo ⏭️

- [x] Clean Architecture (.NET 9 + React 19)
- [x] Multi-tenant (global query filters + JWT claims)
- [x] Sistema de roles custom + 29 permisos granulares
- [x] Motor de cálculo Panamá (CSS, SE, ISR, décimo, liquidaciones, horas extra)
- [x] Reportes PDF (QuestPDF) y Excel (ClosedXML)
- [x] Billing Stripe (Free / Starter / Professional / Enterprise) + webhooks
- [x] Invitaciones por email (Brevo)
- [x] API Platform B2B (API keys, rate limit, idempotency, quota alerts)
- [x] Deploy automatizado CapRover + DigitalOcean
- [x] Health checks + auditoría
- [ ] 🚧 Cobertura completa de tests unitarios y de integración
- [ ] 🚧 Portal de auto-servicio para empleados (pay stubs, solicitudes)
- [ ] ⏭️ Integración bancaria (ACH) para pagos de nómina
- [ ] ⏭️ App móvil (MAUI)
- [ ] ⏭️ Dashboards ejecutivos avanzados con IA (anomaly detection)

---

## 🏢 Sobre Vorluno

**[Vorluno Software](https://vorluno.dev)** — *Where code meets craft.*
Panama City, Panama · UTC-5 · [contacto@vorluno.dev](mailto:contacto@vorluno.dev)

Productos hermanos: **CLAU** (KYC), **Core360** (ERP), **Pagly** (este repo).

---

## 📄 Licencia

Copyright © Vorluno 2025. Todos los derechos reservados. Proprietary — no redistribution.

---

<div align="center">

**[⬆ Volver arriba](#-pagly--vorluno-planilla)**

Hecho con 💜 por [Vorluno](https://vorluno.dev) en Panamá 🇵🇦

</div>
