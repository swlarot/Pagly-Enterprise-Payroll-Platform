# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Comandos de Desarrollo

### Backend
```bash
# Ejecutar backend (puerto 5039)
dotnet run --project src/UI/Planilla.Web

# Agregar migración EF Core
dotnet ef migrations add NombreMigracion --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web

# Aplicar migraciones manualmente
dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
```

### Frontend (puerto 5173)
```bash
cd src/UI/Planilla.Web/ClientApp
npm run dev        # Desarrollo con hot reload
npm run build      # Build de producción
npm run lint       # ESLint
npx tsc --noEmit   # Type checking sin build
```

### Verificación
```
GET /health      # PostgreSQL + MultiTenant checks
GET /api/health  # Check rápido
GET /swagger     # Swagger UI disponible en desarrollo
```

---

## Arquitectura

**Stack:** .NET 9 (ASP.NET Core Web API + EF Core) + React 19 (Vite, TypeScript, Tailwind) + PostgreSQL 16 + Stripe.

**Proyectos .csproj:**
- `src/Core/Planilla.Domain/` — Entidades, enums, interfaces
- `src/Core/Planilla.Application/` — DTOs, interfaces de servicios, servicios de dominio portables
- `src/Infrastructure/Planilla.Infrastructure/` — EF Core, repositorios, servicios externos (Stripe, Email)
- `src/UI/Planilla.Web/` (proyecto: `Vorluno.Planilla.Web.csproj`) — Controllers + SPA React en `ClientApp/`

**Estructura del Frontend (`src/UI/Planilla.Web/ClientApp/src/`):**
- `pages/` — Un archivo = una página, exportada con `export default`
- `components/ui/` — `Button`, `Card`, `Input`, `Select`, `Badge`, `Modal`
- `components/layout/` — `AuthLayout` (tenant), `SystemAdminLayout` (system admin)
- `components/auth/` — `ProtectedRoute`, `RoleGuard`, `SystemAdminRoute`
- `contexts/` — `AuthContext` (estado global de autenticación)
- `services/` — Clientes HTTP (`api.ts` como base, servicios específicos)
- `types/api.ts` — Tipos TypeScript compartidos

---

## Multi-Tenancy (CRÍTICO)

**TODAS** las queries deben filtrar por `TenantId`. Los global query filters de EF Core lo hacen automáticamente, pero al escribir repos manualmente:

```csharp
// Siempre filtrar
var tenantId = _currentTenantService.TenantId;
return await _context.Employees.Where(e => e.TenantId == tenantId).ToListAsync();
```

**JWT Claims:** `tenant_id`, `tenant_role`, `plan`, `sub`, `email`.

**Roles del tenant:** `Owner > Admin > Manager > Accountant > Employee`

**Planes:** `Free (5 emp) → Starter ($29.99, 25 emp) → Professional ($79.99, 100 emp) → Enterprise ($199.99, ilimitado)`

---

## Patrones de Código

### Backend

- Nunca exponer entidades directamente — siempre usar DTOs
- Usar `ActionResponse<T>` para todas las respuestas de servicios
- Verificar límites del plan antes de crear recursos (ver `PlanFeatures.Limits`)
- Registrar todos los servicios en `Program.cs`
- Autorización por roles: `[Authorize(Roles = "Owner,Admin")]`

### Frontend — Crear una nueva página

1. Crear `src/pages/NombrePage.tsx` con `export default function NombrePage()`
2. Agregar import y route en `App.tsx`:
   ```tsx
   // Ruta protegida típica
   <Route path="/ruta" element={
     <ProtectedRoute><AuthLayout><NombrePage /></AuthLayout></ProtectedRoute>
   } />

   // Con roles
   <Route path="/ruta" element={
     <ProtectedRoute>
       <RoleGuard allowedRoles={[TenantRole.Owner]}>
         <AuthLayout><NombrePage /></AuthLayout>
       </RoleGuard>
     </ProtectedRoute>
   } />

   // System admin (sin AuthLayout)
   <Route path="/system-admin/ruta" element={
     <SystemAdminRoute><NombrePage /></SystemAdminRoute>
   } />
   ```
