<div align="center">

<img src="https://raw.githubusercontent.com/pagly/pagly/main/BANNER-GITHUB.png" alt="Pagly" width="100%">

# 💼 Pagly — Enterprise Payroll Platform

### Multi-tenant SaaS for Complete Payroll Management in Panama

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Stripe](https://img.shields.io/badge/Stripe-Ready-635BFF?style=for-the-badge&logo=stripe&logoColor=white)](https://stripe.com)
[![Docker](https://img.shields.io/badge/Docker-CapRover-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://caprover.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)](#-licencia)

**Complete compliance with Panamanian labor legislation (CSS Law 462, Educational Insurance, ISR, Settlements)**

[Documentation](./docs/README.md) · [Project Overview](./docs/OVERVIEW.md) · [Quick Start](./docs/onboarding/INICIO-RAPIDO.md) · [B2B API Platform](./docs/api-platform/README.md)

</div>

---

## 📋 Summary

**Pagly** is the premium payroll SaaS solution designed for businesses operating in Panama that need complete automation of:

- ✅ **CSS (Social Security Law 462)** — Employee & employer contributions with progressive scales
- ✅ **Educational Insurance** — Automatic calculations and compliance
- ✅ **Progressive ISR** — Tax brackets with dependent deductions
- ✅ **Professional Risk** — Configurable by category
- ✅ **Overtime & Special Pay** — 8 types (diurnal, nocturnal, weekend, holiday, etc.)
- ✅ **13th Month & Annual Settlements** — Full compliance with liquidation rules
- ✅ **Multi-tenant Architecture** — Complete data isolation with role-based access control
- ✅ **B2B API Platform** — Rate-limited, idempotent payroll calculation engine for integrators

> **📘 Complete project datasheet** (stack, architecture, domain, roles, compliance, deployment): [`docs/OVERVIEW.md`](./docs/OVERVIEW.md)

---

## ✨ Core Features

### 💰 Advanced Payroll Engine
- **CSS (Law 462)** — 9.75% employee / 13.25% employer (increases to 14.25% Feb 2027, 15.25% Mar 2029) with caps and segments
- **Educational Insurance** — 1.25% employee / 1.50% employer (no maximum cap)
- **Progressive ISR** — Multiple brackets with $800/dependent deduction
- **Professional Risk** — Configurable per role (0.56% — 5.39%)
- **13th Month** — 3 annual payments (April, August, December) with CSS/Insurance
- **Full Settlements** — Seniority bonuses, unpaid leave, prorated benefits
- **Overtime Automation** — 8 types with configurable multipliers

### 👥 Employee Management
- Complete personnel records (employment, tax, social security, contact data)
- Department & position hierarchies
- Salary and contract history
- Absences, vacation requests, loans, advances, recurring deductions

### 📊 Payroll Workflow
- State machine: `Draft → Calculated → Approved → Paid`
- Automatic loan amortization & prorated disbursement
- Vacation request workflow with balance calculation
- Complete audit trail (`CreatedBy`, `ModifiedBy`, timestamps)

### 📈 Reports & Exports
- PDF Receipts (QuestPDF) in compact bank 2×2 format
- Regulatory CSS reports
- ISR declarations
- Excel exports (ClosedXML) for accounting integration

### 🏢 Enterprise Multi-Tenancy
- Native isolation by `TenantId` with EF Core **global query filters** (security-critical)
- **29 granular permissions** with custom role system (`SystemPermission`)
- Email invitations (Brevo) with templates
- Plan-based limits with Stripe upgrades (Free / Starter / Professional / Enterprise)

### 🔌 B2B API Platform
- Endpoints `/v1/*` with SHA256 API key authentication
- Per-minute rate limiting + monthly quotas by plan
- Idempotency keys (24h TTL) for safe retries
- Automatic quota alerts at 80% / 100% usage

---

## 🏗️ Architecture

Clean Architecture with 4 unidirectional dependency layers:

```
src/
├─ Core/
│  ├─ Pagly.Domain/         # Entities, enums, domain interfaces (no dependencies)
│  └─ Pagly.Application/    # DTOs, portable services, calculation logic
├─ Infrastructure/
│  └─ Pagly.Infrastructure/ # EF Core, repositories, Stripe, Brevo, seeders
└─ UI/
   └─ Pagly.Web/            # Controllers + React SPA (ClientApp/)
tests/
├─ Pagly.Application.Tests/       # xUnit
└─ Pagly.Web.IntegrationTests/    # xUnit (API)
```

**Flow:** `Web → Application + Infrastructure + Domain` · `Infrastructure → Application + Domain` · `Application → Domain` · `Domain → ∅`.

| Layer | Responsibility |
|-------|-----------------|
| **Domain** | Entities, enums, value objects, domain interfaces |
| **Application** | DTOs, service interfaces, portable calculation services (CSS, ISR) |
| **Infrastructure** | DbContext, EF Core migrations, repos, Stripe, Brevo, seeders |
| **Web** | REST API, authentication, middleware, SPA hosting |

---

## 🛠️ Tech Stack

### Backend
- **.NET 9** (ASP.NET Core Web API + Identity + JWT Bearer)
- **Entity Framework Core 9.0.2** with **Npgsql** (PostgreSQL)
- **AutoMapper 12** · **Swashbuckle 9** (Swagger/OpenAPI)
- **QuestPDF 2024.3** (PDF receipts) · **ClosedXML 0.102** (Excel)
- **Stripe.net 50.1** (billing/webhooks) · **Brevo** (`sib_api_v3_sdk` 4.0.2, email)
- **xUnit** + **Moq** + **FluentAssertions** (testing)

### Frontend
- **React 19.1** + **Vite 7** + **TypeScript 5.9**
- **React Router v7** · **Tailwind CSS 3.4**
- **Recharts** (dashboards) · **Lucide React** (icons) · **react-hot-toast**
- Global state: Context API (no Redux/Zustand)

### Infrastructure
- **PostgreSQL 16** (local and production)
- **Docker multi-stage** (node:20-alpine → dotnet/sdk:9.0 → dotnet/aspnet:9.0)
- **CapRover** on **DigitalOcean** (push to `master` triggers automatic deployment)

---

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 16](https://www.postgresql.org/download/) running on `localhost:5432`
- Database `PaglyDB` created (or adjust connection string)

### Setup

```bash
# 1. Clone repository
git clone https://github.com/pagly/pagly.git
cd pagly

# 2. Configure connection string in src/UI/Pagly.Web/appsettings.json
#    (or use environment variable ConnectionStrings__DefaultConnection)

# 3. Apply migrations
dotnet ef database update \
  --project src/Infrastructure/Pagly.Infrastructure \
  --startup-project src/UI/Pagly.Web

# 4. Install frontend dependencies
cd src/UI/Pagly.Web/ClientApp && npm install && cd -

# 5a. Start backend (port 5039)
dotnet run --project src/UI/Pagly.Web

# 5b. In another terminal, start frontend (port 5173)
cd src/UI/Pagly.Web/ClientApp && npm run dev
```

### Local Access

| Resource | URL |
|----------|-----|
| API | `http://localhost:5039` |
| Swagger | `http://localhost:5039/swagger` |
| Frontend (dev) | `http://localhost:5173` |
| Health check | `http://localhost:5039/health` |

> **Note:** No public self-registration. Tenants and users are created by system admins via `/system-admin/tenants/create`.

---

## 🚢 Deployment

Push to `master` triggers automatic build on CapRover via GitHub webhook. Docker pipeline applies EF Core migrations at startup (`Program.cs:MigrateAsync`).

**Production environment variables** (CapRover → App Configs):

```
ConnectionStrings__DefaultConnection
Jwt__Key / Jwt__Issuer / Jwt__Audience / Jwt__ExpireHours
Stripe__SecretKey / Stripe__WebhookSecret / Stripe__PriceId* / Stripe__SuccessUrl / Stripe__CancelUrl
Brevo__ApiKey / Brevo__SenderEmail / Brevo__SenderName
Cors__AllowedOrigins      (comma-separated list)
ApiRateLimit__PerMinute   (default: 60)
ASPNETCORE_ENVIRONMENT=Production
```

**Rollback:** CapRover → App → Deployment → previous version (~30 seconds).

Complete guides:
- [`docs/deploy/DEPLOY-CAPROVER.md`](./docs/deploy/DEPLOY-CAPROVER.md)
- [`docs/deploy/DEPLOY-PAGLY.md`](./docs/deploy/DEPLOY-PAGLY.md)

---

## 🔐 Authentication & Authorization

- **JWT Bearer** (24h) + **Refresh tokens** persisted in DB
- **Claims issued:** `sub`, `email`, `tenant_id`, `tenant_role`, `is_system_admin`, `full_name`
- **`TenantRole` enum:** `Owner (0)` / `User (1)`
- **Custom roles per tenant:** `CustomTenantRole` + `RolePermission` + 29 granular `SystemPermission`s
- **B2B API Platform:** separate `ApiKey` scheme with SHA256 hashing and rate-limit

Frontend exposes `hasPermission(p)`, `hasRole(...)`, `canWrite()`, `canDelete()` from `useAuth()`.

Details: [`docs/roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md`](./docs/roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md).

---

## 💳 Plans & Billing

Limits defined in `src/Core/Pagly.Domain/Models/PlanFeatures.cs`:

| Plan | Employees | Users | Businesses | Excel | PDF | API | Price/month | API req/month |
|------|-----------|-------|-----------|-------|-----|-----|-----------:|------------:|
| **Free** | 5 | 1 | 1 | ❌ | ❌ | ❌ | $0 | 0 |
| **Starter** | 25 | 3 | 1 | ✅ | ❌ | ❌ | $29.99 | 0 |
| **Professional** | 100 | 10 | 3 | ✅ | ✅ | ✅ | $79.99 | 10,000 |
| **Enterprise** | ∞ | ∞ | ∞ | ✅ | ✅ | ✅ | $199.99+ | 100,000+ |

Stripe webhooks handled by `StripeWebhookController`: `customer.subscription.created/updated/deleted`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.trial_will_end`.

---

## 📝 Development Conventions

### Hard Rules (Non-Negotiable)
1. **Never hardcode tax rates/brackets** — all comes from `PayrollTaxConfiguration` or `TaxBracket`
2. **No silent fallbacks** — missing configuration raises `InvalidOperationException`
3. **Never blind data deletion** — soft deletes (`IsActive`/`DeletedAt`) by default; hard delete only per policy
4. **Always audit** — `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`
5. **Transactions for multi-table** — use `UnitOfWork`
6. **`DbContext` only in Infrastructure** — never in Domain or Application
7. **Multi-tenancy:** every `ITenantEntity` filters by `TenantId` automatically via global query filters

### Naming Conventions

| Type | Suffix | Example |
|------|--------|---------|
| Transfer DTO | `Dto` | `EmployeeDto` |
| Create/Update DTO | `Request` | `CreateEmployeeRequest` |
| Calculation result | `Result` | `PayrollCalculationResult` |

### Git + Linear Workflow

1. Create ticket in Linear (team `DEV`) with appropriate template **before touching code**
2. Move to `In Progress`
3. Commits prefixed with `DEV-#:` (e.g., `DEV-93: chore: reorganize documentation`)
4. One commit per ticket. No `Co-Authored-By`. Linear titles **without** `DEV-#` prefix

Full details: see `CLAUDE.md`

---

## 📚 Documentation

Complete technical and operational documentation in [`docs/`](./docs/README.md), organized by module:

- **[docs/README.md](./docs/README.md)** — Navigable index with "Recently Updated" section
- **[docs/OVERVIEW.md](./docs/OVERVIEW.md)** — Complete project datasheet
- **[docs/onboarding/](./docs/onboarding/)** — Quick start, environment setup
- **[docs/architecture/](./docs/architecture/)** — Design, frontend conventions, refactor plan
- **[docs/payroll/](./docs/payroll/)** — Calculation engine, CSS/ISR fixes, 13th month
- **[docs/compliance/](./docs/compliance/)** — Panama regulations + data deletion policy
- **[docs/multi-tenant/](./docs/multi-tenant/)** — Multi-tenancy implementation
- **[docs/roles-permisos/](./docs/roles-permisos/)** — Custom role system
- **[docs/api-platform/](./docs/api-platform/)** — B2B API quickstart + roadmap
- **[docs/integrations/](./docs/integrations/)** — Stripe, Brevo, SMTP
- **[docs/deploy/](./docs/deploy/)** — CapRover, domain configuration, build troubleshooting
- **[docs/security/](./docs/security/)** — Auth implementation, hardening
- **[docs/qa/](./docs/qa/)** — Testing, OWASP ZAP, reported bugs
- **[docs/runbooks/](./docs/runbooks/)** — Incident procedures
- **[docs/changelog/](./docs/changelog/)** — Change history

---

## 🗺️ Roadmap

Completed ✅ · In Progress 🚧 · Upcoming ⏭️

- [x] Clean Architecture (.NET 9 + React 19)
- [x] Multi-tenant (global query filters + JWT claims)
- [x] Custom role system + 29 granular permissions
- [x] Panama payroll engine (CSS, ISR, 13th month, settlements, overtime)
- [x] PDF (QuestPDF) and Excel (ClosedXML) reports
- [x] Stripe billing (Free / Starter / Professional / Enterprise) + webhooks
- [x] Email invitations (Brevo)
- [x] B2B API Platform (API keys, rate limit, idempotency, quota alerts)
- [x] Automated CapRover + DigitalOcean deployment
- [x] Health checks + audit trails
- [ ] 🚧 Comprehensive unit and integration test coverage
- [ ] 🚧 Employee self-service portal (pay stubs, requests)
- [ ] ⏭️ Banking integration (ACH) for payroll disbursement
- [ ] ⏭️ Mobile app (.NET MAUI)
- [ ] ⏭️ Advanced executive dashboards with AI (anomaly detection)

---

## 🏢 About Pagly

**Pagly** — *Enterprise Payroll, Simplified.* — is built on proven technology by [Vorluno Software](https://vorluno.dev).

Panama City, Panama · UTC-5 · [hello@pagly.app](mailto:hello@pagly.app)

Related products: **CLAU** (KYC), **Core360** (ERP).

---

## 📄 License

Copyright © Pagly 2025. All rights reserved. Proprietary — no redistribution.

---

<div align="center">

**[⬆ Back to top](#-pagly--enterprise-payroll-platform)**

Built with 💜 by [Vorluno Software](https://vorluno.dev) in Panama 🇵🇦

</div>
