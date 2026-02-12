#!/usr/bin/env python3
"""
UserPromptSubmit Hook: Intelligent Routing for Planilla Multi-Agent System

This hook analyzes incoming user prompts and injects routing context to guide
Claude Code to delegate to appropriate specialist agents automatically.

Execution: Triggered on every user prompt submission
Input: JSON with {"prompt": "user message", ...}
Output: JSON with hookSpecificOutput.additionalContext for routing guidance
"""

import sys
import json
import re
from typing import Dict, List, Tuple

# Agent routing keywords (keyword -> agent_name, priority)
ROUTING_RULES = {
    # Backend keywords (high priority for technical terms)
    'api': ('planilla-backend-architect', 8),
    'endpoint': ('planilla-backend-architect', 8),
    'controller': ('planilla-backend-architect', 8),
    'service': ('planilla-backend-architect', 7),
    'repository': ('planilla-backend-architect', 8),
    'entity framework': ('planilla-backend-architect', 9),
    'ef core': ('planilla-backend-architect', 9),
    'migration': ('planilla-backend-architect', 8),
    'database': ('planilla-backend-architect', 7),
    'sql': ('planilla-backend-architect', 7),
    'postgresql': ('planilla-backend-architect', 8),
    'multi-tenant': ('planilla-backend-architect', 9),
    'tenantid': ('planilla-backend-architect', 9),
    'jwt': ('planilla-backend-architect', 8),
    'authentication': ('planilla-backend-architect', 7),
    'authorization': ('planilla-backend-architect', 7),
    'stripe': ('planilla-backend-architect', 8),
    'subscription': ('planilla-backend-architect', 7),
    'dto': ('planilla-backend-architect', 8),
    'actionresponse': ('planilla-backend-architect', 8),

    # Payroll/Legal keywords (highest priority for compliance)
    'css': ('planilla-payroll-architect', 10),
    'caja de seguro': ('planilla-payroll-architect', 10),
    'seguro educativo': ('planilla-payroll-architect', 10),
    'isr': ('planilla-payroll-architect', 10),
    'impuesto': ('planilla-payroll-architect', 9),
    'overtime': ('planilla-payroll-architect', 9),
    'horas extra': ('planilla-payroll-architect', 9),
    'vacation': ('planilla-payroll-architect', 8),
    'vacaciones': ('planilla-payroll-architect', 8),
    'severance': ('planilla-payroll-architect', 9),
    'liquidación': ('planilla-payroll-architect', 9),
    'décimo': ('planilla-payroll-architect', 9),
    'mitradel': ('planilla-payroll-architect', 9),
    'labor law': ('planilla-payroll-architect', 9),
    'código de trabajo': ('planilla-payroll-architect', 9),
    'payroll': ('planilla-payroll-architect', 8),
    'planilla': ('planilla-payroll-architect', 8),
    'nómina': ('planilla-payroll-architect', 8),

    # Frontend keywords
    'react': ('planilla-frontend-specialist', 8),
    'component': ('planilla-frontend-specialist', 7),
    'jsx': ('planilla-frontend-specialist', 8),
    'tailwind': ('planilla-frontend-specialist', 8),
    'css styling': ('planilla-frontend-specialist', 7),
    'ui': ('planilla-frontend-specialist', 6),
    'frontend': ('planilla-frontend-specialist', 8),
    'page': ('planilla-frontend-specialist', 6),
    'form': ('planilla-frontend-specialist', 6),
    'modal': ('planilla-frontend-specialist', 7),
    'button': ('planilla-frontend-specialist', 6),
    'dashboard': ('planilla-frontend-specialist', 7),

    # AI/ML keywords
    'machine learning': ('planilla-ai-specialist', 8),
    'ml.net': ('planilla-ai-specialist', 9),
    'prediction': ('planilla-ai-specialist', 8),
    'anomaly': ('planilla-ai-specialist', 8),
    'forecast': ('planilla-ai-specialist', 8),
    'ai': ('planilla-ai-specialist', 7),

    # Functional/Process keywords
    'workflow': ('planilla-functional-architect', 8),
    'business process': ('planilla-functional-architect', 8),
    'business rule': ('planilla-functional-architect', 8),
    'integration': ('planilla-functional-architect', 7),
    'module': ('planilla-functional-architect', 6),
    'architecture': ('planilla-functional-architect', 7),

    # Documentation keywords
    'documentation': ('planilla-docs-generator', 8),
    'manual': ('planilla-docs-generator', 8),
    'guide': ('planilla-docs-generator', 7),
    'readme': ('planilla-docs-generator', 8),
    'api docs': ('planilla-docs-generator', 8),

    # UX/UI Design keywords
    'design': ('planilla-uxui-designer', 7),
    'ux': ('planilla-uxui-designer', 8),
    'layout': ('planilla-uxui-designer', 7),
    'responsive': ('planilla-uxui-designer', 7),
    'accessibility': ('planilla-uxui-designer', 7),
    'color': ('planilla-uxui-designer', 6),
    'typography': ('planilla-uxui-designer', 6),

    # Mobile keywords
    'maui': ('planilla-mobile-developer', 9),
    'mobile': ('planilla-mobile-developer', 8),
    'xaml': ('planilla-mobile-developer', 9),
    'android': ('planilla-mobile-developer', 8),
    'ios': ('planilla-mobile-developer', 8),
}

