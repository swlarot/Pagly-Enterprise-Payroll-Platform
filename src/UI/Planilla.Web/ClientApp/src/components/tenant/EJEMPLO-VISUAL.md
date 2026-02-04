# PlanUsageCard - Ejemplo Visual

## Diseño del Componente

El componente `PlanUsageCard` se renderiza como una tarjeta vertical con las siguientes secciones:

```
┌─────────────────────────────────────────────┐
│ ░░░░░░░░░░░░░░░░░ HEADER ░░░░░░░░░░░░░░░░  │
│ ┌─────────────────────────────────────────┐ │
│ │ 👑 Plan Profesional    [Actualizar Plan]│ │
│ │ $79.99/mes                              │ │
│ └─────────────────────────────────────────┘ │
│                                             │
│ ⚠️ Has alcanzado el 90% del límite de      │
│    empleados. Considera actualizar tu plan.│
│                                             │
├─────────────────────────────────────────────┤
│ 📊 BARRAS DE USO                            │
│ ┌─────────────────────────────────────────┐ │
│ │ 👥 Empleados              45 / 100      │ │
│ │ ████████████░░░░░░░░░░░░  45%          │ │
│ │ 55 disponibles                          │ │
│ │                                         │ │
│ │ ✓ Usuarios                 7 / 10       │ │
│ │ 5 activos, 2 pendientes                 │ │
│ │ ████████████████░░░░░░░░  70%          │ │
│ │ 3 disponibles                           │ │
│ │                                         │ │
│ │ 🏢 Compañías                1 / 3       │ │
│ │ ██████████░░░░░░░░░░░░░░░  33%          │ │
│ │ 2 disponibles                           │ │
│ └─────────────────────────────────────────┘ │
│                                             │
├─────────────────────────────────────────────┤
│ ✨ CARACTERÍSTICAS DEL PLAN                 │
│ ┌─────────────────────────────────────────┐ │
│ │ ✅ Exportar Excel    ✅ Exportar PDF    │ │
│ │ ✅ Acceso API        ✅ Auditoría       │ │
│ │ ✅ Email Notif.      ✅ Soporte 24/7    │ │
│ │ ❌ Whitelabel        ❌ SSO             │ │
│ └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

## Estados del Componente

### 1. Estado: Loading

```
┌─────────────────────────────────────────────┐
│                                             │
│              🔄 Cargando...                 │
│                                             │
└─────────────────────────────────────────────┘
```

### 2. Estado: Plan Profesional (uso normal - < 75%)

```
┌─────────────────────────────────────────────┐
│ ████████ Plan Profesional ████████          │
│ $79.99/mes                                  │
│                                             │
│ 👥 Empleados: 45 / 100                      │
│ ████████████░░░░░░░░░░░░ 45% (AZUL)        │
│                                             │
│ ✓ Usuarios: 7 / 10                          │
│ ████████████████░░░░░░░░ 70% (AZUL)         │
│                                             │
│ 🏢 Compañías: 1 / 3                         │
│ ██████████░░░░░░░░░░░░░░ 33% (VERDE)        │
└─────────────────────────────────────────────┘
```

### 3. Estado: Advertencia (75-89% de uso)

```
┌─────────────────────────────────────────────┐
│ ████████ Plan Starter ████████              │
│ $29.99/mes                                  │
│                                             │
│ ⚠️ Estás cerca del límite de empleados     │
│                                             │
│ 👥 Empleados: 22 / 25                       │
│ ████████████████████░░░░ 88% (AMARILLO)     │
│                                             │
│ ✓ Usuarios: 2 / 3                           │
│ ████████████████░░░░░░░░ 67% (AZUL)         │
└─────────────────────────────────────────────┘
```

### 4. Estado: Crítico (>= 90% de uso)

```
┌─────────────────────────────────────────────┐
│ ████████ Plan Free ████████  [ACTUALIZAR]  │
│ $0.00/mes                                   │
│                                             │
│ ⚠️ Has alcanzado el límite de empleados.   │
│    Actualiza para agregar más.              │
│                                             │
│ 👥 Empleados: 5 / 5                         │
│ ████████████████████████ 100% (ROJO)        │
│                                             │
│ ✓ Usuarios: 1 / 1                           │
│ ████████████████████████ 100% (ROJO)        │
└─────────────────────────────────────────────┘
```

### 5. Estado: Plan Enterprise (ilimitado)

```
┌─────────────────────────────────────────────┐
│ ████████ Plan Enterprise ████████           │
│ $199.99/mes                                 │
│                                             │
│ 👥 Empleados: 250 / ∞                       │
│ ░░░░░░░░░░░░░░░░░░░░░░░░ 0% (Ilimitado)    │
│                                             │
│ ✓ Usuarios: 15 / ∞                          │
│ ░░░░░░░░░░░░░░░░░░░░░░░░ 0% (Ilimitado)    │
│                                             │
│ 🏢 Compañías: 5 / ∞                         │
│ ░░░░░░░░░░░░░░░░░░░░░░░░ 0% (Ilimitado)    │
└─────────────────────────────────────────────┘
```

## Colores por Porcentaje de Uso

| Porcentaje | Color      | Código Tailwind | Significado          |
|-----------|------------|-----------------|----------------------|
| 0-49%     | Verde      | `bg-green-500`  | Uso saludable        |
| 50-74%    | Azul       | `bg-blue-500`   | Uso moderado         |
| 75-89%    | Amarillo   | `bg-yellow-500` | Advertencia (cerca)  |
| 90-100%   | Rojo       | `bg-red-600`    | Crítico (límite)     |

## Características por Plan

### Plan Free ($0/mes)
```
✅ 5 Empleados
✅ 1 Usuario
✅ 1 Compañía
✅ Reportes Básicos (Web)
❌ Exportar Excel
❌ Exportar PDF
❌ Acceso API
❌ Notificaciones Email
❌ Auditoría
```

### Plan Starter ($29.99/mes)
```
✅ 25 Empleados
✅ 3 Usuarios
✅ 1 Compañía
✅ Exportar Excel
✅ Notificaciones Email
✅ Reportes Avanzados
❌ Exportar PDF
❌ Acceso API
❌ Auditoría
```

### Plan Professional ($79.99/mes)
```
✅ 100 Empleados
✅ 10 Usuarios
✅ 3 Compañías
✅ Exportar Excel
✅ Exportar PDF
✅ Acceso API
✅ Notificaciones Email
✅ Auditoría (1 año)
✅ Soporte Prioritario
```

### Plan Enterprise ($199.99/mes)
```
✅ Empleados Ilimitados
✅ Usuarios Ilimitados
✅ Compañías Ilimitadas
✅ Todas las características
✅ Exportar Excel + PDF
✅ Acceso API completo
✅ Auditoría permanente
✅ Soporte 24/7
✅ Gerente de cuenta dedicado
✅ Whitelabel (opcional)
✅ SSO (SAML/OAuth)
```

## Ejemplo de Respuesta API

```json
GET /api/tenant/plan-usage

