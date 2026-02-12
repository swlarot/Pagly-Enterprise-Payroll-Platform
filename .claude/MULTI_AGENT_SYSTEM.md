# Sistema Multi-Agente Planilla - Documentación

## Visión General

Este directorio contiene un sistema multi-agente profesional para Claude Code que:

1. **Rutea automáticamente** las solicitudes a agentes especialistas
2. **Orquesta** tareas multi-dominio coordinando varios agentes
3. **Valida** que se sigan las convenciones del proyecto antes de cerrar conversaciones
4. **Ejecuta** build checks automáticos cuando hay cambios de código

## Estructura

```
.claude/
├── agents/                              # Subagents especializados
│   ├── planilla-backend-architect.md   # Backend .NET/EF Core/API
│   ├── planilla-payroll-architect.md   # Cálculos de planilla/leyes Panama
│   ├── planilla-frontend-specialist.md # React/Tailwind/UI
│   ├── planilla-ai-specialist.md       # ML.NET/predicciones/anomalías
│   ├── planilla-functional-architect.md # Procesos de negocio/workflows
│   ├── planilla-docs-generator.md      # Documentación técnica/usuario
│   ├── planilla-uxui-designer.md       # Diseño UX/UI/sistema visual
│   ├── planilla-mobile-developer.md    # .NET MAUI/mobile
│   └── planilla-orchestrator.md        # Orquestador maestro
│
├── hooks/                               # Hooks de automatización
│   ├── route_prompt.py                 # UserPromptSubmit: ruteo inteligente
│   └── stop_guard.py                   # Stop: validación pre-cierre
│
├── settings.json                       # Configuración de hooks
└── MULTI_AGENT_SYSTEM.md              # Esta documentación

```

## Subagents Disponibles

### 1. planilla-backend-architect
**Cuándo usar**: APIs, Entity Framework, multi-tenancy, autenticación, Stripe

**Expertise**:
- Clean Architecture (.NET 9)
- Multi-tenancy con TenantId filtering
- Entity Framework Core 9 + PostgreSQL
- JWT + ASP.NET Core Identity
- Subscription management con Stripe
- Dependency Injection, Program.cs

**Ejemplo de delegación**:
```
"Delegate to planilla-backend-architect: Create API endpoint GET /api/employees
with tenant filtering, pagination, and role-based authorization."
```

### 2. planilla-payroll-architect
**Cuándo usar**: Cálculos CSS/SE/ISR, leyes laborales de Panama, overtime, vacaciones

**Expertise**:
- CSS (Ley 462) con tope de B/.1,500
- Seguro Educativo (sin tope)
- ISR con proyección anual
- Horas extra (1.25x - 1.75x según tipo)
- Décimo tercer mes, vacaciones, liquidaciones
- MITRADEL reporting

**Ejemplo de delegación**:
```
"Delegate to planilla-payroll-architect: Validate overtime calculation formula
for nocturnal hours on Sunday (should be 1.75x base rate)."
```

### 3. planilla-frontend-specialist
**Cuándo usar**: React components, Tailwind CSS, forms, dashboards, API integration

**Expertise**:
- React 19 + Vite
- Tailwind CSS styling
- Authentication context (JWT)
- Subscription plan UI (upgrade prompts, limits)
- Responsive design
- Form validation

**Ejemplo de delegación**:
```
"Delegate to planilla-frontend-specialist: Create EmployeeList component with
search, filters, pagination, and role-based action buttons."
```

### 4. planilla-ai-specialist
**Cuándo usar**: ML features, anomaly detection, predictions, forecasting

**Expertise**:
- ML.NET models
- Payroll anomaly detection
- Predictive analytics
- Smart suggestions
- Natural language queries (RAG)

### 5. planilla-functional-architect
**Cuándo usar**: Business processes, workflows, integration, entity relationships

**Expertise**:
- Workflow design (payroll approval flow, onboarding)
- Entity relationship modeling
- Business rule validation
- Cross-module integration
- Process documentation

