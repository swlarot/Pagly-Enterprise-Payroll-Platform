# AuthContext - Helpers de Permisos Frontend

**Fecha**: 2026-02-01
**Commit**: `b18a296`
**Tipo**: Modificación de Context
**Archivo**: `src/UI/Planilla.Web/ClientApp/src/contexts/AuthContext.tsx`

## Propósito

Agregar funciones helper al contexto de autenticación para verificar permisos de forma consistente en todos los componentes React, evitando lógica duplicada y mejorando la UX con manejo correcto de estados de carga.

## Problema que Resuelve

**Antes**:
- Cada componente tenía que verificar `user.role` manualmente
- Lógica de permisos duplicada en múltiples componentes
- Errores cuando `user` era `undefined` durante carga inicial
- No había forma consistente de verificar permisos de escritura/eliminación

**Después**:
- Funciones helper centralizadas: `hasRole()`, `canWrite()`, `canDelete()`, `isReadOnly()`
- Manejo automático de estados de carga
- Defensive validation para evitar errores de `undefined`
- Código de componentes más limpio y legible

## Funciones Agregadas

### 1. hasRole() - Mejorado con Loading State

#### Antes
```typescript
const hasRole = (...roles: TenantRole[]): boolean => {
  if (!user) return false;
  return roles.includes(user.role);
};
```

**Problema**: Durante carga inicial, `user` es `null` pero `isLoading` es `true`. Esto causaba que componentes renderizaran contenido antes de validar permisos.

#### Después
```typescript
const hasRole = (...roles: TenantRole[]): boolean => {
  // Retornar false si no hay usuario O si está cargando
  if (!user || isLoading) return false;
  return roles.includes(user.role);
};
```

**Mejora**: Ahora verifica también `isLoading`, evitando renders prematuros.

**Uso**:
```typescript
// En componente
const { hasRole } = useAuth();

return (
  <div>
    {hasRole(TenantRole.Owner, TenantRole.Admin) && (
      <button>Configuración Avanzada</button>
    )}
  </div>
);
```

### 2. canWrite() - Nuevo

```typescript
const canWrite = (): boolean => {
  if (!user || isLoading) return false;
  // Owner, Admin y Manager pueden crear y editar
  return user.role === TenantRole.Owner
      || user.role === TenantRole.Admin
      || user.role === TenantRole.Manager;
};
```

**Permisos**:
- ✅ Owner
- ✅ Admin
- ✅ Manager
- ❌ Accountant (solo lectura)
- ❌ Employee (solo lectura)

**Uso**:
```typescript
const { canWrite } = useAuth();

return (
  <div>
    {canWrite() && (
      <Button onClick={handleCreate}>
        <Plus className="w-4 h-4" />
        Crear Empleado
      </Button>
    )}
  </div>
);
```

### 3. canDelete() - Nuevo

```typescript
const canDelete = (): boolean => {
  if (!user || isLoading) return false;
  // Solo Owner y Admin pueden eliminar
  return user.role === TenantRole.Owner || user.role === TenantRole.Admin;
};
```

**Permisos**:
- ✅ Owner
- ✅ Admin
- ❌ Manager (puede editar, no eliminar)
- ❌ Accountant
- ❌ Employee

**Uso**:
```typescript
const { canDelete } = useAuth();

return (
  <div>
    {canDelete() && (
      <Button
        onClick={handleDelete}
        className="bg-red-500 hover:bg-red-600"
      >
        <Trash2 className="w-4 h-4" />
        Eliminar
      </Button>
    )}
  </div>
);
```

### 4. isReadOnly() - Nuevo

```typescript
const isReadOnly = (): boolean => {
  if (!user || isLoading) return true; // ⬅️ Default seguro: true
  // Accountant y Employee son solo lectura
  return user.role === TenantRole.Accountant || user.role === TenantRole.Employee;
};
```

**Permisos**:
- ✅ Accountant (solo lectura)
- ✅ Employee (solo lectura)
- ❌ Manager (puede escribir)
- ❌ Admin (puede escribir)
- ❌ Owner (puede escribir)

**Uso**:
```typescript
const { isReadOnly } = useAuth();

return (
  <td>
    {!isReadOnly() ? (
      <div className="flex gap-2">
        <button>Editar</button>
        <button>Eliminar</button>
      </div>
    ) : (
      <span className="text-sm text-gray-400 italic">
        Solo lectura
      </span>
    )}
  </td>
);
```

## Interface Actualizada

