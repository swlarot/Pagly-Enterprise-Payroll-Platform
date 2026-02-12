# Claude Skills: guía definitiva e implementación para Pagly

**Claude Skills son paquetes modulares de instrucciones, scripts y recursos que Claude carga dinámicamente para mejorar su rendimiento en tareas especializadas.** Para Pagly, representan una evolución directa de los archivos `.md` en `/docs/agentes/` que ya usas, pero con descubrimiento automático, carga progresiva de contexto y un ecosistema de +41,000 skills compartibles. La mayor oportunidad inmediata está en instalar skills existentes de React/shadcn/ui/.NET y crear un skill personalizado de payroll panameño — algo que **no existe en ningún directorio público** y sería exclusivo de Pagly.

---

## Qué son los Skills y cómo funcionan realmente

Un Skill es una carpeta con un archivo `SKILL.md` obligatorio (YAML frontmatter + instrucciones en Markdown) y archivos opcionales de soporte (scripts, templates, referencias). Claude los descubre automáticamente al inicio de sesión leyendo solo el `name` + `description` de cada skill (~100 tokens), y carga el contenido completo únicamente cuando determina que es relevante para la tarea actual. Esta **carga progresiva** es la diferencia clave con CLAUDE.md: los archivos CLAUDE.md se inyectan siempre en el contexto, mientras que los skills solo consumen tokens cuando se necesitan.

El formato de un SKILL.md mínimo es:

```yaml
---
name: mi-skill
description: Qué hace este skill y cuándo usarlo
---
# Instrucciones
[Contenido que Claude sigue cuando el skill está activo]
```

Los skills viven en tres ubicaciones con prioridad descendente: **enterprise > personal > proyecto**:

| Ubicación | Alcance | Uso |
|-----------|---------|-----|
| `~/.claude/skills/nombre/SKILL.md` | Personal (todos tus proyectos) | Workflows individuales, preferencias |
| `.claude/skills/nombre/SKILL.md` | Proyecto (versionable con Git) | Convenciones de equipo, compartible |
| Plugins instalados | Plugin (namespace separado) | Skills de terceros |

La estructura completa de un skill permite archivos de soporte:

```
mi-skill/
├── SKILL.md              # Obligatorio: instrucciones principales
├── templates/            # Opcional: plantillas para Claude
├── examples/             # Opcional: ejemplos de output esperado
├── scripts/              # Opcional: scripts ejecutables
└── references/           # Opcional: documentación de referencia
```

**Dato crítico para ti**: tus archivos en `/docs/agentes/` seguirán funcionando como contexto cargado vía `--add-dir` o referenciado en CLAUDE.md. Los skills en `.claude/skills/` son complementarios — no reemplazan, mejoran. La regla es: CLAUDE.md para convenciones cortas que siempre aplican; skills para workflows complejos que solo aplican en contextos específicos.

---

## skills.sh y el ecosistema: tres capas que debes conocer

