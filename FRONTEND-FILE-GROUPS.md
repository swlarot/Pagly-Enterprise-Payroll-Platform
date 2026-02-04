# Guía de Grupos de Archivos Frontend

## INTRODUCCIÓN

Este documento explica cómo están organizados los archivos del frontend en Planilla y **qué archivos debes modificar cuando haces cambios**. Usa esto como referencia rápida para entender las dependencias entre archivos.

---

## GRUPOS DE ARCHIVOS

### 📄 GRUPO 1: Páginas (`src/pages/`)

**Propósito:** Componentes de nivel superior que se renderizan en rutas específicas.

**Archivos:**
```
src/pages/
├── LoginPage.tsx                      # Login de usuarios
├── AcceptInvitePage.tsx              # Aceptar invitación
├── AdminDashboardPage.tsx            # Dashboard principal del tenant
├── UsersPage.tsx                     # Gestión de usuarios (Owner/Admin)
├── AuditLogPage.tsx                  # Log de auditoría
├── SystemAdminDashboardPage.tsx      # Dashboard del System Admin
├── TenantsManagementPage.tsx         # Lista de todos los tenants
├── CreateTenantPage.tsx              # Crear nuevo tenant
├── TenantDetailsPage.tsx             # Detalles de un tenant
├── EmpleadosPage.jsx                 # Gestión de empleados (legacy)
├── DepartamentosPage.jsx             # Gestión de departamentos
├── PosicionesPage.jsx                # Gestión de posiciones
├── PrestamosPage.jsx                 # Gestión de préstamos
├── DeduccionesPage.jsx               # Gestión de deducciones
├── AnticiposPage.jsx                 # Gestión de anticipos
├── HorasExtraPage.jsx                # Registro de horas extra
├── AusenciasPage.jsx                 # Registro de ausencias
├── VacacionesPage.jsx                # Solicitudes de vacaciones
├── PlanillasPage.jsx                 # Gestión de planillas
├── ReportesPage.jsx                  # Reportes y exportaciones
└── ConfiguracionPage.jsx             # Configuración de tasas CSS/SE/ISR
```

**Cuándo modificar:**
- Agregando una nueva página
- Cambiando la lógica de una página existente
- Actualizando el UI de una página

**Qué más modificar:**
- ✅ `App.tsx` - Agregar/actualizar ruta
- ✅ Servicios correspondientes (si cambia la lógica de API)
- ✅ Tipos (si cambian los DTOs)

---

### 🎨 GRUPO 2: Componentes UI (`src/components/ui/`)

**Propósito:** Componentes reutilizables de bajo nivel sin lógica de negocio.

**Archivos:**
```
src/components/ui/
├── Button.tsx      # Botones con variantes (primary, secondary, danger, etc.)
├── Card.tsx        # Cards (Card, CardHeader, CardBody, CardFooter)
├── Input.tsx       # Inputs de formulario con validación
├── Select.tsx      # Selects con opciones
├── Badge.tsx       # Badges de estado (success, danger, warning, info)
└── Modal.tsx       # Modales con overlay
```

**Cuándo modificar:**
- Agregando un nuevo componente UI base
- Actualizando estilos globales de un componente
- Agregando variantes a componentes existentes

**Qué más modificar:**
- ✅ Páginas que usen el componente modificado (para aprovechar nuevas variantes)
- ⚠️ **PRECAUCIÓN:** Cambios aquí afectan TODAS las páginas que usen el componente

---

### 📐 GRUPO 3: Layouts (`src/components/layout/`)

**Propósito:** Wrappers que proporcionan estructura común a páginas.

**Archivos:**
```
src/components/layout/
├── AuthLayout.tsx          # Layout para páginas de tenant (sidebar + navbar)
└── SystemAdminLayout.tsx   # Layout para páginas de system admin
```

**Cuándo modificar:**
- Cambiando el navbar o sidebar
- Agregando elementos globales al layout
- Modificando la estructura de navegación

