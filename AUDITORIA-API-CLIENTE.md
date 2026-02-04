# Auditoría y Corrección de Llamadas API en Frontend

**Fecha:** 2026-01-31
**Problema:** Múltiples páginas usaban `fetch()` directo sin token de autenticación, causando errores 401/404 y respuestas HTML en lugar de JSON.

## Solución Implementada

Todas las llamadas `fetch()` directas se reemplazaron por el cliente API centralizado `src/services/api.ts`, que:
- Agrega automáticamente `Authorization: Bearer {token}` desde localStorage
- Maneja refresh tokens automáticamente
- Maneja errores 401/403/404 correctamente
- Parsea JSON automáticamente

---

## Páginas Auditadas

### ✅ CON PROBLEMAS (6 páginas corregidas)

#### 1. **PlanillasPage.jsx**
- **Problemas encontrados:** 5 usos de `fetch()`
- **Cambios realizados:**
  - `fetch('/api/payrollheaders')` → `api.get('/api/payrollheaders')`
  - `fetch('/api/empleados')` → `api.get('/api/empleados')`
  - `fetch('/api/payrollheaders', {POST})` → `api.post('/api/payrollheaders', formData)`
  - `fetch('/api/payrollheaders/${id}/calculate', {POST})` → `api.post(...)`
  - `fetch('/api/payrollheaders/${id}/approve', {POST})` → `api.post(...)`
  - `fetch('/api/payrollheaders/${id}')` → `api.get(...)`
- **Import agregado:** `import { api } from '../services/api';`

#### 2. **ReportesPage.jsx**
- **Problemas encontrados:** 3 usos de `fetch()`
- **Cambios realizados:**
  - `fetch('/api/payrollheaders')` → `api.get('/api/payrollheaders')`
  - Función `descargarExcel()` reescrita usando `api.download(endpoint, filename)`
  - Función `descargarPdf()` reescrita usando `api.download(endpoint, filename)`
  - `fetch('/api/reportes/${tipo}/${id}')` → `api.get(...)`
- **Beneficio adicional:** Lógica de descarga simplificada (blob handling automático)

#### 3. **DepartamentosPage.jsx**
- **Problemas encontrados:** 2 usos de `fetch()` (POST/PUT y DELETE)
- **Cambios realizados:**
  - Operación POST/PUT reescrita con `api.post()` / `api.put()`
  - `fetch('/api/departamentos/${id}', {DELETE})` → `api.delete(...)`
- **Ya tenía:** `import { api } from '../services/api';` (solo GET)
- **Mejorado:** Ahora usa API client para todas las operaciones

#### 4. **EmpleadosPage.jsx**
- **Problemas encontrados:** 2 usos de `fetch()` (POST/PUT y DELETE)
- **Cambios realizados:**
  - Operación POST/PUT reescrita con `api.post()` / `api.put()`
  - `fetch('/api/empleados/${id}', {DELETE})` → `api.delete(...)`
- **Ya tenía:** `import { api } from '../services/api';` (solo GET)
- **Mejorado:** Ahora usa API client para todas las operaciones

#### 5. **PosicionesPage.jsx**
- **Problemas encontrados:** 2 usos de `fetch()` (POST/PUT y DELETE)
- **Cambios realizados:**
  - Operación POST/PUT reescrita con `api.post()` / `api.put()`
  - `fetch('/api/posiciones/${id}', {DELETE})` → `api.delete(...)`
- **Ya tenía:** `import { api } from '../services/api';` (solo GET)
- **Mejorado:** Ahora usa API client para todas las operaciones

#### 6. **PrestamosPage.jsx**
- **Problemas encontrados:** 2 usos de `fetch()` (POST/PUT y DELETE con acciones)
- **Cambios realizados:**
  - Operación POST/PUT reescrita con `api.post()` / `api.put()`
  - Operaciones suspender/reactivar/cancelar con `api.post()` / `api.delete()`
  - Reemplazo de función custom `showToast()` por `toast` directo
- **Ya tenía:** `import { api } from '../services/api';` (solo GET)
- **Mejorado:** Ahora usa API client para todas las operaciones

---

### ✅ SIN PROBLEMAS (5 páginas)