### 6. planilla-docs-generator
**Cuándo usar**: API docs, user manuals, installation guides

**Expertise**:
- OpenAPI/Swagger documentation
- User manuals (Spanish)
- Architecture diagrams
- Deployment guides

### 7. planilla-uxui-designer
**Cuándo usar**: Design system, layouts, responsive design, accessibility

**Expertise**:
- Component design patterns
- Color palette, typography
- Responsive layouts (mobile-first)
- Accessibility (WCAG AA)
- SaaS UI patterns (upgrade prompts, plan limits)

### 8. planilla-mobile-developer
**Cuándo usar**: .NET MAUI, mobile app features, XAML

**Expertise**:
- .NET MAUI (Android/iOS)
- MVVM architecture
- Offline-first with SQLite
- Biometric authentication
- SignalR real-time notifications

### 9. planilla-orchestrator (MAESTRO)
**Cuándo usar**: Tareas multi-dominio, features completas end-to-end

**Expertise**:
- Task classification
- Multi-agent coordination
- Standards enforcement (CLAUDE.md)
- Response consolidation

## Hooks

### UserPromptSubmit Hook (route_prompt.py)

**Qué hace**:
- Analiza cada prompt del usuario
- Detecta keywords por dominio (api → backend, css → payroll, react → frontend)
- Calcula scores de relevancia por agente
- Detecta si la tarea es multi-dominio
- Inyecta "routing guidance" como contexto adicional

**Ejemplo de output**:
```
🎯 ROUTING GUIDANCE (Single-Domain Task):

This task appears to be primarily a planilla-backend-architect concern.

Recommendation: Delegate directly to planilla-backend-architect for optimal results.

Context priority scores:
  - planilla-backend-architect: 24
  - planilla-payroll-architect: 8
```

**Cómo funciona**:
1. User prompt → JSON con `{"prompt": "..."}`
2. Hook analiza keywords y patterns
3. Hook devuelve `{"hookSpecificOutput": {"additionalContext": "..."}}`
4. Claude Code muestra el contexto al modelo

### Stop Hook (stop_guard.py)

**Qué hace**:
- Se ejecuta cuando el usuario intenta terminar la conversación
- Verifica que tareas que requieren agentes especialistas fueron delegadas
- Ejecuta `dotnet build` si hubo cambios de código
- Bloquea el cierre si faltan delegaciones o el build falla

**Ejemplo de bloqueo**:
```
⛔ STOP BLOCKED: Missing Agent Delegation

This task appears to require specialist expertise, but no agent delegation was detected.

Recommended agent: planilla-backend-architect

Why this matters:
- Specialist agents ensure compliance with project standards (CLAUDE.md)
- They enforce multi-tenancy, plan limits, and architectural patterns

To proceed:
1. Use the Task tool to delegate to planilla-backend-architect
2. After delegation completes, you can end the conversation
```

**Cómo funciona**:
1. User intenta cerrar → JSON con `{"transcript_path": "..."}`
2. Hook lee transcript completo
3. Hook verifica patrones de delegación y cambios de código
4. Si hay problemas: `{"decision": "block", "reason": "..."}`
5. Si todo OK: `{"decision": "approve"}`

## Verificación del Sistema

### Checklist de Validación

**✅ Subagents Listados:**
```bash
# Claude debe mostrarlos con /agents
/agents
```

Deberías ver:
- planilla-backend-architect
- planilla-payroll-architect
- planilla-frontend-specialist
- planilla-ai-specialist
- planilla-functional-architect
- planilla-docs-generator
- planilla-uxui-designer
- planilla-mobile-developer
- planilla-orchestrator

**✅ Hooks Configurados:**
```bash
# Verifica que los hooks aparecen en configuración
cat .claude/settings.json | grep -A2 hooks
```

Deberías ver:
- UserPromptSubmit → route_prompt.py
- Stop → stop_guard.py

**✅ Hooks Ejecutables:**
```bash
ls -la .claude/hooks/
```