**Qué más modificar:**
- ✅ Páginas si cambias props del layout
- ✅ `App.tsx` si creas un nuevo layout

---

### 🔒 GRUPO 4: Guards de Autenticación (`src/components/auth/`)

**Propósito:** Protección de rutas según autenticación y permisos.

**Archivos:**
```
src/components/auth/
├── ProtectedRoute.tsx      # Requiere autenticación (cualquier usuario logueado)
├── RoleGuard.tsx           # Requiere roles específicos (Owner, Admin, etc.)
└── SystemAdminRoute.tsx    # Requiere ser system admin
```

**Cuándo modificar:**
- ⚠️ **RARAMENTE** - Solo si la lógica de autenticación cambia
- Agregando nuevos guards para casos especiales

**Qué más modificar:**
- ✅ `App.tsx` - Actualizar rutas que usen el guard modificado

---

### 🌐 GRUPO 5: Servicios API (`src/services/`)

**Propósito:** Comunicación con el backend.

**Archivos:**
```
src/services/
├── api.ts                   # Cliente HTTP base (fetch wrapper)
├── config.ts                # Configuración (API_BASE_URL)
├── authService.ts           # Login, logout, refresh, me
├── tenantService.ts         # Operaciones del tenant (users, invitations, usage)
├── systemAdminService.ts    # Operaciones de system admin (tenants, metrics)
├── auditService.ts          # Logs de auditoría
└── subscriptionService.ts   # Suscripciones y billing
```

**Cuándo modificar:**
- Agregando llamadas a nuevos endpoints del backend
- Cambiando la estructura de requests/responses
- Agregando manejo de errores específico

**Qué más modificar:**
- ✅ Páginas que llamen al servicio modificado
- ✅ Tipos (`types/api.ts`) si cambian los DTOs
- ✅ Contextos si el servicio es usado globalmente

---

### 🧩 GRUPO 6: Contextos (`src/contexts/`)

**Propósito:** Estado global compartido entre componentes.

**Archivos:**
```
src/contexts/
└── AuthContext.tsx    # Usuario autenticado, tenant, subscription
```

**Cuándo modificar:**
- Agregando nuevo estado global
- Cambiando la lógica de autenticación
- Agregando helpers (como `canAccessFeature`, `hasRole`)

**Qué más modificar:**
- ✅ Componentes que usen el contexto (`useAuth()`)
- ✅ Servicios si cambia la forma de almacenar tokens
- ✅ Guards si cambia la forma de verificar permisos

---

### 🔤 GRUPO 7: Tipos TypeScript (`src/types/`)

**Propósito:** Definiciones de tipos compartidos.

**Archivos:**
```
src/types/
└── api.ts    # DTOs del backend (UserDto, TenantDto, SubscriptionDto, etc.)
```

**Cuándo modificar:**
- El backend agrega/modifica un DTO
- Necesitas tipar un nuevo request/response
- Cambios en enums (TenantRole, SubscriptionPlan, etc.)

**Qué más modificar:**
- ✅ Servicios que usen los tipos modificados
- ✅ Páginas que consuman esos tipos
- ✅ Componentes que reciban props con esos tipos

---

### 🔧 GRUPO 8: Utilidades (`src/utils/`)

**Propósito:** Funciones helper compartidas.

**Archivos:**
```
src/utils/
└── jwt.ts    # Decodificar y validar JWT tokens
```

**Cuándo modificar:**
- Agregando nuevas funciones helper
- Cambiando la lógica de utilidades existentes

**Qué más modificar:**
- ✅ Servicios o componentes que usen las utilidades

---

### 🗺️ GRUPO 9: Enrutamiento (`src/App.tsx`)

**Propósito:** Definición central de todas las rutas.

**Archivo:**
```
src/App.tsx
```

**Cuándo modificar:**
- ✅ Agregando una nueva página (SIEMPRE)
- ✅ Cambiando protección de una ruta
- ✅ Cambiando el layout de una ruta
- ✅ Modificando redirecciones

