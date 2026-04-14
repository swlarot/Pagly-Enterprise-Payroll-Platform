# Planilla - Documentación Técnica del Sistema

## Consulta 1: ¿Cómo Funciona el Cálculo de Planillas?

### Resumen Ejecutivo

El sistema Planilla utiliza un **motor de cálculo especializado** que cumple 100% con las regulaciones laborales y fiscales de Panamá, específicamente:
- **Ley 462 de la CSS** (Caja de Seguro Social)
- **Regulaciones del Seguro Educativo**
- **Tabla ISR de la DGI** (Dirección General de Ingresos)

### Arquitectura de Cálculo

El sistema implementa un patrón de **Orquestador + Servicios Especializados**:

```
┌─────────────────────────────────────────────────────────┐
│          PayrollCalculationOrchestrator                 │
│  (Coordina el cálculo completo de un empleado)          │
└─────────────────┬───────────────────────────────────────┘
                  │
      ┌───────────┼───────────┐
      │           │           │
      ▼           ▼           ▼
┌──────────┐ ┌──────────┐ ┌─────────────┐
│   CSS    │ │ Seguro   │ │     ISR     │
│ Service  │ │Educativo │ │   Service   │
│          │ │ Service  │ │             │
└──────────┘ └──────────┘ └─────────────┘
```

### 1. Servicio de Cálculo CSS (Caja de Seguro Social)

**Ubicación:** `Planilla.Application/Services/CssCalculationServicePortable.cs`

**Cumplimiento Legal:** Ley 462, Art. 178, numeral 2

**Características:**

#### A. Topes Escalonados de Cotización

El sistema aplica **topes variables** según antigüedad y salario histórico:

| Tope | Condiciones | Monto Máximo |
|------|-------------|--------------|
| **Estándar** | Todos los empleados (por defecto) | B/. 1,500 |
| **Intermedio** | 25+ años cotizados Y promedio ≥ B/. 2,000 | B/. 2,000 |
| **Alto** | 30+ años cotizados Y promedio ≥ B/. 2,500 | B/. 2,500 |

**Ejemplo de Cálculo:**
```
Empleado: Juan Pérez
Salario Bruto: B/. 3,000
Años Cotizados: 28 años
Promedio 10 años: B/. 2,300

1. Determinar tope aplicable:
   - 28 años ≥ 25 años ✓
   - B/. 2,300 ≥ B/. 2,000 ✓
   → Tope Intermedio: B/. 2,000

2. Base de cotización:
   MIN(3,000, 2,000) = B/. 2,000

3. Cálculo CSS Empleado:
   2,000 × 9.75% = B/. 195.00

4. Cálculo CSS Patrono:
   2,000 × 14.25% = B/. 285.00

5. Riesgo Profesional (variable por industria):
   3,000 × 2.50% = B/. 75.00 (ejemplo clase II)
```

#### B. Tasas Aplicadas

- **CSS Empleado:** 9.75% sobre salario base topeado
- **CSS Patrono:** 14.25% sobre salario base topeado
- **Riesgo Profesional:** Variable por actividad económica
  - Clase I (oficina): 0.56%
  - Clase II (comercio): 2.50%
  - Clase III (industrial): 5.39%

### 2. Servicio de Seguro Educativo

**Ubicación:** `Planilla.Application/Services/EducationalInsuranceServicePortable.cs`

**Característica Principal:** **SIN tope máximo** (aplica sobre salario completo)

**Tasas:**
- **Empleado:** 1.25%
- **Patrono:** 1.50%

**Ejemplo:**
```
Salario Bruto: B/. 3,000

SE Empleado: 3,000 × 1.25% = B/. 37.50
SE Patrono:  3,000 × 1.50% = B/. 45.00
```

### 3. Servicio de ISR (Impuesto Sobre la Renta)

**Ubicación:** `Planilla.Application/Services/IncomeTaxCalculationServicePortable.cs`

**Método:** Proyección anual con tramos progresivos

**Tabla ISR 2025 (Panamá):**

| Rango Ingreso Anual | Tasa | Sobre el exceso de |
|---------------------|------|--------------------|
| $0 - $11,000 | 0% (Exento) | - |
| $11,001 - $50,000 | 15% | $11,000 |
| Más de $50,000 | 25% | $50,000 |

