# Documento del Sistema Pagly

**Versión:** 1.0  
**Fecha:** Febrero 2026  
**Propósito:** Descripción del sistema, cálculos, infraestructura y credenciales de acceso

---

## 1. ¿Qué es Pagly?

**Pagly** (antes Vorluno Planilla) es un **sistema de nómina y planilla** diseñado para empresas en Panamá. Es un **SaaS multi-tenant**: varias empresas pueden usar el mismo sistema de forma aislada, cada una con sus empleados, planillas y configuración.

Cumple con las regulaciones laborales panameñas: Ley 462 de la CSS, Seguro Educativo e Impuesto Sobre la Renta (ISR).

---

## 2. ¿Qué hace el sistema?

### Gestión de empleados
- Registro de empleados con datos laborales y fiscales
- Departamentos y posiciones
- Historial de salarios y contratos
- Vinculación de usuarios del sistema con empleados

### Planilla (nómina)
- Cálculo automático de deducciones (CSS, Seguro Educativo, ISR)
- Horas extras con multiplicadores configurables
- Anticipos y préstamos con amortización automática
- Vacaciones y ausencias
- Estados: Borrador → Calculado → Aprobado → Pagado

### Reportes y exportaciones
- Comprobantes de pago
- Reportes CSS para la Caja de Seguro Social
- Declaraciones ISR
- Exportación a Excel

### Panel de administración (solo SystemAdmin)
- Gestión de empresas (tenants)
- Creación de usuarios y asignación a empresas
- Cambio de planes de suscripción
- Logs de auditoría

### Roles y permisos
- **Owner:** Dueño de la empresa, acceso total
- **Roles personalizados:** Permisos granulares (ver planillas, reportes, empleados, etc.)
- **Empleado - Auto Servicio:** Empleados que solo ven su información

---

## 3. Cómo se hacen los cálculos

### 3.1 CSS (Caja de Seguro Social) – Ley 462

| Concepto | Tasa empleado | Tasa patronal |
|----------|---------------|---------------|
| CSS | 9.75% | 13.25% (hasta feb. 2027) |
| Seguro Educativo | 1.25% | 1.50% |

**Topes de cotización** (base sobre la que se calcula):

| Tope | Base máxima | Condiciones |
|------|-------------|-------------|
| Estándar | B/. 1,500 | Por defecto |
| Intermedio | B/. 2,000 | 25+ años cotizados y promedio ≥ B/. 2,000 |
| Alto | B/. 2,500 | 30+ años cotizados y promedio ≥ B/. 2,500 |

Si el salario bruto es mayor al tope, solo se cotiza sobre el tope. Las tasas son fijas según Ley 462.

### 3.2 Riesgo Profesional

Tasa según categoría de riesgo del puesto:
- Bajo: 0.56%
- Medio: 2.50%
- Alto: 5.39%

Lo paga el empleador, no el empleado.

### 3.3 ISR (Impuesto Sobre la Renta)

Escala progresiva anual (2025/2026):

| Tramo | Ingreso anual | Tasa |
|-------|----------------|------|
| Exento | Hasta B/. 11,000 | 0% |
| 15% | B/. 11,000.01 – B/. 50,000 | 15% |
| 25% | Mayor a B/. 50,000 | 25% (B/. 5,850 fijo + 25% sobre el exceso) |

**Deducción por dependientes:** B/. 800 por dependiente, máximo 3 dependientes (B/. 2,400 anual).

**Proceso:**
1. Proyectar ingreso anual según frecuencia de pago (mensual, quincenal, semanal)
2. Restar deducción por dependientes
3. Aplicar tramos sobre el ingreso gravable anual
4. Convertir impuesto anual a retención del período

### 3.4 Fuente de datos

Las tasas, topes y escalas provienen de la configuración de la empresa en **Configuración → Planilla**. Se pueden ajustar por año fiscal. El sistema no usa valores fijos en el código; todo es configurable.

---

## 4. Con qué está desplegado

### Infraestructura

| Componente | Tecnología |
|------------|------------|
| **Hosting** | CapRover (plataforma de contenedores) |
| **Dominio** | pagly.clau.com.pa |
| **Base de datos** | PostgreSQL 16 |
| **Despliegue** | Docker (imagen multi-stage) |
| **CI/CD** | GitHub webhook → build automático en el servidor |

### Stack tecnológico

| Capa | Tecnología |
|------|------------|
| **Backend** | .NET 9, ASP.NET Core Web API, Entity Framework Core |
| **Frontend** | React 19, Vite, Tailwind CSS |
| **Base de datos** | PostgreSQL 16 |
| **Autenticación** | ASP.NET Core Identity + JWT Bearer |
| **Correos** | Brevo (Sendinblue) |
| **Pagos** | Stripe (opcional, para suscripciones) |

### Arquitectura

- **Multi-tenant:** Cada empresa tiene sus datos aislados
- **Clean Architecture:** Domain, Application, Infrastructure, Web
- **API REST:** Backend y frontend separados; el frontend se sirve desde el mismo dominio

---

## 5. Acceso y credenciales

### URL de la aplicación

**https://pagly.clau.com.pa**

### Usuarios creados

| Rol | Email | Contraseña | Uso |
|-----|-------|------------|-----|
| **Administrador del sistema** | gjoseluisgonzalez507@gmail.com | HATSUKIMINARA* | Panel de administración (tenants, usuarios, planes) |
| **Administrador del sistema** | contacto@vorluno.dev | HatsukiMinara507* | Panel de administración |
| **Owner de empresa** | hatsukiminara@gmail.com | Planilla2024!Temp | Gestión de empleados, planillas, reportes |

### Cómo acceder

1. Abrir https://pagly.clau.com.pa
2. Iniciar sesión con el email y contraseña correspondientes
3. **Administradores del sistema:** Menú superior → **Panel de Administración** (o ruta `/system-admin`)
4. **Owner:** Tras iniciar sesión, se muestra el dashboard de la empresa

### Recomendaciones de seguridad

- Cambiar las contraseñas temporales en el primer acceso
- La contraseña de hatsukiminara@gmail.com (Planilla2024!Temp) es temporal y debe cambiarse
- No compartir credenciales por canales inseguros

---

## 6. Mantenimiento y actualizaciones

### Despliegues automáticos

Cada vez que se hace `git push` al repositorio en GitHub, el webhook dispara un nuevo build en CapRover y la aplicación se actualiza automáticamente. No hace falta desplegar manualmente.

### Base de datos

- Las migraciones de Entity Framework se ejecutan automáticamente al arrancar la aplicación
- La base de datos PostgreSQL está en el mismo servidor que la app (One-Click en CapRover)
- Se recomienda hacer backups periódicos de PostgreSQL

### Health check

El sistema expone un endpoint de salud: `https://pagly.clau.com.pa/health`. Devuelve el estado de la base de datos y del multi-tenant. CapRover lo usa para comprobar que la app está funcionando.

---

## 7. Resumen rápido

| Concepto | Valor |
|----------|-------|
| **URL** | https://pagly.clau.com.pa |
| **Sistema** | SaaS de nómina para Panamá, multi-tenant |
| **Cumplimiento** | Ley 462 CSS, Seguro Educativo, ISR |
| **Hosting** | CapRover + PostgreSQL |
| **Despliegue** | Automático desde GitHub |

---

*Documento generado para uso interno y entrega al cliente.*
