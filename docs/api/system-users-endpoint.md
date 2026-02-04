# Sistema de Usuarios - Endpoint de Listar Todos los Usuarios

## Descripción

Endpoint para listar TODOS los usuarios del sistema (no filtrado por tenant) con información completa de sus membresías en diferentes tenants.

**IMPORTANTE**: Este endpoint solo está disponible para usuarios con rol SystemAdmin y muestra usuarios de todos los tenants del sistema.

## Endpoint

```
GET /api/admin/system/users
```

## Autenticación

Requiere:
- JWT Bearer Token
- Usuario debe tener `IsSystemAdmin = true`
- Policy: `RequireSystemAdmin`

## Parámetros Query String

| Parámetro | Tipo | Requerido | Default | Descripción |
|-----------|------|-----------|---------|-------------|
| search | string | No | null | Búsqueda case-insensitive por email, nombre completo o username |
| page | int | No | 1 | Número de página (1-indexed) |
| pageSize | int | No | 20 | Tamaño de página (mín: 1, máx: 100) |

## Ejemplos de Uso

### 1. Listar todos los usuarios (primera página)

```bash
GET /api/admin/system/users
```

### 2. Buscar usuarios por email

```bash
GET /api/admin/system/users?search=maria@example.com
```

### 3. Buscar usuarios con paginación

```bash
GET /api/admin/system/users?search=admin&page=2&pageSize=10
```

## Respuesta Exitosa (200 OK)

### Estructura del Response

```typescript
interface SystemUserPagedResultDto {
  data: SystemUserDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

interface SystemUserDto {
  userId: string;
  email: string;
  fullName: string;
  createdAt: string; // ISO 8601
  isActive: boolean;
  isSystemAdmin: boolean;
  tenants: UserTenantMembershipDto[];
}

interface UserTenantMembershipDto {
  tenantId: number;
  tenantName: string;
  role: string; // "Owner", "Admin", "Manager", "Accountant", "Employee"
  joinedAt: string; // ISO 8601
  isActive: boolean;
  lastLoginAt?: string; // ISO 8601
}
```

### Ejemplo de Response JSON

```json
{
  "data": [
    {
      "userId": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
      "email": "admin@sistema.com",
      "fullName": "Administrador del Sistema",
      "createdAt": "2024-01-15T10:00:00Z",
      "isActive": true,
      "isSystemAdmin": true,
      "tenants": []
    },
    {
      "userId": "b2c3d4e5-f6g7-8901-2345-678901bcdefg",
      "email": "maria.perez@empresa-abc.com",
      "fullName": "María Pérez",
      "createdAt": "2024-02-01T14:30:00Z",
      "isActive": true,
      "isSystemAdmin": false,
      "tenants": [
        {
          "tenantId": 1,
          "tenantName": "Empresa ABC S.A.",
          "role": "Owner",
          "joinedAt": "2024-02-01T14:30:00Z",
          "isActive": true,
          "lastLoginAt": "2024-03-15T09:45:00Z"
        }
      ]
    },
    {
      "userId": "c3d4e5f6-g7h8-9012-3456-789012cdefgh",
      "email": "juan.rodriguez@consultora.com",
      "fullName": "Juan Rodríguez",
      "createdAt": "2024-01-20T11:15:00Z",
      "isActive": true,
      "isSystemAdmin": false,
      "tenants": [
        {
          "tenantId": 2,
          "tenantName": "Consultora XYZ",
          "role": "Admin",
          "joinedAt": "2024-01-20T11:15:00Z",
          "isActive": true,
          "lastLoginAt": "2024-03-14T16:20:00Z"
        },
        {
          "tenantId": 5,
          "tenantName": "Empresa DEF",
          "role": "Manager",
          "joinedAt": "2024-02-15T10:00:00Z",
          "isActive": true,
          "lastLoginAt": "2024-03-10T08:30:00Z"
        }
      ]
    },
    {
      "userId": "d4e5f6g7-h8i9-0123-4567-890123defghi",
      "email": "usuario.nuevo@email.com",
      "fullName": "Usuario Sin Tenant",
      "createdAt": "2024-03-10T09:00:00Z",
      "isActive": true,
      "isSystemAdmin": false,
      "tenants": []
    }
  ],
  "total": 47,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

## Casos de Uso

### 1. Reutilizar usuario existente al invitar a un tenant

Cuando un SystemAdmin quiere invitar un usuario a un tenant, primero puede buscar si el usuario ya existe en el sistema:

```bash
# Buscar si el usuario existe
GET /api/admin/system/users?search=maria@example.com