**Deducciones Permitidas:**
- $800 por dependiente anual
- Gastos educativos: hasta $5,000/año
- Intereses hipotecarios: hasta $15,000/año

**Ejemplo de Cálculo (Quincenal):**
```
Empleado: María González
Salario Quincenal: B/. 1,500
Dependientes: 2

1. Proyección Anual:
   1,500 × 24 quincenas = B/. 36,000

2. Deducción Dependientes:
   2 × $800 = $1,600

3. Base Gravable:
   36,000 - 1,600 = $34,400

4. Cálculo ISR Anual:
   - Tramo 1 ($0 - $11,000): $0
   - Tramo 2 ($11,001 - $34,400):
     (34,400 - 11,000) × 15% = $3,510

   ISR Anual Total = $3,510

5. ISR Quincenal:
   3,510 ÷ 24 = $146.25
```

### 4. Flujo de Cálculo Completo (PayrollCalculationOrchestrator)

**Proceso paso a paso:**

```
1. Recibe datos del empleado:
   - Salario bruto del período
   - Años cotizados
   - Salario promedio 10 años
   - Riesgo profesional de la empresa
   - Dependientes
   - Flags de aplicabilidad (CSS/SE/ISR)

2. Ejecuta cálculos en secuencia:
   ├─► Calcular CSS completo (empleado + patrono + riesgo)
   ├─► Calcular Seguro Educativo (empleado + patrono)
   └─► Calcular ISR (proyección anual ÷ períodos)

3. Consolidar resultado:
   ┌─────────────────────────────────────┐
   │ PayrollCalculationResult            │
   ├─────────────────────────────────────┤
   │ GrossPay:              3,000.00     │
   │ CssEmployee:             195.00     │
   │ EducationalInsEmployee:   37.50     │
   │ IncomeTax:               146.25     │
   │ ─────────────────────────────────   │
   │ TotalDeductions:         378.75     │
   │ NetPay:                2,621.25     │
   │                                     │
   │ (Costos Patronales)                 │
   │ CssEmployer:             285.00     │
   │ EducationalInsEmployer:   45.00     │
   │ RiskContribution:         75.00     │
   │ ─────────────────────────────────   │
   │ TotalEmployerCost:       405.00     │
   └─────────────────────────────────────┘

4. Guardar resultado en PayrollDetail
```

### Precisión de Redondeo

**Estándar aplicado:** Redondeo a 2 decimales con `MidpointRounding.AwayFromZero`

Todas las operaciones monetarias usan:
```csharp
Math.Round(amount, 2, MidpointRounding.AwayFromZero)
```

Esto garantiza:
- Precisión contable exacta
- Cumplimiento con estándares bancarios
- Sin acumulación de errores en sumas

### Validaciones Implementadas

1. **Configuración Obligatoria:**
   - El sistema NO permite cálculos sin configuración activa
   - Lanza `InvalidOperationException` si falta configuración de tasas

2. **Datos del Empleado:**
   - Validación de datos mínimos requeridos
   - Valores negativos rechazados
   - Flags de aplicabilidad respetados

3. **Transaccionalidad:**
   - Cálculos de planilla completa ejecutados en transacción
   - Rollback automático en caso de error
   - Garantía de integridad de datos

---

## Consulta 2: ¿El Sistema Trabaja en la Nube o Local?

### Resumen Ejecutivo

**Planilla es un SaaS 100% en la nube** con arquitectura multi-tenant diseñado para:
- Acceso desde cualquier lugar con internet
- Sin instalación local requerida
- Escalabilidad automática
- Mantenimiento centralizado
- Backups automáticos

### Arquitectura de Despliegue

```
┌───────────────────────────────────────────────────────────────┐
│                    INTERNET (Acceso Global)                    │
└────────────────────────┬──────────────────────────────────────┘
                         │
                         │ HTTPS/TLS
                         │
          ┌──────────────▼─────────────────┐
          │    CapRover (Orquestador)      │
          │    Servidor DigitalOcean       │
          │    Ubuntu 22.04 LTS            │
          └────────────┬───────────────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
┌──────────────┐ ┌──────────┐ ┌──────────────┐
│   Backend    │ │Frontend  │ │  PostgreSQL  │
│  .NET 9 API  │ │React SPA │ │   Database   │
│              │ │          │ │              │
│ Container 1  │ │Container2│ │  Container 3 │
└──────────────┘ └──────────┘ └──────────────┘
```

