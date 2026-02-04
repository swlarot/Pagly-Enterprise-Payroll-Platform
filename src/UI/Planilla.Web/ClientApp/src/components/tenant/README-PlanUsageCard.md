# PlanUsageCard Component

Componente React para mostrar información de uso del plan de suscripción del tenant actual.

## Ubicación
`src/components/tenant/PlanUsageCard.tsx`

## Características

✅ Muestra plan actual y precio mensual
✅ Barras de progreso visuales para:
   - Empleados activos
   - Usuarios activos + invitaciones pendientes
   - Compañías activas
✅ Colores semánticos según porcentaje de uso:
   - Verde: < 50%
   - Azul: 50-74%
   - Amarillo: 75-89%
   - Rojo: >= 90%
✅ Lista de características del plan (checkmarks visuales)
✅ Alerta de upgrade cuando se debe actualizar
✅ Botón "Actualizar Plan" visible cuando corresponde
✅ Loading state
✅ Maneja límites ilimitados (muestra "∞")
✅ Responsive (mobile y desktop)

## Uso Básico

```tsx
import PlanUsageCard from '../components/tenant/PlanUsageCard';

export default function MyPage() {
  return (
    <div className="p-6 space-y-6">
      <h1>Mi Dashboard</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Otros componentes */}

        <div className="lg:col-span-1">
          <PlanUsageCard />
        </div>
      </div>
    </div>
  );
}
```

## Integración en Dashboard Existente

### Opción 1: AdminDashboardPage.tsx

```tsx
// En src/pages/AdminDashboardPage.tsx
import PlanUsageCard from '../components/tenant/PlanUsageCard';

export default function AdminDashboardPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-gray-900">Panel de Control</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Columna principal con métricas */}
        <div className="lg:col-span-2 space-y-6">
          {/* Tus componentes de métricas aquí */}
        </div>

        {/* Sidebar con información del plan */}
        <div className="lg:col-span-1">
          <PlanUsageCard />
        </div>
      </div>
    </div>
  );
}
```

### Opción 2: Crear nueva página DashboardPage.tsx

```tsx
// En src/pages/DashboardPage.tsx
import React from 'react';
import PlanUsageCard from '../components/tenant/PlanUsageCard';
import { BarChart3, Users, FileText, Calendar } from 'lucide-react';

export default function DashboardPage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-gray-600 mt-2">Bienvenido a tu panel de control</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Métricas rápidas */}
        <div className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <StatCard
              icon={<Users className="w-8 h-8 text-blue-600" />}
              title="Empleados Activos"
              value="25"
              trend="+5 este mes"
            />
            <StatCard
              icon={<FileText className="w-8 h-8 text-green-600" />}
              title="Planillas Procesadas"
              value="12"
              trend="Este año"
            />
            <StatCard
              icon={<Calendar className="w-8 h-8 text-purple-600" />}
              title="Próxima Planilla"
              value="15 Feb"
              trend="En 5 días"
            />
            <StatCard
              icon={<BarChart3 className="w-8 h-8 text-amber-600" />}
              title="Total Pagado"
              value="$45,280"
              trend="Este mes"
            />
          </div>
        </div>

        {/* Plan Usage Card */}
        <div className="lg:col-span-1">
          <PlanUsageCard />
        </div>
      </div>
    </div>
  );
}

function StatCard({ icon, title, value, trend }: {
  icon: React.ReactNode;
  title: string;
  value: string;
  trend: string;
}) {
  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <p className="text-gray-600 text-sm font-medium">{title}</p>
          <p className="text-3xl font-bold text-gray-900 mt-2">{value}</p>
          <p className="text-gray-500 text-sm mt-1">{trend}</p>
        </div>
        <div className="ml-4">
          {icon}
        </div>
      </div>
    </div>
  );
}
```

## API Endpoint Requerido

El componente consume: `GET /api/tenant/plan-usage`

**Respuesta esperada:**
```json
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
    "companiesPercentage": 33.3
  },
  "features": [
    {
      "featureName": "Exportar Excel",
      "isAvailable": true,
      "description": "Exportación de reportes a Excel"
    },
    {
      "featureName": "Exportar PDF",
      "isAvailable": true,
      "description": "Exportación de reportes a PDF"
    }
  ],
  "canInviteUsers": true,
  "canCreateEmployees": true,
  "canCreateCompanies": true,
  "shouldUpgrade": false,
  "upgradeMessage": null
}
```

## Personalización

### Cambiar colores de progreso
Edita las funciones `getColorClass()` y `getTextColor()` en el componente.

### Agregar navegación al botón "Actualizar Plan"
Reemplaza el `onClick` del botón de upgrade:

```tsx
import { useNavigate } from 'react-router-dom';

// Dentro del componente:
const navigate = useNavigate();

// En el botón:
onClick={() => navigate('/settings/subscription')}
```

## Testing

```bash
# Build
cd src/UI/Planilla.Web/ClientApp
npm run build

# Dev mode
npm run dev
```

Verifica que:
- ✅ El componente carga sin errores
- ✅ Se muestra el nombre correcto del plan
- ✅ Las barras de progreso muestran los porcentajes correctos
- ✅ Los colores cambian según el umbral
- ✅ Las features muestran checkmarks verdes/grises
- ✅ El alert de upgrade aparece cuando shouldUpgrade es true

## Dependencias

- `lucide-react` - Iconos
- `react-hot-toast` - Notificaciones
- `react`, `react-dom` - Framework
- TailwindCSS - Estilos

## Notas

- El componente es **read-only** - no modifica datos
- Se auto-actualiza al montarse (useEffect)
- Maneja estados de loading y error
- Compatible con todos los planes (Free, Starter, Professional, Enterprise)
- Muestra "∞" para límites ilimitados (int.MaxValue = 2147483647)
