# Documentación de Pagly (Planilla)

> **📘 Para una ficha completa del proyecto** (qué es Pagly, stack, URLs, accesos, arquitectura, compliance, roles actuales), ver **[OVERVIEW.md](./OVERVIEW.md)**.

Esta carpeta centraliza toda la documentación del repositorio. Los archivos `.md` están organizados por módulo para facilitar la navegación.

---

## 🆕 Recientes

Últimos documentos actualizados o añadidos (ordenados por fecha de commit, más recientes arriba):

| Fecha       | Documento                                                                                             | Tema                                                             |
|-------------|-------------------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| 2026-04-13  | [runbooks/payday-down.md](./runbooks/payday-down.md)                                                  | Runbook de incidente "payday down" (B2B)                         |
| 2026-04-11  | [api-platform/README.md](./api-platform/README.md)                                                    | Quickstart del API Platform B2B (Pagly Payroll API)              |
| 2026-04-11  | [api-platform/API-PLATFORM-ROADMAP.md](./api-platform/API-PLATFORM-ROADMAP.md)                        | Roadmap Track A / Track B del API Platform                       |
| 2026-03-29  | [qa/guia-zap-planilla.md](./qa/guia-zap-planilla.md) · [qa/zap/zap-idor-manual.md](./qa/zap/zap-idor-manual.md) | Pruebas de seguridad con OWASP ZAP (DEV-186)                     |
| 2026-03-04  | [payroll/payroll-calculations.md](./payroll/payroll-calculations.md)                                  | Motor de cálculo de nómina Panamá 2025-2026                     |
| 2026-02-20  | [payroll/HORAS-EXTRA-Y-DASHBOARDS-IMPLEMENTACION.md](./payroll/HORAS-EXTRA-Y-DASHBOARDS-IMPLEMENTACION.md) | Horas extra y dashboards                                         |
| 2026-02-19  | [qa/PRUEBAS-VALIDACION-2026-02-19.md](./qa/PRUEBAS-VALIDACION-2026-02-19.md)                          | Pruebas de validación CSS/ISR                                    |
| 2026-02-16  | [payroll/FIX-CALCULOS-ISR-CSS-Y-UI-INTEGRACIONES.md](./payroll/FIX-CALCULOS-ISR-CSS-Y-UI-INTEGRACIONES.md) | Fix tasa CSS patronal 12.25% → 13.25% (Reforma CSS)             |

> Al añadir o actualizar un documento importante, agrega una fila arriba y elimina la más antigua para mantener 8-10 entradas.

---

## 📚 Por módulo

| Carpeta                                 | Propósito                                                                |
|-----------------------------------------|--------------------------------------------------------------------------|
| [architecture/](./architecture/)        | Diseño de sistema, convenciones, plan de refactor, reglas de frontend   |
| [api/](./api/)                          | Documentación de endpoints internos y auditorías del cliente API         |
| [api-platform/](./api-platform/)        | API Platform B2B (quickstart + roadmap)                                  |
| [implementation/](./implementation/)    | Serie numerada 01-06 de pasos de implementación (custom roles, etc.)    |
| [roles-permisos/](./roles-permisos/)    | Sistema de roles custom y permisos granulares                            |
| [multi-tenant/](./multi-tenant/)        | Implementación multi-tenant (TenantContext, filtros globales)            |
| [payroll/](./payroll/)                  | Motor de cálculo, horas extra, décimos, fixes de ISR/CSS                 |
| [features/](./features/)                | Specs de features concretas (self-service, invitaciones, PlanUsageCard) |
| [compliance/](./compliance/)            | Regulación Panamá + política y specs de eliminación de datos             |
| [integrations/](./integrations/)        | Stripe, Brevo (email), SMTP                                              |
| [deploy/](./deploy/)                    | CapRover, deploy a Pagly/CLAU, fixes de build                            |
| [security/](./security/)                | Implementación de seguridad y fixes de auth admin                        |
| [admin/](./admin/)                      | System admin panel                                                       |
| [qa/](./qa/)                            | Pruebas, validaciones, guía OWASP ZAP, bugs encontrados                  |
| [runbooks/](./runbooks/)                | Procedimientos operativos (ej. payday-down)                              |
| [onboarding/](./onboarding/)            | Inicio rápido, cómo iniciar desarrollo, verify-setup                     |
| [changelog/](./changelog/)              | Historial de cambios y commits                                           |
| [archive/](./archive/)                  | Sesiones cerradas, resúmenes históricos, artefactos temporales           |

> `seeds/` también vive en esta carpeta pero contiene SQL/JSON, no Markdown.

---

## 🗄️ Archivados

En [archive/](./archive/) conservamos documentos que pertenecen a sesiones de trabajo ya cerradas o cuyo contenido fue absorbido por otros archivos vivos (ej. `DOCUMENTO-SISTEMA-PAGLY.md` → ahora en `OVERVIEW.md`). No se borran para preservar contexto histórico, pero **no son referencia activa**.

---

## 🧭 Guía de navegación

- Empieza siempre por **[OVERVIEW.md](./OVERVIEW.md)** si eres nuevo al proyecto.
- Para arrancar local → `onboarding/INICIO-RAPIDO.md`.
- Para entender cálculos → `payroll/payroll-calculations.md`.
- Para entender roles → `roles-permisos/ROLES-PERMISOS-IMPLEMENTATION.md`.
- Para un incidente en producción → `runbooks/`.
- Buscador recomendado: usa el buscador de tu editor (Ctrl+Shift+F) restringido a `docs/` — los nombres de archivo son descriptivos.

### Convenciones al agregar documentos

1. Nombrar en mayúsculas + kebab (ej. `FIX-NOMBRE-DESCRIPTIVO.md`) o snake (ej. `EMAIL_SETUP.md`) siguiendo el estilo existente del módulo.
2. Ubicar en la carpeta del módulo correspondiente (no en la raíz de `docs/`).
3. Si es un doc temporal / resumen de sesión cerrada → va a `archive/`.
4. Actualizar la sección **🆕 Recientes** de este índice si el doc es relevante.
5. El `README.md` raíz del repo redirige aquí — no duplicar documentación allí.
