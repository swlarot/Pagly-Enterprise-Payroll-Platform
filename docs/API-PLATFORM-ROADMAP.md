# API Platform B2B — Roadmap completo

> **Este archivo vive en `C:\Planilla` (repo Pagly).**
> Documenta todo el plan del API Platform B2B, dividido en **Track A** (código, en este repo)
> y **Track B** (marketing, en `C:\vorluno-pagina web\vorluno-web`).
> Este repo solo ejecuta Track A. Track B se ejecuta en el otro repo — su propio archivo
> hermano vive allá.

---

## 1. Contexto — dos repos, dos productos

### Repos independientes

**1.** **`C:\Planilla`** (este repo) — **Pagly SaaS**.
- Backend .NET 9 + frontend React dashboard + PostgreSQL.
- Deploy: `app.pagly.clau.com.pa` (CapRover → Docker).
- Estado actual: **9 chunks commiteados**, API Platform B2B funcional end-to-end
  (auth, calculator, tracking, CRUD de keys, plan gate, rate limiting, dashboard self-service).
- **206 tests verdes** (135 Application + 71 Integration).
- Aquí viven todos los endpoints, entities, middlewares, handlers, migraciones.

**2.** **`C:\vorluno-pagina web\vorluno-web`** — **página de marketing de Vorluno (la empresa)**.
- Stack: Next.js 16, TypeScript, Tailwind, GSAP, R3F, base-ui.
- Deploy: `vorluno.dev` (Vercel).
- Tiene `/productos/pagly` con hero, colilla demo, features, pricing (B/. 0 → 7 → 15 → Custom),
  compliance, FAQ, desktop downloads, CTA.
- Todos los CTAs van a `wa.me/50769430930` — **no hay signup público**.
- El pricing menciona "API dedicada" solo en Enterprise pero **no explica qué es**.
- Aquí **no vive ningún código del API Platform**. Solo contenido que habla del API.

### Separación conceptual

| URL | Audiencia | Dónde vive |
|---|---|---|
| `vorluno.dev/productos/pagly` | Cliente final — Owner de empresa que busca software de planilla | `vorluno-web` |
| `vorluno.dev/productos/pagly/api` (futuro) | Developer que quiere integrar cálculos Panamá como API | `vorluno-web` |
| `app.pagly.clau.com.pa` (dashboard SaaS) | Owner/User del tenant | `C:\Planilla` |
| `app.pagly.clau.com.pa/v1/payroll/calculate` | Cliente API (máquina) | `C:\Planilla` |
| `app.pagly.clau.com.pa/v1/docs` (futuro, Chunk 10) | Developer leyendo docs | `C:\Planilla` |

**Regla mental:** si procesa HTTP → `Planilla`. Si es texto/imagen/ejemplo/landing/blog → `vorluno-web`.

### Pricing actual vs API Platform

- **Pricing del landing** (en `vorluno-web`): planes SaaS del código (Free/Starter/Professional/Enterprise) en B/.
- **Enforcement del API** (en `C:\Planilla`): `CanUseApi=true` solo para Professional+ (y cualquier plan durante Trial, por diseño).
- **Gap actual**: no hay un pricing específico del API (ej: requests/mes incluidos, overage) — se resuelve con la **Decisión 2** (ver abajo).

---

## 2. ¿Dónde habita qué? — diagrama visual