# Multi-domain patterns (require orchestrator)
ORCHESTRATION_PATTERNS = [
    r'(add|create|implement|build).*(feature|module|system)',
    r'(full|complete|end-to-end)',
    r'(backend|api).*(frontend|ui)',
    r'(frontend|ui).*(backend|api)',
]


def analyze_prompt(prompt: str) -> Tuple[Dict[str, float], bool]:
    """
    Analyze prompt and return agent scores and orchestration flag.

    Returns:
        (agent_scores, needs_orchestration)
    """
    prompt_lower = prompt.lower()
    agent_scores = {}

    # Score agents based on keyword matches
    for keyword, (agent, priority) in ROUTING_RULES.items():
        if keyword in prompt_lower:
            agent_scores[agent] = agent_scores.get(agent, 0) + priority

    # Check if orchestration is needed
    needs_orchestration = any(
        re.search(pattern, prompt_lower) for pattern in ORCHESTRATION_PATTERNS
    )

    # If multiple high-scoring agents, consider orchestration
    high_score_agents = [a for a, s in agent_scores.items() if s >= 15]
    if len(high_score_agents) >= 2:
        needs_orchestration = True

    return agent_scores, needs_orchestration


def generate_routing_context(prompt: str) -> str:
    """Generate routing guidance based on prompt analysis."""
    agent_scores, needs_orchestration = analyze_prompt(prompt)

    if not agent_scores:
        # No clear routing signals
        return ""

    # Sort agents by score
    sorted_agents = sorted(agent_scores.items(), key=lambda x: x[1], reverse=True)
    top_agent, top_score = sorted_agents[0]

    if needs_orchestration:
        # Multi-domain task - suggest orchestrator
        involved_agents = [a for a, s in sorted_agents if s >= 10]
        context = f"""
[ROUTING GUIDANCE] Multi-Agent Task Detected:

This task appears to span multiple domains. Consider using the planilla-orchestrator agent to coordinate:

Suggested agents to involve:
{chr(10).join(f'  - {agent} (relevance: {score})' for agent, score in sorted_agents[:3])}

If you proceed without orchestration, delegate directly to the most relevant specialist.

Remember to:
1. Read CLAUDE.md for project conventions
2. Ensure multi-tenancy (TenantId filtering) in all database queries
3. Verify subscription plan limits before creating resources
4. Use DTOs (never expose entities directly)
"""
    else:
        # Single-domain task - suggest direct delegation
        context = f"""
[ROUTING GUIDANCE] Single-Domain Task:

This task appears to be primarily a {top_agent} concern.

Recommendation: Delegate directly to {top_agent} for optimal results.

Context priority scores:
{chr(10).join(f'  - {agent}: {score}' for agent, score in sorted_agents[:3])}

Remember to:
- Verify multi-tenancy (TenantId filtering) if touching database
- Check plan limits if creating resources
- Use DTOs for API responses
"""

    return context.strip()


def main():
    """Main hook execution."""
    try:
        # Read hook input from stdin
        input_data = json.load(sys.stdin)
        prompt = input_data.get('prompt', '')

        if not prompt:
            # No prompt, nothing to route - allow continuation
            print(json.dumps({}))
            return

        # Generate routing context
        routing_context = generate_routing_context(prompt)

        # Output result using correct schema
        # For UserPromptSubmit, use systemMessage to inject context
        result = {
            'systemMessage': routing_context
        } if routing_context else {}

        print(json.dumps(result))

    except Exception as e:
        # On error, pass through without blocking
        # Don't output anything to avoid validation errors
        print(json.dumps({}))


if __name__ == '__main__':
    main()
