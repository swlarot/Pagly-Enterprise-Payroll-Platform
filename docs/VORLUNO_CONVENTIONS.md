# Convenciones de Vorluno

Este documento establece las convenciones de nomenclatura y estructura para todos los productos de Vorluno.

## Identificadores de Producto

### 1. Subdominios

Formato: `<app>.vorluno.dev`

**Ejemplos:**
- `planilla.vorluno.dev` - Sistema de nómina
- `menu.vorluno.dev` - Sistema de gestión de menús
- `docs.vorluno.dev` - Documentación corporativa
- `api.vorluno.dev` - API Gateway

### 2. Repositorios en GitHub

Formato: `vorluno/<app>`

**Ejemplos:**
- `vorluno/planilla` - Sistema de nómina
- `vorluno/menu` - Sistema de gestión de menús
- `vorluno/core360-integrations` - Integraciones con sistemas legacy

**Reglas:**
- Todo en minúsculas
- Usar guiones (`-`) para separar palabras
- Nombres descriptivos y concisos
- Evitar abreviaciones poco claras

### 3. Identificador Interno / Código de App

Formato: `VOR-<CÓDIGO>`

**Reglas:**
- Prefijo: `VOR-`
- Código: 3-5 letras en MAYÚSCULAS
- Debe ser memorable y relacionado con el producto
- Usar en documentación interna, tickets, commits importantes

**Ejemplos:**
- `VOR-PLAN` - Planilla (Nómina)
- `VOR-PAY` - Alternativa para Planilla
- `VOR-MENU` - Sistema de menús
- `VOR-CRM` - CRM empresarial
- `VOR-INV` - Sistema de inventario

### 4. Namespaces C#/.NET

Formato: `Vorluno.<App>.<Layer>`

**Estructura:**
```
Vorluno.<App>.<Layer>.<SubLayer>
```

**Ejemplos para Planilla:**
```csharp
// Capa de Dominio
Vorluno.Planilla.Domain
Vorluno.Planilla.Domain.Entities
Vorluno.Planilla.Domain.Enums
Vorluno.Planilla.Domain.ValueObjects

// Capa de Aplicación
Vorluno.Planilla.Application
Vorluno.Planilla.Application.Services
Vorluno.Planilla.Application.DTOs
Vorluno.Planilla.Application.Interfaces
Vorluno.Planilla.Application.Mappings

// Capa de Infraestructura
Vorluno.Planilla.Infrastructure
Vorluno.Planilla.Infrastructure.Data
Vorluno.Planilla.Infrastructure.Repositories
Vorluno.Planilla.Infrastructure.Services

// Capa de Presentación/UI
Vorluno.Planilla.Web
Vorluno.Planilla.Web.Controllers
Vorluno.Planilla.Web.Extensions
Vorluno.Planilla.Web.Services

// Tests
Vorluno.Planilla.Application.Tests
Vorluno.Planilla.Domain.Tests
Vorluno.Planilla.Integration.Tests
```

**Reglas:**
- Primera letra en mayúscula (PascalCase)
- Máximo 4 niveles de profundidad
- Layer debe ser uno de: Domain, Application, Infrastructure, Web, Tests
- SubLayer debe ser descriptivo y singular/plural según contexto

### 5. Nombres de Archivos .csproj

Formato: `Vorluno.<App>.<Layer>.csproj`

**Ejemplos:**
```
Vorluno.Planilla.Domain.csproj
Vorluno.Planilla.Application.csproj
Vorluno.Planilla.Infrastructure.csproj
Vorluno.Planilla.Web.csproj
Vorluno.Planilla.Application.Tests.csproj
```

### 6. Metadatos en .csproj

Todos los archivos .csproj deben incluir los siguientes metadatos:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>

  <!-- Metadatos de Vorluno -->
  <Company>Vorluno</Company>
  <Product>Vorluno [NombreApp] - VOR-[CODE]</Product>
  <Description>[Descripción de la capa/proyecto]</Description>
  <Authors>Vorluno</Authors>
  <Copyright>Copyright © Vorluno 2025</Copyright>
  <RepositoryUrl>https://github.com/vorluno/[app]</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>
```

**Ejemplo para Planilla:**
```xml
<PropertyGroup>
  <Company>Vorluno</Company>
  <Product>Vorluno Planilla - VOR-PLAN</Product>
  <Description>Domain layer for Vorluno Planilla - Enterprise payroll management system</Description>
  <Authors>Vorluno</Authors>
  <Copyright>Copyright © Vorluno 2025</Copyright>
  <RepositoryUrl>https://github.com/vorluno/planilla</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>
```

## Convenciones de Estructura de Proyectos

### Estructura de Directorios

```
vorluno/[app]/
├── src/
│   ├── Core/
│   │   ├── Vorluno.[App].Domain/
│   │   └── Vorluno.[App].Application/
│   ├── Infrastructure/
│   │   └── Vorluno.[App].Infrastructure/
│   └── UI/ o Presentation/
│       └── Vorluno.[App].Web/
├── tests/
│   ├── Vorluno.[App].Domain.Tests/
│   ├── Vorluno.[App].Application.Tests/
│   └── Vorluno.[App].Integration.Tests/
├── docs/
│   ├── README.md
│   ├── VORLUNO_CONVENTIONS.md
│   └── [otros docs]
├── .gitignore
├── README.md
└── [App].sln (opcional: Vorluno.[App].sln)
```

## Convenciones de Commits

### Formato de Mensajes de Commit

Para commits regulares:
```
tipo: descripción breve

Descripción más detallada si es necesario
```

Para commits importantes con identificador:
```
[VOR-PLAN] tipo: descripción breve

Descripción más detallada si es necesario
```

**Tipos de commit:**
- `feat`: Nueva funcionalidad
- `fix`: Corrección de bugs
- `docs`: Cambios en documentación
- `style`: Cambios de formato (no afectan la lógica)
- `refactor`: Refactorización de código
- `test`: Agregar o modificar tests
- `chore`: Tareas de mantenimiento

**Ejemplos:**
```
feat: implementar cálculo de ISR según brackets 2025

fix: corregir redondeo en cálculo de CSS

[VOR-PLAN] feat: migrar namespaces a convención Vorluno

docs: actualizar README con convenciones Vorluno
```

## Convenciones de Branches

### Nombres de Branches

```
tipo/descripcion-breve

o

tipo/VOR-CODE-descripcion-breve
```

**Tipos:**
- `feature/` - Nuevas funcionalidades
- `fix/` - Corrección de bugs
- `refactor/` - Refactorizaciones
- `docs/` - Cambios en documentación
- `chore/` - Tareas de mantenimiento

**Ejemplos:**
```
feature/calculo-vacaciones
fix/redondeo-css
feature/VOR-PLAN-reportes-pdf
refactor/namespace-migration
docs/vorluno-conventions
```

## Licencia y Copyright

Todos los proyectos de Vorluno deben incluir:

```
Copyright © Vorluno 2025. Todos los derechos reservados.
```

## Referencias

- Repositorio GitHub de Vorluno: https://github.com/vorluno
- Dominio principal: https://vorluno.dev

---

**Última actualización**: 2025-12-30
**Versión**: 1.0