```
┌─────────────────────────────────────────────────────────────────────┐
│ C:\Planilla  (ESTE REPO — Pagly SaaS + API Platform)                │
│                                                                     │
│  ✅ Backend .NET                                                    │
│     - Controllers: CalculatorController (/v1/payroll/calculate)     │
│     - Controllers: ApiKeysController   (/api/api-keys)              │
│     - Middleware: RequestId, ProblemDetails, UsageTracking          │
│     - Service: ApiKeyService (hash + prefix lookup)                 │
│     - Auth: ApiKeyAuthenticationHandler (header X-Api-Key)          │
│     - Config: StaticPayrollConfigProvider (Ley 462, 3 fases)        │
│     - Tabla: ApiKeys (migración EF Core)                            │
│     - Rate limiter sliding window                                   │
│                                                                     │
│  ✅ Frontend React (dashboard del SaaS)                             │
│     - /settings/api-keys — ApiKeysPage.tsx                          │
│     - services/apiKeysService.ts                                    │
│     - types/api.ts (ApiKeyDto, CreateApiKeyRequest, ...)            │
│                                                                     │
│  ✅ Deploy                                                          │
│     - app.pagly.clau.com.pa  (CapRover → Docker)                    │
│     - URL del endpoint:  https://app.pagly.clau.com.pa/v1/payroll/  │
│     - URL docs (futuro):  https://app.pagly.clau.com.pa/v1/docs     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ C:\vorluno-pagina web\vorluno-web  (OTRO REPO — marketing Vorluno)  │
│                                                                     │
│  ⛔  Cero código del API Platform.                                  │
│  ✅  Solo contenido que habla del API y linkea al docs:             │
│      - /productos/pagly (landing actual — se le agrega sección dev) │
│      - /productos/pagly/api (futuro developer portal)               │
│      - /blog/integrar-pagly-api (futuro post SEO)                   │
│                                                                     │
│  Deploy: vorluno.dev (Vercel/Next.js)                               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Observaciones clave del landing actual (`vorluno-web`)

Estas observaciones guían el Track B (marketing). Están aquí para que no se pierdan cuando
alternes entre terminales.

1. **Tono**: enterprise-cliente-final, no developer. Los CTAs son "Comenzar gratis" (WhatsApp)
   e "Iniciar sesión" (dashboard SaaS).
2. **Código en la landing**: cero. Un developer necesita ejemplos curl/JS/Python **concretos**
   para convencerse en 10 segundos.
3. **Compliance badges** (Ley 462, CSS, DGI, MITRADEL, SE) son **exactamente** el argumento que
   un developer querría ver para un API de cálculo B2B — ya están ahí pero en contexto
   cliente-final. Hay que reutilizarlos en la sección dev.
4. **Pricing toggle** existe como componente pero mezcla ambos mundos. Si agrego pricing API
   en el toggle actual **confundo**. Mejor una sección dev separada sin toggle.
5. **Schema.org `SoftwareApplication`** está bien para el SaaS; para el API necesitaré un
   schema distinto (`Product` o `Service` con `offers` tipo `OfferCatalog`).
6. **Blog** ya tiene posts técnicos (`calculo-css-panama`, `decimo-tercer-mes-panama`). Es el
   lugar natural para un post "Integrar Pagly API".

---

## 4. Lo que YA está hecho (9 chunks commiteados)

| # | Commit  | Descripción                                                  | Tests |
|---|---------|--------------------------------------------------------------|-------|
| 1 | `2c0db3b` | refactor: `IPayrollConfigProvider` con keyed DI (tenant leakage fix) | +19 |
| 2 | `8aa6e5a` | feat: RFC 7807 + RequestId middleware                        | +17 |
| 3 | `7ce07c0` | feat: `ApiKey` infrastructure (entity + service + hash)      | +60 |
| 4 | `73c761f` | feat: `POST /v1/payroll/calculate` primer endpoint           | +9  |
| 5 | `6cc618e` | feat: usage tracking middleware                              | +8  |
| 6 | `ed81023` | feat: `ApiKeysController` CRUD self-service                  | +11 |
| 7 | `c220ed5` | feat: dashboard React `ApiKeysPage`                          | —   |
| 8 | `8e67c53` | feat: plan gate `CanUseApi` (Free/Starter → 403)             | +5  |
| 9 | `7124036` | feat: rate limiter sliding window                            | +3  |

**Estado global:**
- ✅ Build verde (0 errores .NET, 0 errores TypeScript).
- ✅ **206 tests verdes** (135 Application + 71 Integration).
- ✅ Frontend `npm run build` verde.
- ✅ Migración `AddApiKeys` aplicada en startup (se aplica automático en deploy).

**Capacidades funcionales hoy:**

1. Un Owner abre `app.pagly.clau.com.pa/settings/api-keys` y genera una key.
2. Ese Owner copia el plaintext (`pk_live_abc12345xxxx...`) del modal one-time.
3. Se la da a un developer cliente (ej: Urbis).
4. El cliente hace `POST /v1/payroll/calculate` con header `X-Api-Key`.
5. Response en <100ms con breakdown completo (CSS/SE/ISR, neto, costo patronal).
6. Si un cliente spamea el endpoint → 429 con `Retry-After`.
7. El Owner refresca la página y ve `totalRequests: 1,247`, `lastUsedAt: hace 2min`.
8. Si la key se compromete → Owner revoca con un clic → próxima request recibe 401.

**El sistema ya es 100% deployable a producción.** El resto son mejoras incrementales.

---

## 5. Track A — Cerrar el MVP técnico (este repo)

Estos chunks viven en `C:\Planilla`. **No tocan `vorluno-web`**.

| Chunk | Descripción | Esfuerzo | Bloquea Track B |
|---|---|---|---|
| **10** | Swagger público `/v1/docs` separado del interno. Dos `SwaggerDoc`, `DocInclusionPredicate` por path `/v1/*`. Expuesto en producción con header `X-Api-Key`. XML comments en DTOs. Título "Pagly API v1". | 2-3h | **SÍ** — el landing debe linkear a docs reales. |
| **11** | Contract / golden tests para `PayrollCalculateResponse`. Snapshot del shape canónico (campos + orden + precisión decimal). Falla si alguien rename un field por accidente. | 1h | No bloquea, pero lo quiero antes de exponer públicamente. |
| **12** | Handle del 429 en el dashboard frontend. En `api.ts` detectar status 429 → toast especial con countdown al `Retry-After`. En `ApiKeysPage` señal visual si el Owner genera spam. | 30 min | No bloquea. |
| **13** | Dashboard charts mínimo: en `ApiKeysPage` agregar un bar chart "Top keys by usage" (Recharts) con `totalRequests` de la lista actual. Sin timeseries todavía — eso requiere tabla `ApiUsageRecord` que no agrego sin demanda real. | 1h | No bloquea. |

**Orden dentro del track**: 10 → 11 → 12 → 13.

**Razón del orden**: el 10 desbloquea Track B (el landing necesita linkear a `/v1/docs` real).
El 11 es cleanup de deuda técnica — rápido y protege contra regresiones. El 12 y 13 son UX del
dashboard, no bloquean nada.

### Detalle técnico del Chunk 10 (siguiente)

**Cambios concretos en `Program.cs`:**
- Dos `SwaggerDoc`:
  - `"v1_internal"` (actual) → endpoints `/api/*` del SaaS (requiere gate en producción).
  - `"v1_public"` → endpoints `/v1/*` del API Platform (expuesto abiertamente).
- `DocInclusionPredicate((docName, api) => ...)` que filtra por `ApiDescription.RelativePath`:
  - Si empieza con `v1/` → incluir solo en `v1_public`.
  - Si no → incluir solo en `v1_internal`.
- Security definition `"ApiKeyHeader"` en `v1_public` con `type: apiKey, in: header, name: X-Api-Key`.
- `UseSwaggerUI` con dos endpoints visibles: `/swagger/v1_public/swagger.json` y `/swagger/v1_internal/swagger.json`.
- Routing del Swagger UI público en `/v1/docs` (no `/swagger` que es la ruta interna).
- `IncludeXmlComments` habilitado en el `.csproj` si no lo está.

**Cambios en controllers y DTOs:**
- `CalculatorController.Calculate`: agregar `[ProducesResponseType(401)]`, `[ProducesResponseType(429)]`.
- XML `<summary>` / `<param>` / `<returns>` en `PayrollCalculateRequest`, `PayrollCalculateResponse`, `CalculatorController.Calculate` (completar lo existente).

**Verificación manual del Chunk 10:**
```bash
# 1. Swagger UI público accesible
curl https://app.pagly.clau.com.pa/v1/docs

# 2. OpenAPI spec solo con /v1/*
curl https://app.pagly.clau.com.pa/swagger/v1_public/swagger.json | jq '.paths | keys'
# Debe mostrar solo ["/v1/payroll/calculate"]

# 3. Desde Swagger UI: botón Authorize → pega key real → ejecuta POST
# 4. Recibe 200 con breakdown correcto.
```

---

## 6. Track B — Marketing del API Platform (repo `vorluno-web`)

**Estos chunks NO se ejecutan en este repo.** Se ejecutan en la otra terminal.
Están listados aquí para que tengas la foto completa del plan.

| Chunk | Descripción | Esfuerzo | Depende de |
|---|---|---|---|
| **14** | Sección "Para developers" en la landing actual (`/productos/pagly`). Nueva sección entre `PaglyFeatures` y `PaglyPricing` con: headline, 3 bullets técnicos (cálculo Ley 462 en una llamada, response <100ms, RFC 7807 errores), un bloque **code switcher** (curl / TypeScript / Python / PHP) con ejemplo real del endpoint, link "Ver docs completas →" al Swagger público. **No altera nada del landing existente — solo agrega.** | 2-3h | **Chunk 10** (necesita que `/v1/docs` exista). |
| **15** | **Developer portal dedicado** `/productos/pagly/api`. Página nueva con: hero "API de planilla Panamá para tu producto", "Quickstart en 5 minutos", 4 pasos (obtener key → instalar client → 1 request → recibir breakdown), comparación vs. construir tu propio calculador (tiempo, compliance, costo de bugs), SEO focus en "API planilla Panama" / "calcular CSS API" / "integrar ISR Panama". Schema.org `Product` con `offers` tipo `OfferCatalog`. | 3-4h | **Chunk 10** + idealmente **Chunk 11** (golden tests dan confianza para afirmar "shape estable"). |
| **16** | **Blog post técnico**: *"Integrar Pagly API en tu backend Node.js: cálculos de planilla Panamá con una llamada HTTP"*. SEO long-tail. Code real ejecutable, screenshots del dashboard, link al developer portal. | 2h | **Chunks 10 + 15** (linkea al portal). |

---

## 7. Decisiones de diseño (tomadas, por default A/A/A)

Las tres decisiones del plan original están **resueltas**. Se registran aquí para trazabilidad.

### Decisión 1 — Subdominio del developer portal

- ✅ **Opción A elegida**: integrado en `vorluno-web`.
- **URLs finales**:
  - Landing dev: `vorluno.dev/productos/pagly/api`
  - Docs interactivas: `app.pagly.clau.com.pa/v1/docs`
  - Blog post: `vorluno.dev/blog/integrar-pagly-api-nestjs`
- **Pros**: SEO unificado en `vorluno.dev`, un solo sitio de marketing, cero infra nueva.
- **Contras aceptados**: docs viven en otra URL que el content — aceptable en MVP, se evalúa
  migrar a `developer.pagly.com` cuando haya 10+ clientes activos.
- **Opción B descartada**: subdomain `developer.pagly.com` requiere DNS + deploy extra,
  esfuerzo mayor, no escala hasta que haya masa crítica de clientes.

### Decisión 2 — Pricing del API en el landing

- ✅ **Opción A elegida**: **Add-on al SaaS actual**. Professional/Enterprise ya incluyen
  API (enforzado por `CanUseApi=true` en `PlanFeatures`). Sin página de pricing API separada.
- **Flujo para cliente solo-API**: va por Enterprise custom vía WhatsApp.
- **Razón**: el esfuerzo de B (plan independiente "API-only") son ~4 semanas sin validación
  de demanda. Con A, los primeros 2-3 clientes son Enterprise con trato custom, aprendemos
  qué quieren realmente, y **después** decidimos si B vale la pena.
- **Opción B descartada**: plan independiente `$XX/mes` específico para devs sin dashboard.
  Requeriría: nuevo enum value en `PlanFeatures`, nueva pricing section, `IsApiOnly` flag en
  `Tenant`, Stripe metered billing. Todo eso es un mes de trabajo + Stripe learning curve.

### Decisión 3 — CTAs del developer portal

- ✅ **Opción A elegida**: **WhatsApp** consistente con el landing actual (`wa.me/50769430930`).
  Manual, hands-on, pero funciona para los primeros 5-10 clientes.
- **Escalado**: cuando el volumen justifique un CRM → Opción B (formulario de contacto con
  captura estructurada en `/contacto/api`).
- **Opción C descartada**: self-service signup público — prohibido por ahora, requiere
  captcha + verificación email + Stripe metered. Fuera de scope MVP.

**Las tres decisiones son modificables.** Si en algún punto del futuro cambias de opinión,
este archivo se actualiza y los chunks futuros reflejan el cambio. No hay nada cementado.

---

## 8. Qué NO está en este plan (explícito)

Estas cosas **no se hacen** en el MVP. Son buenas ideas que aparecerán naturalmente cuando
crezca el producto, pero hoy serían prematuras.

| Item | Razón de excluir | Cuándo sí |
|---|---|---|
| **SDK npm** `@vorluno/panama-payroll` | Mantener un SDK requiere versioning + changelog + CI que publique a npm. Costo real: semanas de mantenimiento. | Cuando 2+ clientes lo pidan explícitamente. |
| **Self-service signup público** para API | Requiere captcha (abuse), verificación de email, integración Stripe (billing), legal T&C. Atajos aquí te muerden en producción. | Después de 10+ clientes manuales, cuando el CAC de soporte individual supere el costo del self-service. |
| **Stripe metered billing para overages** | 3-5 días de trabajo. El modelo tier+overage de Stripe tiene una curva de aprendizaje real (usage records, invoice previews, etc.). Solo vale con data real de uso. | Cuando tengas 1 mes de datos de uso real para calibrar pricing. |
| **Tabla `ApiUsageRecord`** (timeseries de cada request) | Schema decision irreversible. Hoy solo necesitas contadores (`TotalRequests`). Crear la tabla antes de saber qué queries de analytics vas a correr = over-engineering. | Cuando un cliente pida "dame mis últimos 30 días de uso hora por hora". |
| **Legal / T&C del API** | Necesita abogado — liability, SLA contractual, data processing agreement, uptime guarantees. Fuera del scope de ingeniería. | Antes del primer contrato pagado > $500/mes. |
| **Status page / uptime monitoring** | Uptime Kuma en CapRover es 1 día de trabajo, pero es **posterior** al MVP marketing — el valor existe solo con audiencia que se preocupe por uptime. | Después del Track B, cuando el API esté en el landing. |
| **Load testing** (k6 / Locust) | Valida el rate limiter bajo carga real. No urgente hasta que un cliente pague por SLA. | Antes de firmar el primer contrato Enterprise con SLA. |
| **Security audit externo** | Pen test por empresa externa. Caro. | Antes del primer cliente Enterprise serio. |

---

## 9. Orden de ejecución recomendado

### Semana 1 — Track A (técnico, en este repo)

| Día | Chunks | Milestone |
|---|---|---|
| Día 1 | **Chunk 10** (Swagger público `/v1/docs`) | Docs navegables en producción. |
| Día 2 | **Chunks 11 + 12** (contract tests + 429 frontend) | Shape del response protegido + UX del dashboard pulida. |
| Día 3 | **Chunk 13** (dashboard charts) | Track A **cerrado**. Pagly SaaS tiene MVP técnico completo + documentación navegable. |

### Semana 2 — Track B (marketing, en `vorluno-web`)

| Día | Chunks | Milestone |
|---|---|---|
| Día 1-2 | **Chunk 14** (sección devs en landing actual) | Visibilidad del API en la landing existente. |
| Día 3-4 | **Chunk 15** (developer portal dedicado) | URL propia del API con quickstart. |
| Día 5 | **Chunk 16** (blog post) | Track B **cerrado**. API Platform visible con SEO básico. |

**Total**: ~8-10 días de trabajo concentrado para cerrar el MVP completo (técnico + GTM).

### Política de parada entre tracks

Después de **Track A completo** (chunks 10-13 en este repo), **pausa obligatoria**. Motivo:
- Ver el Swagger público funcionando en producción.
- Decidir si el landing necesita cambios basándote en cómo se ve la docs real.
- Cambiar de repo (de `C:\Planilla` a `vorluno-web`) es un context switch — mejor hacerlo
  una sola vez con data real en vez de ir y venir.

---

## 10. Cómo ejecutar el siguiente chunk

Cuando quieras avanzar, me dices:

> **"Continúa con Chunk N"** (ej: "Continúa con Chunk 10")

Y yo:
1. Releo este archivo para saber dónde estamos.
2. Ejecuto el chunk punto por punto.
3. Actualizo la sección 4 (changelog) al completarlo.
4. Corro `dotnet build` + `dotnet test` verdes antes de commit.
5. Si toca frontend: `npm run build` antes de commit.
6. Commit con prefijo `feat:` / `refactor:` / `docs:`.
7. Te reporto el resultado con el hash del commit.

**Reglas que sigo automáticamente:**
- Un commit por chunk (no acumulo varios en uno).
- Nunca toco archivos fuera del alcance del chunk.
- **Nunca toco nada en `vorluno-web` desde este repo.**
- Tests verdes siempre antes de commit.
- Sin `Co-Authored-By` en los commits (memoria del usuario).

---

## 11. Cosas que viven aquí vs. en `vorluno-web` (referencia rápida)

### ⛔ NUNCA en `C:\Planilla`

| Cosa | Dónde vive realmente |
|---|---|
| Landing page `/productos/pagly` | `vorluno-web/app/productos/pagly/page.tsx` |
| Developer portal público con copy | `vorluno-web/app/productos/pagly/api/page.tsx` (Chunk 15) |
| Blog posts de SEO | `vorluno-web/app/blog/*/page.tsx` |
| Diseño gráfico, imágenes, videos | `vorluno-web/public/products/pagly/` |
| Schema.org de marketing | `vorluno-web` (dentro de cada `page.tsx`) |
| Pricing de marketing | `vorluno-web/lib/pagly-data.ts` |
| CTAs a WhatsApp (`wa.me/50769430930`) | `vorluno-web` |

### ⛔ NUNCA en `vorluno-web`

| Cosa | Vive en |
|---|---|
| Endpoint `/v1/payroll/calculate` | `C:\Planilla` (este repo) |
| Validación de API keys (hash lookup) | `C:\Planilla` |
| Rate limiter | `C:\Planilla` |
| Global query filters multi-tenant | `C:\Planilla` |
| Tabla `ApiKeys` en Postgres | servida por `C:\Planilla` |
| Swagger UI (`/v1/docs`) | `C:\Planilla` — expuesto por .NET |
| Contract / golden tests | `C:\Planilla` (tests/) |

---

## 12. Archivo hermano para Track B

El roadmap equivalente del Track B debería vivir en:

```
C:\vorluno-pagina web\vorluno-web\docs\API-PLATFORM-MARKETING-ROADMAP.md
```

(Se crea cuando abras esa terminal y lo pidas — aún no existe.)

Ese archivo documentará solo los chunks 14, 15, 16. Linkeará a este archivo como
"repo hermano" y mencionará que el backend del API vive en `app.pagly.clau.com.pa` —
nunca replica código.

**Regla**: los dos roadmaps se referencian mutuamente pero nunca se pisan. Cada terminal
mira solo su archivo.

---

## 13. Estado actual en una sola línea

> **Ya tienes un API Platform B2B funcional en producción. 9 chunks commiteados, 206 tests verdes. Siguiente: Chunk 10 (Swagger público `/v1/docs`) — pre-requisito para el marketing.**

---

*Última actualización: chunk 9 commiteado (`7124036` feat: rate limiter sliding window).*
*Próximo: Chunk 10 (Swagger público `/v1/docs`).*
