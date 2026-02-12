# 🚀 Quick Start - Sistema Multi-Agente Planilla

## 1️⃣ Verificación Inmediata (2 minutos)

### Paso 1: Verificar Subagents
```bash
# En Claude Code CLI:
/agents
```

**✅ Esperado**: Deberías ver 9 agentes listados:
- planilla-backend-architect
- planilla-payroll-architect
- planilla-frontend-specialist
- planilla-ai-specialist
- planilla-functional-architect
- planilla-docs-generator
- planilla-uxui-designer
- planilla-mobile-developer
- planilla-orchestrator

### Paso 2: Test de Routing
```
Prompt de prueba:
"Create API endpoint to list employees with pagination"
```

**✅ Esperado**: En la primera respuesta, verás:
```
🎯 ROUTING GUIDANCE (Single-Domain Task):
This task appears to be primarily a planilla-backend-architect concern.
```

### Paso 3: Test de Delegación
```
Continúa la conversación anterior:
"OK, please implement it following clean architecture"
```

**✅ Esperado**: Claude debería:
1. Usar Task tool con subagent_type='planilla-backend-architect'
2. O mencionar explícitamente la delegación

### Paso 4: Test de Stop Guard
```
En una nueva conversación:
"Implement CSS calculation for Panama law"

(Claude responde con código PERO no delega)

Intenta cerrar la conversación (Ctrl+D)
```

**✅ Esperado**: Hook bloquea con:
```
⛔ STOP BLOCKED: Missing Agent Delegation
Recommended agent: planilla-payroll-architect
```

---

## 2️⃣ Uso Diario

### Para Tareas Backend
```
Ejemplo: "Add Employee CRUD endpoints"
→ Routing sugiere: planilla-backend-architect
→ Claude delega automáticamente o tú invocas con /task
```

### Para Tareas Payroll
```
Ejemplo: "Validate overtime calculation formula"
→ Routing sugiere: planilla-payroll-architect
→ Claude consulta al especialista en leyes panameñas
```

### Para Features Completas
```
Ejemplo: "Build employee self-service portal"
→ Routing sugiere: planilla-orchestrator
→ Orchestrator coordina: functional → backend → frontend
```

---

## 3️⃣ Comandos Útiles

```bash
# Listar agentes disponibles
/agents

# Ver configuración de hooks
cat .claude/settings.json | grep -A5 hooks

# Test manual de hook de routing
echo '{"prompt":"Create API endpoint"}' | python .claude/hooks/route_prompt.py

# Ver permisos de hooks
ls -lah .claude/hooks/

# Deshabilitar hook temporalmente (edita settings.json)
# Comenta la sección del hook que quieres deshabilitar
```

---

## 4️⃣ Troubleshooting Rápido

### Hooks no aparecen
```bash
# Verificar Python
python3 --version

# Verificar permisos
chmod +x .claude/hooks/*.py

# Verificar sintaxis
python3 -m py_compile .claude/hooks/route_prompt.py
```

### Build check falla incorrectamente
```bash
# Test manual
cd C:/Planilla
dotnet build --no-incremental

# Si falla, fix los errores
# Si pasa, verifica que stop_guard.py encuentra la solution
```

### Routing sugiere agente incorrecto
```python
# Edita .claude/hooks/route_prompt.py
# Ajusta ROUTING_RULES:
ROUTING_RULES = {
    'tu_keyword': ('planilla-tu-agente', 10),  # Score más alto
    # ...
}
```

---

## 5️⃣ Flujo Típico de Trabajo

```
┌─────────────────────────────────────────────────────┐
│ Usuario: "Add overtime approval workflow"          │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Hook: route_prompt.py analiza                       │
│ Detecta: "overtime" → payroll domain               │
│ Inyecta: "Routing guidance: payroll-architect"     │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Claude: Ve el routing guidance                      │
│ Decisión: Delegar a planilla-payroll-architect     │
│ Usa: Task tool con subagent_type                   │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Payroll Architect: Valida formula overtime         │
│ - Overtime nocturno domingo = 1.75x                 │
│ - Cita: Código de Trabajo Art. X                   │
│ - Retorna: Formula validada + implementación       │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Claude: Consolida respuesta del subagent           │
│ Presenta al usuario la solución completa           │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Usuario: Intenta cerrar conversación               │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│ Hook: stop_guard.py valida                          │
│ ✅ Delegación a payroll-architect: Encontrada      │
│ ✅ Build check: dotnet build passed                │
│ Decisión: APPROVE (permite cerrar)                 │
└─────────────────────────────────────────────────────┘
```

---

## 6️⃣ Documentación Completa

- **Sistema completo**: `.claude/MULTI_AGENT_SYSTEM.md`
- **Entrega detallada**: `.claude/ENTREGA_MULTI_AGENTE.md`
- **Convenciones proyecto**: `CLAUDE.md` (raíz)
- **Quick start**: `.claude/QUICK_START.md` (este archivo)

---

## 7️⃣ Tips Pro

### Forzar Delegación Manual
```
Si el routing no delega automáticamente:
"Please delegate this to planilla-backend-architect"
```

### Usar Orchestrator para Features Grandes
```
"Use planilla-orchestrator to coordinate implementing
the employee onboarding workflow with UI and email notifications"
```

### Deshabilitar Temporalmente Stop Guard
```json
// En .claude/settings.json - comenta la sección:
{
  "hooks": {
    "UserPromptSubmit": { ... },
    // "Stop": { ... }  // ← Comentado
  }
}
```

### Ver Transcript para Debug
```bash
# El transcript_path está en el input del stop hook
# Normalmente en: ~/.claude/conversations/<id>/transcript.json
```

---

## ✅ Checklist de "Está Funcionando"

- [ ] `/agents` lista 9 agentes
- [ ] Prompts de backend muestran routing guidance
- [ ] Claude delega a especialistas (ves "Task tool" en transcript)
- [ ] Stop guard bloquea cuando falta delegación
- [ ] Stop guard ejecuta build check si hay cambios de código
- [ ] Build passing permite cerrar conversación

**Si todos los checks pasan**: 🎉 El sistema funciona perfectamente!

---

**Última actualización**: 2026-01-07
