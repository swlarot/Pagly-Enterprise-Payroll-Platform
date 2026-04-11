# API Platform B2B — Roadmap completo

> **Este archivo vive en `C:\Planilla` (repo Pagly) y es el master del plan completo.**
>
> Documenta todo el plan del API Platform B2B, dividido en:
> - **Track A** — código del producto (backend + frontend dashboard), en `C:\Planilla`.
> - **Track B** — marketing (landing, developer portal, blog), en `C:\vorluno-pagina web\vorluno-web`.
>
> **Ambos tracks se ejecutan desde la misma sesión de Claude Code.** Cuando toca Track B,
> Claude hace `cd` a `vorluno-web`, ejecuta los cambios, corre build y tests, y commitea
> en **ese** repo. Cuando toca Track A, vuelve a `C:\Planilla`. **Este archivo es la fuente
> única de verdad del plan**; no existe un "archivo hermano" duplicado en vorluno-web.

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

## 4. Lo que YA está hecho (11 chunks commiteados)

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
| 10 | `7c16295` | feat: Swagger público `/v1/docs` con DocInclusionPredicate | — |
| 11 | `f5f801a` | test: contract/golden test de `PayrollCalculateResponse` | +1 |
| **12** | *(next)* | **feat: handle del 429 en el dashboard frontend** | — |

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

| Chunk | Descripción | Esfuerzo | Bloquea Track B | Estado |
|---|---|---|---|---|
| **10** | Swagger público `/v1/docs` separado del interno. Dos `SwaggerDoc`, `DocInclusionPredicate` por path `/v1/*`. Expuesto en producción con header `X-Api-Key`. XML comments en DTOs. Título "Pagly API v1". | 2-3h | **SÍ** — el landing debe linkear a docs reales. | ✅ **Done** |
| **11** | Contract / golden tests para `PayrollCalculateResponse`. Snapshot del shape canónico (campos + orden + precisión decimal). Falla si alguien rename un field por accidente. | 1h | No bloquea, pero lo quiero antes de exponer públicamente. | ✅ **Done** |
| **12** | Handle del 429 en el dashboard frontend. En `api.ts` detectar status 429 → toast especial con countdown al `Retry-After`. En `ApiKeysPage` señal visual si el Owner genera spam. | 30 min | No bloquea. | ✅ **Done** |
| **13** | Dashboard charts mínimo: en `ApiKeysPage` agregar un bar chart "Top keys by usage" (Recharts) con `totalRequests` de la lista actual. Sin timeseries todavía — eso requiere tabla `ApiUsageRecord` que no agrego sin demanda real. | 1h | No bloquea. | ⏳ Next |

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

**Estos chunks se ejecutan en `C:\vorluno-pagina web\vorluno-web`.**

Claude hace `cd /c/vorluno-pagina\ web/vorluno-web` al empezar el chunk, ejecuta todos
los cambios ahí, corre `npm run build` / `npm run lint` en ese repo, y commitea en
**ese** repo. El roadmap master sigue siendo este archivo — los commits de vorluno-web
lo referencian en su mensaje como `ver pagly/docs/API-PLATFORM-ROADMAP.md`.

| Chunk | Descripción | Esfuerzo | Depende de | Estado |
|---|---|---|---|---|
| **14** | Sección "Para developers" en la landing actual (`/productos/pagly`). Nueva sección entre `PaglyFeatures` y `PaglyPricing` con: headline, 3 bullets técnicos (cálculo Ley 462 en una llamada, response <100ms, RFC 7807 errores), un bloque **code switcher** (curl / TypeScript / Python / PHP) con ejemplo real del endpoint, link "Ver docs completas →" al Swagger público. **No altera nada del landing existente — solo agrega.** | 2-3h | **Chunk 10** ✅ | ⏳ |
| **15** | **Developer portal dedicado** `/productos/pagly/api`. Página nueva con: hero "API de planilla Panamá para tu producto", "Quickstart en 5 minutos", 4 pasos (obtener key → instalar client → 1 request → recibir breakdown), comparación vs. construir tu propio calculador (tiempo, compliance, costo de bugs), SEO focus en "API planilla Panama" / "calcular CSS API" / "integrar ISR Panama". Schema.org `Product` con `offers` tipo `OfferCatalog`. | 3-4h | **Chunk 10** ✅ + idealmente **Chunk 11** (golden tests dan confianza para afirmar "shape estable"). | ⏳ |
| **16** | **Blog post técnico**: *"Integrar Pagly API en tu backend Node.js: cálculos de planilla Panamá con una llamada HTTP"*. SEO long-tail. Code real ejecutable, screenshots del dashboard, link al developer portal. | 2h | **Chunks 10 ✅ + 15** (linkea al portal). | ⏳ |