### Stack de Infraestructura

#### 1. Servidor Cloud

**Proveedor:** DigitalOcean Droplet

**Especificaciones Recomendadas:**
- **CPU:** 2-4 vCPUs
- **RAM:** 4-8 GB
- **Almacenamiento:** 80-160 GB SSD
- **SO:** Ubuntu 22.04 LTS
- **Región:** Miami o Nueva York (menor latencia para Panamá)

**Costo Estimado:** $24-$48 USD/mes (sin incluir Stripe)

#### 2. Orquestador de Contenedores

**Tecnología:** CapRover (PaaS auto-hospedado)

**Ventajas:**
- Deploy con un solo comando git push
- Gestión de SSL automática (Let's Encrypt)
- UI web para administración
- Sin costo de licencia (open source)
- Compatible con Docker

**Alternativas consideradas:**
- ~~Heroku~~ (muy costoso para SaaS)
- ~~AWS ECS~~ (complejidad innecesaria para MVP)
- ~~Kubernetes~~ (overkill para este tamaño)

#### 3. Base de Datos

**Tecnología:** PostgreSQL 16+

**Configuración:**
- **Modo:** Container en el mismo servidor (o RDS en producción)
- **Persistencia:** Volúmenes Docker para datos
- **Backups:** Automáticos diarios con retención 30 días
- **Cifrado:** At-rest y in-transit

**Escalabilidad Futura:**
- Migración a PostgreSQL managed (DigitalOcean Database)
- Read replicas para reportes pesados
- Particionamiento por tenant si es necesario

### Modelo de Acceso

#### Frontend (React SPA)

**URL de Acceso:** `https://app.planilla.cloud`

**Características:**
- Single Page Application (SPA)
- Servida desde CDN (Cloudflare o similar)
- Progressive Web App (PWA) - funciona offline básico
- Responsive design (móvil, tablet, desktop)

**Compilación:**
```bash
npm run build
→ Genera archivos estáticos en wwwroot/
→ Servidos por ASP.NET Core
```

#### Backend (API REST)

**URL Base:** `https://api.planilla.cloud`

**Características:**
- API RESTful con JSON
- Autenticación JWT Bearer
- CORS configurado para frontend
- Rate limiting por plan de suscripción
- API documentation (Swagger/OpenAPI)

**Endpoints Principales:**
```
POST   /api/auth/login
GET    /api/payrollheaders
POST   /api/payrollheaders/{id}/calculate
GET    /api/reportes/css/{id}/excel
```

### Multi-Tenancy en la Nube

**Modelo:** Shared Database, Shared Schema con aislamiento por TenantId

```
┌─────────────────────────────────────────┐
│      PostgreSQL Database (Único)        │
├─────────────────────────────────────────┤
│  Tenants                                │
│  ├─ Tenant #1: Empresa ABC (RUC: 123)   │
│  ├─ Tenant #2: Empresa XYZ (RUC: 456)   │
│  └─ Tenant #3: Empresa QWE (RUC: 789)   │
│                                         │
│  PayrollHeaders                         │
│  ├─ Planilla #10 → TenantId: 1         │
│  ├─ Planilla #11 → TenantId: 2         │
│  └─ Planilla #12 → TenantId: 1         │
│                                         │
│  Employees                              │
│  ├─ Empleado #50 → TenantId: 1         │
│  ├─ Empleado #51 → TenantId: 2         │
│  └─ Empleado #52 → TenantId: 1         │
└─────────────────────────────────────────┘
```

**Seguridad de Aislamiento:**
1. Todas las queries filtran por `TenantId` (obligatorio)
2. JWT contiene `tenant_id` claim verificado en cada request
3. Middleware valida TenantId antes de ejecutar controladores
4. Índices compuestos con TenantId para performance

### Acceso de Usuarios

#### Desde la Empresa (Oficina)

```
Usuario en oficina
    │
    ├─► Navegador Web (Chrome, Edge, Firefox)
    │   └─► https://app.planilla.cloud
    │       └─► Login con email + password
    │           └─► JWT Token con TenantId
    │               └─► Acceso a datos de SU empresa
    │
    └─► Mobile (opcional - futuro)
        └─► App MAUI (.NET)
            └─► Mismo backend API
```

#### Desde Casa (Trabajo Remoto)

✅ **Funciona exactamente igual** - sin VPN ni configuración especial
- Solo necesita conexión a internet
- Mismo nivel de seguridad (HTTPS/TLS)
- Autenticación por roles (Owner, Admin, Manager, etc.)

#### Acceso Multi-dispositivo

| Dispositivo | Soporte | Notas |
|-------------|---------|-------|
| PC Windows | ✅ Full | Navegador moderno requerido |
| Mac | ✅ Full | Safari, Chrome, Firefox |
| Android | ✅ Full | Responsive design automático |
| iOS | ✅ Full | Safari móvil optimizado |
| Tablet | ✅ Full | Layout adaptativo |

### Requisitos del Cliente (Empresa)

#### Hardware
- ❌ **No se requiere servidor local**
- ❌ **No se requiere instalación de software**
- ✅ Cualquier PC/laptop con navegador moderno
- ✅ Conexión a Internet estable (mínimo 2 Mbps)

#### Software
- ✅ Windows 10/11, macOS, o Linux
- ✅ Navegador actualizado:
  - Google Chrome 90+
  - Microsoft Edge 90+
  - Firefox 88+
  - Safari 14+

#### Red
- ✅ Conexión HTTPS (puerto 443)
- ❌ No requiere puertos especiales abiertos
- ❌ No requiere configuración de firewall

### Datos y Respaldos

#### Almacenamiento
- **Datos transaccionales:** PostgreSQL en servidor cloud
- **Archivos adjuntos:** (futuro) S3-compatible storage
- **Logs de auditoría:** Retención según plan de suscripción

#### Backups Automáticos
1. **Base de datos:**
   - Backup completo diario (3:00 AM UTC)
   - Retención: 30 días
   - Backups incrementales cada 6 horas
   - Ubicación: Región separada (disaster recovery)

2. **Disaster Recovery:**
   - RPO (Recovery Point Objective): 6 horas
   - RTO (Recovery Time Objective): 2 horas
   - Pruebas de restauración mensuales

#### Seguridad de Datos
- **Cifrado en reposo:** AES-256
- **Cifrado en tránsito:** TLS 1.3
- **Cumplimiento:** Preparado para GDPR/LOPD
- **Auditoría:** Logs completos de accesos y cambios

### Planes de Suscripción y Límites

#### Límites por Plan (Actuales)

| Característica | Free | Starter | Professional | Enterprise |
|----------------|------|---------|--------------|------------|
| **Empleados** | 5 | 25 | 100 | Ilimitado |
| **Usuarios** | 1 | 3 | 10 | Ilimitado |
| **Empresas/Tenant** | 1 | 1 | 3 | Ilimitado |
| **Export Excel** | ❌ | ✅ | ✅ | ✅ |
| **Export PDF** | ❌ | ❌ | ✅ | ✅ |
| **API Access** | ❌ | ❌ | ✅ | ✅ |
| **Retención** | 90 días | 1 año | 2 años | Permanente |
| **Precio Mensual** | $0 | $29.99 | $79.99 | $199.99+ |

### Escalabilidad

#### Crecimiento Previsto

**Fase 1 (MVP - 10 tenants):**
- 1 servidor DigitalOcean ($24/mes)
- PostgreSQL en mismo servidor
- CapRover básico

**Fase 2 (50 tenants):**
- 1 servidor más potente ($48/mes)
- PostgreSQL managed database ($15/mes)
- CDN para frontend (Cloudflare Free)

**Fase 3 (200+ tenants):**
- Load balancer + 2-3 servidores backend
- PostgreSQL con read replicas
- Redis para caché
- Monitoreo con Grafana/Prometheus

#### Límites Técnicos Actuales

- **Concurrencia:** 100 usuarios simultáneos por servidor
- **Procesamiento:** 50 planillas/minuto
- **Almacenamiento:** 100 GB por tenant (límite suave)
- **Tráfico:** 1 TB/mes bandwidth

### Alternativa: Instalación On-Premise (A Pedido)

**Disponibilidad:** Solo para planes Enterprise

**Requisitos:**
- Servidor Windows Server 2022 o Ubuntu 22.04
- 8 GB RAM mínimo
- PostgreSQL 16+ instalado
- .NET 9 Runtime
- Certificado SSL válido

**Costo Adicional:**
- Licencia perpetua: $5,000 USD (one-time)
- Mantenimiento anual: $1,200 USD
- Instalación y configuración: $500 USD

**Limitaciones:**
- Sin actualizaciones automáticas
- Soporte limitado
- Cliente responsable de backups

---

## Comparación: SaaS Cloud vs On-Premise

| Aspecto | SaaS Cloud (Actual) | On-Premise (Opcional) |
|---------|---------------------|----------------------|
| **Costo Inicial** | $0 - $199.99/mes | $5,500+ inicial |
| **Mantenimiento** | Incluido | Cliente responsable |
| **Actualizaciones** | Automáticas | Manuales ($1,200/año) |
| **Acceso Remoto** | Nativo (cualquier lugar) | Requiere VPN |
| **Backups** | Automáticos | Cliente responsable |
| **Escalabilidad** | Automática | Limitada por hardware |
| **Seguridad** | Enterprise-grade | Depende del cliente |
| **Soporte** | 24/7 (Professional+) | Horario limitado |
| **Setup Time** | 5 minutos | 2-5 días |

---

## Recomendación Oficial

### Para la Mayoría de Clientes: **SaaS Cloud**

**Ventajas:**
✅ Sin inversión inicial en infraestructura
✅ Acceso inmediato desde cualquier lugar
✅ Actualizaciones automáticas con nuevas features
✅ Backups y seguridad incluidos
✅ Escalabilidad sin costo adicional de hardware
✅ Cumplimiento legal automático (actualizaciones de Ley 462, ISR, etc.)

**Ideal para:**
- Pequeñas y medianas empresas (1-100 empleados)
- Empresas con trabajo remoto o múltiples sucursales
- Empresas sin departamento de IT
- Empresas que quieren enfocarse en su negocio, no en IT

### Para Clientes Específicos: On-Premise

**Solo recomendado si:**
- Restricciones regulatorias estrictas que impiden cloud
- Infraestructura IT existente robusta
- Más de 500 empleados con presupuesto IT dedicado
- Requisitos de integración profunda con sistemas legacy locales

---

## Arquitectura Técnica Detallada

### Componentes del Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                      Planilla Cloud                          │
└─────────────────────┬───────────────────────────────────────┘
                      │
    ┌─────────────────┼─────────────────┐
    │                 │                 │
    ▼                 ▼                 ▼
┌─────────┐     ┌──────────┐     ┌──────────┐
│Frontend │     │ Backend  │     │Database  │
│ React   │────▶│.NET API  │────▶│PostgreSQL│
│ SPA     │     │          │     │          │
└─────────┘     └──────────┘     └──────────┘
                      │
                      ▼
              ┌──────────────┐
              │   Stripe     │
              │  (Billing)   │
              └──────────────┘
```

### Flujo de Solicitud Completo

```
1. Usuario accede a https://app.planilla.cloud
   │
2. Navegador carga React SPA desde CDN
   │
3. Usuario hace login (POST /api/auth/login)
   │
4. Backend valida credenciales contra PostgreSQL
   │
5. Backend genera JWT con claims:
   {
     "sub": "user-guid",
     "email": "usuario@empresa.com",
     "tenant_id": "123",
     "tenant_role": "Admin",
     "plan": "Professional"
   }
   │
6. Frontend almacena token en localStorage
   │
7. Todas las solicitudes incluyen:
   Authorization: Bearer {jwt-token}
   │
8. Middleware extrae tenant_id del token
   │
9. Todas las queries filtran por tenant_id automáticamente
   │
10. Respuesta JSON devuelta al frontend
    │
11. React renderiza UI con datos
```

### Seguridad Implementada

#### 1. Autenticación
- ASP.NET Core Identity
- Passwords con bcrypt (salt + hash)
- JWT Bearer tokens (expiración 24h)
- Refresh tokens (expiración 30 días)

#### 2. Autorización
- Role-Based Access Control (RBAC)
- Roles por tenant: Owner, Admin, Manager, Accountant, Employee
- Custom roles con permisos granulares (24 permisos)
- Policy-based authorization

#### 3. Multi-Tenancy
- TenantId en todas las entidades
- Middleware de validación de tenant
- Índices compuestos para performance
- Queries automáticamente filtradas

#### 4. Red
- HTTPS/TLS 1.3 obligatorio
- CORS configurado para dominios autorizados
- Rate limiting por IP y plan
- DDoS protection (Cloudflare)

#### 5. Datos
- Cifrado AES-256 en reposo
- Cifrado TLS en tránsito
- Backups cifrados
- Audit log completo

---

## Monitoreo y Mantenimiento

### Logs y Métricas

**Herramientas:**
- Application Insights (Azure) o Seq
- Sentry para error tracking
- Uptime monitoring (UptimeRobot)

**Métricas Monitoreadas:**
- Request latency (objetivo: <200ms p95)
- Error rate (objetivo: <0.1%)
- Disponibilidad (objetivo: 99.9%)
- Database query performance
- Memory/CPU usage

### Actualizaciones

**Frecuencia:**
- Security patches: Inmediato (0-24h)
- Bug fixes: Semanal
- Features nuevas: Mensual
- Major versions: Trimestral

**Proceso:**
1. Deploy a staging environment
2. Automated tests
3. Manual QA
4. Deploy a producción (blue-green)
5. Monitoreo post-deploy
6. Rollback automático si error rate >1%

---

## Preguntas Frecuentes

### ¿Qué pasa si se cae Internet en la empresa?

**Respuesta:** Al ser SaaS cloud, se requiere internet para operar. Sin embargo:
- El frontend tiene capacidad offline básica (PWA)
- Los reportes descargados (Excel/PDF) están disponibles localmente
- Recomendamos conexión backup (4G/5G móvil)

### ¿Los datos están seguros en la nube?

**Respuesta:** Sí. Implementamos:
- Cifrado military-grade (AES-256)
- Aislamiento total entre tenants
- Backups automáticos diarios
- Compliance con estándares internacionales
- Auditoría completa de accesos

### ¿Pueden acceder a nuestros datos?

**Respuesta:**
- Solo administradores de sistema con autenticación 2FA
- Solo para soporte técnico autorizado por el cliente
- Logs de auditoría de todos los accesos
- Política de privacidad estricta

### ¿Qué pasa si Planilla cierra?

**Respuesta:**
- Plan de contingencia: 90 días de aviso
- Export completo de datos en formato estándar (Excel, SQL)
- Opción de migración a on-premise
- Código fuente disponible para Enterprise clients

---

## Conclusión

### Sistema de Cálculo

Planilla implementa un **motor de cálculo robusto y compliant** con:
- ✅ Cumplimiento 100% Ley 462 CSS (topes escalonados)
- ✅ Seguro Educativo sin tope
- ✅ ISR con proyección anual y tramos progresivos
- ✅ Precisión de 2 decimales en todos los cálculos
- ✅ Validaciones estrictas
- ✅ Transaccionalidad garantizada

### Modelo de Despliegue

Planilla es un **SaaS 100% en la nube** con:
- ✅ Infraestructura en DigitalOcean + CapRover
- ✅ Acceso desde cualquier lugar con internet
- ✅ Sin instalación local requerida
- ✅ Multi-tenant con aislamiento garantizado
- ✅ Backups automáticos y disaster recovery
- ✅ Escalabilidad automática
- ✅ Seguridad enterprise-grade

**Modelo de negocio:** Suscripción mensual por número de empleados, con planes desde $0 (Free) hasta $199.99+ (Enterprise).

---

**Documento Técnico v1.0**
**Fecha:** 31 de enero de 2026
**Sistema:** Planilla SaaS - Gestión de Nómina para Panamá