**Qué más modificar:**
- ✅ Página correspondiente a la ruta
- ✅ Layout si cambias el wrapper de la ruta

---

### 📦 GRUPO 10: Otros Componentes (`src/components/`)

**Propósito:** Componentes específicos del negocio.

**Archivos:**
```
src/components/
└── ConfirmModal.jsx    # Modal de confirmación
```

**Cuándo modificar:**
- Agregando componentes de negocio específicos
- Modificando componentes compartidos entre páginas

**Qué más modificar:**
- ✅ Páginas que usen el componente

---

## MATRIZ: "SI MODIFICAS X, DEBES REVISAR Y"

| SI MODIFICAS...                          | DEBES REVISAR/MODIFICAR...                                       |
|------------------------------------------|------------------------------------------------------------------|
| **Página existente**                     | Nada más (cambio aislado)                                        |
| **Nueva página**                         | `App.tsx` (import + ruta)                                       |
| **Servicio API**                         | Páginas que lo usen + Tipos (si cambia DTO)                     |
| **Tipo/DTO**                             | Servicios + Páginas que lo consuman                             |
| **Componente UI**                        | ⚠️ **TODAS las páginas** que lo usen (cambio global)            |
| **Layout**                               | Páginas que lo usen + `App.tsx` si es nuevo                     |
| **Guard de autenticación**               | `App.tsx` (rutas) + Contextos si cambia lógica                  |
| **Contexto (AuthContext)**               | Componentes que usen `useAuth()` + Guards                       |
| **`App.tsx` (rutas)**                    | Página correspondiente si cambias protección o layout           |
| **Utilidades**                           | Servicios/componentes que las usen                              |

---

## FLUJO: AGREGAR UNA NUEVA PÁGINA

Cuando agregas una **nueva página**, sigue este flujo:

### Paso 1: Crear la página
```
📄 Crear: src/pages/MiNuevaPage.tsx
```

### Paso 2: Registrar en App.tsx
```tsx
// 1. Import (líneas ~1-40)
import MiNuevaPage from './pages/MiNuevaPage';

// 2. Route (dentro de <Routes>)
<Route
  path="/mi-ruta"
  element={
    <ProtectedRoute>
      <AuthLayout>
        <MiNuevaPage />
      </AuthLayout>
    </ProtectedRoute>
  }
/>
```

### Paso 3: (Opcional) Crear servicio si necesita API
```
🌐 Crear/Modificar: src/services/miServicio.ts
```

### Paso 4: (Opcional) Agregar tipos si usa DTOs nuevos
```
🔤 Modificar: src/types/api.ts
```

### Paso 5: Validar
```bash
npm run build    # Debe compilar sin errores
npm run dev      # Debe arrancar sin errores
# Abrir http://localhost:5173/mi-ruta
```

---

## FLUJO: MODIFICAR UN COMPONENTE UI

Cuando modificas un **componente UI base** (Button, Card, etc.):

### Paso 1: Modificar el componente
```
🎨 Modificar: src/components/ui/Button.tsx
```

### Paso 2: Verificar uso en páginas
```bash
# Buscar todas las páginas que usan el componente
grep -r "import.*Button" src/pages/
```

### Paso 3: Actualizar páginas afectadas
```
📄 Modificar: Todas las páginas que importan el componente
```

### Paso 4: Testing exhaustivo
- ⚠️ **CRÍTICO:** Probar TODAS las páginas que usan el componente
- Verificar que no se rompió nada

---

## FLUJO: AGREGAR UN NUEVO ENDPOINT

Cuando el backend expone un nuevo endpoint:

### Paso 1: Agregar tipos
```tsx
🔤 Modificar: src/types/api.ts

export interface MiNuevoDto {
  id: number;
  name: string;
}
```