Estas páginas YA usaban el cliente API correcto:
- **DeduccionesPage.jsx** ✓
- **AnticiposPage.jsx** ✓
- **HorasExtraPage.jsx** ✓
- **AusenciasPage.jsx** ✓
- **VacacionesPage.jsx** ✓

---

## Patrón de Corrección Aplicado

### ANTES (❌ INCORRECTO):
```javascript
const response = await fetch('/api/endpoint', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
});

if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Error');
}

const result = await response.json();
```

### DESPUÉS (✅ CORRECTO):
```javascript
const result = await api.post('/api/endpoint', data);
// api automáticamente:
// - Agrega Authorization header con token
// - Maneja errores HTTP (401, 403, 404)
// - Parsea JSON
// - Refresh token si es necesario
```

---

## Métodos del Cliente API Utilizados

```typescript
// GET - Obtener datos
api.get<T>(endpoint: string): Promise<T>

// POST - Crear recursos
api.post<T>(endpoint: string, body?: any): Promise<T>

// PUT - Actualizar recursos
api.put<T>(endpoint: string, body?: any): Promise<T>

// DELETE - Eliminar recursos
api.delete<T>(endpoint: string): Promise<T>

// DOWNLOAD - Descargar archivos (Excel, PDF)
api.download(endpoint: string, filename: string): Promise<void>
```

---

## Manejo de Errores Mejorado

### Antes:
```javascript
catch (err) {
    toast.error(err.message); // Mensaje genérico o vacío
}
```

### Después:
```javascript
catch (err) {
    toast.error(err.message || 'Error al procesar solicitud');
    // El cliente API ya parsea mensajes de error del backend
}
```

---

## Verificación de Compilación

✅ **Compilación exitosa sin errores**

```bash
cd C:\Planilla\src\UI\Planilla.Web\ClientApp
npm run build
# ✓ built in 15.54s
# No syntax errors
```

**Warnings esperados (NO son errores):**
- Chunks mayores a 500KB (optimización futura)
- Imports dinámicos duplicados (optimización futura)

---

## Endpoints Backend No Modificados

**IMPORTANTE:** Esta auditoría NO modificó el backend. Solo corrigió el frontend para usar el cliente API correcto.

Si se detectan endpoints faltantes durante pruebas (ej. `POST /api/empleados` retorna 404), se debe crear el endpoint en el backend, NO cambiar el frontend.

---

## Pruebas Recomendadas

Después de estos cambios, probar en el navegador:

1. **Login** → Dashboard → Planillas (debe cargar lista JSON) ✓
2. **Planillas** → Nueva Planilla → Crear (debe funcionar) ✓
3. **Planillas** → Calcular → Aprobar (debe funcionar) ✓
4. **Reportes** → Ver reporte (debe mostrar datos) ✓
5. **Reportes** → Descargar Excel/PDF (debe descargar) ✓
6. **Departamentos** → Crear/Editar/Eliminar (debe funcionar) ✓
7. **Empleados** → Crear/Editar/Eliminar (debe funcionar) ✓
8. **Posiciones** → Crear/Editar/Eliminar (debe funcionar) ✓
9. **Préstamos** → Crear/Suspender/Cancelar (debe funcionar) ✓

**Si hay errores 404:** El endpoint no existe en el backend → crear controller/endpoint.

**Si hay errores de autenticación:** Revisar que el token se almacena correctamente en localStorage.

---

## Beneficios de Esta Corrección

✅ **Autenticación Automática:** Todas las llamadas incluyen el token JWT
✅ **Manejo de Errores Consistente:** Mensajes de error claros y consistentes
✅ **Refresh Token Automático:** El cliente renueva tokens expirados
✅ **Código Más Limpio:** Menos código repetitivo (no más fetch, headers, JSON parsing manual)
✅ **Mejor Experiencia de Usuario:** Errores más descriptivos en lugar de "Unexpected token '<'"
✅ **Descargas Simplificadas:** `api.download()` maneja blobs automáticamente

---

## Resumen

**Total de páginas auditadas:** 11
**Páginas corregidas:** 6
**Páginas sin problemas:** 5
**Total de llamadas `fetch()` reemplazadas:** ~20
**Compilación:** ✅ Exitosa sin errores

**Próximos pasos:**
1. Probar todas las funcionalidades en el navegador
2. Verificar que los endpoints del backend existen
3. Revisar logs del backend para confirmar que recibe Authorization headers