### Workflow de Claude en Track B

Cuando el usuario dice "continúa con chunk 14" (o 15/16):

1. **`cd`** a `C:\vorluno-pagina web\vorluno-web` (path con espacio — usar comillas o escape).
2. Leer el estado del repo: `git status`, `git log --oneline -5`, `ls app/productos/pagly/`.
3. Ejecutar el chunk: editar/crear archivos dentro de `vorluno-web/*`.
4. Build: `npm run build` (o `bun run build` si `bun.lock` existe — verificar primero).
5. Lint si existe: `npm run lint`.
6. Commit en `vorluno-web` con mensaje tipo:
   ```
   feat: [descripción del chunk]

   Chunk N del API Platform roadmap.
   Ver referencia en pagly-repo/docs/API-PLATFORM-ROADMAP.md sección 6.
   ```
7. **`cd`** de vuelta a `C:\Planilla`.
8. Actualizar este archivo (sección 4 changelog + sección 6 estado) con el commit hash del repo vorluno-web.
9. Commit en `C:\Planilla` solo del cambio al roadmap:
   ```
   docs: roadmap — mark chunk N done (commit <hash> in vorluno-web)
   ```

### Reglas que Claude sigue automáticamente en Track B

- **NO duplica código** entre los dos repos. Si el chunk 14 necesita el JSON de
  `PayrollCalculateResponse`, no copia el record C# — escribe un ejemplo en TypeScript/JSON
  hardcoded o importa el tipo generado desde el OpenAPI spec.
- **NO hace requests HTTP reales** al backend durante el build — los ejemplos de código
  son estáticos (literals).
- **Las URLs del API** en la landing son **hardcoded** a `https://app.pagly.clau.com.pa/v1/*`
  (no variables de entorno por ahora — YAGNI).
- **Imágenes/screenshots** del dashboard → si los chunks los necesitan, Claude pide al
  usuario que los aporte (o usa placeholders existentes del repo vorluno-web).
- **No toca `vorluno-web/app/productos/pagly/page.tsx` existente** más allá de agregar
  la nueva sección del chunk 14. Preserva todo lo demás intacto.

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

Cuando el usuario dice:

> **"Continúa con Chunk N"** (ej: "Continúa con Chunk 11")

Claude determina automáticamente **en qué repo vive ese chunk** según el número:

| Rango | Repo | Lenguaje / Stack |
|---|---|---|
| Chunks 10–13 | `C:\Planilla` | .NET 9 + React + PostgreSQL |
| Chunks 14–16 | `C:\vorluno-pagina web\vorluno-web` | Next.js 16 + Tailwind + GSAP |

### Workflow genérico (aplica a ambos tracks)

1. **Verificar repo correcto**: `pwd` → si no está en el repo del chunk, hacer `cd`.
2. **Leer estado**: `git status`, `git log --oneline -5`, `ls` de la carpeta relevante.
3. **Releer este archivo** (`docs/API-PLATFORM-ROADMAP.md`) — siempre vive en `C:\Planilla`,
   así que si Claude está en vorluno-web, usa la ruta absoluta para leerlo.
4. **Ejecutar el chunk** editando/creando archivos del repo actual.
5. **Build** en el repo actual:
   - Track A backend: `dotnet build` + `dotnet test`.
   - Track A frontend: `cd src/UI/Planilla.Web/ClientApp && npx tsc --noEmit && npm run build`.
   - Track B: `npm run build` (o `bun run build` si hay `bun.lock`).
6. **Commit en el repo actual** con prefijo `feat:` / `refactor:` / `docs:`.
7. **Si el chunk es del Track B**: hacer `cd` de vuelta a `C:\Planilla`, actualizar este
   archivo (sección 4 + 6), commit del roadmap como `docs:`.
8. **Reportar al usuario** el hash del commit (o los dos si Track B) + estado de tests.