# Si existe, usar el userId para agregarlo a otro tenant
POST /api/admin/tenants/3/users
{
  "email": "maria@example.com",
  "fullName": "María Pérez",
  "password": "TempPassword123!",
  "role": 1 // Admin
}
```

### 2. Ver todos los tenants de un usuario

Útil para auditoría y gestión de accesos:

```bash
GET /api/admin/system/users?search=juan.rodriguez@consultora.com
```

El resultado mostrará todos los tenants donde el usuario tiene acceso.

### 3. Identificar usuarios sin tenant

Buscar usuarios que fueron creados pero no asignados a ningún tenant:

```bash
GET /api/admin/system/users
```

Los usuarios con `tenants: []` no tienen acceso a ningún tenant.

### 4. Identificar SystemAdmins

Buscar usuarios con privilegios de sistema:

```bash
GET /api/admin/system/users
```

Filtrar en el cliente por `isSystemAdmin: true`.

## Errores Posibles

### 401 Unauthorized

```json
{
  "error": "No autorizado"
}
```

**Causa**: Token JWT inválido o expirado.

### 403 Forbidden

```json
{
  "error": "Acceso denegado"
}
```

**Causa**: Usuario no tiene `IsSystemAdmin = true`.

### 500 Internal Server Error

```json
{
  "error": "Error al obtener los usuarios del sistema"
}
```

**Causa**: Error de base de datos u otro error interno.

## Notas Técnicas

### Ordenamiento
- Los usuarios se ordenan alfabéticamente por email
- Esto asegura resultados consistentes entre páginas

### Búsqueda Case-Insensitive
- La búsqueda convierte todo a minúsculas antes de comparar
- Busca en: Email, NombreCompleto, UserName

### Performance
- Usa paginación para evitar cargar todos los usuarios
- Máximo 100 usuarios por página
- Incluye solo tenants activos en las membresías

### Datos Faltantes
- Si `NombreCompleto` es null, se usa el email como fallback
- Si `CreatedAt` no está disponible, se usa la fecha actual (temporal)
- Si `LastLoginAt` es null, no se incluye en la respuesta

## Logging

El endpoint registra cada consulta para auditoría:

```
SystemAdmin {AdminId} listed all system users (Total: {Total}, Page: {Page}, Search: {Search})
```

## Seguridad

- Solo accesible por SystemAdmin
- No expone contraseñas ni tokens
- Registra todas las consultas para auditoría
- Límite de pageSize previene DoS

## Integración Frontend

### Service TypeScript

```typescript
import { systemAdminService } from '@/services/systemAdminService';

// Listar usuarios
const response = await systemAdminService.getAllSystemUsers({
  search: 'maria',
  page: 1,
  pageSize: 20
});

console.log(`Total usuarios: ${response.total}`);
console.log(`Usuarios en esta página: ${response.data.length}`);

// Ejemplo: Buscar si un email existe
const userExists = response.data.find(u => u.email === 'maria@example.com');
if (userExists) {
  console.log(`Usuario encontrado en ${userExists.tenants.length} tenants`);
}
```

### Componente React

```tsx
import { useState, useEffect } from 'react';
import { systemAdminService } from '@/services/systemAdminService';

function SystemUsersList() {
  const [users, setUsers] = useState([]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);

  useEffect(() => {
    loadUsers();
  }, [search, page]);

  const loadUsers = async () => {
    const response = await systemAdminService.getAllSystemUsers({
      search,
      page,
      pageSize: 20
    });
    setUsers(response.data);
    setTotal(response.total);
  };

  return (
    <div>
      <input
        type="text"
        placeholder="Buscar por email o nombre..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <table>
        <thead>
          <tr>
            <th>Email</th>
            <th>Nombre</th>
            <th>Tenants</th>
            <th>SystemAdmin</th>
          </tr>
        </thead>
        <tbody>
          {users.map(user => (
            <tr key={user.userId}>
              <td>{user.email}</td>
              <td>{user.fullName}</td>
              <td>{user.tenants.length}</td>
              <td>{user.isSystemAdmin ? '✓' : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div>
        Total: {total} usuarios | Página {page}
      </div>
    </div>
  );
}
```

## Próximos Pasos

1. **Agregar `CreatedAt` a `AppUser`**: Actualmente se usa DateTime.UtcNow como placeholder
2. **Agregar `IsActive` a `AppUser`**: Para deshabilitar usuarios del sistema
3. **Filtros adicionales**: Por IsSystemAdmin, por tenant específico
4. **Export a Excel/CSV**: Para reportes de usuarios
5. **Ordenamiento configurable**: Por fecha, nombre, cantidad de tenants
