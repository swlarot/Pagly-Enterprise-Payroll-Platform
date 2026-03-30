# Guía OWASP ZAP — Planilla (Pagly)

**Para:** Levi (QA onboarding)
**Ambiente target:** `https://staging-planilla.vorluno.dev` *(actualizar cuando staging esté listo)*
**Stack:** .NET 9 ASP.NET Core + React 19 + PostgreSQL + JWT multi-tenant

---

## Índice

1. [Configuración inicial de ZAP para Planilla](#1-configuración-inicial-de-zap-para-planilla)
2. [Endpoints por módulo y nivel de riesgo](#2-endpoints-por-módulo-y-nivel-de-riesgo)
3. [Paso 1: Passive Scan](#3-paso-1-passive-scan)
4. [Paso 2: Active Scan con autenticación JWT](#4-paso-2-active-scan-con-autenticación-jwt)
5. [Paso 3: Pruebas IDOR cross-tenant (PRIORIDAD MÁXIMA)](#5-paso-3-pruebas-idor-cross-tenant)
6. [Paso 4: Fuzzer — verificar rate limiting](#6-paso-4-fuzzer--verificar-rate-limiting)
7. [Paso 5: ZAP API Scan (OpenAPI)](#7-paso-5-zap-api-scan-openapi)
8. [Alertas a ignorar (false positives conocidos)](#8-alertas-a-ignorar-false-positives-conocidos)
9. [Qué reportar](#9-qué-reportar)

---

## 1. Configuración inicial de ZAP para Planilla

### 1.1 Autenticación JWT (OBLIGATORIO antes de cualquier scan)

Planilla usa JWT con un claim personalizado `tenant_id`. ZAP debe conocer cómo autenticarse:

1. En ZAP, haz clic derecho en el sitio → **Include in Context** → **Default Context**
2. Ve a **Session Properties** (ícono de engranaje) → **Authentication**
3. Selecciona `JSON-based Authentication`
4. Configura:

```
Login Request URL: https://staging-planilla.vorluno.dev/api/auth/login

Login Request POST Data:
{"email":"{%username%}","password":"{%password%}"}

Logged In Indicator (regex):  \QaccessToken\E
Logged Out Indicator (regex): \QUnauthorized\E
```

5. Ve a **Users** → Add → ingresa credenciales del tenant de prueba A
6. Agrega un segundo usuario del tenant de prueba B (para pruebas IDOR)
7. Habilita **Forced User** → selecciona tenant A para scans generales

> **Nota sobre el token JWT de Planilla:** Contiene los claims `tenant_id`, `tenant_role`, `plan`, `sub`, `email`. Si modificas manualmente el `tenant_id` en jwt.io y el sistema lo acepta, es una vulnerabilidad crítica.

### 1.2 Scope del contexto

Incluye en el scope:
```
https://staging-planilla.vorluno.dev/api/.*
```

Excluye explícitamente:
```
https://staging-planilla.vorluno.dev/api/stripe/webhook
https://staging-planilla.vorluno.dev/health
https://staging-planilla.vorluno.dev/api/health
https://staging-planilla.vorluno.dev/api/auth/dev/.*
```

---

## 2. Endpoints por módulo y nivel de riesgo

### Criticidad MÁXIMA — Probar con Active Scan + IDOR manual

| Módulo | Endpoints | Riesgo |
|--------|-----------|--------|
| **Empleados** | `GET /api/empleados`, `GET /api/empleados/{id}`, `PUT /api/empleados/{id}`, `DELETE /api/empleados/{id}`, `GET /api/empleados/{id}/saldo-inicial` | IDOR cross-tenant |
| **Planillas** | `GET /api/payrollheaders`, `GET /api/payrollheaders/{id}`, `POST /api/payrollheaders/{id}/calculate`, `POST /api/payrollheaders/{id}/approve`, `POST /api/payrollheaders/{id}/pay` | IDOR + lógica financiera |
| **Detalle planilla** | `GET /api/payrollheaders/{id}/details/{detailId}/breakdown`, `GET /api/payrollheaders/{id}/details/{detailId}/deducciones` | IDOR + data leakage |

### Criticidad ALTA

| Módulo | Endpoints | Riesgo |
|--------|-----------|--------|
| **Auth** | `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me` | Brute force (sin rate limiting), JWT manipulation |
| **Anticipos** | `GET /api/anticipos`, `GET /api/anticipos/{id}`, `POST /api/anticipos`, `POST /api/anticipos/{id}/aprobar` | IDOR, negative values |
| **Préstamos** | `GET /api/prestamos`, `GET /api/prestamos/{id}`, `POST /api/prestamos`, `PUT /api/prestamos/{id}` | IDOR, manipulación de montos |
| **Deducciones** | `GET /api/deducciones`, `GET /api/deducciones/{id}`, `POST /api/deducciones`, `PUT /api/deducciones/{id}` | IDOR, manipulación financiera |
| **Reportes** | `GET /api/reportes/planilla-regular/{planillaId}`, `GET /api/reportes/planilla-regular/{planillaId}/pdf` | IDOR, data leakage masivo |
| **Admin** | `GET /api/admin/tenants`, `GET /api/admin/metrics`, `GET /api/admin/system/users` | Privilege escalation |

### Criticidad MEDIA

| Módulo | Endpoints | Riesgo |
|--------|-----------|--------|
| **Décimo** | `GET /api/decimo`, `POST /api/decimo` | IDOR |
| **Acreedores** | `GET /api/acreedores`, `POST /api/acreedores` | IDOR |
| **Billing** | `GET /api/billing`, `GET /api/subscription` | Data de Stripe |
| **Configuración** | `GET /api/configuracion`, `PUT /api/configuracion` | Tenant config manipulation |
| **Horas Extra** | `GET /api/horasextra`, `POST /api/horasextra` | Manipulación de cálculos |
| **Ausencias** | `GET /api/ausencias`, `POST /api/ausencias` | Manipulación |

### Criticidad BAJA (excluir del Active Scan)

| Endpoint | Razón |
|----------|-------|
| `GET /health` | Público, sin datos sensibles |
| `GET /api/health` | Público, sin datos sensibles |
| `POST /api/stripe/webhook` | AllowAnonymous legítimo, tiene validación de firma Stripe |
| `GET /api/auth/validate-invite` | Solo valida token de invitación |
| `POST /api/auth/dev/confirm-all-emails` | Solo existe en ambiente de desarrollo |

---

## 3. Paso 1: Passive Scan

**Cuándo:** Siempre primero. Sin riesgo para el servidor.

**Qué detecta automáticamente ZAP en Planilla:**
- Cookies sin flags `HttpOnly` / `Secure` / `SameSite`
- Headers de seguridad faltantes: `X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy`
- HSTS configurado: debería aparecer como ✅ (Planilla lo tiene activo en producción)
- Information disclosure en responses (stack traces, versiones)

**Procedimiento:**
1. Activa el proxy ZAP en Firefox (localhost:8080)
2. Navega completamente la aplicación:
   - Login como admin del Tenant A
   - Ve a cada sección: Empleados, Planillas, Anticipos, Préstamos, Deducciones, Décimo, Reportes
   - Crea/edita al menos un registro en cada módulo
   - Genera un reporte PDF
   - Ve a Configuración y Billing
   - Haz logout y vuelve a entrar
3. En ZAP, revisa la pestaña **Alerts** al terminar
4. Anota todas las alertas rojas y naranjas

**Alertas esperadas en Planilla (no son bugs, son configuraciones conocidas):**
- Ver sección 8 de este documento antes de reportar

---

## 4. Paso 2: Active Scan con autenticación JWT

**Cuándo:** Después de completar el Passive Scan y confirmar que la autenticación ZAP funciona.

**Duración estimada:** 1–3 horas en staging.

**Procedimiento:**
1. Asegúrate de que el usuario de Tenant A está configurado en ZAP (sección 1.1)
2. En el panel **Sites**, haz clic derecho sobre `staging-planilla.vorluno.dev`
3. Selecciona **Attack** → **Active Scan**
4. En la pestaña **Context** del diálogo, selecciona el contexto con autenticación
5. En **Technology**, deja todo seleccionado excepto:
   - Desmarca: `OS > Windows`, `OS > MacOS`, `Language > PHP`, `Language > Python`, `Language > Ruby`
   - Deja activo: `.NET`, `PostgreSQL`, `HTML`, `JavaScript`
6. Haz clic en **Start Scan**
7. Monitorea en la pestaña **Active Scan** — verás los requests en tiempo real

**Priorizar manualmente estos paths en el scan:**
```
/api/empleados
/api/payrollheaders
/api/anticipos
/api/prestamos
/api/deducciones
/api/reportes
/api/admin
```

---

## 5. Paso 3: Pruebas IDOR cross-tenant

> **ESTA ES LA PRUEBA MÁS IMPORTANTE.** Planilla es multi-tenant: los datos de cada empresa deben estar completamente aislados. El sistema usa global query filters en EF Core como defensa principal — ZAP Active Scan no detecta esto bien; hay que probarlo manualmente.

Ver instrucciones detalladas en: `docs/qa/zap/zap-idor-manual.md`

**Resumen del procedimiento:**

1. Login como **Tenant A** → anota IDs de: empleado, planilla, anticipo, préstamo, deducción, reporte
2. Login como **Tenant B** → obtén token JWT
3. Con el token de B, intenta acceder a los recursos de A:

```bash
# Ejemplo con curl:
curl -H "Authorization: Bearer TOKEN_TENANT_B" \
  https://staging-planilla.vorluno.dev/api/empleados/ID_DE_TENANT_A

# Esperado: 404 o 403
# Vulnerable: 200 con datos del empleado
```

4. Repite para CADA tipo de recurso en la tabla de endpoints críticos
5. Prueba también operaciones de escritura (PUT, DELETE, POST aprobar)

**Resultado esperado:** 404 o 403 en todos los casos. NUNCA 200.

---

## 6. Paso 4: Fuzzer — verificar rate limiting

> **ALERTA CONOCIDA:** Planilla NO tiene rate limiting implementado actualmente. Esta prueba debe CONFIRMAR y DOCUMENTAR el problema, no encontrar la solución.

**Procedimiento con ZAP Fuzzer:**

1. En la pestaña **History** de ZAP, encuentra un request `POST /api/auth/login`
2. Haz clic derecho → **Attack** → **Fuzz**
3. En el campo `password`, haz clic en **Add** → **Strings** → agrega 50 contraseñas incorrectas (ej: `wrong1`, `wrong2`, ..., `wrong50`)
4. Haz clic en **Start Fuzzer**
5. Observa los responses:
   - Si todos retornan **401** con el mismo tiempo de respuesta → sin rate limiting (**documenta como vulnerabilidad ALTA**)
   - Si después de X intentos retorna **429 Too Many Requests** → rate limiting activo

**Resultado esperado actual:** 50 x 401 sin bloqueo. Documentar como `SEC-XXX: Ausencia de rate limiting en /api/auth/login`.

---

## 7. Paso 5: ZAP API Scan (OpenAPI)

Disponible solo cuando el backend corre en modo desarrollo (Swagger habilitado).

**Procedimiento:**
1. Obtén el spec OpenAPI desde el ambiente de desarrollo local:
   ```
   GET http://localhost:5039/swagger/v1/swagger.json
   ```
2. Descarga el archivo `swagger.json`
3. En ZAP: **Import** → **Import an OpenAPI definition from a file**
4. Selecciona el archivo descargado
5. ZAP importará todos los endpoints automáticamente
6. Ejecuta Active Scan sobre el contexto importado

> **Nota:** En producción y staging, Swagger está **deshabilitado** — este paso solo aplica si tienes acceso al ambiente local del desarrollador.

---

## 8. Alertas a ignorar (false positives conocidos)

Estos son falsos positivos comunes del stack .NET + React en ZAP. NO los reportes como bugs sin verificar primero:

| Alerta ZAP | Razón para ignorar | Cómo verificar antes de reportar |
|------------|-------------------|----------------------------------|
| `X-Frame-Options Header Not Set` | El frontend React no tiene este header por defecto; verificar si realmente aplica cross-frame attacks | Intenta cargar el sitio en un iframe manualmente |
| `Content Security Policy (CSP) Header Not Set` | Planilla no tiene CSP configurado — **SÍ reportar como MEDIA**, no ignorar | N/A, sí es un hallazgo real |
| `Missing Anti-clickjacking Header` | Relacionado con X-Frame-Options | Igual que arriba |
| `Server Leaks Version Information` | Verificar si el header `Server` expone versión real | `curl -I URL \| grep -i server` |
| `Re-examine Cache-control Directives` | Las páginas React usan `no-cache` intencionalmente | Verificar que es para archivos HTML/JS, no APIs |
| `Timestamp Disclosure` | Fechas en JSON responses — normal en una app de planillas | Solo reportar si expone datos de infraestructura |
| Alertas en `/health` o `/api/health` | Endpoints públicos intencionalmente sin autenticación | Confirmar que no retornan datos sensibles |

---

## 9. Qué reportar

### Hallazgos críticos → reportar INMEDIATAMENTE a José por mensaje directo

- IDOR que retorna datos de otro tenant (200 en lugar de 403/404)
- SQL Injection confirmada
- Autenticación bypasseable

### Formato de reporte para cada hallazgo

Usar plantilla del documento `guia-qa-seguridad-performance-vorluno.md` sección 7.1:

```
ID: SEC-001
Título: [descripción clara]
Producto: Planilla (Pagly)
Severidad: CRÍTICA / ALTA / MEDIA / BAJA
Endpoint: GET /api/empleados/42
Pasos para reproducir: [detallados]
Resultado esperado: 403 Forbidden
Resultado actual: 200 OK con datos del empleado
Evidencia: [screenshot/curl/output ZAP]
Fecha: dd/MM/yyyy
```

### Checklist de entrega semanal

- [ ] Reporte HTML generado en ZAP (Report → Generate HTML Report)
- [ ] Cada hallazgo documentado en archivo individual `SEC-###.md`
- [ ] Pruebas IDOR completadas para todos los módulos de la tabla §2
- [ ] Output de rate limiting fuzzer guardado
- [ ] Archivos commiteados en `qa-security-tests` repo con `git push`
