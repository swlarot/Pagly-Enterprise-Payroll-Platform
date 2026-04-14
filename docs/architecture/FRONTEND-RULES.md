# Reglas de Frontend - React + Vite + TypeScript

## VISIÓN GENERAL

Este documento establece las reglas OBLIGATORIAS para el desarrollo frontend en Planilla. **TODOS** los desarrolladores y agentes deben seguir estas reglas al pie de la letra para evitar páginas en blanco, errores de compilación y problemas de integración.

---

## 1. CHECKLIST OBLIGATORIO AL CREAR UNA NUEVA PÁGINA

Cuando crees una nueva página React (`.tsx` o `.jsx`), debes completar **TODOS** estos pasos:

### ✅ Paso 1: Crear el archivo de la página

**Ubicación:** `src/UI/Planilla.Web/ClientApp/src/pages/`

**Convención de nombres:**
- Usa PascalCase
- Termina en `Page.tsx` o `Page.jsx`
- Ejemplos: `EmpleadosPage.tsx`, `SystemAdminDashboardPage.tsx`

**Estructura mínima obligatoria:**

```tsx
import React from 'react';

export default function MiNuevaPagePage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Mi Nueva Página</h1>
        <p className="text-gray-600 mt-2">Descripción de la página</p>
      </div>

      {/* Contenido aquí */}
    </div>
  );
}
```

**CRÍTICO:**
- ✅ SIEMPRE exporta con `export default function`
- ✅ El nombre de la función DEBE coincidir con el nombre del archivo (sin la extensión)
- ❌ NUNCA uses `export const` sin un `export default` adicional
- ❌ NUNCA olvides el `export` - causará error de importación

---

### ✅ Paso 2: Registrar la ruta en App.tsx

**Archivo a modificar:** `src/UI/Planilla.Web/ClientApp/src/App.tsx`

**Pasos obligatorios:**

#### 2.1. Agregar el import al inicio del archivo

```tsx
// En la sección de imports (líneas ~1-40)
import MiNuevaPagePage from './pages/MiNuevaPagePage';
```

**Grupos de imports en App.tsx:**
1. Imports de React y librerías
2. Imports de contextos y componentes propios
3. **Auth Pages** (LoginPage, AcceptInvitePage)
4. **Admin Pages (Tenant)** - páginas de administración de tenant
5. **System Admin Pages** - páginas de administración del sistema
6. **Existing Pages** - páginas legacy de planilla

#### 2.2. Agregar la ruta en el componente Routes

```tsx
// Dentro del componente <Routes>
<Route
  path="/mi-ruta"
  element={
    <ProtectedRoute>
      <AuthLayout>
        <MiNuevaPagePage />
      </AuthLayout>
    </ProtectedRoute>
  }
/>
```

**Patrones de rutas según el tipo de página:**

##### Rutas Públicas (sin autenticación)
```tsx
<Route path="/login" element={<LoginPage />} />
<Route path="/accept-invite" element={<AcceptInvitePage />} />
```

##### Rutas Protegidas (requiere autenticación)
```tsx
<Route
  path="/mi-pagina"
  element={
    <ProtectedRoute>
      <AuthLayout>
        <MiPaginaPage />
      </AuthLayout>
    </ProtectedRoute>
  }
/>
```

##### Rutas con Control de Roles
```tsx
<Route
  path="/usuarios"
  element={
    <ProtectedRoute>
      <RoleGuard allowedRoles={[TenantRole.Owner, TenantRole.Admin]}>
        <AuthLayout>
          <UsersPage />
        </AuthLayout>
      </RoleGuard>
    </ProtectedRoute>
  }
/>
```

##### Rutas de System Admin
```tsx
<Route
  path="/system-admin/dashboard"
  element={
    <SystemAdminRoute>
      <SystemAdminDashboardPage />
    </SystemAdminRoute>
  }
/>
```

**NOTA:** Las rutas de System Admin NO usan `<AuthLayout>`, usan `<SystemAdminLayout>` internamente en el componente.

---

### ✅ Paso 3: Verificar imports de dependencias

**Componentes UI disponibles:**
- `Card`, `CardHeader`, `CardBody`, `CardFooter` → `'../components/ui/Card'`
- `Button` → `'../components/ui/Button'`
- `Input` → `'../components/ui/Input'`
- `Select` → `'../components/ui/Select'`
- `Badge` → `'../components/ui/Badge'`
- `Modal` → `'../components/ui/Modal'`
- `ConfirmModal` → `'../components/ConfirmModal'`