{
  "planName": "Professional",
  "planDisplayName": "Plan Profesional",
  "monthlyPrice": 79.99,
  "limits": {
    "maxEmployees": 100,
    "maxUsers": 10,
    "maxCompanies": 3
  },
  "usage": {
    "activeEmployees": 45,
    "activeUsers": 5,
    "pendingInvitations": 2,
    "activeCompanies": 1
  },
  "remaining": {
    "employees": 55,
    "users": 3,
    "companies": 2,
    "employeesPercentage": 45.0,
    "usersPercentage": 70.0,
    "companiesPercentage": 33.33
  },
  "features": [
    {
      "featureName": "Exportar Excel",
      "isAvailable": true,
      "description": "Exportación de reportes CSS, SE, ISR a formato Excel"
    },
    {
      "featureName": "Exportar PDF",
      "isAvailable": true,
      "description": "Exportación de reportes a formato PDF profesional"
    },
    {
      "featureName": "Acceso API",
      "isAvailable": true,
      "description": "API REST para integración con sistemas externos"
    },
    {
      "featureName": "Notificaciones Email",
      "isAvailable": true,
      "description": "Alertas automáticas por email para eventos importantes"
    },
    {
      "featureName": "Registro de Auditoría",
      "isAvailable": true,
      "description": "Historial completo de cambios y accesos (retención 1 año)"
    },
    {
      "featureName": "Soporte Prioritario",
      "isAvailable": true,
      "description": "Respuesta garantizada en menos de 4 horas"
    },
    {
      "featureName": "Whitelabel",
      "isAvailable": false,
      "description": "Marca personalizada (solo Enterprise)"
    },
    {
      "featureName": "SSO",
      "isAvailable": false,
      "description": "Single Sign-On SAML/OAuth (solo Enterprise)"
    }
  ],
  "canInviteUsers": true,
  "canCreateEmployees": true,
  "canCreateCompanies": true,
  "shouldUpgrade": false,
  "upgradeMessage": null
}
```

## Escenarios de Uso

### Escenario 1: Usuario en Plan Free cerca del límite
```
shouldUpgrade: true
upgradeMessage: "Has usado 4 de 5 empleados disponibles. Actualiza a Starter para agregar hasta 25 empleados."
Botón: [Actualizar Plan] → visible y destacado en amarillo
```

### Escenario 2: Usuario que alcanzó el límite
```
shouldUpgrade: true
upgradeMessage: "Has alcanzado el límite de empleados en tu plan Free. Actualiza para continuar agregando empleados."
canCreateEmployees: false
Botón: [Actualizar Plan] → visible y pulsante en rojo/amarillo
```

### Escenario 3: Usuario cómodo en su plan
```
shouldUpgrade: false
upgradeMessage: null
Botón: Oculto
```

## Responsive Design

### Desktop (>= 1024px)
- Card ocupa 1/3 del ancho del grid
- Características en 2 columnas
- Todas las secciones visibles

### Tablet (768px - 1023px)
- Card ocupa ancho completo
- Características en 2 columnas
- Layout horizontal optimizado

### Mobile (< 768px)
- Card ocupa ancho completo
- Características en 1 columna
- Barras de progreso con menos padding
- Texto más compacto

## Interacciones

1. **Botón "Actualizar Plan"**: Navega a `/settings/subscription` (o muestra modal)
2. **Hover en Features**: Tooltip muestra `description` completa
3. **Click en alertas**: Expande información adicional (opcional)

## Próximas Mejoras

- [ ] Animaciones de barras de progreso al cargar
- [ ] Gráfico circular (donut chart) del uso total
- [ ] Comparación de planes en modal
- [ ] Historial de uso (últimos 30 días)
- [ ] Proyección de límite ("al ritmo actual, alcanzarás el límite en 45 días")
