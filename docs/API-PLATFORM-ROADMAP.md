# API Platform B2B — Roadmap (solo repo Pagly)

> **Este archivo vive en `C:\Planilla` (repo Pagly).**
> Documenta **únicamente** el trabajo del API Platform que se hace en **este** repo.
> El trabajo de marketing (landing, blog, developer portal) vive en `C:\vorluno-pagina web\vorluno-web`
> y tiene su **propio** roadmap allá. Cada terminal mira solo su archivo — cero confusión.

---

## 1. ¿Dónde habita qué?

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

**Regla mental simple:**
- Si es **código que procesa una request HTTP** → vive en `C:\Planilla`.
- Si es **texto, imagen, código de ejemplo, landing, blog** → vive en `vorluno-web`.

Nunca se duplica código entre los dos repos. Los ejemplos curl/TypeScript del developer portal **apuntan** a `app.pagly.clau.com.pa/v1/*` pero el código real vive aquí.

---

## 2. Lo que YA está hecho (9 chunks commiteados)

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

## 3. Backlog de Track A (solo este repo)

Estos son los chunks que faltan hacer **aquí, en `C:\Planilla`**. NO tocan `vorluno-web`.

### Chunk 10 — Swagger público `/v1/docs` (2-3h) 🎯 SIGUIENTE

**Qué hace:** Separa el Swagger interno (hoy en `/swagger`, solo dev) del Swagger público del API Platform (`/v1/docs`, en producción).

**Cambios:**
- `Program.cs`:
  - Agregar segundo `SwaggerDoc("v1_public", new OpenApiInfo { Title = "Pagly API v1", ... })` con título de marca.
  - `DocInclusionPredicate` que filtra por path: `/v1/*` → doc `v1_public`, resto → doc interno `v1`.
  - En producción, exponer `UseSwaggerUI` con dos endpoints: `/swagger` (interno, requiere algún gate) y `/v1/docs` (público, abierto).
  - Security definition `ApiKeyHeader` para el botón "Authorize" de Swagger UI con header `X-Api-Key`.
