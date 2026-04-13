#!/usr/bin/env python3
"""
Stop Hook: Enforcement Guard for Planilla Multi-Agent System

This hook prevents premature conversation termination by verifying that:
1. Appropriate specialist agents were consulted for the task
2. Code changes pass build validation (dotnet build)
3. Multi-agent tasks received proper delegation

Execution: Triggered when user attempts to end conversation
Input: JSON with {"transcript_path": "...", ...}
Output: JSON with decision:"approve|block" and optional reason
"""

import sys
import json
import os
import subprocess
import re
from pathlib import Path
from typing import Dict, List, Tuple, Optional


# Task patterns that require agent delegation
DELEGATION_REQUIRED_PATTERNS = [
    # Backend patterns
    (r'(create|add|implement|modify|fix).*(api|endpoint|controller|service|repository)', 'planilla-backend-architect'),
    (r'(entity framework|ef core|migration|database)', 'planilla-backend-architect'),
    (r'(multi-tenant|tenantid|jwt|authentication)', 'planilla-backend-architect'),

    # Payroll patterns (no incluir "planilla" sola — es el nombre del proyecto)
    (r'(seguro educativo|isr|impuesto sobre la renta|horas extra)', 'planilla-payroll-architect'),
    (r'(vacaciones|liquidación|décimo tercer mes)', 'planilla-payroll-architect'),
    (r'(payroll calculation|cálculo de nómina|cálculo salarial)', 'planilla-payroll-architect'),

    # Frontend patterns
    (r'(create|add|implement|modify).*(react|component|page|form|modal|ui)', 'planilla-frontend-specialist'),
    (r'(tailwind|css styling|frontend)', 'planilla-frontend-specialist'),

    # Full feature patterns (require orchestration)
    (r'(full|complete|end-to-end).*(feature|module|system)', 'planilla-orchestrator'),
    (r'(add|create|implement).*(crud|management|workflow)', 'planilla-orchestrator'),
]

# Markers that indicate agent delegation occurred
AGENT_DELEGATION_MARKERS = [
    'Task tool',
    'subagent_type',
    'delegat',
    'invoking agent',
    'planilla-backend-architect',
    'planilla-payroll-architect',
    'planilla-frontend-specialist',
    'planilla-orchestrator',
]

# Code change indicators
CODE_CHANGE_MARKERS = [
    'Write tool',
    'Edit tool',
    '```csharp',
    '```jsx',
    '```tsx',
    '.cs"',
    '.jsx"',
    '.tsx"',
    'Program.cs',
    'Controller.cs',
    'Service.cs',
]


def read_transcript(transcript_path: str) -> str:
    """Read conversation transcript."""
    try:
        if not os.path.exists(transcript_path):
            return ""
        with open(transcript_path, 'r', encoding='utf-8') as f:
            return f.read()
    except Exception as e:
        print(f"Warning: Could not read transcript: {e}", file=sys.stderr)
        return ""


def check_agent_delegation(transcript: str) -> Tuple[bool, List[str]]:
    """
    Check if appropriate agent delegation occurred.

    Returns:
        (delegation_found, agents_used)
    """
    agents_used = []

    for marker in AGENT_DELEGATION_MARKERS:
        if marker.lower() in transcript.lower():
            agents_used.append(marker)

    delegation_found = len(agents_used) > 0
    return delegation_found, agents_used


def check_code_changes(transcript: str) -> bool:
    """Check if code changes were made."""
    for marker in CODE_CHANGE_MARKERS:
        if marker in transcript:
            return True
    return False


def should_require_delegation(transcript: str) -> Tuple[bool, Optional[str]]:
    """
    Determine if task requires delegation based on transcript content.

    Returns:
        (requires_delegation, recommended_agent)
    """
    transcript_lower = transcript.lower()

    for pattern, agent in DELEGATION_REQUIRED_PATTERNS:
        if re.search(pattern, transcript_lower):
            return True, agent

    return False, None