**Layouts disponibles:**
- `AuthLayout` → `'../components/layout/AuthLayout'` (tenant pages)
- `SystemAdminLayout` → `'../components/layout/SystemAdminLayout'` (system admin)

**Guards y protección:**
- `ProtectedRoute` → `'../components/auth/ProtectedRoute'`
- `RoleGuard` → `'../components/auth/RoleGuard'`
- `SystemAdminRoute` → `'../components/auth/SystemAdminRoute'`

**Contextos:**
- `useAuth` → `'../contexts/AuthContext'`

**Servicios:**
- `api` → `'../services/api'`
- `authService` → `'../services/authService'`
- `tenantService` → `'../services/tenantService'`
- `systemAdminService` → `'../services/systemAdminService'`
- `auditService` → `'../services/auditService'`
- `subscriptionService` → `'../services/subscriptionService'`

**Iconos (Lucide React):**
```tsx
import { Plus, Edit, Trash2, Search, Loader2 } from 'lucide-react';
```

**Toast notifications:**
```tsx
import toast from 'react-hot-toast';

toast.success('Operación exitosa');
toast.error('Error al procesar');
```

---

### ✅ Paso 4: Validar en el navegador

**Pasos de validación:**

1. **Build exitoso**
   ```bash
   cd src/UI/Planilla.Web/ClientApp
   npm run build
   ```
   - Si hay errores de TypeScript, DEBES corregirlos antes de continuar

2. **Arrancar el servidor de desarrollo**
   ```bash
   npm run dev
   ```

3. **Abrir el navegador**
   - Navega a `http://localhost:5173/mi-ruta`
   - Verifica que la página carga correctamente
   - Abre la consola del navegador (F12) y verifica que NO haya errores

4. **Validaciones visuales**
   - ✅ La página muestra contenido (no está en blanco)
   - ✅ El layout correcto está aplicado (AuthLayout o SystemAdminLayout)
   - ✅ Los estilos Tailwind funcionan correctamente
   - ✅ Los componentes UI se renderizan bien

---

## 2. ESTRUCTURA DE ARCHIVOS Y GRUPOS LÓGICOS

### Grupo 1: Páginas (`src/pages/`)

**Responsabilidad:** Componentes de nivel página que se renderizan en rutas

**Estructura:**
```
src/pages/
├── LoginPage.tsx                  # Autenticación
├── AcceptInvitePage.tsx
├── AdminDashboardPage.tsx         # Dashboard del tenant
├── UsersPage.tsx                  # Gestión de usuarios del tenant
├── AuditLogPage.tsx              # Log de auditoría
├── SystemAdminDashboardPage.tsx  # System Admin
├── TenantsManagementPage.tsx     # Gestión de tenants
├── CreateTenantPage.tsx          # Crear tenant
├── TenantDetailsPage.tsx         # Detalles de tenant
├── EmpleadosPage.jsx             # Páginas legacy
├── ConfiguracionPage.jsx
└── ...
```

**Reglas:**
- Un archivo = una página
- Nombre del archivo = Nombre del componente + `Page`
- SIEMPRE `export default`

---

### Grupo 2: Componentes UI (`src/components/ui/`)

**Responsabilidad:** Componentes reutilizables de bajo nivel

**Componentes actuales:**
```
src/components/ui/
├── Button.tsx        # Botones con variantes
├── Card.tsx          # Cards (Card, CardHeader, CardBody, CardFooter)
├── Input.tsx         # Inputs de formulario
├── Select.tsx        # Selects de formulario
├── Badge.tsx         # Badges de estado
└── Modal.tsx         # Modales
```

**Reglas:**
- Componentes simples, sin lógica de negocio
- SIEMPRE exportar con `export function`
- Props tipadas con TypeScript
- Usar Tailwind para estilos

**Ejemplo de nuevo componente UI:**
```tsx
// src/components/ui/NewComponent.tsx
import React from 'react';

interface NewComponentProps {
  children: React.ReactNode;
  variant?: 'primary' | 'secondary';
}

export function NewComponent({ children, variant = 'primary' }: NewComponentProps) {
  return <div className="...">{children}</div>;
}
```

---

### Grupo 3: Layouts (`src/components/layout/`)