**skills.sh** (https://skills.sh/) **no es un producto oficial de Anthropic** — es un directorio comunitario construido por Vercel que indexa skills de repositorios GitHub públicos. Funciona con un CLI propio: `npx skills add owner/repo@skill-name`. Es actualmente el registro más grande del ecosistema.

El ecosistema tiene tres capas diferenciadas:

**Capa 1 — Anthropic oficial**: El repositorio `anthropics/skills` en GitHub contiene skills de referencia (PDF, DOCX, XLSX, PPTX, webapp-testing, skill-creator, MCP builder). Estos son los que potencian las capacidades de documentos en claude.ai. Se instalan en Claude Code vía `/plugin marketplace add anthropics/skills`.

**Capa 2 — Partners certificados**: Disponibles en claude.com/connectors, incluyen skills de Figma (diseño-a-código), Atlassian (Jira/Confluence), Notion, Stripe, Vercel, Cloudflare, Sentry y Zapier. Están diseñados para funcionar con sus MCP connectors respectivos.

**Capa 3 — Comunidad abierta**: El estándar abierto Agent Skills (agentskills.io, publicado diciembre 2025) ha sido adoptado por Microsoft (VS Code, GitHub Copilot), OpenAI (Codex CLI), Cursor y Gemini CLI. **Un mismo SKILL.md funciona en todas estas herramientas.** Los directorios comunitarios incluyen skills.sh, skillsmp.com (+160K skills indexados), skillsdirectory.com (+41K), y repos curados como `VoltAgent/awesome-agent-skills` (+300 skills de equipos oficiales).

Para instalar skills desde skills.sh:
```bash
npx skills find react          # Buscar skills
npx skills add vercel-labs/agent-skills@vercel-react-best-practices  # Instalar
npx skills check               # Verificar actualizaciones
```

---

## Catálogo de skills existentes directamente útiles para Pagly

### Frontend React / UI / UX (MÁXIMA PRIORIDAD)

| Skill | Fuente | Por qué importa para Pagly |
|-------|--------|---------------------------|
| **frontend-design** | `anthropics/claude-code` (plugin oficial) | Genera interfaces production-grade con React, Tailwind, shadcn/ui. Evita la estética genérica "AI slop" |
| **tailwind-v4-shadcn** | `jezweb/claude-skills` | Previene **8 errores documentados** de Tailwind v4 + shadcn. Sin este skill: ~65K tokens desperdiciados en debugging; con skill: ~20K tokens (70% reducción) |
| **react-hook-form-zod** | `jezweb/claude-skills` | React Hook Form + Zod con patrones shadcn, **multi-step forms**, warnings de performance para +300 campos — crítico para formularios de planilla |
| **Vercel React Best Practices** | `vercel-labs/agent-skills` | 45+ reglas en 8 categorías con prioridad CRITICAL→LOW para React/Next.js |
| **Google Labs shadcn-ui** | `google-labs-code/stitch-skills` | Descubrimiento, instalación y personalización de componentes shadcn/ui |
| **Mastering TypeScript** | `SpillwaveSolutions` | TypeScript 5.9+, React 19, Zustand, Zod, patrones enterprise |
| **Frontend Code Review** | `@staruhub/ClaudeSkills` | Review de código: seguridad (XSS, CSRF), accesibilidad (WCAG), performance |

### Backend .NET / Clean Architecture

| Skill | Fuente | Relevancia |
|-------|--------|-----------|
| **dotnet-skills** (30 skills + 5 agents) | `Aaronontheweb/dotnet-skills` | C#, EF Core patterns, dependency injection, API design, database performance, testing — el más completo |
| **csharp-developer** | `jeffallan/claude-skills` | C# con .NET 8+, ASP.NET Core, EF Core, MediatR, CQRS |
| **dotnet-backend-patterns** | `wshobson/agents` | Clean Architecture: Domain/Application/Infrastructure/Api layers |
| **postgresql-best-practices** | `mindrally/skills` | Diseño de schema, optimización de queries, administración |

### DevOps / Deployment

| Skill | Fuente | Utilidad |
|-------|--------|---------|
| **docker** | `mindrally/skills` | Best practices de containerización |
| **ci-cd-best-practices** | `mindrally/skills` | Pipelines automatizados, estrategias de deployment |
| **devops-engineer** | `jeffallan/claude-skills` | CI/CD, IaC, automatización de deployment |

### Lo que NO existe y debes crear custom
- **Payroll panameño / compliance CSS/ISR** — ningún directorio tiene esto
- **CapRover deployment** — no hay skills específicos
- **Multi-tenant SaaS architecture** — solo el skill `architecture` de `carlheath/ogmios` lo menciona tangencialmente

---

## Cómo usar skills en Claude Code CLI paso a paso

### Activar skills (ya debería estar activo)

Claude Code detecta automáticamente skills en `~/.claude/skills/` y `.claude/skills/`. No hay "activación" manual. Puedes verificar que tus skills están cargados con el comando `/context` dentro de una sesión.

### Instalar skills existentes

```bash
# Método 1: Plugin marketplace oficial
/plugin marketplace add anthropics/skills
/plugin install example-skills@anthropic-agent-skills

# Método 2: skills.sh CLI
npx skills add jezweb/claude-skills@tailwind-v4-shadcn
npx skills add jezweb/claude-skills@react-hook-form-zod
npx skills add vercel-labs/agent-skills@vercel-react-best-practices

# Método 3: Manual (clonar y copiar)
git clone https://github.com/Aaronontheweb/dotnet-skills.git /tmp/dotnet-skills
cp -r /tmp/dotnet-skills/skills/* ~/.claude/skills/

# Método 4: Agregar el shadcn MCP server (complementario)
claude mcp add --transport http shadcn https://www.shadcn.io/api/mcp
```

### Usar skills en sesión

Los skills se invocan de dos formas. **Automática**: Claude detecta por contexto que un skill es relevante y lo carga solo (por ejemplo, si pides "crear un formulario de empleados", cargará automáticamente el skill de react-hook-form-zod si está instalado). **Manual**: escribes `/nombre-del-skill` como slash command.

### Opciones avanzadas del frontmatter

```yaml
---
name: deploy-pagly
description: Deploy Pagly to CapRover production
disable-model-invocation: true    # Solo TÚ puedes invocarlo (no auto)
context: fork                      # Se ejecuta en sub-agente aislado
agent: Explore                     # Tipo de sub-agente
allowed-tools: Bash(git *), Read   # Restricción de herramientas
---
```

El campo `disable-model-invocation: true` es esencial para skills con side effects como deployment — previene que Claude lo ejecute espontáneamente.

---

## Templates de skills personalizados para Pagly

### Skill 1: Pagly React Component Generator (MÁXIMA PRIORIDAD)

Crear `.claude/skills/pagly-react/SKILL.md`:

```yaml
---
name: pagly-react
description: >
  Generate React 19 components for Pagly payroll SaaS.
  Use when creating components, forms, tables, wizards, or dashboard widgets.
  Follows project conventions: TypeScript strict, shadcn/ui, Tailwind CSS,
  React Hook Form + Zod for forms, TanStack Table for data grids.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---
# Pagly React Component Specialist

## Stack
- React 19, TypeScript strict (nunca `any`)
- Tailwind CSS + shadcn/ui (siempre usar `cn()` de @/lib/utils)
- React Hook Form + Zod para formularios
- TanStack Table para tablas de datos
- Zustand para estado global, React Query para estado servidor

## Estructura de Componente
```tsx
interface Props {
  className?: string;
  tenantId: string; // SIEMPRE requerido en multi-tenant
}

export function ComponentName({ className, tenantId, ...props }: Props) {
  return (
    <div className={cn("base-classes", className)}>
      {/* contenido */}
    </div>
  );
}
```

## Patrón de Formulario Payroll
```tsx
const employeeSchema = z.object({
  cedula: z.string().regex(/^\d{1,2}-\d{1,4}-\d{1,6}$/, "Formato: X-XXXX-XXXXXX"),
  nombreCompleto: z.string().min(2),
  salarioBruto: z.number().positive(),
  tipoContrato: z.enum(["PERMANENTE", "DEFINIDO", "OBRA"]),
});

// Usar mode: "onSubmit" para formularios grandes (>50 campos)
const form = useForm({ resolver: zodResolver(schema), mode: "onSubmit" });
```

## Reglas Inquebrantables
1. SIEMPRE incluir `tenantId` en props y API calls
2. SIEMPRE usar componentes shadcn/ui — nunca HTML raw para inputs, buttons, selects
3. SIEMPRE agregar aria-labels a elementos interactivos (WCAG 2.1 AA)
4. Formatear montos con `Intl.NumberFormat('es-PA', { style: 'currency', currency: 'PAB' })`
5. Tablas de >100 filas DEBEN usar virtualización (react-window o TanStack Virtual)
6. Multi-step wizards usan `shouldUnregister: true` en React Hook Form
```

### Skill 2: Panama Payroll Compliance (EXCLUSIVO — no existe en ningún directorio)

Crear `.claude/skills/panama-payroll/SKILL.md`:

```yaml
---
name: panama-payroll
description: >
  Panama payroll compliance calculations and rules. Use when implementing
  CSS (Caja de Seguro Social), ISR (income tax), Seguro Educativo,
  décimo tercer mes, vacaciones, or any payroll calculation for Panama.
---
# Compliance de Planilla Panameña

## Deducciones del Empleado
- **CSS (Seguro Social)**: 9.75% del salario bruto
- **Seguro Educativo**: 1.25% del salario bruto
- **ISR (Impuesto sobre la Renta)**: escala progresiva anual:
  - $0 - $11,000: 0%
  - $11,000.01 - $50,000: 15% sobre el excedente de $11,000
  - $50,000.01+: $5,850 + 25% sobre el excedente de $50,000

## Aportes del Empleador
- **CSS Patronal**: 12.25% del salario bruto
- **Seguro Educativo Patronal**: 1.50% del salario bruto
- **Riesgos Profesionales**: varía por actividad económica (1.62%-5.67%)

## Décimo Tercer Mes
- Tres partidas anuales: 15 abril, 15 agosto, 15 diciembre
- Cálculo: salario de los 4 meses anteriores / 3
- Período 1: dic-mar → pago 15 abril
- Período 2: abr-jul → pago 15 agosto
- Período 3: ago-nov → pago 15 diciembre

## Vacaciones
- 30 días por cada 11 meses de trabajo continuo
- Proporcional: (días trabajados / 11 meses) × 30 días
- Se pagan como 1/12 del salario ordinario acumulado

## Prima de Antigüedad
- 1 semana de salario por cada año trabajado (al terminar relación laboral)
- Tope: salario semanal máximo según ley vigente

## Reglas de Validación
- Cédula panameña: formato X-XXXX-XXXXXX (provincia-tomo-asiento)
- Período de pago: quincenal o mensual (más común quincenal)
- Salario mínimo: varía por región y actividad económica
- Horas extra: 25% recargo (diurnas), 50% (nocturnas), 75% (domingos/feriados)

## Al Generar Código de Cálculos
1. SIEMPRE usar decimal (no float/double) para montos monetarios
2. SIEMPRE redondear a 2 decimales con MidpointRounding.ToEven
3. SIEMPRE validar que salario >= salario mínimo vigente
4. SIEMPRE considerar el tope de CSS cuando aplique
5. Los cálculos de ISR son ANUALIZADOS — proyectar salario anual para determinar tasa
```

### Skill 3: Deploy a CapRover

Crear `.claude/skills/deploy-caprover/SKILL.md`:

```yaml
---
name: deploy-caprover
description: Deploy Pagly to CapRover on DigitalOcean
disable-model-invocation: true
context: fork
allowed-tools: Bash, Read
---
# Deploy Pagly a CapRover

## Pre-deploy checks
1. Ejecutar `dotnet test` — todos deben pasar
2. Ejecutar `cd frontend && pnpm build` — verificar sin errores
3. Verificar que `captain-definition` existe en raíz

## Backend (.NET)
```bash
# Build y push
docker build -t pagly-api -f Dockerfile.api .
caprover deploy -a pagly-api
```

## Frontend (React)
```bash
docker build -t pagly-web -f Dockerfile.web .
caprover deploy -a pagly-web
```

## Post-deploy
1. Verificar health endpoint: `curl https://api.pagly.app/health`
2. Verificar frontend: `curl -I https://app.pagly.app`
3. Verificar migrations: confirmar que EF Core aplicó migrations pendientes
```

---

## Plan de acción: empieza por aquí, hoy mismo

### Fase 1 — HOY (30 minutos): Fundación

```bash
# 1. Crear estructura de skills en tu proyecto Pagly
mkdir -p .claude/skills/pagly-react
mkdir -p .claude/skills/panama-payroll
mkdir -p .claude/skills/deploy-caprover

# 2. Copiar los 3 SKILL.md de la sección anterior a cada carpeta

# 3. Instalar skills de comunidad esenciales para frontend
npx skills add jezweb/claude-skills@tailwind-v4-shadcn
npx skills add jezweb/claude-skills@react-hook-form-zod
npx skills add vercel-labs/agent-skills@vercel-react-best-practices

# 4. Agregar shadcn MCP server
claude mcp add --transport http shadcn https://www.shadcn.io/api/mcp

# 5. Verificar que todo cargó
# (Dentro de una sesión de Claude Code):
/context
```

### Fase 2 — Esta semana: Skills de backend y testing

```bash
# Instalar dotnet-skills (30 skills para .NET)
git clone https://github.com/Aaronontheweb/dotnet-skills.git /tmp/dotnet-skills
# Copiar los que necesites: efcore-patterns, api-design, modern-csharp-coding-standards
cp -r /tmp/dotnet-skills/skills/efcore-patterns ~/.claude/skills/
cp -r /tmp/dotnet-skills/skills/api-design ~/.claude/skills/

# Instalar PostgreSQL skill
npx skills add mindrally/skills@postgresql-best-practices

# Crear skill de multi-tenant security
mkdir -p .claude/skills/multi-tenant
# Escribir SKILL.md con reglas de TenantId obligatorio, RLS, etc.
```

### Fase 3 — Semana 2: Optimización y documentación

Refinar los skills basándote en la experiencia de la primera semana. Medir cuántos tokens ahorras comparado con tu sistema actual de `/docs/agentes/`. Crear un skill de documentación técnica que genere docs en el formato específico de Pagly. Versionar todos los skills en `.claude/skills/` con Git para compartir con tu equipo.

### Fase 4 — Semana 3+: Skills avanzados

Explorar `context: fork` para skills que requieren investigación profunda (por ejemplo, un skill que analice toda la base de código buscando violaciones de multi-tenancy). Crear skills con scripts de validación (por ejemplo, un script Python que valide cálculos de CSS/ISR contra casos de prueba conocidos). Evaluar si publicar tu skill de `panama-payroll` en skills.sh como contribución comunitaria.

---

## Limitaciones y gotchas que debes conocer

Hay **13 trampas concretas** que pueden costarte horas si no las conoces. El presupuesto de descripciones de skills tiene un límite del **2% de la ventana de contexto** (~16,000 caracteres). Si instalas demasiados skills, algunos se excluirán silenciosamente — usa `/context` para verificar. El YAML inválido en el frontmatter hace que el skill no cargue sin error visible; usa `claude --debug` para diagnosticar. Los archivos `SKILL.md` **deben estar dentro de un subdirectorio** (`.claude/skills/nombre/SKILL.md`), nunca directamente en `.claude/skills/SKILL.md`.

El hot-reload funciona en Claude Code 2.1+ — puedes editar skills sin reiniciar la sesión. Sin embargo, `context: fork` tiene un bug conocido (GitHub issue #17283) donde puede ejecutarse en el contexto principal en vez de un sub-agente aislado. Para skills con side effects como deployment, **siempre** usa `disable-model-invocation: true`.

Respecto a frontend específicamente: Claude **no escribe tests a menos que se lo pidas explícitamente** — codifica esto en tu CLAUDE.md o skills. Tailwind v4 tiene 8 cambios breaking documentados que el skill `tailwind-v4-shadcn` previene automáticamente. Claude tiende a importar `Form` de `react-hook-form` en vez de `@/components/ui/form` (shadcn) — documenta la importación correcta en tu skill. Y sin dirección estética explícita, Claude genera interfaces con "Inter fonts, gradientes púrpura sobre fondo blanco" — el skill `frontend-design` oficial o tu propio skill de diseño previene esta convergencia genérica.

---

## Tu sistema actual vs el nuevo: una migración natural

Tus archivos `.md` en `/docs/agentes/` funcionan como contexto estático cargado siempre. Los skills en `.claude/skills/` son **contexto dinámico** que se carga solo cuando es relevante. La migración ideal no es reemplazar uno con otro, sino **combinarlos**:

Tu **CLAUDE.md** (raíz del proyecto) debe contener las convenciones inmutables de Pagly: stack tecnológico, comandos de desarrollo, estructura de carpetas, reglas de código que siempre aplican. Tus **skills** deben contener el conocimiento especializado que solo aplica en contextos específicos: compliance panameño (solo cuando se trabaja en cálculos), patrones React (solo cuando se crean componentes), deployment (solo cuando se despliega). Tus **archivos en `/docs/agentes/`** pueden mantenerse como referencia profunda que los skills referencian cuando necesitan más detalle.

Esta arquitectura de tres capas maximiza la eficiencia del contexto: las convenciones base siempre están presentes (~500 tokens), los skills relevantes se cargan bajo demanda (~300-500 tokens cada uno), y la documentación profunda solo se lee cuando un skill la referencia explícitamente.