```typescript
interface AuthContextType {
  // Propiedades existentes
  user: UserInfoDto | null;
  tenant: TenantInfoDto | null;
  subscription: SubscriptionInfoDto | null;
  availableTenants: TenantSummaryDto[];
  isAuthenticated: boolean;
  isSystemAdmin: boolean;
  isLoading: boolean;

  // Métodos existentes
  login: (email: string, password: string) => Promise<{
    requiresTenantSelection: boolean;
    availableTenants?: TenantSummaryDto[];
  }>;
  selectTenant: (tenantId: number) => Promise<void>;
  logout: () => void;
  acceptInvite: (token: string, password: string, confirmPassword: string) => Promise<void>;
  canAccessFeature: (feature: keyof SubscriptionInfoDto) => boolean;

  // ⬅️ NUEVOS HELPERS DE PERMISOS
  hasRole: (...roles: TenantRole[]) => boolean;
  canWrite: () => boolean;
  canDelete: () => boolean;
  isReadOnly: () => boolean;
}
```

## Defensive Validation - Patrón Aplicado

Todas las funciones siguen el mismo patrón defensivo:

```typescript
const somePermissionCheck = (): boolean => {
  // 1. Verificar que user existe
  if (!user) return false; // O true si es más seguro

  // 2. Verificar que no está cargando
  if (isLoading) return false;

  // 3. Lógica de negocio
  return /* condición de permiso */;
};
```

**Ventajas**:
1. No hay errores de `Cannot read property 'role' of undefined`
2. Durante carga, permisos están denegados (fail-safe)
3. Componentes pueden usar helpers sin try/catch

## Matriz de Permisos

| Rol | hasRole | canWrite | canDelete | isReadOnly |
|-----|---------|----------|-----------|------------|
| **Owner** | ✅ | ✅ | ✅ | ❌ |
| **Admin** | ✅ | ✅ | ✅ | ❌ |
| **Manager** | ✅ | ✅ | ❌ | ❌ |
| **Accountant** | ✅ | ❌ | ❌ | ✅ |
| **Employee** | ✅ | ❌ | ❌ | ✅ |
| **Loading...** | ❌ | ❌ | ❌ | ✅ |
| **No autenticado** | ❌ | ❌ | ❌ | ✅ |

## Ejemplos de Uso en Componentes

### Ejemplo 1: Botones Condicionales
```typescript
import { useAuth } from '../contexts/AuthContext';

function EmpleadosPage() {
  const { canWrite, canDelete } = useAuth();

  return (
    <div>
      {canWrite() && (
        <Button onClick={handleCreate}>Crear Empleado</Button>
      )}

      <table>
        {empleados.map(emp => (
          <tr key={emp.id}>
            <td>{emp.nombre}</td>
            <td>
              {canWrite() && <Button onClick={() => handleEdit(emp)}>Editar</Button>}
              {canDelete() && <Button onClick={() => handleDelete(emp.id)}>Eliminar</Button>}
            </td>
          </tr>
        ))}
      </table>
    </div>
  );
}
```

### Ejemplo 2: Mensaje "Solo Lectura"
```typescript
function DeduccionesPage() {
  const { isReadOnly, canWrite, canDelete } = useAuth();

  return (
    <div>
      <table>
        <tbody>
          {deducciones.map(ded => (
            <tr key={ded.id}>
              <td>{ded.nombre}</td>
              <td>
                {!isReadOnly() ? (
                  <div className="flex gap-2">
                    {canWrite() && <Button>Editar</Button>}
                    {canDelete() && <Button>Eliminar</Button>}
                  </div>
                ) : (
                  <span className="text-sm text-gray-400 italic">
                    Solo lectura
                  </span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

### Ejemplo 3: Roles Específicos
```typescript
function ConfiguracionPage() {
  const { hasRole } = useAuth();

  return (
    <div>
      {hasRole(TenantRole.Owner, TenantRole.Admin) && (
        <Link to="/users">Gestión de Usuarios</Link>
      )}

      {hasRole(TenantRole.Owner, TenantRole.Admin, TenantRole.Manager, TenantRole.Accountant) && (
        <Link to="/audit">Audit Log</Link>
      )}
    </div>
  );
}
```

### Ejemplo 4: Combining Multiple Checks
```typescript
function PlanillasPage() {
  const { canWrite, canDelete, hasRole, user } = useAuth();

  const canApprove = hasRole(TenantRole.Owner, TenantRole.Admin, TenantRole.Manager);

  return (
    <div>
      {canWrite() && <Button>Crear Planilla</Button>}

      {planillas.map(planilla => (
        <div key={planilla.id}>
          <h3>{planilla.periodo}</h3>

          {canWrite() && planilla.status === 'Draft' && (
            <Button onClick={() => handleEdit(planilla.id)}>Editar</Button>
          )}

          {canApprove && planilla.status === 'Pending' && (
            <Button onClick={() => handleApprove(planilla.id)}>Aprobar</Button>
          )}

          {canDelete() && (
            <Button onClick={() => handleDelete(planilla.id)}>Eliminar</Button>
          )}
        </div>
      ))}
    </div>
  );
}
```

## Fix del Bug: "Cannot read properties of undefined (reading 'filter')"

### Error Original
```typescript
// En ConfiguracionPage.jsx
const allTabs = [
  {
    id: 'usuarios',
    label: 'Usuarios',
    visible: hasRole(TenantRole.Owner, TenantRole.Admin) // ❌ hasRole undefined durante carga
  }
];