**Responsabilidad:** Layouts que envuelven páginas

**Layouts actuales:**
```
src/components/layout/
├── AuthLayout.tsx           # Layout para tenant pages
└── SystemAdminLayout.tsx    # Layout para system admin pages
```

**Cuándo crear un nuevo layout:**
- Necesitas una estructura común para múltiples páginas
- Quieres un navbar o sidebar específico

**Ejemplo:**
```tsx
// src/components/layout/MyLayout.tsx
import React from 'react';

interface MyLayoutProps {
  children: React.ReactNode;
}

export default function MyLayout({ children }: MyLayoutProps) {
  return (
    <div className="min-h-screen bg-gray-50">
      <header>{/* Navbar */}</header>
      <main>{children}</main>
    </div>
  );
}
```

---

### Grupo 4: Guards de Autenticación (`src/components/auth/`)

**Responsabilidad:** Proteger rutas según autenticación y permisos

**Guards actuales:**
```
src/components/auth/
├── ProtectedRoute.tsx      # Requiere autenticación
├── RoleGuard.tsx           # Requiere roles específicos
└── SystemAdminRoute.tsx    # Requiere ser system admin
```

**NO modificar estos componentes a menos que sea absolutamente necesario.**

---

### Grupo 5: Servicios (`src/services/`)

**Responsabilidad:** Comunicación con el backend (API calls)

**Servicios actuales:**
```
src/services/
├── api.ts                    # Cliente HTTP base
├── authService.ts           # Login, logout, refresh
├── tenantService.ts         # Operaciones del tenant
├── systemAdminService.ts    # Operaciones de system admin
├── auditService.ts          # Logs de auditoría
├── subscriptionService.ts   # Suscripciones
└── config.ts                # Configuración
```

**Cuándo crear un nuevo servicio:**
- Necesitas comunicarte con un nuevo endpoint del backend
- Quieres agrupar lógica de API relacionada

**Ejemplo:**
```tsx
// src/services/myService.ts
import api from './api';

export const myService = {
  async getData() {
    const response = await api.get<DataDto>('/api/my-endpoint');
    return response.data;
  },

  async createData(dto: CreateDataDto) {
    const response = await api.post<DataDto>('/api/my-endpoint', dto);
    return response.data;
  },
};
```

---

### Grupo 6: Tipos (`src/types/`)

**Responsabilidad:** Definiciones de TypeScript

**Archivo principal:** `src/types/api.ts`

**Cuándo agregar tipos:**
- Recibes un DTO del backend que no está tipado
- Necesitas tipos para props de componentes complejos
- Quieres inferencia de tipos en servicios

**Ejemplo:**
```tsx
// En src/types/api.ts
export interface MyNewDto {
  id: number;
  name: string;
  createdAt: string;
}
```

---

## 3. MATRIZ DE DECISIÓN: "SI AGREGAS X, DEBES MODIFICAR Y"

| SI AGREGAS...                     | DEBES MODIFICAR...                                         |
|-----------------------------------|------------------------------------------------------------|
| Nueva página                      | `App.tsx` (import + route)                                |
| Página con autenticación          | `App.tsx` (wrap con `<ProtectedRoute>`)                  |
| Página con roles                  | `App.tsx` (wrap con `<RoleGuard>`)                       |
| Página de system admin            | `App.tsx` (wrap con `<SystemAdminRoute>`)                |
| Nuevo componente UI               | Crear en `components/ui/` + exportar                      |
| Nuevo layout                      | Crear en `components/layout/` + usar en páginas          |
| Nuevo servicio API                | Crear en `services/` + importar en páginas               |
| Nuevo tipo/DTO                    | Agregar en `types/api.ts`                                 |
| Nueva ruta en el navbar           | Modificar `AuthLayout.tsx` o `SystemAdminLayout.tsx`     |

---

## 4. PATRONES DE CÓDIGO OBLIGATORIOS

### 4.1. Estructura de una página típica

