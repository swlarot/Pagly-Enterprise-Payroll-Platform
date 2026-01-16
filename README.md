<div align="center">

<img src="https://raw.githubusercontent.com/vorluno/vorluno/main/BANNER-GITHUB.png" alt="Vorluno" width="100%">

# 💼 Vorluno Planilla (VOR-PLAN)

### Enterprise Payroll Management System for Panama

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![Entity Framework](https://img.shields.io/badge/EF_Core-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://docs.microsoft.com/en-us/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)](LICENSE)

**Full compliance with Panama labor regulations (CSS, Educational Insurance, ISR)**

[Live Demo](https://planilla.vorluno.dev) • [Report Bug](https://github.com/vorluno/Vorluno-Planilla/issues) • [Request Feature](https://github.com/vorluno/Vorluno-Planilla/discussions)

</div>

---

## 📋 Overview

**Vorluno Planilla** is an enterprise-grade payroll management system designed specifically for businesses operating in Panama. It ensures full compliance with local labor regulations including Social Security (CSS), Educational Insurance, and Income Tax (ISR) calculations with progressive brackets.

Built with **Clean Architecture** principles, the system provides a robust foundation for managing employees, processing payroll, and generating regulatory reports.

---

## ✨ Key Features

### 💰 Payroll Engine
- **CSS Calculations** — Tiered caps per Law 462 (9.75% employee / 12.25% employer)
- **Educational Insurance** — 1.25% employee / 1.50% employer (no cap)
- **ISR (Income Tax)** — Progressive brackets with dependent deductions
- **Professional Risk** — Configurable by job category (0.56% - 5.39%)

### 👥 Employee Management
- Complete employee records with labor and tax information
- Department and position hierarchy
- Salary history and contract tracking
- Soft deletes with full audit trail

### 📊 Payroll Workflow
- Draft → Calculated → Approved → Paid state machine
- Overtime with configurable multipliers
- Advances and loans with automatic amortization
- Vacation and absence management

### 📈 Reports & Exports
- Pay stubs (PDF generation)
- CSS regulatory reports
- ISR declarations
- Excel exports

### 🖥️ Modern UI
- React 19 SPA with Tailwind CSS
- Responsive design
- Real-time calculations

---

## 🏗️ Architecture

The project follows **Clean Architecture** principles with clear separation of concerns:

```
src/
├── Core/
│   ├── Vorluno.Planilla.Domain/         # Entities, enums, value objects
│   └── Vorluno.Planilla.Application/    # Services, DTOs, interfaces
├── Infrastructure/
│   └── Vorluno.Planilla.Infrastructure/ # EF Core, repositories
└── UI/
    └── Vorluno.Planilla.Web/           # API Controllers, React SPA
        └── ClientApp/                   # React application
```

### Layer Responsibilities

| Layer | Purpose |
|-------|---------|
| **Domain** | Business entities, enums, value objects (zero dependencies) |
| **Application** | Business logic, DTOs, service interfaces, validation |
| **Infrastructure** | Data access, EF Core, external service implementations |
| **Web** | REST API, configuration, SPA hosting |

---

## 🛠️ Tech Stack

### Backend
- **.NET 9** — Latest LTS framework
- **ASP.NET Core** — RESTful Web API
- **Entity Framework Core 9** — ORM with SQL Server
- **ASP.NET Core Identity** — Authentication & authorization
- **xUnit + Moq + FluentAssertions** — Testing

### Frontend
- **React 19** — UI library
- **Vite** — Build tool & dev server
- **Tailwind CSS** — Utility-first CSS
- **Axios** — HTTP client

### Reports
- **ClosedXML** — Excel generation
- **QuestPDF** — PDF generation

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [SQL Server](https://www.microsoft.com/sql-server) (LocalDB, Express, or higher)

### Installation

```bash
# Clone repository
git clone https://github.com/vorluno/Vorluno-Planilla.git
cd Vorluno-Planilla

# Configure database connection
# Edit src/UI/Vorluno.Planilla.Web/appsettings.json

# Apply migrations
cd src/UI/Vorluno.Planilla.Web
dotnet ef database update --project ../../Infrastructure/Vorluno.Planilla.Infrastructure

# Install frontend dependencies
cd ClientApp
npm install

# Run application
cd ..
dotnet run
```

### Access Points

| Service | URL |
|---------|-----|
| API | `https://localhost:7105` |
| Frontend | `http://localhost:5173` |
| Swagger | `https://localhost:7105/swagger` |

---

## 🇵🇦 Panama Compliance

### Implemented Regulations

| Regulation | Rate | Notes |
|------------|------|-------|
| **CSS (Social Security)** | 9.75% employee / 12.25% employer | Tiered caps per Law 462 |
| **Educational Insurance** | 1.25% employee / 1.50% employer | No maximum cap |
| **ISR (Income Tax)** | Progressive | Annual brackets with deductions |
| **Professional Risk** | 0.56% - 5.39% | Based on job category |

### Formats

- **Currency**: USD (B/.) — Format: `1,234.56`
- **Date**: `dd/MM/yyyy`

---

## 📝 Development Guidelines

### Hard Rules (Never Break)

1. **Never hardcode rates/amounts** — All config from `PayrollTaxConfiguration`
2. **No silent fallbacks** — Missing config throws `InvalidOperationException`
3. **Never delete data** — Soft deletes with `IsActive`/`DeletedAt`
4. **Always audit** — `CreatedBy`, `CreatedAt`, `ModifiedBy`, `ModifiedAt`
5. **Always use transactions** — Multi-table ops within `UnitOfWork`
6. **DbContext in Infrastructure only** — Never in Domain or Application

### Naming Conventions

| Type | Suffix | Example |
|------|--------|---------|
| Transfer DTO | `Dto` | `EmpleadoDto` |
| Create/Update DTO | `Request` | `CreateEmpleadoRequest` |
| Calculation Result | `Result` | `PayrollCalculationResult` |

---

## 🗺️ Roadmap

- [x] Clean Architecture setup
- [x] Employee CRUD (API + UI)
- [x] CSS/ISR calculation services
- [x] Payroll workflow entities
- [ ] Complete unit test coverage
- [ ] PDF reports & pay stubs
- [ ] Multi-tenant support
- [ ] Bank integration (ACH)
- [ ] Employee self-service portal

---

## 📄 License

Copyright © Vorluno 2025. All rights reserved.

---

<div align="center">

**[⬆ Back to Top](#-vorluno-planilla-vor-plan)**

Made with 💜 by [Vorluno](https://github.com/vorluno) in Panama 🇵🇦

</div>
