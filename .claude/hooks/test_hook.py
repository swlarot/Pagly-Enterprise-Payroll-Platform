#!/usr/bin/env python3
import sys
sys.path.insert(0, '.claude/hooks')
from route_prompt import generate_routing_context, main
import json

# Test the function directly
prompt = "create an api controller for managing employees with multi-tenant support"
context = generate_routing_context(prompt)
print(f"Context generated: {repr(context)}")
print(f"Context length: {len(context)}")
print(f"First 100 chars: {context[:100] if context else 'EMPTY'}")

# Test the main function
print("\n--- Testing main() ---")
import io
old_stdin = sys.stdin
sys.stdin = io.StringIO('{"prompt": "' + prompt + '"}')

import io
old_stdout = sys.stdout
sys.stdout = io.StringIO()

try:
    main()
    output = sys.stdout.getvalue()
    sys.stdout = old_stdout
    print(f"Main output: {output}")
    result = json.loads(output)
    print(f"Parsed: {result}")
except Exception as e:
    sys.stdout = old_stdout
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
finally:
    sys.stdin = old_stdin