```tsx
import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { myService } from '../services/myService';
import { Card, CardHeader, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Plus, Edit, Trash2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import type { MyDto } from '../types/api';

export default function MyPage() {
  // 1. Contextos
  const { user, tenant } = useAuth();

  // 2. Estado
  const [data, setData] = useState<MyDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // 3. Efectos
  useEffect(() => {
    loadData();
  }, []);

  // 4. Funciones
  const loadData = async () => {
    try {
      setIsLoading(true);
      const result = await myService.getData();
      setData(result);
    } catch (error: any) {
      toast.error(error.message || 'Error al cargar datos');
    } finally {
      setIsLoading(false);
    }
  };

  // 5. Renderizado condicional (loading)
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      </div>
    );
  }

  // 6. Renderizado principal
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Mi Página</h1>
          <p className="text-gray-600 mt-2">Descripción</p>
        </div>
        <Button icon={Plus} onClick={handleCreate}>
          Crear Nuevo
        </Button>
      </div>

      {/* Content */}
      <Card>
        <CardHeader>
          <h3 className="font-semibold text-gray-900">Datos</h3>
        </CardHeader>
        <CardBody>
          {/* Tu contenido aquí */}
        </CardBody>
      </Card>
    </div>
  );
}
```

### 4.2. Manejo de errores

```tsx
// ❌ MAL - No mostrar errores al usuario
try {
  await myService.createData(data);
} catch (error) {
  console.error(error);
}

// ✅ BIEN - Mostrar toast con el error
try {
  await myService.createData(data);
  toast.success('Creado exitosamente');
} catch (error: any) {
  toast.error(error.message || 'Error al crear');
}
```

### 4.3. Loading states

```tsx
// ✅ BIEN - Mostrar loader mientras carga
if (isLoading) {
  return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
    </div>
  );
}
```

### 4.4. Empty states

```tsx
// ✅ BIEN - Mostrar mensaje cuando no hay datos
{data.length === 0 ? (
  <div className="text-center py-12 text-gray-500">
    <p>No hay datos disponibles</p>
  </div>
) : (
  <table>...</table>
)}
```

---

## 5. ESTILOS Y TAILWIND CSS

### 5.1. Clases Tailwind comunes

**Espaciado:**
- `space-y-6` - espaciado vertical entre elementos
- `gap-4` - espaciado en flex/grid
- `px-6 py-4` - padding horizontal y vertical

**Colores:**
- `text-gray-900` - texto principal
- `text-gray-600` - texto secundario
- `bg-white` - fondo blanco
- `bg-gray-50` - fondo gris claro
- `border-gray-200` - bordes

**Tipografía:**
- `text-3xl font-bold` - títulos principales
- `text-lg font-semibold` - subtítulos
- `text-sm` - texto pequeño

**Botones (via componente Button):**
```tsx
<Button variant="primary">Guardar</Button>
<Button variant="outline">Cancelar</Button>
<Button variant="danger">Eliminar</Button>
```

### 5.2. Patrones de layout

**Container principal:**
```tsx
<div className="max-w-7xl mx-auto px-6 py-8">
  {/* Contenido */}
</div>
```

**Grid responsive:**
```tsx
<div className="grid grid-cols-1 md:grid-cols-3 gap-6">
  {/* Cards */}
</div>
```

**Flex row:**
```tsx
<div className="flex items-center justify-between">
  <div>{/* Izquierda */}</div>
  <div>{/* Derecha */}</div>
</div>
```

---

## 6. DEBUGGING Y TROUBLESHOOTING

### Problema: Página en blanco

**Checklist de diagnóstico:**

1. ✅ ¿El componente está exportado con `export default`?
2. ✅ ¿La ruta está registrada en `App.tsx`?
3. ✅ ¿El import en `App.tsx` es correcto?
4. ✅ ¿Hay errores en la consola del navegador?
5. ✅ ¿Hay errores en la terminal de npm?

**Solución:**
```bash
# 1. Verificar que el build funciona
npm run build

# 2. Revisar consola del navegador (F12)
# 3. Buscar el componente en React DevTools
```

### Problema: Componente no se importa

**Error típico:**
```
Module not found: Can't resolve './pages/MyPage'
```

**Solución:**
- Verifica que el archivo existe en la ruta correcta
- Verifica que tiene `export default`
- Verifica que el nombre coincide (case-sensitive)

### Problema: Props undefined

**Error típico:**
```
Cannot read property 'map' of undefined
```

**Solución:**
- Inicializa el estado con un valor por defecto
```tsx
const [data, setData] = useState<MyDto[]>([]); // ✅ Array vacío
```

---

## 7. CONVENCIONES DE CÓDIGO

### 7.1. Nombres de archivos y componentes