const tabs = allTabs.filter(tab => tab.visible); // ❌ Error aquí
```

### Fix Aplicado
```typescript
// Opción 1: Defensive check
const allTabs = [
  {
    id: 'usuarios',
    label: 'Usuarios',
    visible: hasRole ? hasRole(TenantRole.Owner, TenantRole.Admin) : false
  }
];

// Opción 2: Usar isLoading en hasRole (implementado)
const hasRole = (...roles: TenantRole[]): boolean => {
  if (!user || isLoading) return false; // ⬅️ Ahora retorna false durante carga
  return roles.includes(user.role);
};
```

## Testing

### Unit Test - hasRole
```typescript
describe('AuthContext hasRole', () => {
  it('should return false when user is null', () => {
    const { result } = renderHook(() => useAuth(), {
      wrapper: ({ children }) => (
        <AuthProvider value={{ user: null, isLoading: false }}>
          {children}
        </AuthProvider>
      )
    });

    expect(result.current.hasRole(TenantRole.Admin)).toBe(false);
  });

  it('should return false when loading', () => {
    const { result } = renderHook(() => useAuth(), {
      wrapper: ({ children }) => (
        <AuthProvider value={{ user: mockUser, isLoading: true }}>
          {children}
        </AuthProvider>
      )
    });

    expect(result.current.hasRole(TenantRole.Admin)).toBe(false);
  });

  it('should return true when user has role', () => {
    const mockUser = { role: TenantRole.Admin };
    const { result } = renderHook(() => useAuth(), {
      wrapper: ({ children }) => (
        <AuthProvider value={{ user: mockUser, isLoading: false }}>
          {children}
        </AuthProvider>
      )
    });

    expect(result.current.hasRole(TenantRole.Admin)).toBe(true);
  });
});
```

### Unit Test - canWrite
```typescript
describe('AuthContext canWrite', () => {
  it('should return true for Owner', () => {
    const mockUser = { role: TenantRole.Owner };
    const { result } = renderHook(() => useAuth(), {
      wrapper: createWrapper({ user: mockUser, isLoading: false })
    });

    expect(result.current.canWrite()).toBe(true);
  });

  it('should return true for Manager', () => {
    const mockUser = { role: TenantRole.Manager };
    const { result } = renderHook(() => useAuth(), {
      wrapper: createWrapper({ user: mockUser, isLoading: false })
    });

    expect(result.current.canWrite()).toBe(true);
  });

  it('should return false for Employee', () => {
    const mockUser = { role: TenantRole.Employee };
    const { result } = renderHook(() => useAuth(), {
      wrapper: createWrapper({ user: mockUser, isLoading: false })
    });

    expect(result.current.canWrite()).toBe(false);
  });
});
```

## Performance Considerations

### Memoización (Opcional)
```typescript
// Si hasRole se llama frecuentemente en renders
const hasRole = useMemo(() => {
  return (...roles: TenantRole[]): boolean => {
    if (!user || isLoading) return false;
    return roles.includes(user.role);
  };
}, [user, isLoading]);
```

**Nota**: Probablemente innecesario ya que las funciones son simples y rápidas.

### Evitar Re-renders Innecesarios
```typescript
// En componentes que solo leen permisos
const MemoizedButton = React.memo(({ canWrite }: { canWrite: boolean }) => {
  if (!canWrite) return null;
  return <Button>Crear</Button>;
});

// Uso
<MemoizedButton canWrite={canWrite()} />
```

## Ventajas

1. **Consistencia**: Misma lógica de permisos en toda la app
2. **Mantenibilidad**: Cambios en lógica de permisos se hacen una vez
3. **Legibilidad**: `canWrite()` es más claro que `user?.role === TenantRole.Owner || ...`
4. **Seguridad**: Defensive validation previene errores runtime
5. **DRY**: No duplicar lógica de permisos

## Próximos Pasos

1. Aplicar helpers en todos los componentes existentes
2. Agregar `canApprove()` para planillas y vacaciones
3. Agregar `canExport()` según plan de suscripción
4. Crear hook `usePermissions()` con caché para performance
5. Documentar patrones de uso en guía de desarrollo

---

**Impacto**: Alto - Usado en todos los componentes
**Complejidad**: Baja - Funciones simples
**Prioridad**: Crítica - Base para UI permissions