Deberías ver `-rwxr-xr-x` (ejecutable)

**✅ Test de Routing:**

Prompt de prueba:
```
"Create a new API endpoint to list employees"
```

Esperado en la respuesta inicial:
```
🎯 ROUTING GUIDANCE (Single-Domain Task):

This task appears to be primarily a planilla-backend-architect concern.
```

**✅ Test de Delegación Automática:**

Prompt de prueba:
```
"Implement CSS calculation for employees"
```

Esperado:
- Claude debe delegar a `planilla-payroll-architect` automáticamente
- O al menos mencionar que es un task de payroll

**✅ Test de Stop Guard:**

1. Inicia conversación con tarea de backend: "Create Employee API"
2. Claude responde pero NO delega a agente
3. Intenta cerrar con `/stop` o equivalente
4. Esperado: Hook BLOQUEA y pide delegación

## Troubleshooting

### Hooks no se ejecutan

**Problema**: Los prompts no muestran routing guidance

**Soluciones**:
1. Verifica que `.claude/settings.json` existe y tiene los hooks
2. Verifica permisos ejecutables: `chmod +x .claude/hooks/*.py`
3. Verifica que Python está en PATH: `python --version`
4. Revisa logs de Claude Code para errores de hooks

### Subagents no aparecen en /agents

**Problema**: `/agents` no muestra los subagents

**Soluciones**:
1. Verifica que `.claude/agents/*.md` existen
2. Verifica que el YAML frontmatter está correcto (name, description)
3. Reinicia Claude Code
4. Verifica que no hay errores de sintaxis en los .md files

### Build check falla incorrectamente

**Problema**: Stop hook bloquea aunque el build está OK

**Soluciones**:
1. Ejecuta manualmente: `dotnet build --no-incremental`
2. Verifica que `Planilla.sln` existe en el directorio raíz
3. Revisa output del hook en stderr
4. Temporalmente deshabilita el hook de Stop en settings.json

### Routing sugiere agente incorrecto

**Problema**: Hook sugiere backend cuando debería ser payroll

**Soluciones**:
1. Revisa `.claude/hooks/route_prompt.py` - keyword priorities
2. Ajusta scores en `ROUTING_RULES` dictionary
3. Agrega keywords faltantes

## Mejoras Futuras

### Corto Plazo:
- [ ] Mejorar heurística de routing con ML (embeddings)
- [ ] Agregar hook `PreToolUse` para validar parámetros
- [ ] Cache de resultados de build checks

### Mediano Plazo:
- [ ] Dashboard web para visualizar delegaciones
- [ ] Métricas de uso de agentes
- [ ] Auto-tunning de routing basado en feedback

### Largo Plazo:
- [ ] Integración con CI/CD (GitHub Actions)
- [ ] Agentes adicionales (DevOps, Security, Performance)
- [ ] Multi-language support (English agents)

## Recomendaciones de Uso

### Para Tareas Simples:
- Deja que el routing hook sugiera el agente
- Delega directamente si estás seguro del dominio
- Ejemplo: Bug en cálculo CSS → directo a `planilla-payroll-architect`

### Para Tareas Complejas:
- Usa `planilla-orchestrator` como punto de entrada
- Déjalo clasificar y delegar a múltiples especialistas
- Ejemplo: "Add employee self-service portal" → orchestrator coordina

### Para Asegurar Calidad:
- Confía en el stop guard
- Si bloquea, es porque detectó un problema real
- Sigue las instrucciones del bloqueo (delegar, fix build, etc.)

## Soporte

**Problemas con hooks**: Revisa `.claude/hooks/*.py` - tienen logging a stderr

**Problemas con agentes**: Revisa `.claude/agents/*.md` - valida YAML frontmatter

**Problemas generales**: Consulta `CLAUDE.md` en la raíz del proyecto

---

**Versión**: 1.0.0
**Última actualización**: 2026-01-07
**Autor**: Tech Lead DevEx - Planilla SaaS