def run_build_check(project_path: str = "C:/Planilla") -> Tuple[bool, str]:
    """
    Run dotnet build to verify code changes don't break the build.

    Returns:
        (build_passed, output)
    """
    try:
        # Check if this is a .NET project
        if not os.path.exists(os.path.join(project_path, "Planilla.sln")):
            # Not a .NET project, skip build check
            return True, "Not a .NET project, skipping build check"

        # Run dotnet build
        result = subprocess.run(
            ['dotnet', 'build', '--no-incremental'],
            cwd=project_path,
            capture_output=True,
            text=True,
            timeout=120  # 2 minute timeout
        )

        if result.returncode == 0:
            return True, "Build passed ✓"
        else:
            return False, f"Build failed:\n{result.stdout}\n{result.stderr}"

    except subprocess.TimeoutExpired:
        return False, "Build timed out after 2 minutes"
    except FileNotFoundError:
        # dotnet CLI not found - skip build check
        return True, "dotnet CLI not found, skipping build check"
    except Exception as e:
        # On error, don't block - just warn
        return True, f"Build check error (not blocking): {str(e)}"


def check_stop_hook_active(input_data: Dict) -> bool:
    """Check if stop hook is explicitly active (prevents recursion)."""
    return input_data.get('stop_hook_active', False)


def main():
    """Main hook execution."""
    try:
        # Read hook input from stdin
        input_data = json.load(sys.stdin)

        # Check for recursion guard
        if check_stop_hook_active(input_data):
            # Hook already ran, approve to prevent loops
            print(json.dumps({'decision': 'approve'}))
            return

        transcript_path = input_data.get('transcript_path', '')

        if not transcript_path:
            # No transcript, can't validate - approve
            print(json.dumps({'decision': 'approve'}))
            return

        # Read transcript
        transcript = read_transcript(transcript_path)

        if not transcript:
            # Empty transcript, approve
            print(json.dumps({'decision': 'approve'}))
            return

        # Check if task required delegation
        requires_delegation, recommended_agent = should_require_delegation(transcript)

        if requires_delegation:
            # Check if delegation occurred
            delegation_found, agents_used = check_agent_delegation(transcript)

            if not delegation_found:
                # Block: task required delegation but none occurred
                reason = f"""
⛔ STOP BLOCKED: Missing Agent Delegation

This task appears to require specialist expertise, but no agent delegation was detected.

**Recommended agent**: `{recommended_agent}`

**Why this matters**:
- Specialist agents ensure compliance with project standards (CLAUDE.md)
- They enforce multi-tenancy, plan limits, and architectural patterns
- They provide domain-specific expertise (e.g., Panama labor law for payroll)

**To proceed**:
1. Use the Task tool to delegate to `{recommended_agent}`
2. Or use `planilla-orchestrator` for multi-domain tasks
3. After delegation completes, you can end the conversation

**Example**:
```
I'm going to delegate this to {recommended_agent} for proper implementation.
[Uses Task tool with subagent_type='{recommended_agent}']
```
"""
                print(json.dumps({
                    'decision': 'block',
                    'reason': reason.strip()
                }))
                return

        # Check if code changes were made
        code_changed = check_code_changes(transcript)

        if code_changed:
            # Run build check
            build_passed, build_output = run_build_check()

            if not build_passed:
                # Block: build failed
                reason = f"""
⛔ STOP BLOCKED: Build Validation Failed

Code changes were detected, but the build is failing.

**Build output**:
```
{build_output}
```

**To proceed**:
1. Fix the build errors shown above
2. Run `dotnet build` locally to verify
3. Once build passes, you can end the conversation

**Common issues**:
- Missing using statements
- Typos in class/method names
- Missing dependency registrations in Program.cs
- Migration issues (run `dotnet ef migrations add` if needed)
"""
                print(json.dumps({
                    'decision': 'block',
                    'reason': reason.strip()
                }))
                return

        # All checks passed - approve stop
        print(json.dumps({
            'decision': 'approve',
            'message': '✅ All validation checks passed. Conversation can end safely.'
        }))

    except Exception as e:
        # On error, approve (don't block user unnecessarily)
        print(f"Stop hook error: {str(e)}", file=sys.stderr)
        print(json.dumps({
            'decision': 'approve',
            'message': f'⚠️ Stop hook encountered an error but approved: {str(e)}'
        }))


if __name__ == '__main__':
    main()
