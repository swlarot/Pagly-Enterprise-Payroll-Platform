# Sistema Multi-Agente Planilla

## 🎯 Objetivo

Convertir el sistema de "skills" en un **sistema multi-agente profesional** con:
- ✅ Auto-routing inteligente
- ✅ Orquestación multi-dominio
- ✅ Enforcement de calidad
- ✅ Validación automática

## 📊 Estado: ✅ COMPLETADO

**Fecha de implementación**: 2026-01-07
**Versión**: 1.0.0

## 🗂️ Estructura

```
.claude/
├── agents/             # 9 subagents especializados
├── hooks/              # 2 hooks Python (routing + enforcement)
├── settings.json       # Configuración de hooks
├── QUICK_START.md      # Empezar aquí ⭐
├── MULTI_AGENT_SYSTEM.md    # Documentación completa
├── ENTREGA_MULTI_AGENTE.md  # Entrega detallada
└── README.md           # Este archivo
```

## 🚀 Quick Start

### 1. Verificar instalación
```bash
/agents
# Debe listar 9 agentes
```

### 2. Probar routing automático
```
Prompt: "Create API endpoint to list employees"
# Debe mostrar: 🎯 ROUTING GUIDANCE
```

### 3. Probar enforcement
```
Prompt: "Implement CSS calculation"
(Claude responde sin delegar)
(Intentar cerrar)
# Debe bloquear: ⛔ STOP BLOCKED
```

## 📚 Documentación

- **Empezar aquí**: [QUICK_START.md](QUICK_START.md)
- **Sistema completo**: [MULTI_AGENT_SYSTEM.md](MULTI_AGENT_SYSTEM.md)
- **Entrega detallada**: [ENTREGA_MULTI_AGENTE.md](ENTREGA_MULTI_AGENTE.md)
- **Convenciones proyecto**: [../CLAUDE.md](../CLAUDE.md)

## 🤖 Agentes Disponibles

1. **planilla-backend-architect** - Backend .NET/API/EF Core
2. **planilla-payroll-architect** - Payroll Panama/Leyes laborales
3. **planilla-frontend-specialist** - React/Tailwind/UI
4. **planilla-ai-specialist** - ML.NET/Predictions
5. **planilla-functional-architect** - Business processes
6. **planilla-docs-generator** - Documentation
7. **planilla-uxui-designer** - UX/UI design
8. **planilla-mobile-developer** - .NET MAUI mobile
9. **planilla-orchestrator** - Coordinador maestro ⭐

## 🔧 Hooks

- **route_prompt.py** - Analiza prompts y sugiere agentes
- **stop_guard.py** - Valida delegación y build antes de cerrar

## ✅ Features

- [x] Auto-routing por keywords
- [x] Orquestación multi-dominio
- [x] Stop enforcement con build check
- [x] Validación de delegación
- [x] Compliance con CLAUDE.md
- [x] Documentación completa
- [x] Tests de verificación

## 🎓 Uso

### Tarea Simple (Backend)
```
User: "Create Employee API endpoint"
→ Routing sugiere: planilla-backend-architect
→ Claude delega automáticamente
```

### Tarea Multi-Dominio
```
User: "Build employee self-service portal"
→ Routing sugiere: planilla-orchestrator
→ Orchestrator coordina: backend + frontend + payroll
```

### Enforcement Activo
```
User: "Implement CSS calculation" (sin delegar)
→ Stop guard bloquea
→ Mensaje: "Debe delegar a planilla-payroll-architect"
```

## 🔒 Seguridad

- Hooks ejecutan con permisos restringidos
- Timeouts en operaciones largas (2 min)
- Input validation en todos los hooks
- Build checks sandboxeados (dotnet build)

## 🐛 Troubleshooting

### Hooks no funcionan
```bash
chmod +x .claude/hooks/*.py
python3 --version
```

### Routing incorrecto
Editar `.claude/hooks/route_prompt.py` - ajustar `ROUTING_RULES`

### Build check falla
```bash
dotnet build --no-incremental
# Fix errores manualmente
```

## 📞 Soporte

- Issues: Ver troubleshooting en [MULTI_AGENT_SYSTEM.md](MULTI_AGENT_SYSTEM.md)
- Logs: stderr de Claude Code
- Configs: `.claude/settings.json`

---

**Implementado por**: Tech Lead DevEx - Planilla SaaS
**Última actualización**: 2026-01-07