### Reglas invariables (ambos tracks)

- **Un commit por chunk** en el repo al que pertenece. No acumular.
- **Tests verdes siempre** antes de commit. Sin regresiones.
- **Sin `Co-Authored-By`** en los commits (memoria del usuario).
- **Nunca duplicar código** entre los dos repos — si necesitas data del otro, hardcodea
  el valor o usa el OpenAPI spec como fuente de verdad.
- **Al cambiar de repo**, `cd` con el path absoluto completo. Nunca `cd ..` a ciegas.
- **El archivo `.claude/scheduled_tasks.lock`** solo existe en `C:\Planilla` — ignorar.
- **Archivos no versionados** (`.agents-backup/`, `CLAUDE.local.md`, etc.) → nunca agregar al commit.

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

## 12. Un solo roadmap master (este archivo)

**No hay archivo hermano en `vorluno-web`.** El roadmap master es este archivo
(`C:\Planilla\docs\API-PLATFORM-ROADMAP.md`) — fuente única de verdad del plan completo
Track A + Track B.

**¿Por qué un solo archivo?**
- Evita desincronización entre dos documentos (el mayor enemigo de la documentación).
- El plan es un documento evolutivo: el usuario pregunta "¿dónde estamos?" y Claude
  abre **un** archivo, no dos.
- Los commits de vorluno-web pueden referenciar este archivo en su mensaje
  (ruta: `pagly/docs/API-PLATFORM-ROADMAP.md`) sin duplicar contenido.

**¿Cómo encuentra Claude este archivo cuando está trabajando en vorluno-web?**
Usa la ruta absoluta: `C:\Planilla\docs\API-PLATFORM-ROADMAP.md`. Claude puede leerlo
con el tool `Read` sin importar en qué directorio esté el shell.

**¿Y si el usuario busca el roadmap desde vorluno-web?**
Puede abrir cualquier commit reciente de vorluno-web, leer el mensaje del commit, y
ahí verá la referencia al archivo master. Alternativamente, el `README.md` de
`vorluno-web/app/productos/pagly/api/` (cuando exista, chunk 15) linkeará al master
con un comentario tipo:

```markdown
<!-- Plan completo del API Platform: ../../../../C:/Planilla/docs/API-PLATFORM-ROADMAP.md -->
```

(O mejor: un link a un doc público si el repo Pagly está en GitHub — por ahora es privado,
así que el comentario local basta.)

---

## 13. Estado actual en una sola línea

> **Track A: 12/13 chunks done. Track B: 0/3 chunks done. Claude ejecuta ambos tracks desde la misma sesión. Siguiente: Chunk 13 (bar chart "Top keys" en `ApiKeysPage`) en `C:\Planilla`.**

### Resumen por track

| Track | Repo | Chunks | Done | Pending | Tests |
|---|---|---|---|---|---|
| **A** (producto) | `C:\Planilla` | 10-13 (4 chunks) | 10, 11, 12 ✅ | 13 | 207 verdes |
| **B** (marketing) | `C:\vorluno-pagina web\vorluno-web` | 14-16 (3 chunks) | ninguno | 14, 15, 16 | N/A (Next.js build) |

### Orden recomendado a futuro

Claude ejecuta en este orden cuando el usuario avance:

1. **Chunk 11** (Planilla) — contract tests. ~1h.
2. **Chunk 12** (Planilla) — 429 frontend. ~30 min.
3. **Chunk 13** (Planilla) — bar chart. ~1h.
4. **Parada opcional**: revisar Track A completo antes de cambiar de repo.
5. **Chunk 14** (vorluno-web) — sección devs en landing. ~2-3h.
6. **Chunk 15** (vorluno-web) — developer portal dedicado. ~3-4h.
7. **Chunk 16** (vorluno-web) — blog post. ~2h.

El usuario puede saltar chunks (ej: "continúa con chunk 14") y Claude hará `cd` al
repo correcto automáticamente, pero **para Track B es obligatorio que Chunk 10 esté
done** (ya está ✅).

---

*Última actualización: chunk 12 commiteado — feat: handle del 429 en el dashboard frontend (listener global `rateLimitExceeded` en App.tsx + soporte de body RFC 7807 en api.ts).*
*Próximo: Chunk 13 (bar chart "Top keys by usage" en `ApiKeysPage`).*