### Paso 2: Crear/modificar servicio
```tsx
🌐 Modificar: src/services/miServicio.ts

export const miServicio = {
  async getData() {
    const response = await api.get<MiNuevoDto[]>('/api/mi-endpoint');
    return response.data;
  }
};
```

### Paso 3: Usar en página
```tsx
📄 Modificar: src/pages/MiPage.tsx

import { miServicio } from '../services/miServicio';

const loadData = async () => {
  const data = await miServicio.getData();
  setData(data);
};
```

---

## FLUJO: CAMBIAR AUTENTICACIÓN/PERMISOS

Cuando cambias la lógica de autenticación:

### Paso 1: Modificar contexto
```
🧩 Modificar: src/contexts/AuthContext.tsx
```

### Paso 2: Actualizar guards
```
🔒 Modificar: src/components/auth/ProtectedRoute.tsx
🔒 Modificar: src/components/auth/RoleGuard.tsx
```

### Paso 3: Actualizar servicios de auth
```
🌐 Modificar: src/services/authService.ts
```

### Paso 4: Revisar rutas
```
🗺️ Modificar: src/App.tsx (si cambia protección de rutas)
```

### Paso 5: Testing completo
- Probar login/logout
- Probar rutas protegidas
- Probar roles

---

## DEPENDENCIAS ENTRE GRUPOS

```
┌──────────────────────────────────────────────────────────────┐
│                        App.tsx (Router)                        │
│  Importa: Páginas, Layouts, Guards                           │
└────────────────────────┬─────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ▼               ▼               ▼
    ┌────────┐      ┌────────┐      ┌────────┐
    │ Páginas │      │Layouts │      │ Guards │
    └────┬───┘      └────┬───┘      └────┬───┘
         │               │               │
         │ usa           │ usa           │ usa
         ▼               ▼               ▼
    ┌─────────────────────────────────────────┐
    │         Componentes UI + Contextos      │
    └─────────┬───────────────────────────────┘
              │ usa
              ▼
         ┌─────────┐
         │Servicios│
         └────┬────┘
              │ usa
              ▼
         ┌─────────┐
         │  Tipos  │
         └─────────┘
```

**Lectura:**
- `App.tsx` depende de Páginas, Layouts, Guards
- Páginas dependen de Componentes UI, Contextos, Servicios
- Servicios dependen de Tipos
- Cambios en niveles bajos (Tipos, Servicios) afectan niveles altos

---

## ARCHIVOS QUE NUNCA DEBES MODIFICAR (CORE)

Estos archivos son críticos y solo deben modificarse con extrema precaución:

- `src/contexts/AuthContext.tsx` - Lógica de autenticación global
- `src/services/api.ts` - Cliente HTTP base
- `src/components/auth/*.tsx` - Guards de autenticación
- `vite.config.ts` - Configuración de Vite
- `tsconfig.json` - Configuración de TypeScript

**Si necesitas modificar estos archivos, consulta antes.**

---

## CHECKLIST DE VALIDACIÓN

Después de modificar archivos, verifica:

### Si modificaste una página:
- [ ] Build exitoso (`npm run build`)
- [ ] Página carga en navegador
- [ ] No hay errores en consola

### Si modificaste un componente UI:
- [ ] Build exitoso
- [ ] Todas las páginas que lo usan cargan correctamente
- [ ] No hay regresiones visuales

### Si modificaste un servicio:
- [ ] Build exitoso
- [ ] Tipos actualizados
- [ ] Páginas que lo usan funcionan correctamente

### Si modificaste App.tsx:
- [ ] Build exitoso
- [ ] Todas las rutas funcionan
- [ ] Redirecciones correctas

---

## CONCLUSIÓN

Usa esta guía como referencia rápida cuando trabajes en el frontend. Entender las dependencias entre archivos te ayudará a evitar errores y hacer cambios de forma más eficiente.

**Regla de oro:** Siempre verifica qué archivos dependen de lo que estás modificando antes de hacer el cambio.

---

**Última actualización:** 2026-01-30
**Versión:** 1.0.0