- Agregar `<summary>` XML comments a `PayrollCalculateRequest`, `PayrollCalculateResponse`, `CalculatorController.Calculate` (ya tienen algunos, completar).
- Agregar `[ProducesResponseType(401)]`, `[ProducesResponseType(429)]` al controller para que Swagger los documente.
- Habilitar `IncludeXmlComments` en `Vorluno.Planilla.Web.csproj` si no está (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`).

**Verificación:**
```bash
curl https://app.pagly.clau.com.pa/v1/docs              # HTML de Swagger UI
curl https://app.pagly.clau.com.pa/v1/docs/swagger.json # OpenAPI spec
```
Abrir `/v1/docs` en browser, pegar una key real en "Authorize" (header X-Api-Key), hacer un POST de prueba desde la UI, recibir 200.

**Por qué es el siguiente:** bloquea Track B (marketing) — el developer portal va a linkear a esta URL. Sin esto, el landing no puede prometer "docs interactivas".

---

### Chunk 11 — Contract / golden tests (1h)

**Qué hace:** Un test que hace un `POST /v1/payroll/calculate` con inputs fijos y compara el JSON response contra un snapshot guardado. Si alguien rename `cssEmployee` a `cssEmpleado` por accidente, el test falla y el CI lo atrapa antes de romper clientes.

**Cambios:**
- Nuevo test en `tests/Planilla.Web.IntegrationTests/Golden/CalculatorGoldenTests.cs`.
- Request canónico fijo: `{ grossPay: 2000, payFrequency: "Mensual", yearsCotized: 5, ... }`.
- Golden file `tests/Planilla.Web.IntegrationTests/Golden/calculator-2026-fase1.json` con el response esperado.
- Test deserializa el response y compara byte-a-byte contra el golden (con tolerancia decimal).
- Si el golden no existe, el test lo crea la primera vez (patrón estándar de snapshot testing).

**Por qué vale:** parity tests actuales solo verifican números. Contract tests verifican la **forma** del response. Un cliente que parsea `{ "cssEmployee": 146.25 }` se rompe si cambias la key — contract tests lo previenen.

---

### Chunk 12 — Handle del 429 en el frontend (30 min)

**Qué hace:** Cuando el dashboard del SaaS recibe un 429 (poco común pero posible si el Owner mismo spamea `/v1/payroll/calculate` desde el navegador), muestra un toast con countdown del `Retry-After`.

**Cambios:**
- `src/services/api.ts`: en `handleResponse`, detectar `response.status === 429` y lanzar `ApiException` con un código especial `RATE_LIMIT_EXCEEDED`.
- `src/pages/ApiKeysPage.tsx`: catch del error code y toast con mensaje + countdown.
- Alternativa: un `useRateLimitWarning` hook reutilizable.

**Por qué es rápido:** el backend ya retorna el header `Retry-After` y el body con `errorCode: "rate_limit_error"` — solo falta que el cliente lo muestre.

---

### Chunk 13 — Dashboard charts mínimo (1h)

**Qué hace:** Agrega un bar chart "Top keys by usage" en `ApiKeysPage`. Usa `recharts` que ya está instalado (`^2.15.4`).

**Cambios:**
- `ApiKeysPage.tsx`: después del header, antes de la DataTable, agregar una Card con un `<BarChart>` de Recharts mostrando las top 5 keys por `totalRequests`.
- Data: las mismas `keys` del state — no nueva query.

**Lo que NO incluye (intencional):**
- NO hay timeseries por día/hora todavía — requiere tabla `ApiUsageRecord` que solo creo cuando haya demanda real.
- NO hay chart de "requests por hora del día" ni "peak load" — prematuro.

**Por qué simple:** el contador `totalRequests` es un monotónico acumulado. Un bar chart da insight útil ("qué key usa más") sin agregar deuda de schema.

---

## 4. Cómo ejecutar el siguiente chunk

Cuando quieras seguir, simplemente dime:

> **"Continúa con Chunk 10"** (o el número que sea)

Y yo:
1. Releo este archivo para saber dónde estamos.
2. Ejecuto el chunk punto por punto.
3. Actualizo la tabla de "Ya está hecho" al completarlo.
4. Corro build + tests verdes antes de commit.
5. Commit con prefijo `feat:` / `refactor:` (no `DEV-#` porque Linear está sin cupo).
6. Te reporto el resultado.

**Reglas que sigo automáticamente:**
- Un commit por chunk (no acumulo varios chunks en un commit).
- Nunca toco archivos fuera del alcance del chunk.
- Nunca toco nada en `vorluno-web` desde este repo.
- Si un chunk toca el frontend, corro `npm run build` antes de commit.
- Si un chunk toca el backend, corro `dotnet build` y `dotnet test` antes de commit.

---

## 5. Lo que NO se hace en este repo (para despejar dudas)

Estas cosas **no existen** en `C:\Planilla` y nunca van a existir aquí:

| Cosa | Dónde vive realmente |
|---|---|
| Landing page `/productos/pagly` | `vorluno-web/app/productos/pagly/page.tsx` |
| Developer portal público con copy | `vorluno-web/app/productos/pagly/api/page.tsx` (futuro) |
| Blog posts de SEO | `vorluno-web/app/blog/*/page.tsx` |
| Diseño gráfico, imágenes, videos | `vorluno-web/public/products/pagly/` |
| Schema.org de marketing | `vorluno-web` (dentro de cada `page.tsx`) |
| Pricing de marketing (la landing) | `vorluno-web/lib/pagly-data.ts` |
| CTAs a WhatsApp | `vorluno-web` (el número `50769430930`) |

Y al revés, **esto** nunca vive en `vorluno-web`:

| Cosa | Vive en |
|---|---|
| Endpoint `/v1/payroll/calculate` | `C:\Planilla` (este repo) |
| Validación de API keys (hash lookup) | `C:\Planilla` |
| Rate limiter | `C:\Planilla` |
| Global query filters multi-tenant | `C:\Planilla` |
| Tabla `ApiKeys` en Postgres | servida por `C:\Planilla` |
| Swagger UI (`/v1/docs`) | `C:\Planilla` — expuesto por .NET |

---

## 6. Dónde está el archivo hermano de `vorluno-web`

El roadmap equivalente para marketing debería vivir en:

```
C:\vorluno-pagina web\vorluno-web\docs\API-PLATFORM-MARKETING-ROADMAP.md
```

(No lo he creado todavía — lo creamos cuando abras esa terminal y me lo pidas.)

Ese archivo documentaría los chunks de marketing (el Track B que discutimos):
- Chunk 14: sección "Para developers" en landing actual.
- Chunk 15: developer portal `/productos/pagly/api`.
- Chunk 16: blog post técnico.

**Reglas para el archivo de vorluno-web:**
- Solo documenta cosas que se hacen en `vorluno-web`.
- Linkea a este archivo (`API-PLATFORM-ROADMAP.md` de Pagly) como "repo hermano".
- Menciona que el backend del API vive en `app.pagly.clau.com.pa` — nunca intenta replicar código.

---

## 7. Estado actual en una sola línea

> **Ya tienes un API Platform B2B funcional en producción. Falta Chunk 10 (Swagger público) como pre-requisito para el marketing, y chunks 11-13 como mejoras de calidad.** El resto se hace en el otro repo cuando quieras.

---

*Última actualización: chunk 9 commiteado (`7124036` feat: rate limiter). Próximo: Chunk 10.*