3. Ejecutar `npm run build` — debe compilar sin errores

**Estructura mínima de página:**
```tsx
import React, { useEffect, useState } from 'react';
import { Card, CardBody } from '../components/ui/Card';
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

  if (isLoading) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
    </div>
  );

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-900">Mi Página</h1>
      <Card><CardBody>{/* Contenido */}</CardBody></Card>
    </div>
  );
}
```

**Servicios API:** `import api from './api'` (base) → el interceptor en `api.ts` maneja refresh token automáticamente.

**Tipos:** Definir en `src/types/api.ts`. Importar con `import type { MiDto } from '../types/api'`.

---

## Deploy — CapRover + DigitalOcean

**Todo push a `master` dispara un deploy automático en producción** (GitHub webhook → CapRover).

**Pipeline Dockerfile (3 stages):**
1. `node:20-alpine`: `npm ci` + `npm run build` → `vite outDir: '../wwwroot'`
2. `dotnet/sdk:9.0`: `dotnet restore` + `dotnet publish`; copia wwwroot
3. `dotnet/aspnet:9.0`: runtime en puerto 80; `Program.cs` ejecuta `MigrateAsync()` al arrancar

**Checklist antes de push a master:**
- Si agregaste paquetes npm: commitear `package-lock.json` (`npm ci` en Docker, no `npm install`)
- Si creaste migraciones EF Core: deben estar commiteadas (se aplican en producción al arrancar)
- Si creaste un nuevo `.csproj`: agregarlo a `Planilla.sln`
- Si registraste nuevos servicios: verificar que estén en `Program.cs`

**Archivos críticos — no renombrar:**
- `Dockerfile`, `captain-definition`
- `vite.config.js` (`outDir: '../wwwroot'` obligatorio)
- `Vorluno.Planilla.Web.csproj` (nombre usado en Dockerfile)
- `Planilla.sln`

**Rollback:** Panel CapRover → App → Deployment → Deploy en versión anterior (~30 seg).

**Variables de entorno en CapRover:**
```
ConnectionStrings__DefaultConnection
Jwt__Key / Jwt__Issuer / Jwt__Audience
ASPNETCORE_ENVIRONMENT=Production
Stripe__PublishableKey / Stripe__SecretKey / Stripe__WebhookSecret
```

---

## Stripe Webhooks

`POST /api/webhooks/stripe` maneja: `customer.subscription.created/updated/deleted`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.trial_will_end`.

---

## No hay auto-registro público

El registro de usuarios nuevos lo hace un admin del sistema desde `/system-admin/tenants/create`. El flujo de usuario solo tiene `/login` y `/accept-invite`.

---

## Flujo obligatorio — Linear Tickets (CRÍTICO)

**Antes de tocar cualquier archivo**, sin excepción de tamaño:

1. **Crear ticket en Linear** (equipo `DEV`) con:
   - Título descriptivo (sin prefijo `DEV-#`)
   - **Descripción: OBLIGATORIO usar la plantilla correspondiente al tipo** (ver MEMORY.md). Nunca crear ticket sin plantilla.
     - `Bug` → plantilla Bug Report
     - `Feature` → plantilla Feature Request
     - `Refactor` / `Performance` / tech task → plantilla Tech Task
   - Labels apropiados (`Bug` / `Feature` / `Refactor` / etc., `WEBAPP` / `API` / etc.)
   - Estado: `Todo`

2. **Mover a `In Progress`** al empezar a trabajar.

3. **Implementar** el cambio.

4. **Mover a `Done`** al terminar.

Esto aplica a: bugs de una línea, cambios de texto, refactors grandes, nuevas features — **todo**.