| Tipo                | Convención          | Ejemplo                    |
|---------------------|---------------------|----------------------------|
| Páginas             | PascalCase + Page   | `EmpleadosPage.tsx`       |
| Componentes UI      | PascalCase          | `Button.tsx`              |
| Servicios           | camelCase + Service | `authService.ts`          |
| Tipos               | PascalCase + Dto    | `CreateEmployeeDto`       |
| Hooks personalizados| use + PascalCase    | `useAuth.ts`              |

### 7.2. Orden de imports

```tsx
// 1. React y librerías externas
import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';

// 2. Componentes propios
import { Button } from '../components/ui/Button';
import AuthLayout from '../components/layout/AuthLayout';

// 3. Contextos y hooks
import { useAuth } from '../contexts/AuthContext';

// 4. Servicios
import { myService } from '../services/myService';

// 5. Tipos
import type { MyDto } from '../types/api';

// 6. Iconos
import { Plus, Edit } from 'lucide-react';

// 7. Otros
import toast from 'react-hot-toast';
```

### 7.3. Comentarios en español

```tsx
// ✅ BIEN - Comentarios en español
// Cargar datos del servidor
const loadData = async () => { ... };

// ❌ MAL - Comentarios en inglés
// Load data from server
const loadData = async () => { ... };
```

---

## 8. VALIDACIÓN FINAL (CHECKLIST COMPLETO)

Antes de considerar una página "completa", verifica:

### Desarrollo
- [ ] Archivo creado en `src/pages/` con nombre correcto
- [ ] Componente exportado con `export default`
- [ ] Import agregado en `App.tsx`
- [ ] Ruta agregada en `<Routes>` de `App.tsx`
- [ ] Protección de ruta aplicada (ProtectedRoute, RoleGuard, etc.)
- [ ] Layout correcto aplicado (AuthLayout o SystemAdminLayout)

### Funcionalidad
- [ ] Estado inicial definido
- [ ] Loading state implementado
- [ ] Error handling con toast
- [ ] Empty states manejados
- [ ] Servicios API llamados correctamente

### Estilos
- [ ] Usa componentes UI (`Button`, `Card`, etc.)
- [ ] Usa clases Tailwind correctamente
- [ ] Layout responsive (funciona en móvil y desktop)
- [ ] Iconos de Lucide React usados apropiadamente

### Testing
- [ ] `npm run build` ejecuta sin errores
- [ ] Página carga en el navegador
- [ ] No hay errores en la consola del navegador
- [ ] No hay warnings de TypeScript
- [ ] React DevTools muestra el componente correctamente

---

## 9. REFERENCIA RÁPIDA

### Crear nueva página paso a paso

```bash
# 1. Crear archivo
cd src/UI/Planilla.Web/ClientApp/src/pages
# Crear MiPaginaPage.tsx con la estructura base

# 2. Editar App.tsx
# - Agregar import
# - Agregar route

# 3. Build
npm run build

# 4. Validar
npm run dev
# Abrir http://localhost:5173/mi-ruta
```

### Comandos útiles

```bash
# Build de producción
npm run build

# Desarrollo con hot reload
npm run dev

# Linting
npm run lint

# Type checking
npx tsc --noEmit
```

---

## 10. RESPONSABILIDADES DEL FRONTEND SPECIALIST

Como **planilla-frontend-specialist**, DEBES:

1. ✅ Seguir TODAS las reglas de este documento al pie de la letra
2. ✅ Validar que cada página nueva funcione en el navegador
3. ✅ Mantener consistencia con páginas existentes
4. ✅ Usar componentes UI existentes antes de crear nuevos
5. ✅ Escribir código limpio, tipado y comentado en español
6. ✅ Manejar errores apropiadamente con toast notifications
7. ✅ Implementar loading y empty states
8. ✅ Asegurar que el código compila sin errores ni warnings

---

## CONCLUSIÓN

Este documento es la **fuente de verdad** para el desarrollo frontend en Planilla. Cualquier violación de estas reglas puede resultar en páginas en blanco, errores de compilación o problemas de integración.

**IMPORTANTE:** Si tienes dudas, consulta páginas existentes que funcionan correctamente como referencia (ConfiguracionPage.jsx, EmpleadosPage.jsx, AdminDashboardPage.tsx).

---

**Última actualización:** 2026-01-30
**Versión:** 1.0.0
